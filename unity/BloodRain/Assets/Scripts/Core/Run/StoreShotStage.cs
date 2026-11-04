#nullable enable
// 스토어 스크린샷 한 장의 시작 상태로 한 생을 세운다(ST-03, docs/store/trailer_screens.md '찍는 법').
// PC Unity 세션이 이 결과로 같은 그림을 몇 번이고 다시 찍는다. 순서:
//   1) 혼과 몸: 서장 진소운이면 첫 생, 문파 몸이면 서장을 마친 혼에 성질 없는 그 문파 몸.
//   2) 시계: 레벨 1인 채로 태어난 땅에서 세상 시간을 minutes 까지 민다(땅의 주인이 깨기 전이라 싸움이 걸리지 않는다).
//   3) 자리: 찍을 지역 한가운데로 옮긴다.
//   4) 진기: level 까지 먹인다. 돌파 연출은 끝까지 돌려 두고, breakthrough 장면만 마지막 돌파 연출을 막 시작한 채로 둔다.
//   5) 무공·심법: 적힌 레벨까지 레벨업 카드로 올린다(음양 게이지도 그만큼 기운다). 절기는 깨워서 각성 연출을 끝까지 돌린다.
//   6) 음양 극성·오의 게이지·레벨업 카드.
//   7) 땅의 주인: boss 가 있으면 싸움을 걸고 패턴 시계를 민다. 없는데 그 지역 주인이 깨어 있으면 혈운 기둥 원 밖에 세운다.
// 데이터가 게임 규칙으로 닿을 수 없는 상태(칸·레벨 수 초과, 문턱 안 된 절기 …)면 InvalidOperationException.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;

namespace BloodRain.Core.Run
{
    /// <summary>세운 장면. 싸움 화면이면 Life 가 있고, 침방이면 Candidates 만 있다.</summary>
    public sealed class StoreShotStage
    {
        /// <summary>연출을 끝까지 돌릴 때 한 번에 미는 실제 시간(초).</summary>
        const float CinematicStep = 0.05f;
        /// <summary>패턴 판정을 기다릴 때 한 번에 미는 시간(초)과 상한.</summary>
        const float PatternStep = 0.05f;
        const float PatternLimitSeconds = 120f;
        /// <summary>보스 없는 장면에서 혈운 기둥 원 반경의 몇 배 떨어진 자리에 세우나.</summary>
        const float AwayFromBossRadii = 2f;

        public StoreShotDef Shot { get; }
        public Soul? Soul { get; private set; }
        public Life? Life { get; private set; }
        /// <summary>땅의 주인 패턴 시계(보스 장면만). Unity 쪽 보스는 이 시계를 이어서 민다.</summary>
        public BossPatterns? Patterns { get; private set; }
        /// <summary>땅의 주인의 지금 기혈(최대 기혈 × health).</summary>
        public float BossHealth { get; private set; }
        /// <summary>레벨업 화면 카드(levelUpCards 가 있을 때).</summary>
        public IReadOnlyList<Offer> LevelUpOffers { get; private set; } = Array.Empty<Offer>();
        /// <summary>침방 후보 카드(rebirth 화면).</summary>
        public IReadOnlyList<RebirthCandidate> Candidates { get; private set; } = Array.Empty<RebirthCandidate>();

        StoreShotStage(StoreShotDef shot) { Shot = shot; }

        /// <summary>장면 하나를 세운다. seed 는 파일의 seed(장면마다 seed + no 를 쓴다).</summary>
        public static StoreShotStage Build(GameData data, StoreShotDef shot, int seed)
        {
            var stage = new StoreShotStage(shot);
            if (shot.Screen == StoreShotScreen.Rebirth) stage.Candidates = RebirthCards(data, shot);
            else if (shot.Screen == StoreShotScreen.Battle) stage.BuildBattle(data, new Random(seed + shot.No));
            else throw new InvalidOperationException($"{shot.No}번: 모르는 화면 '{shot.Screen}'");
            return stage;
        }

        static List<RebirthCandidate> RebirthCards(GameData data, StoreShotDef shot)
        {
            var c = data.Cheonha;
            return shot.Candidates.Select(k =>
            {
                var sect = c.Sect(k.Sect);
                var trait = c.BirthTraits.List.FirstOrDefault(t => t.Id == k.Trait)
                    ?? throw new InvalidOperationException($"{shot.No}번: 없는 성질 {k.Trait}");
                return new RebirthCandidate(sect, c.Region(sect.Region), new[] { trait });
            }).ToList();
        }

        void BuildBattle(GameData data, Random rng)
        {
            var shot = Shot;
            var c = data.Cheonha;

            // 1) 혼과 몸
            var soul = new Soul(data);
            RebirthBody body;
            if (shot.Body == c.PrologueBody.Character) body = Rebirth.Prologue(data);
            else
            {
                soul.EndLife(soul.BeginLife(Rebirth.Prologue(data)).Finish(LifeEndWay.Fallen, 0)); // 서장을 마친 혼
                var sect = c.Sect(shot.Body);
                body = Rebirth.Body(c, new RebirthCandidate(sect, c.Region(sect.Region), Array.Empty<BirthTraitDef>()));
            }
            var life = new Life(soul, body, rng);
            Soul = soul;
            Life = life;

            // 2) 시계
            while (life.Minutes < shot.Minutes)
            {
                float left = (shot.Minutes - life.Minutes) * 60f;
                life.Tick(Math.Min(1f, Math.Max(left, 0.001f)), life.Position, shot.Enemies);
            }

            // 3) 자리
            var center = life.Regions.Center(shot.Region);
            life.MoveTo(center);

            // 4) 진기
            if (shot.Level < 1) throw new InvalidOperationException($"{shot.No}번: 레벨이 1보다 작다");
            if (shot.Breakthrough)
            {
                var last = data.Balance.Realms.Where(r => r.Level <= shot.Level).OrderBy(r => r.Level).Last();
                if (last.Level <= 1) throw new InvalidOperationException($"{shot.No}번: 삼류는 돌파 연출이 없다");
                FeedTo(life, last.Level - 1);
                FinishCinematics(life);
                FeedTo(life, shot.Level);
            }
            else
            {
                FeedTo(life, shot.Level);
                FinishCinematics(life);
            }
            if (life.Scale.RealmIndex(shot.Realm) != life.Realm)
                throw new InvalidOperationException($"{shot.No}번: Lv{shot.Level} 은 {life.Xp.Realm.Id} 다({shot.Realm} 아님)");

            // 5) 무공·심법·절기
            ApplyBuild(data, life, shot);
            if (shot.Evolution != null)
            {
                var evo = data.Evolutions.FirstOrDefault(e => e.Id == shot.Evolution)
                    ?? throw new InvalidOperationException($"{shot.No}번: 없는 절기 {shot.Evolution}");
                if (!life.Loadout.Awakenable(life.Level).Contains(evo))
                    throw new InvalidOperationException($"{shot.No}번: {evo.Id} 는 아직 깨어날 수 없다(문턱·절기 그릇)");
                life.Awaken(evo, AwakeningLength.Short);
                for (int guard = 0; life.Awakening.IsActive && guard < 100000; guard++) life.Awakening.Tick(CinematicStep);
            }

            // 6) 음양·오의·레벨업 카드
            if (shot.Yinyang != null)
            {
                int edge = data.Balance.Yinyang.ExtremeFrom;
                life.Loadout.Gauge.ResetTo(shot.Yinyang switch
                {
                    StoreShotYinyang.YangExtreme => edge,
                    StoreShotYinyang.YinExtreme => -edge,
                    _ => throw new InvalidOperationException($"{shot.No}번: 모르는 음양 '{shot.Yinyang}'"),
                });
            }
            if (shot.Ultimate != null)
            {
                if (shot.Ultimate != body.Character.Ultimate)
                    throw new InvalidOperationException($"{shot.No}번: {shot.Body} 몸의 오의는 {body.Character.Ultimate} 다({shot.Ultimate} 아님)");
                life.Charge.AddFlat(life.Charge.Max);
            }
            if (shot.LevelUpCards.Count > 0) LevelUpOffers = Cards(data, soul, life, shot);

            // 7) 땅의 주인
            var region = life.Regions.Region(shot.Region);
            if (shot.Boss != null)
            {
                if (region.Boss == null) throw new InvalidOperationException($"{shot.No}번: {shot.Region} 에는 땅의 주인이 없다");
                var fight = life.Bosses.Engage(region.Id, life.Level, life.Minutes);
                life.Tick(0f, center, shot.Enemies); // 생성기에 보스전임을 알린다
                Patterns = RunPatterns(data, fight.Boss.Id, shot);
                BossHealth = fight.MaxHealth * shot.Boss.Health;
            }
            else
            {
                if (region.Boss != null && life.Bosses.State(region.Id) == World.BossRegionState.Awake)
                    life.MoveTo(center + new Vector2(life.Bosses.EngageRadius(life.Level) * AwayFromBossRadii, 0f));
                life.Tick(0f, life.Position, shot.Enemies); // 막 깨어난 땅 표시를 원 밖에서 털어 둔다
            }
        }

        /// <summary>레벨이 target 이 될 때까지 진기를 먹인다(심득 배율을 나눠 넣어 넘치지 않게).</summary>
        static void FeedTo(Life life, int target)
        {
            for (int guard = 0; life.Level < target; guard++)
            {
                if (guard > 10000) throw new InvalidOperationException("진기를 먹여도 레벨이 오르지 않는다");
                float growth = life.CurrentStats().Growth;
                if (growth <= 0f) throw new InvalidOperationException("심득이 0 이하라 레벨이 오르지 않는다");
                double need = (life.Xp.Required - life.Xp.Xp) / growth;
                life.Absorb(need * 1.0000001 + 1e-6);
            }
            if (life.Level != target) throw new InvalidOperationException($"레벨이 Lv{target} 을 넘었다(Lv{life.Level})");
        }

        static void FinishCinematics(Life life)
        {
            for (int guard = 0; life.Breakthrough.IsActive && guard < 100000; guard++) life.Breakthrough.Tick(CinematicStep);
        }

        /// <summary>적힌 레벨까지 레벨업 카드로 올린다. 레벨업 횟수(level - 1)와 칸을 넘으면 예외.</summary>
        static void ApplyBuild(GameData data, Life life, StoreShotDef shot)
        {
            var lo = life.Loadout;
            string start = life.Body.Character.StartWeapon;
            if (!shot.Weapons.ContainsKey(start))
                throw new InvalidOperationException($"{shot.No}번: 몸의 시작 무공 {start} 이 무공 목록에 없다");

            int picks = shot.Weapons.Sum(kv => kv.Value) - 1 + shot.Passives.Sum(kv => kv.Value);
            if (picks > life.Level - 1)
                throw new InvalidOperationException($"{shot.No}번: 레벨업 {picks}번이 필요한데 Lv{life.Level} 은 {life.Level - 1}번뿐이다");

            foreach (var kv in shot.Weapons)
            {
                if (data.Weapons.All(w => w.Id != kv.Key)) throw new InvalidOperationException($"{shot.No}번: 없는 무공 {kv.Key}");
                if (!lo.Weapons.ContainsKey(kv.Key)) lo.Apply(new Offer(OfferKind.NewWeapon, kv.Key));
                while (lo.Weapons[kv.Key] < kv.Value) lo.Apply(new Offer(OfferKind.UpgradeWeapon, kv.Key));
            }
            foreach (var kv in shot.Passives)
            {
                if (data.Passives.All(p => p.Id != kv.Key)) throw new InvalidOperationException($"{shot.No}번: 없는 심법 {kv.Key}");
                if (!lo.Passives.ContainsKey(kv.Key)) lo.Apply(new Offer(OfferKind.NewPassive, kv.Key));
                while (lo.Passives[kv.Key] < kv.Value) lo.Apply(new Offer(OfferKind.UpgradePassive, kv.Key));
            }
        }

        /// <summary>레벨업 카드: 가진 것이면 단계 올리기, 아니면 새로 배우기. 지금 로드아웃의 후보에 없는 카드면 예외.</summary>
        static List<Offer> Cards(GameData data, Soul soul, Life life, StoreShotDef shot)
        {
            var lo = life.Loadout;
            var pool = lo.Candidates(soul.UnlockedWeapons());
            var list = new List<Offer>();
            foreach (var id in shot.LevelUpCards)
            {
                Offer offer;
                if (data.Weapons.Any(w => w.Id == id))
                    offer = new Offer(lo.Weapons.ContainsKey(id) ? OfferKind.UpgradeWeapon : OfferKind.NewWeapon, id);
                else if (data.Passives.Any(p => p.Id == id))
                    offer = new Offer(lo.Passives.ContainsKey(id) ? OfferKind.UpgradePassive : OfferKind.NewPassive, id);
                else throw new InvalidOperationException($"{shot.No}번: 레벨업 카드 {id} 는 무공도 심법도 아니다");
                if (!pool.Contains(offer)) throw new InvalidOperationException($"{shot.No}번: 레벨업 카드 {offer} 는 지금 나올 수 없다");
                list.Add(offer);
            }
            return list;
        }

        /// <summary>health 로 패턴 시계를 밀어 그 패턴이 판정되는 순간에 멈춘다.</summary>
        static BossPatterns RunPatterns(GameData data, string bossId, StoreShotDef shot)
        {
            var boss = shot.Boss!;
            var set = data.BossPatternSet(bossId);
            if (set.Patterns.All(p => p.Id != boss.Pattern) && set.Phase2.All(p => p.Id != boss.Pattern))
                throw new InvalidOperationException($"{shot.No}번: {bossId} 에 {boss.Pattern} 패턴이 없다");
            var patterns = new BossPatterns(set);
            for (float t = 0; t < PatternLimitSeconds; t += PatternStep)
                foreach (var e in patterns.Tick(PatternStep, boss.Health))
                    if (e.Kind == BossPatternEventKind.Resolve && e.Pattern?.Id == boss.Pattern) return patterns;
            throw new InvalidOperationException($"{shot.No}번: {PatternLimitSeconds}초 안에 {boss.Pattern} 이 판정되지 않았다");
        }
    }
}
