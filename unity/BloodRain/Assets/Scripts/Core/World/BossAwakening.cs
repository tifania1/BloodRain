#nullable enable
// 땅의 주인이 깨어나는 규칙(기획서 7장 '땅의 주인이 깨어나는 규칙', 6장 땅의 주인 표). 한 생에 하나 만든다.
// 숫자는 cheonha.json bosses, 기준 체력은 enemies.json bosses, 장막·화면 대각선은 World.Scale, 지역 위치는 World.Regions.
// 보스 패턴·2막은 Combat.BossPatterns 가, 한 생의 적 생성은 Spawning 쪽이 맡는다. 여기서는 언제 깨어나 언제 싸우고 무엇을 주는지만.
//
// 계정 기록(이미 쓰러뜨린 땅의 주인, 봉인석 수)은 IBossRecord 로 받는다. Core/Meta 의 혼(계정 진행)이 이걸 구현해 넘기고,
// 그 전에는 BossRecord(집합 하나)를 쓴다.
//
// Unity 쪽 흐름:
//   1. 생이 시작되면 new BossAwakening(data, 출생지 지역 id, 혼).
//   2. 돌파마다 UpdateRealm(새 레벨) → 새로 깨어난 지역에 혈운 기둥을 세우고 화면 끝에 붉은 표시.
//   3. 매 프레임(또는 몇 프레임마다) TryEngage(플레이어 위치, 레벨, 경과 분). 싸움이 정해지면 경고 뒤 보스전.
//      보스전 동안 생성 목표에 SpawnTargetScale(0.4)을 곱한다. 보스는 Fight.Realm 축척으로 그리고, 밟히지 않는다.
//   4. 보스를 쓰러뜨리면 Defeat() → 보상(BossKill)을 혼에 반영한 뒤 Refresh() 로 봉마곡이 열리는지 다시 본다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>땅의 주인 규칙이 보는 계정 기록. 혼(계정 진행)이 구현한다.</summary>
    public interface IBossRecord
    {
        /// <summary>그 지역 땅의 주인을 이미 쓰러뜨렸나. 참이면 다음부터 혈영으로 일어나고, 첫 처치 보상은 없다.</summary>
        bool HasDefeated(string regionId);
        /// <summary>지금 가진 봉인석 수.</summary>
        int SealStones { get; }
    }

    /// <summary>집합 하나로 된 기록. 테스트·시뮬레이터용이고, 혼이 생기기 전까지 임시로 쓴다.</summary>
    public sealed class BossRecord : IBossRecord
    {
        public HashSet<string> Defeated { get; }
        public int SealStones { get; set; }

        public BossRecord(IEnumerable<string>? defeated = null, int sealStones = 0)
        {
            Defeated = new HashSet<string>(defeated ?? Array.Empty<string>());
            SealStones = sealStones;
        }

        public bool HasDefeated(string regionId) => Defeated.Contains(regionId);

        /// <summary>처치 보상을 기록에 반영한다. 첫 처치면 지역을 넣고 봉인석이 있으면 하나 더한다.</summary>
        public void Apply(BossKill kill)
        {
            if (kill.IsFinal) return;
            Defeated.Add(kill.RegionId);
            if (kill.SealStone != null) SealStones++;
        }
    }

    public enum BossRegionState
    {
        /// <summary>아직 장막 밖이거나 경지가 모자라 잠들어 있다.</summary>
        Dormant,
        /// <summary>깨어났다. 한가운데에 들어서면 싸움이 시작된다.</summary>
        Awake,
        /// <summary>지금 싸우는 중.</summary>
        Fighting,
        /// <summary>이번 생에서 쓰러뜨렸다. 이번 생에는 다시 일어나지 않는다.</summary>
        Defeated,
    }

    /// <summary>시작된 보스전 하나.</summary>
    public sealed class BossFight
    {
        public RegionDef Region { get; }
        public BossDef Boss { get; }
        /// <summary>보스를 그리는 축척(싸움을 건 순간의 플레이어 경지 순번). 싸움 도중 돌파해도 그대로다.</summary>
        public int Realm { get; }
        /// <summary>싸움을 건 시각(한 생 경과 분).</summary>
        public float Minute { get; }
        /// <summary>보스 기혈 = 기준 체력 × (1 + 0.08 × 경과 분) ÷ 2.6.</summary>
        public float MaxHealth { get; }
        /// <summary>이미 쓰러뜨린 적이 있는 땅의 주인이 다시 일어난 것(혈영).</summary>
        public bool IsShadow { get; }
        /// <summary>종장(봉마곡 혈마). 단계 전환은 보스 패턴 쪽이 맡는다.</summary>
        public bool IsFinal => Region.Final;

        public BossFight(RegionDef region, BossDef boss, int realm, float minute, float maxHealth, bool isShadow)
        {
            Region = region;
            Boss = boss;
            Realm = realm;
            Minute = minute;
            MaxHealth = maxHealth;
            IsShadow = isShadow;
        }
    }

    /// <summary>땅의 주인을 쓰러뜨려 얻는 것. 윤회의 기는 성질 배율 전 보너스 값이다(10장 식의 '보너스').</summary>
    public sealed class BossKill
    {
        public string RegionId = "";
        public string BossId = "";
        /// <summary>첫 처치. 거짓이면 혈영을 다시 쓰러뜨린 것.</summary>
        public bool First;
        /// <summary>종장 혈마. 봉인 엔딩으로 넘어가고 아래 보상은 없다.</summary>
        public bool IsFinal;
        /// <summary>봉인석 이름(첫 처치). 없으면 null.</summary>
        public string? SealStone;
        /// <summary>이 처치로 새로 환생할 수 있게 되는 문파(첫 처치). 처음부터 열린 문파의 땅이면 null.</summary>
        public string? UnlockSect;
        /// <summary>객잔에 합류하는 대표 인물(첫 처치, 21장). 화산·곤륜처럼 대표 인물이 없으면 null.</summary>
        public string? Leader;
        /// <summary>이 처치로 레벨업 풀에 들어오는 무공(weapons.json unlock 이 boss:&lt;지역&gt;).</summary>
        public List<string> UnlockWeapons = new List<string>();
        public int RebirthQi;
        public int BloodCrystal;
        /// <summary>보갑 하나(절기 계기, 8장).</summary>
        public bool Chest;
    }

    public sealed class BossAwakening
    {
        readonly GameData _data;
        readonly Scale _scale;
        readonly Regions _regions;
        readonly IBossRecord _record;
        readonly BossRulesDef _rules;
        readonly RegionDef _birth;
        readonly Vector2 _birthCenter;
        readonly int _awakenFrom;
        readonly int _finalRealm;
        readonly List<RegionDef> _bossRegions;
        readonly Dictionary<string, BossRegionState> _state = new Dictionary<string, BossRegionState>();

        public BossAwakening(GameData data, string birthRegionId, IBossRecord record)
            : this(data, new Scale(data), new Regions(data), birthRegionId, record) { }

        public BossAwakening(GameData data, Scale scale, Regions regions, string birthRegionId, IBossRecord record)
        {
            _data = data;
            _scale = scale;
            _regions = regions;
            _record = record;
            _rules = data.Cheonha.Bosses;
            _birth = regions.Region(birthRegionId);
            _birthCenter = regions.Center(_birth);
            _awakenFrom = scale.RealmIndex(_rules.AwakenFrom);
            _finalRealm = scale.RealmIndex(_rules.FinalRequires.Realm);
            _bossRegions = regions.All.Where(r => r.Boss != null).ToList();
            foreach (var r in _bossRegions) _state[r.Id] = BossRegionState.Dormant;
        }

        public RegionDef Birthplace => _birth;
        /// <summary>마지막으로 알려 준 플레이어 경지 순번.</summary>
        public int Realm { get; private set; }
        /// <summary>땅의 주인이 있는 지역(봉마곡 포함).</summary>
        public IReadOnlyList<RegionDef> BossRegions => _bossRegions;
        public BossRegionState State(string regionId) =>
            _state.TryGetValue(regionId, out var s) ? s : throw new ArgumentException($"땅의 주인이 없는 지역: {regionId}", nameof(regionId));
        /// <summary>깨어나 있고 아직 싸우지 않은 지역.</summary>
        public IReadOnlyList<RegionDef> Awake => _bossRegions.Where(r => _state[r.Id] == BossRegionState.Awake).ToList();

        /// <summary>지금 보스전. 없으면 null.</summary>
        public BossFight? Fight { get; private set; }
        public bool InFight => Fight != null;
        /// <summary>적 생성 목표에 곱하는 값. 보스전 동안 0.4, 아니면 1.</summary>
        public float SpawnTargetScale => InFight ? _rules.SpawnTargetScale : 1f;

        // ---- 깨어남 ----

        /// <summary>그 경지에서 깨어나 있어야 하는가. 땅의 주인은 절정부터 장막 안(현경은 장막이 없어 모두),
        /// 봉마곡은 봉인석 일곱과 현경을 모두 갖췄을 때.</summary>
        public bool ShouldBeAwake(RegionDef region, int realm, int sealStones)
        {
            if (region.Boss == null) return false;
            if (region.Final)
                return sealStones >= _rules.FinalRequires.SealStones && realm >= _finalRealm && Regions.IsVisible(region, sealStones);
            if (realm < _awakenFrom) return false;
            var radius = _scale.BoundaryRadius(realm);
            return radius == null || Vector2.Distance(_birthCenter, _regions.Center(region)) <= radius.Value;
        }

        /// <summary>이 출생지에서 그 경지면 깨어나 있을 땅(지금 계정 기록 기준). 침방 지도에 '어느 땅의 주인까지 닿는지' 그릴 때.</summary>
        public List<RegionDef> AwakeAt(int realm) => _bossRegions.Where(r => ShouldBeAwake(r, realm, _record.SealStones)).ToList();

        /// <summary>경지가 바뀌었을 때(돌파). 새로 깨어난 지역을 데이터 순서대로 돌려준다.</summary>
        public List<RegionDef> UpdateRealm(int level)
        {
            Realm = Math.Max(Realm, _scale.RealmIndex(level));
            return Refresh();
        }

        /// <summary>지금 경지와 계정 기록으로 다시 본다. 봉인석이 늘었을 때(봉마곡) 부른다. 새로 깨어난 지역.</summary>
        public List<RegionDef> Refresh()
        {
            var woke = new List<RegionDef>();
            foreach (var r in _bossRegions)
            {
                if (_state[r.Id] != BossRegionState.Dormant) continue;
                if (!ShouldBeAwake(r, Realm, _record.SealStones)) continue;
                _state[r.Id] = BossRegionState.Awake;
                woke.Add(r);
            }
            return woke;
        }

        // ---- 싸움 ----

        /// <summary>보스 기혈 = 기준 체력 × (1 + 0.08 × 경과 분) ÷ 2.6. 20분이면 기준 체력 그대로, 12분이면 약 0.75배.</summary>
        public float Health(BossDef boss, float minute) =>
            boss.Health * (1f + _rules.HealthPerMinute * Math.Max(0f, minute)) / _rules.NormalizeAt20;

        /// <summary>싸움이 시작되는 반경(세계 m) = 지금 화면 대각선 × 0.5.</summary>
        public float EngageRadius(float level) => _scale.ViewDiagonal(level) * _rules.EngageViewMultiple;

        /// <summary>깨어난 지역 한가운데(화면 대각선 0.5배 안)에 들어섰으면 싸움을 시작한다. 여럿이면 가장 가까운 곳.
        /// 이미 싸우는 중이거나 들어선 곳이 없으면 null.</summary>
        public BossFight? TryEngage(Vector2 player, float level, float minute)
        {
            if (InFight) return null;
            float radius = EngageRadius(level);
            RegionDef? best = null;
            float bestD = float.MaxValue;
            foreach (var r in _bossRegions)
            {
                if (_state[r.Id] != BossRegionState.Awake) continue;
                float d = Vector2.Distance(player, _regions.Center(r));
                if (d <= radius && d < bestD) { best = r; bestD = d; }
            }
            return best == null ? null : Begin(best, level, minute);
        }

        /// <summary>깨어난 지역의 보스전을 바로 시작한다(연출·시험용). 깨어나 있지 않으면 예외.</summary>
        public BossFight Engage(string regionId, float level, float minute)
        {
            if (InFight) throw new InvalidOperationException("이미 보스전 중입니다");
            if (State(regionId) != BossRegionState.Awake) throw new InvalidOperationException($"{regionId} 땅의 주인이 깨어 있지 않습니다");
            return Begin(_regions.Region(regionId), level, minute);
        }

        BossFight Begin(RegionDef region, float level, float minute)
        {
            var boss = _data.Bosses.First(b => b.Id == region.Boss);
            int realm = _scale.RealmIndex((int)Math.Floor(level));
            Fight = new BossFight(region, boss, realm, minute, Health(boss, minute), _record.HasDefeated(region.Id));
            _state[region.Id] = BossRegionState.Fighting;
            return Fight;
        }

        /// <summary>지금 보스를 쓰러뜨렸다. 보상을 돌려준다. 계정 기록에 반영하는 일은 부른 쪽(혼)이 한다.</summary>
        public BossKill Defeat()
        {
            var fight = Fight ?? throw new InvalidOperationException("보스전 중이 아닙니다");
            var region = fight.Region;
            _state[region.Id] = BossRegionState.Defeated;
            Fight = null;

            var kill = new BossKill { RegionId = region.Id, BossId = fight.Boss.Id };
            if (fight.IsFinal)
            {
                kill.IsFinal = true;
                kill.First = !fight.IsShadow;
                return kill;
            }
            kill.Chest = true;
            if (fight.IsShadow)
            {
                kill.RebirthQi = _rules.RepeatKill.RebirthQi;
                kill.BloodCrystal = _rules.RepeatKill.BloodCrystal;
                return kill;
            }
            kill.First = true;
            kill.RebirthQi = _rules.FirstKill.RebirthQi;
            if (_rules.FirstKill.SealStone) kill.SealStone = region.SealStone;
            if (_rules.FirstKill.UnlockSect)
                kill.UnlockSect = _data.Cheonha.Sects.FirstOrDefault(s => s.UnlockBossRegion == region.Id)?.Id;
            kill.Leader = region.Sect == null ? null : _data.Cheonha.Sects.FirstOrDefault(s => s.Id == region.Sect)?.Leader;
            kill.UnlockWeapons = _data.Weapons.Where(w => w.UnlockBossRegion == region.Id).Select(w => w.Id).ToList();
            return kill;
        }
    }
}
