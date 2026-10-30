// 한 생을 묶는 입구(기획서 7장 '한 생의 흐름', 21장 환혼과 윤회).
// 환생으로 몸을 받으면 Life 를 만들고, 매 프레임 Tick(실제 시간, 플레이어 위치, 화면 위 적 수)을 부른다.
// Life 가 하는 일: 세상 시간(돌파·각성 연출 중엔 느려짐), 서 있는 지역과 처음 밟은 땅, 진기 흡수 → 레벨·돌파(축척) → 땅의 주인 깨어남,
// 땅의 주인 싸움 시작·처치, 매초 적 생성(LifeDirector), 기혈역류, 생의 끝(쓰러짐·좌화·봉인)과 정산 기록.
// 전투(맞히기·피해)는 Unity 쪽 Sim 이 하고 결과만 여기로 알려 준다(Absorb, DefeatBoss, End).
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;

namespace BloodRain.Core.Run
{
    public enum LifePhase
    {
        /// <summary>보통 싸움.</summary>
        Roaming,
        /// <summary>땅의 주인과 싸우는 중.</summary>
        BossFight,
        /// <summary>생이 끝났다(정산 기록이 있다).</summary>
        Ended,
    }

    /// <summary>생이 끝나는 세 가지(cheonha.json lifeEnd.ways).</summary>
    public static class LifeEndWay
    {
        public const string Fallen = "fallen";
        public const string Zuohwa = "zuohwa";
        public const string Sealed = "sealed";
    }

    /// <summary>진기를 먹은 결과. 돌파가 있었으면 경지마다 연출 정보가 붙는다.</summary>
    public sealed class LifeAbsorb
    {
        public AbsorbResult Xp { get; }
        public IReadOnlyList<BreakthroughInfo> Breakthroughs { get; }
        /// <summary>이 돌파로 새로 깨어난 땅의 주인 지역.</summary>
        public IReadOnlyList<RegionDef> WokenBosses { get; }

        public LifeAbsorb(AbsorbResult xp, IReadOnlyList<BreakthroughInfo> breakthroughs, IReadOnlyList<RegionDef> woken)
        {
            Xp = xp; Breakthroughs = breakthroughs; WokenBosses = woken;
        }
    }

    public sealed class Life
    {
        readonly GameData _data;
        readonly Soul _soul;
        double _secondAcc;

        public Life(Soul soul, RebirthBody body, Random rng)
        {
            _soul = soul;
            _data = soul.Data;
            Body = body;
            Log = soul.BeginLife(body);
            Loadout = body.NewLoadout(_data);
            Xp = new RunXp(new XpCurve(_data.Balance.XpCurve), new Realms(_data.Balance.Realms));
            Regions = new Regions(_data);
            Scale = new Scale(_data);
            Breakthrough = new Breakthrough(Scale, _data.Cheonha.Breakthrough, _data.Cheonha.Crush);
            Awakening = new Awakening(_data.Balance.Awakening);
            Backlash = new Backlash(_data.Balance.Yinyang.Extreme);
            Director = new LifeDirector(_data, rng);
            Bosses = new BossAwakening(_data, body.Region.Id, new BossRecord(soul.BossesKilled, soul.SealStoneCount));
            if (body.Sect?.BloodArt is { } art) BloodArt = new BloodArt(art, Loadout.Gauge, body.YinyangStart);
            RegionId = body.Region.Id;
            Position = Regions.Center(body.Region);
        }

        public RebirthBody Body { get; }
        public LifeLog Log { get; }
        public Loadout Loadout { get; }
        public RunXp Xp { get; }
        public Regions Regions { get; }
        public Scale Scale { get; }
        public Breakthrough Breakthrough { get; }
        public Awakening Awakening { get; }
        public Backlash Backlash { get; }
        public LifeDirector Director { get; }
        public BossAwakening Bosses { get; }
        /// <summary>마교 몸의 혈공(5장). 다른 몸은 null.</summary>
        public BloodArt? BloodArt { get; }

        /// <summary>세상 시간으로 흐른 분.</summary>
        public float Minutes { get; private set; }
        public int Level => Xp.Level;
        public int Realm => Scale.RealmIndex(Xp.Level);
        public Vector2 Position { get; private set; }
        /// <summary>서 있는 지역(가장 가까운 지역 중심).</summary>
        public string RegionId { get; private set; }
        public LifePhase Phase => Record != null ? LifePhase.Ended : Bosses.InFight ? LifePhase.BossFight : LifePhase.Roaming;
        /// <summary>끝났으면 정산 기록(Soul.EndLife 에 넘긴다).</summary>
        public LifeRecord? Record { get; private set; }
        /// <summary>혈마의 그림자 단계(0이면 없음).</summary>
        public int ShadowStep => Director.ShadowStep(Minutes, Level, _soul.SealStoneCount);
        /// <summary>지금 혈마의 그림자 세기(단계 0이면 Active 가 false).</summary>
        public BloodShadowStrength Shadow => BloodShadow.Strength(_data.Cheonha.LifeEnd.Shadow, ShadowStep);
        /// <summary>지금 세상 시간 배율(돌파·각성 연출 중엔 느리다). Unity 의 Time.timeScale 에 넣는다.</summary>
        public float WorldTimeScale => Math.Min(Breakthrough.WorldTimeScale, Awakening.IsActive ? Awakening.WorldTimeScale : 1f);

        /// <summary>한 프레임. 세상 시간으로 1초가 찰 때마다 그 1초의 생성 지시를 돌려준다(아니면 null).</summary>
        public LifeSecond? Tick(float unscaledDt, Vector2 position, int aliveNow)
        {
            if (Record != null) return null;

            if (Breakthrough.IsActive) Breakthrough.Tick(unscaledDt);
            if (Awakening.IsActive && (Awakening.Tick(unscaledDt) & AwakeningCue.Shock) != 0)
                Director.Hold(_data.Balance.Awakening.SpawnPauseSeconds);

            float dt = unscaledDt * WorldTimeScale;
            Minutes += dt / 60f;
            Director.Tick(dt);
            Awakening.TickWorld(dt);
            Backlash.Tick(dt, Loadout.Gauge.Band == YinYangBand.Extreme);

            MoveTo(position);

            Bosses.Tick(dt, Position, Level, Minutes);
            Director.BossFight = Bosses.InFight;

            _secondAcc += dt;
            if (_secondAcc < 1.0) return null;
            _secondAcc -= Math.Floor(_secondAcc);
            return Director.Second(Minutes, Level, RegionId, aliveNow);
        }

        /// <summary>플레이어 위치를 옮긴다(Tick 이 부른다). 다른 지역에 들어서면 일지에 남긴다.</summary>
        public void MoveTo(Vector2 position)
        {
            Position = position;
            string now = Regions.Nearest(position, _soul.SealStoneCount).Id;
            if (now == RegionId) return;
            RegionId = now;
            Log.Enter(now);
        }

        /// <summary>진기 구슬을 먹었다. 윤회의 기는 구슬을 주운 자리의 혈운 농도로 센다.</summary>
        public LifeAbsorb Absorb(double orbXp, int orbs = 1)
        {
            if (Record != null) throw new InvalidOperationException("끝난 생입니다");
            float density = Regions.Density(Position, _soul.SealStoneCount);
            Log.AbsorbOrb(orbXp, density, orbs); // 윤회의 기는 구슬 값 그대로(혈공으로 늘지 않음)
            var r = Xp.Absorb(BloodArt?.OrbXp(orbXp, density) ?? orbXp);
            var infos = new List<BreakthroughInfo>();
            var woken = new List<RegionDef>();
            if (r.Breakthroughs.Count > 0)
            {
                foreach (var realm in r.Breakthroughs) infos.Add(Log.Breakthrough(realm.Id));
                bool revisit = infos.All(i => !i.LongCinematic);
                Breakthrough.Start(r.FromLevel, r.ToLevel, revisit);
                Director.OnBreakthrough(revisit);
                woken = Bosses.UpdateRealm(Xp.Level);
            }
            return new LifeAbsorb(r, infos, woken);
        }

        /// <summary>절기가 깨어났다. 긴 연출 여부는 혼의 기록과 설정으로.</summary>
        public AwakenInfo Awaken(EvolutionDef evolution, AwakeningLength setting = AwakeningLength.Auto)
        {
            var info = Log.Awaken(evolution.Id);
            Loadout.Evolve(evolution);
            Awakening.Start(evolution, info.LongByDefault, setting);
            return info;
        }

        /// <summary>싸우던 땅의 주인을 쓰러뜨렸다. 종장(봉마곡)이면 봉인으로 생이 끝난다.</summary>
        public BossKill DefeatBoss(int leftoverSilverIfFinal = 0)
        {
            var kill = Bosses.Defeat();
            Director.BossFight = false;
            if (kill.IsFinal)
            {
                End(LifeEndWay.Sealed, leftoverSilverIfFinal);
                return kill;
            }
            Log.KillBoss(kill.RegionId);
            return kill;
        }

        /// <summary>생이 끝났다(쓰러짐·좌화·봉인). 정산 기록을 만든다. 혼에 옮기는 일은 부른 쪽이 Soul.EndLife 로.</summary>
        public LifeRecord End(string way, int leftoverSilver)
        {
            if (Record != null) throw new InvalidOperationException("이미 끝난 생입니다");
            if (!_data.Cheonha.LifeEnd.Ways.Contains(way)) throw new ArgumentException($"모르는 생의 끝입니다: {way}");
            Record = Log.Finish(way, leftoverSilver);
            return Record;
        }
    }
}
