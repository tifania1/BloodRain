using System;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>정예·장삼·땅의 주인 기본 모양 다섯(Core PatternShapes) — 웹 castPattern 과 같은 규칙인지(WP-64).</summary>
    public class PatternShapesTests
    {
        readonly GameData _d = Load();

        BossPatternDef Elite(string set, string id) => _d.ElitePatternSet(set).Pattern(id);
        BossPatternDef Boss(string set, string id) => _d.BossPatternSet(set).Pattern(id);

        [Fact] public void 웹이_같은_기본값과_모양을_쓴다()
        {
            var html = ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("len = P.length || viewDiag() * 0.6", html);                         // DefaultFlightViewRatio
            Assert.Contains("r: P.width ? P.width / 2 : 0.5, dmg, life: len / sp", html);        // FanShotRadius
            Assert.Contains("const sp = P.scatter || 2.5", html);                                // DefaultScatter
            Assert.Contains("r = rand(1.5, Math.max(1.5, P.scatter || 0))", html);               // SummonOffsets
            Assert.Contains("a0 + (P.count > 1 ? -half + 2 * half * i / (P.count - 1) : 0)", html); // FanDegrees
            Assert.Contains("const step = P.telegraphSeconds + P.waveIntervalSeconds", html);    // JumpTakeoffSeconds
            Assert.Contains("e.windup = P.telegraphSeconds + P.length / sp + (P.recoverySeconds || 0)", html); // LineHoldSeconds
            Assert.Contains("P.arrangement === 'big_dipper' ? bigDipperAt(", html);
            var js = "const BIG_DIPPER = [" + string.Join(", ", BossPatterns.BigDipperShape.Select(s =>
                $"[{s.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {s.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}]")) + "];";
            Assert.Contains(js, html);
            Assert.Equal(0.5f, PatternShapes.DefaultShotRadius);
            Assert.Equal(0.6f, PatternShapes.DefaultFlightViewRatio);
            Assert.Equal(2.5f, PatternShapes.DefaultScatter);
            Assert.Equal(1.5f, PatternShapes.SummonMinDistance);
        }

        [Fact] public void 부채_탄은_양_끝을_포함해_고르게_하나면_조준_그대로()
        {
            var palm = Elite("gyeyul_monk", "palm_triple");
            var a = PatternShapes.FanDegrees(palm, 90f);
            Assert.Equal(3, a.Length);
            Assert.Equal(90f - palm.AngleDegrees / 2f, a[0], 3);
            Assert.Equal(90f, a[1], 3);
            Assert.Equal(90f + palm.AngleDegrees / 2f, a[2], 3);
            var ice = Elite("yeti_king", "ice_boulder");
            Assert.Equal(new[] { 30f }, PatternShapes.FanDegrees(ice, 30f));
        }

        [Fact] public void 얼음덩이는_기획서_지름_1점2m_나머지는_기본_반경()
        {
            Assert.Equal(0.6f, PatternShapes.FanShotRadius(Elite("yeti_king", "ice_boulder")), 3);
            Assert.Equal(PatternShapes.DefaultShotRadius, PatternShapes.FanShotRadius(Elite("gyeyul_monk", "palm_triple")));
        }

        [Fact] public void 길이가_없는_혈침_탄막은_화면_대각선_비율만큼()
        {
            // 종장 혈의의 혈침 탄막(final stages)은 length 가 없다 — 같은 꼴로
            var needle = new BossPatternDef { Shape = "projectile_fan", Count = 9, ProjectileSpeed = 180f };
            Assert.Equal(12f, PatternShapes.FanFlightLength(needle, 20f), 3);
            var palm = Elite("gyeyul_monk", "palm_triple");
            Assert.Equal(palm.Length, PatternShapes.FanFlightLength(palm, 20f));
            float mb = _d.Balance.Stats.MoveBase;
            Assert.Equal(palm.Length / (palm.ProjectileSpeed / 100f * mb), PatternShapes.FanFlightSeconds(palm, 20f, mb), 3);
            Assert.Equal(palm.TelegraphSeconds, PatternShapes.WaveSeconds(palm, 0));
        }

        [Fact] public void 뛰어_내려찍기_마지막_착지가_CastSeconds_와_같다()
        {
            var jumps = _d.ElitePatterns.Values.SelectMany(s => s.Patterns).Concat(_d.BossPatterns.Values.SelectMany(s => s.Patterns))
                .Where(p => p.Shape == "jump_slam").ToList();
            Assert.NotEmpty(jumps);
            foreach (var p in jumps)
            {
                Assert.Equal(0f, PatternShapes.JumpTakeoffSeconds(p, 0));
                Assert.Equal(BossPatterns.CastSeconds(p), PatternShapes.JumpLandSeconds(p, p.Count - 1), 3);
                for (int k = 1; k < p.Count; k++) Assert.True(PatternShapes.JumpTakeoffSeconds(p, k) > PatternShapes.JumpLandSeconds(p, k - 1));
            }
        }

        [Fact] public void 원_자리_하나면_플레이어_자리_여럿이면_흩고_칠성은_북두칠성()
        {
            var rng = new Random(7);
            Assert.Equal(new[] { (0f, 0f) }, PatternShapes.CircleCenters(Elite("sulsa_elder", "great_blood_pool"), rng, 0f));
            var bell = Boss("wongak", "bell_drop_x3");
            var c = PatternShapes.CircleCenters(bell, rng, 0f);
            Assert.Equal(bell.Count, c.Length);
            Assert.All(c, o => Assert.True(Math.Abs(o.x) <= bell.Scatter && Math.Abs(o.y) <= bell.Scatter));
            var star = Boss("hyeolheo", "seven_star_sword_drop");
            var s = PatternShapes.CircleCenters(star, rng, 45f);
            Assert.Equal(BossPatterns.BigDipperOffsets(star, 45f), s);
            Assert.Equal((0f, 0f), s[6]);
        }

        [Fact] public void 부르기는_가장_가까워도_1점5m_흩는_폭_안()
        {
            var rng = new Random(3);
            foreach (var p in _d.ElitePatterns.Values.SelectMany(s => s.Patterns).Concat(_d.BossPatterns.Values.SelectMany(s => s.Patterns)).Where(p => p.Shape == "summon"))
            {
                var o = PatternShapes.SummonOffsets(p, rng);
                Assert.Equal(p.Count, o.Length);
                float far = Math.Max(PatternShapes.SummonMinDistance, p.Scatter);
                Assert.All(o, v => { var d = Math.Sqrt(v.x * v.x + v.y * v.y); Assert.InRange(d, PatternShapes.SummonMinDistance - 1e-3, far + 1e-3); });
            }
        }

        [Fact] public void 돌진은_예고_달리기_굳음_일섬은_예고_굳음()
        {
            float mb = _d.Balance.Stats.MoveBase;
            var shield = Elite("ironclad_fort_guard", "shield_charge");
            Assert.Equal(shield.TelegraphSeconds + shield.Length / (shield.ProjectileSpeed / 100f * mb) + shield.RecoverySeconds, PatternShapes.LineHoldSeconds(shield, mb), 3);
            var slash = _d.BossPatterns.Values.SelectMany(s => s.Patterns.Concat(s.Phase2)).First(p => p.Id == "screen_slash");
            Assert.Equal(0f, PatternShapes.ChargeRunSeconds(slash, mb));
            Assert.Equal(slash.TelegraphSeconds + slash.RecoverySeconds, PatternShapes.LineHoldSeconds(slash, mb), 3);
        }

        [Fact] public void 여러_파는_마지막_파가_CastSeconds_와_같고_갈고리만_다시_겨눈다()
        {
            var html = ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("const a = w ? Math.atan2(G.player.y - e.y, G.player.x - e.x) : ca;", html);
            Assert.Contains("const cx = P.center === 'boss' ? e.x : G.player.x", html);
            var sword = Elite("huashan_elder_gangsi", "sword_triple");
            var hook = Elite("pirate_chief", "hook_x3");
            foreach (var p in new[] { sword, hook, Boss("dang_sohye", "dart_fan_x3") })
                Assert.Equal(BossPatterns.CastSeconds(p), PatternShapes.WaveSeconds(p, p.Waves - 1), 3);
            Assert.False(PatternShapes.WaveReaims(hook, 0));
            Assert.True(PatternShapes.WaveReaims(hook, 1));
            Assert.False(PatternShapes.WaveReaims(sword, 1));
            Assert.True(PatternShapes.SummonAroundCaster(Elite("water_ghost_chief", "call_water_ghosts")));
            Assert.False(PatternShapes.SummonAroundCaster(Boss("jang_sam", "whistle_summon")));
        }

        [Fact] public void 장판이_있는_정예_패턴은_웹도_남긴다()
        {
            var html = ReadRepoFile("web", "prototype", "template.html");
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, @"if \(P\.poolRadius\) \(G\.pools = G\.pools \|\| \[\]\)\.push").Count);  // circles·jump_slam·circles self
            Assert.True(Elite("poison_toad_king", "poison_hop_x3").PoolRadius > 0f);
            Assert.True(Elite("dogin_elder", "fog_expand").PoolRadius > 0f);
        }
    }
}
