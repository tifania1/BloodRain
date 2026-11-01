using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>공통 적·지역 고유 적의 행동 갈래마다 Unity 가 읽을 숫자 칸이 다 있는지.
    /// 2026-11-01~02 PC 세션(C-02b)이 '숫자가 데이터에 없다'로 여러 번 멈췄다. 새 적이나 새 갈래를 넣을 때
    /// 칸을 빼먹으면 여기서 먼저 걸린다. 정예(family elite)의 패턴은 bosspatterns.json 쪽이라 보지 않는다.</summary>
    public class EnemyBehaviorParamsTests
    {
        readonly GameData _d = Load();

        /// <summary>갈래 → 그 갈래를 돌리는 데 꼭 있어야 하는 칸 검사. 아무 칸도 필요 없는 갈래는 빈 배열.</summary>
        static readonly Dictionary<string, (string 칸, Func<EnemyDef, bool> 있음)[]> Needs = new Dictionary<string, (string, Func<EnemyDef, bool>)[]>
        {
            ["hop_chase"] = new (string, Func<EnemyDef, bool>)[0],
            ["line_charge"] = new (string, Func<EnemyDef, bool>)[] { ("lifeSeconds", e => e.LifeSeconds > 0) },
            ["telegraph_dash"] = new (string, Func<EnemyDef, bool>)[] { ("telegraph", e => e.Telegraph > 0), ("dash", e => e.Dash != null) },
            ["fly_curve"] = new (string, Func<EnemyDef, bool>)[] { ("curve", e => e.Curve != null), ("ignoresTerrain", e => e.IgnoresTerrain) },
            ["kite_shoot"] = new (string, Func<EnemyDef, bool>)[] { ("shoot", e => e.Shoot != null), ("projectileDamage", e => e.ProjectileDamage > 0) },
            ["petal_shot"] = new (string, Func<EnemyDef, bool>)[] { ("shoot", e => e.Shoot != null), ("projectileDamage", e => e.ProjectileDamage > 0) },
            ["ice_throw"] = new (string, Func<EnemyDef, bool>)[] { ("shoot", e => e.Shoot != null), ("projectileDamage", e => e.ProjectileDamage > 0) },
            ["mark_teleport"] = new (string, Func<EnemyDef, bool>)[] { ("telegraph", e => e.Telegraph > 0), ("mark", e => e.Mark != null) },
            ["blood_pool"] = new (string, Func<EnemyDef, bool>)[] { ("pool", e => e.Pool != null), ("zoneDamagePerSecond", e => e.ZoneDamagePerSecond > 0) },
            ["buff_aura"] = new (string, Func<EnemyDef, bool>)[] { ("auraRadius", e => e.AuraRadius > 0), ("auraBonus", e => e.AuraBonus > 0) },
            ["flee"] = new (string, Func<EnemyDef, bool>)[] { ("lifeSeconds", e => e.LifeSeconds > 0), ("reward", e => e.Reward != null) },
            ["water_pull"] = new (string, Func<EnemyDef, bool>)[] { ("pull", e => e.Pull != null), ("onlyInWater", e => e.OnlyInWater) },
            ["hook_bind"] = new (string, Func<EnemyDef, bool>)[] { ("telegraph", e => e.Telegraph > 0), ("hook", e => e.Hook != null) },
            ["twin_revive"] = new (string, Func<EnemyDef, bool>)[] { ("twin", e => e.Twin != null), ("reviveSeconds", e => e.ReviveSeconds > 0) },
            ["flank_pack"] = new (string, Func<EnemyDef, bool>)[] { ("group", e => e.Group != null) },
            ["spear_line"] = new (string, Func<EnemyDef, bool>)[] { ("group", e => e.Group != null) },
            ["snake_line"] = new (string, Func<EnemyDef, bool>)[] { ("group", e => e.Group != null) },
            ["mimic"] = new (string, Func<EnemyDef, bool>)[] { ("mimic", e => e.Mimic != null) },
            ["charge_roar"] = new (string, Func<EnemyDef, bool>)[] { ("telegraph", e => e.Telegraph > 0), ("dash", e => e.Dash != null), ("roarSeconds", e => e.RoarSeconds > 0), ("auraBonus", e => e.AuraBonus > 0) },
            ["poison_aura"] = new (string, Func<EnemyDef, bool>)[] { ("zoneRadius", e => e.ZoneRadius > 0), ("zoneDamagePerSecond", e => e.ZoneDamagePerSecond > 0) },
        };

        IEnumerable<EnemyDef> Field => _d.Enemies.Where(e => e.Family != "elite");

        [Fact] public void 공통_지역_적의_갈래는_모두_알려진_갈래다()
        {
            var unknown = Field.Where(e => !Needs.ContainsKey(e.Behavior)).Select(e => $"{e.Id}:{e.Behavior}").ToList();
            Assert.True(unknown.Count == 0, "Needs 에 없는 갈래(새로 넣었으면 필요한 칸을 적을 것): " + string.Join(", ", unknown));
        }

        [Fact] public void 갈래마다_필요한_숫자_칸이_있다()
        {
            var missing = new List<string>();
            foreach (var e in Field)
                if (Needs.TryGetValue(e.Behavior, out var needs))
                    foreach (var (칸, 있음) in needs)
                        if (!있음(e)) missing.Add($"{e.Id}({e.Behavior}) {칸}");
            Assert.True(missing.Count == 0, "빠진 칸: " + string.Join(", ", missing));
        }

        /// <summary>쓰러질 때 남기는 것은 갈래와 따로 붙는다(목인나한·독두꺼비는 hop_chase). 이름이 있으면 크기·피해도 있어야 한다.</summary>
        [Fact] public void 쓰러질_때_남기는_것에는_크기와_피해가_있다()
        {
            foreach (var e in _d.Enemies)
            {
                Assert.Equal(e.OnDeath != null, e.Death != null);
                if (e.OnDeath != null) Assert.Contains(e.OnDeath, new[] { "splinter_burst", "poison_pool" });
            }
        }

        /// <summary>둔화 시간이 있으면 배율도 있고, 배율은 1보다 작다.</summary>
        [Fact] public void 닿으면_느려지는_적은_시간과_배율이_같이_있다()
        {
            foreach (var e in _d.Enemies.Where(e => e.OnHitSlowSeconds > 0 || e.OnHitSlowMultiplier > 0))
            {
                Assert.True(e.OnHitSlowSeconds > 0, e.Id);
                Assert.InRange(e.OnHitSlowMultiplier, 0.01f, 0.99f);
            }
        }

        /// <summary>쏘는 적은 사거리 안에서 쏜다: 거리 지키는 적이면 KeepMin &lt; KeepMax &lt;= 사거리.</summary>
        [Fact] public void 쏘는_적의_지키는_거리는_사거리_안이다()
        {
            foreach (var e in _d.Enemies.Where(e => e.Shoot != null))
            {
                var s = e.Shoot!;
                Assert.True(s.Interval > 0 && s.Speed > 0 && s.Range > 0, e.Id);
                if (s.KeepMax > 0)
                {
                    Assert.True(s.KeepMin < s.KeepMax, $"{e.Id}: KeepMin {s.KeepMin} >= KeepMax {s.KeepMax}");
                    Assert.True(s.KeepMax <= s.Range, $"{e.Id}: 지키는 거리 {s.KeepMax} 가 사거리 {s.Range} 밖");
                }
            }
        }
    }
}
