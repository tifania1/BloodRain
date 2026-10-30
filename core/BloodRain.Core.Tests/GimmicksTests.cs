using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>지역 기믹 첫 묶음(T-15): 범종·탑림·우물·독안개·물·태극 바닥의 수치가 기획서·cheonha.json·웹 프로토타입과 맞고, 판정이 규칙대로인지.</summary>
    public class GimmicksTests
    {
        readonly GameData _d = Load();
        GimmicksDef G => _d.Gimmicks;
        /// <summary>절정 축척(경지 순번 3).</summary>
        static readonly float Jeoljeong = (float)Math.Pow(2.25, 3);

        string[] RegionRow(string nameKo) => GddTable(GddSection("지역과 혈운 농도")).Single(r => r[0] == nameKo);
        string[] GimmickRow(string nameKo) => GddTable(GddSection("지형 기믹 수치")).Single(r => r[0] == nameKo);

        // ---- 데이터 ----

        [Fact] public void 기믹마다_cheonha_지역에_같은_이름이_있다()
        {
            foreach (var g in G.All)
            {
                Assert.False(string.IsNullOrEmpty(g.Id));
                Assert.False(string.IsNullOrEmpty(g.NameKo));
                Assert.Same(g, G.Find(g.Id));
            }
            var where = new Dictionary<string, string>
            {
                ["bell"] = "songshan", ["pagoda_forest"] = "songshan", ["well"] = "cheongun",
                ["poison_fog"] = "danggata", ["water"] = "janggang", ["taiji_floor"] = "wudangsan",
                ["wine_jars"] = "gaebang_hq", ["sword_racks"] = "namgung_manor", ["falling_rocks"] = "huashan", ["plum_heaps"] = "huashan",
                ["blizzard"] = "kunlun", ["ice_floor"] = "kunlun", ["campfire"] = "kunlun", ["blood_altars"] = "sipmandaesan", ["red_moon"] = "sipmandaesan",
            };
            Assert.Equal(where.Keys.OrderBy(x => x), G.All.Select(g => g.Id).OrderBy(x => x));
            foreach (var (id, region) in where) Assert.Contains(id, _d.Cheonha.Region(region).Gimmicks);
            Assert.Null(G.Find("bamboo_thicket")); // 아직 수치 없음
        }

        [Fact] public void 지역표의_기믹_칸이_데이터와_같다()
        {
            Assert.Contains($"범종: 닿으면 둘레 적 {Num(G.Bell.StunSeconds)}초 기절, {Num(G.Bell.CooldownSeconds)}초에 한 번", RegionRow("숭산")[5]);
            Assert.Contains("탑림: 돌탑 사이 좁은 길", RegionRow("숭산")[5]);
            Assert.Contains("우물(다가가면 한 번 회복", RegionRow("청운진")[5]);
            Assert.Equal(1, G.Well.Uses);
            var f = G.PoisonFog;
            Assert.Contains($"독안개: {Num(f.PeriodSeconds)}초마다 퍼짐, 안에선 초당 기혈 {Num(f.HealthLossPerSecond * 100)}% 감소 대신 진기 {Num(f.QiMultiplier)}배", RegionRow("당가타")[5]);
            Assert.Contains($"물: 이동 -{Num((1 - G.Water.MoveSpeedMultiplier) * 100)}%, 경공 뒤 {Num(G.Water.WaterWalkSeconds)}초 수상비", RegionRow("장강 수로")[5]);
            var t = G.TaijiFloor;
            Assert.Contains($"태극 바닥: {Num(t.DurationSeconds)}초간 내 음양과 반대 속성 피해 +{Num(t.OppositeAspectDamageBonus * 100)}%", RegionRow("무당산")[5]);
        }

        [Fact] public void 지형_기믹_수치_표가_데이터와_같다()
        {
            var b = G.Bell;
            var drop = _d.BossPatternSet("wongak").Pattern("bell_drop_x3");
            Assert.Contains($"{Num(b.TouchRadius)}m 안에 닿으면 울림. 반경 {Num(b.StunRadius)}m 안 적 {Num(b.StunSeconds)}초 기절(보스 {Num(b.BossStunSeconds)}초). {Num(b.CooldownSeconds)}초에 한 번", GimmickRow("범종")[1]);
            Assert.Contains($"원각이 떨어뜨린 범종은 {Num(drop.LeaveSeconds)}초 동안 한 번만", GimmickRow("범종")[1]);
            Assert.Equal(1, drop.LeaveUses);

            var p = G.PagodaForest;
            Assert.Equal($"반경 {Num(p.PatchRadius)}m 무더기에 돌탑(반경 {Num(p.PagodaRadius)}m)을 {Num(p.Spacing)}m 간격 격자로 놓고 {Num(p.Jitter)}m씩 흔듦. "
                + $"자리 {Num(p.SkipChance * 100)}%와, 옆 탑과 사이가 {Num(p.MinGap)}m보다 좁아지는 탑은 뺌. 보통 길 폭 {Num(PagodaForest.NominalGap(p))}m", GimmickRow("탑림")[1]);
            Assert.Contains("나는 적과 투사체는 지나감", GimmickRow("탑림")[2]);
            Assert.False(p.BlocksFlying);
            Assert.False(p.BlocksProjectiles);

            var w = G.Well;
            Assert.Equal($"{Num(w.TouchRadius)}m 안에 들어서면 한 번, 최대 기혈 {Num(w.HealMaxHealthRatio * 100)}% 회복", GimmickRow("우물")[1]);

            var f = G.PoisonFog;
            Assert.Equal($"한 생 {Num(f.PeriodSeconds)}초마다 샘에서 {Num(f.SpreadSeconds)}초 동안 반경 {Num(f.Radius)}m까지 퍼져 {Num(f.HoldSeconds)}초 머물고 {Num(f.FadeSeconds)}초에 걷힘. "
                + $"안에선 초당 최대 기혈 {Num(f.HealthLossPerSecond * 100)}%를 잃고, 그동안 얻는 진기 {Num(f.QiMultiplier)}배", GimmickRow("독안개")[1]);
            Assert.Equal(f.PeriodSeconds, f.FirstSpreadSeconds); // '90초마다' 그대로: 처음도 90초
            Assert.True(f.EpisodeSeconds < f.PeriodSeconds);
            Assert.Contains("보스는 빼고", GimmickRow("독안개")[2]);
            Assert.True(f.BossImmune);

            var water = G.Water;
            Assert.Equal($"이동속도 ×{Num(water.MoveSpeedMultiplier)}. 경공을 쓰고 {Num(water.WaterWalkSeconds)}초는 수상비로 느려지지 않음", GimmickRow("물")[1]);
            Assert.Contains("나는 적은 빼고", GimmickRow("물")[2]);
            Assert.True(water.FlyingIgnores);

            var t = G.TaijiFloor;
            Assert.Contains($"반경 {Num(t.TouchRadius)}m 안을 밟으면 {Num(t.DurationSeconds)}초 동안 내 음양과 반대 속성 무공 피해 +{Num(t.OppositeAspectDamageBonus * 100)}%", GimmickRow("태극 바닥")[1]);
            Assert.Contains($"바닥 하나는 {Num(t.CooldownSeconds)}초에 한 번", GimmickRow("태극 바닥")[1]);
        }

        [Fact] public void 범종_수치가_웹_프로토타입과_같다()
        {
            var html = ReadRepoFile("web", "prototype", "template.html");
            float Grab(string pattern, int group = 1)
            {
                var m = Regex.Match(html, pattern);
                Assert.True(m.Success, $"template.html 에서 /{pattern}/ 를 못 찾았습니다. 프로토타입 코드가 바뀌었으면 이 패턴도 같이 고친다");
                return float.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);
            }
            Assert.Equal(G.Bell.TouchRadius, Grab(@"if \(b\.cd <= 0 && Math\.hypot\(p\.x - b\.x, p\.y - b\.y\) < ([\d.]+)\)"));
            Assert.Equal(G.Bell.CooldownSeconds, Grab(@"b\.cd = Math\.max\(\d+, ([\d.]+) / G\.timeScale\)"));
            const string stun = @"forNear\(b\.x, b\.y, ([\d.]+), e => \{ if \(!e\.boss\) e\.stun = ([\d.]+); else e\.stun = ([\d.]+); \}\)";
            Assert.Equal(G.Bell.StunRadius, Grab(stun, 1));
            Assert.Equal(G.Bell.StunSeconds, Grab(stun, 2));
            Assert.Equal(G.Bell.BossStunSeconds, Grab(stun, 3));
        }

        // ---- 범종 ----

        [Fact] public void 범종은_닿으면_울리고_60초가_지나야_다시_울린다()
        {
            var bell = new BellGimmick(G.Bell);
            Assert.False(bell.TryRing(1.5f));
            Assert.True(bell.TryRing(1.4f));
            Assert.Equal(60f, bell.CooldownRemaining);
            Assert.False(bell.TryRing(0.5f));
            bell.Tick(59.9f);
            Assert.False(bell.TryRing(0.5f));
            bell.Tick(0.1f);
            Assert.True(bell.Ready);
            Assert.True(bell.TryRing(0.5f));
            Assert.False(bell.Gone); // 지역의 종은 사라지지 않는다
        }

        [Fact] public void 범종은_반경_8m_안_적을_2초_보스는_0초6_기절시키고_플레이어는_그대로()
        {
            var bell = new BellGimmick(G.Bell);
            Assert.Equal(2f, bell.StunSeconds(GimmickTargetKind.Normal, 8f));
            Assert.Equal(0f, bell.StunSeconds(GimmickTargetKind.Normal, 8.1f));
            Assert.Equal(2f, bell.StunSeconds(GimmickTargetKind.Elite, 5f));
            Assert.Equal(0.6f, bell.StunSeconds(GimmickTargetKind.Boss, 3f));
            Assert.Equal(0f, bell.StunSeconds(GimmickTargetKind.Player, 1f));
        }

        [Fact] public void 절정에_생긴_범종은_반경이_축척만큼_크고_시간은_같다()
        {
            var bell = new BellGimmick(G.Bell, Jeoljeong);
            Assert.Equal(G.Bell.StunRadius * Jeoljeong, bell.StunRadius, 3);
            Assert.True(bell.TryRing(1.4f * Jeoljeong - 0.01f));
            Assert.Equal(G.Bell.CooldownSeconds, bell.CooldownRemaining);
            Assert.Equal(2f, bell.StunSeconds(GimmickTargetKind.Normal, 80f));
            Assert.Equal(0f, bell.StunSeconds(GimmickTargetKind.Normal, 8f * Jeoljeong + 1f));
        }

        [Fact] public void 원각이_떨어뜨린_범종은_12초_동안_한_번만_울린다()
        {
            var drop = _d.BossPatternSet("wongak").Pattern("bell_drop_x3");
            Assert.Equal(G.Bell.Id, drop.LeaveGimmick);

            var used = BellGimmick.FromBossDrop(G.Bell, drop);
            Assert.Equal(1, used.UsesLeft);
            Assert.True(used.TryRing(1f));
            Assert.True(used.Gone);
            used.Tick(60f);
            Assert.False(used.TryRing(1f));

            var left = BellGimmick.FromBossDrop(G.Bell, drop);
            left.Tick(11.9f);
            Assert.False(left.Gone);
            left.Tick(0.2f);
            Assert.True(left.Gone);
            Assert.False(left.TryRing(1f));
        }

        // ---- 탑림 ----

        [Fact] public void 탑림의_돌탑은_무더기_안에_서고_서로_좁은_길_폭_이상_떨어진다()
        {
            var p = G.PagodaForest;
            Assert.True(PagodaForest.NominalGap(p) > p.MinGap);
            for (int seed = 1; seed <= 20; seed++)
            {
                var stones = PagodaForest.Layout(p, new Random(seed));
                Assert.InRange(stones.Count, 20, 60);
                foreach (var s in stones)
                {
                    Assert.Equal(p.PagodaRadius, s.Radius);
                    Assert.True(Math.Sqrt(s.X * s.X + s.Y * s.Y) + s.Radius <= p.PatchRadius + 1e-4);
                }
                for (int i = 0; i < stones.Count; i++)
                for (int j = i + 1; j < stones.Count; j++)
                {
                    double d = Math.Sqrt(Math.Pow(stones[i].X - stones[j].X, 2) + Math.Pow(stones[i].Y - stones[j].Y, 2));
                    Assert.True(d - 2 * p.PagodaRadius >= p.MinGap - 1e-4, $"시드 {seed}: 탑 {i}·{j} 사이 {d - 2 * p.PagodaRadius:0.00}m");
                }
            }
        }

        [Fact] public void 탑림은_같은_시드면_같은_배치이고_축척은_자리와_크기에만_곱한다()
        {
            var p = G.PagodaForest;
            var a = PagodaForest.Layout(p, new Random(3));
            var again = PagodaForest.Layout(p, new Random(3));
            var big = PagodaForest.Layout(p, new Random(3), Jeoljeong);
            Assert.Equal(a.Select(s => (s.X, s.Y)), again.Select(s => (s.X, s.Y)));
            Assert.Equal(a.Count, big.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].X * Jeoljeong, big[i].X, 3);
                Assert.Equal(a[i].Y * Jeoljeong, big[i].Y, 3);
                Assert.Equal(a[i].Radius * Jeoljeong, big[i].Radius, 4);
            }
            Assert.NotEqual(a.Select(s => (s.X, s.Y)), PagodaForest.Layout(p, new Random(4)).Select(s => (s.X, s.Y)));
        }

        [Fact] public void 탑림은_걷는_것을_막고_나는_적은_지나간다()
        {
            Assert.True(PagodaForest.BlocksMovement(G.PagodaForest, flying: false));
            Assert.False(PagodaForest.BlocksMovement(G.PagodaForest, flying: true));
        }

        // ---- 우물 ----

        [Fact] public void 우물은_다가가면_한_번_최대_기혈의_절반을_채운다()
        {
            var well = new WellGimmick(G.Well);
            Assert.Equal(0f, well.TryUse(1.6f, 120f));
            Assert.False(well.Dry);
            Assert.Equal(60f, well.TryUse(1.5f, 120f));
            Assert.True(well.Dry);
            Assert.Equal(0f, well.TryUse(0.2f, 120f));

            var big = new WellGimmick(G.Well, Jeoljeong);
            Assert.Equal(50f, big.TryUse(10f, 100f));
        }

        // ---- 독안개 ----

        [Fact] public void 독안개는_90초마다_4초에_퍼져_20초_머물고_4초에_걷힌다()
        {
            var fog = new PoisonFogGimmick(G.PoisonFog);
            Assert.Equal(FogPhase.Clear, fog.PhaseAt(0f));
            Assert.Equal(FogPhase.Clear, fog.PhaseAt(89.9f));
            Assert.Equal(0f, fog.RadiusAt(89.9f));
            Assert.Equal(FogPhase.Spreading, fog.PhaseAt(90f));
            Assert.Equal(5f, fog.RadiusAt(92f), 4);
            Assert.Equal(FogPhase.Holding, fog.PhaseAt(94f));
            Assert.Equal(10f, fog.RadiusAt(94f));
            Assert.Equal(FogPhase.Holding, fog.PhaseAt(113.9f));
            Assert.Equal(FogPhase.Fading, fog.PhaseAt(114f));
            Assert.Equal(5f, fog.RadiusAt(116f), 4);
            Assert.Equal(FogPhase.Clear, fog.PhaseAt(118f));
            Assert.Equal(FogPhase.Spreading, fog.PhaseAt(180.5f));
            Assert.Equal(FogPhase.Holding, fog.PhaseAt(30 * 60f + 10f)); // 30분 = 90초 x 20, 10초째

            Assert.Equal(90f, fog.NextSpreadAt(0f));
            Assert.Equal(180f, fog.NextSpreadAt(100f));
            Assert.Equal(270f, fog.NextSpreadAt(180f));
        }

        [Fact] public void 독안개_안에선_누구나_초당_최대_기혈_1퍼센트를_잃고_보스와_독_면역은_잃지_않는다()
        {
            var fog = new PoisonFogGimmick(G.PoisonFog);
            Assert.Equal(1f, fog.HealthLoss(GimmickTargetKind.Player, 100f, 1f), 5);
            Assert.Equal(0.1f, fog.HealthLoss(GimmickTargetKind.Normal, 20f, 0.5f), 5);
            Assert.Equal(10f, fog.HealthLoss(GimmickTargetKind.Elite, 1000f, 1f), 4);
            Assert.Equal(0f, fog.HealthLoss(GimmickTargetKind.Boss, 90000f, 1f));
            Assert.Equal(0f, fog.HealthLoss(GimmickTargetKind.Player, 100f, 1f, poisonImmune: true));
            // 다 머무는 동안(20초) 안에 서 있으면 최대 기혈 20%
            Assert.Equal(20f, fog.HealthLoss(GimmickTargetKind.Player, 100f, G.PoisonFog.HoldSeconds), 4);
        }

        [Fact] public void 독안개_안에_있는_동안_플레이어가_얻는_진기는_두_배()
        {
            var fog = new PoisonFogGimmick(G.PoisonFog);
            Assert.True(fog.Contains(9.9f, 100f));
            Assert.False(fog.Contains(10.1f, 100f));
            Assert.False(fog.Contains(0f, 50f)); // 퍼지기 전
            Assert.Equal(2f, fog.QiMultiplier(fog.Contains(3f, 100f)));
            Assert.Equal(1f, fog.QiMultiplier(fog.Contains(3f, 130f)));
        }

        [Fact] public void 절정에_생긴_독안개는_반경만_축척만큼_크다()
        {
            var fog = new PoisonFogGimmick(G.PoisonFog, Jeoljeong);
            Assert.Equal(10f * Jeoljeong, fog.RadiusAt(100f), 3);
            Assert.True(fog.Contains(100f, 100f));
            Assert.Equal(FogPhase.Clear, fog.PhaseAt(118f));
        }

        // ---- 물 ----

        [Fact] public void 물에선_플레이어든_적이든_0점7배이고_나는_적은_그대로()
        {
            var w = G.Water;
            Assert.Equal(0.7f, WaterGimmick.MoveSpeedMultiplier(w, inWater: true));
            Assert.Equal(1f, WaterGimmick.MoveSpeedMultiplier(w, inWater: false));
            Assert.Equal(1f, WaterGimmick.MoveSpeedMultiplier(w, inWater: true, flying: true));
            Assert.Equal(1f, WaterGimmick.MoveSpeedMultiplier(w, inWater: true, waterWalking: true));
            Assert.Equal(1f, WaterGimmick.MoveSpeedMultiplier(w, inWater: true, ignoresWater: true)); // 수귀, 수로채 성질
            // 날아다니는 적은 데이터에 있다(비강)
            Assert.Contains(_d.Enemies, e => e.Id == "bigang");
        }

        [Fact] public void 경공을_쓰고_3초는_수상비로_물에서_느려지지_않는다()
        {
            var ww = new WaterWalk(G.Water);
            Assert.False(ww.Active);
            ww.OnDash();
            ww.Tick(2.9f);
            Assert.True(ww.Active);
            Assert.Equal(1f, WaterGimmick.MoveSpeedMultiplier(G.Water, true, waterWalking: ww.Active));
            ww.Tick(0.1f);
            Assert.False(ww.Active);
            Assert.Equal(0.7f, WaterGimmick.MoveSpeedMultiplier(G.Water, true, waterWalking: ww.Active));
            ww.OnDash();
            Assert.Equal(3f, ww.Remaining);
        }

        // ---- 태극 바닥 ----

        [Fact] public void 태극_바닥은_게이지와_반대_속성_무공을_30초_동안_30퍼센트_올린다()
        {
            var floor = new TaijiFloorGimmick(G.TaijiFloor);
            var bless = new TaijiBlessing(G.TaijiFloor);
            Assert.False(floor.TryStep(2.1f, 30, bless));
            Assert.True(floor.TryStep(2f, 30, bless));
            Assert.Equal(YinYang.Yin, bless.Aspect);
            Assert.Equal(1.3f, bless.DamageMultiplier(YinYang.Yin), 5);
            Assert.Equal(1f, bless.DamageMultiplier(YinYang.Yang));
            Assert.Equal(1f, bless.DamageMultiplier(YinYang.Neutral));
            bless.Tick(29.9f);
            Assert.True(bless.Active);
            bless.Tick(0.2f);
            Assert.False(bless.Active);
            Assert.Equal(YinYang.Neutral, bless.Aspect);
            Assert.Equal(1f, bless.DamageMultiplier(YinYang.Yin));
        }

        [Fact] public void 게이지가_0이면_태극_바닥은_아무_일도_없고_쓰이지도_않는다()
        {
            var floor = new TaijiFloorGimmick(G.TaijiFloor);
            var bless = new TaijiBlessing(G.TaijiFloor);
            Assert.False(floor.TryStep(1f, 0, bless));
            Assert.True(floor.Ready);
            Assert.False(bless.Active);
            Assert.Equal(YinYang.Yang, TaijiBlessing.OppositeOf(-1));
            Assert.Equal(YinYang.Yin, TaijiBlessing.OppositeOf(1));
        }

        [Fact] public void 바닥_하나는_60초에_한_번이고_다른_바닥을_밟으면_새로_건다()
        {
            var a = new TaijiFloorGimmick(G.TaijiFloor);
            var b = new TaijiFloorGimmick(G.TaijiFloor);
            var bless = new TaijiBlessing(G.TaijiFloor);
            Assert.True(a.TryStep(1f, -40, bless));
            Assert.Equal(YinYang.Yang, bless.Aspect);
            a.Tick(30f); bless.Tick(20f);
            Assert.False(a.TryStep(1f, -40, bless));
            Assert.Equal(10f, bless.Remaining, 4);
            Assert.True(b.TryStep(1f, 10, bless));
            Assert.Equal(YinYang.Yin, bless.Aspect);
            Assert.Equal(30f, bless.Remaining);
            a.Tick(30f);
            Assert.True(a.Ready);
        }

        [Fact] public void 양_무공을_올린_몸이_태극_바닥을_밟으면_음_무공이_세진다()
        {
            var g = new YinYangGauge(_d.Balance.Yinyang);
            g.OnWeaponLevelUp(_d.Weapon("nahan_fist"));
            g.OnWeaponLevelUp(_d.Weapon("nahan_fist"));
            Assert.True(g.Value > 0);
            Assert.Equal(YinYangBand.Harmony, g.Band); // 태극 구간이어도 기운 쪽이 있으면 반대가 오른다

            var bless = new TaijiBlessing(G.TaijiFloor);
            Assert.True(new TaijiFloorGimmick(G.TaijiFloor).TryStep(0f, g.Value, bless));
            Assert.Equal(1.3f, bless.DamageMultiplier(_d.Weapon("flying_dagger").Aspect), 5);
            Assert.Equal(1f, bless.DamageMultiplier(_d.Weapon("nahan_fist").Aspect));
            Assert.Equal(1f, bless.DamageMultiplier(_d.Weapon("samjae_sword").Aspect));
        }
    }
}
