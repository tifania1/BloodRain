using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 9장 '절기로 바뀐 뒤 날아가는 것' — 옮아가는 셈이 웹 프로토타입(lightning·random_lightning 절기·bomb·탄 튕김)과 같은지.
    public class EvolvedHitsTests
    {
        readonly GameData _d = TestData.Load();
        EvolutionDef Evo(string id) => _d.Evolutions.First(e => e.Id == id);

        // 한 줄로 늘어선 적: 0, 2, 4, 6, 8m
        static readonly float[] Xs = { 0f, 2f, 4f, 6f, 8f };
        static readonly float[] Ys = { 0f, 0f, 0f, 0f, 0f };

        [Fact]
        public void 천뢰검진_첫_줄기_다음_곁의_적으로_두_번_0_8배씩()
        {
            var hops = EvolvedHits.SkyLightning(Evo("cheonroe_array"), 0, Xs, Ys);
            Assert.Equal(3, hops.Count);
            Assert.Equal(new ChainHop(-1, 0, 1f).ToString(), hops[0].ToString());
            Assert.Equal(1, hops[1].To); Assert.Equal(0.8f, hops[1].DamageScale, 4);
            Assert.Equal(2, hops[2].To); Assert.Equal(0.64f, hops[2].DamageScale, 4);
        }

        [Fact]
        public void 곁의_적이_멀면_거기서_멈춘다()
        {
            // 천뢰검진 2.5m: 0 → 2m 는 닿고, 그다음 5.5m 는 안 닿음
            var hops = EvolvedHits.SkyLightning(Evo("cheonroe_array"), 0, new[] { 0f, 2f, 7.5f }, new[] { 0f, 0f, 0f });
            Assert.Equal(new[] { 0, 1 }, hops.Select(h => h.To));
        }

        [Fact]
        public void 천뢰검진_확률은_데이터_25퍼센트()
        {
            var e = Evo("cheonroe_array");
            Assert.True(EvolvedHits.SkyLightningRolls(e, 0.24));
            Assert.False(EvolvedHits.SkyLightningRolls(e, 0.25));
        }

        [Fact]
        public void 오뢰정법_한_곳에_맞은_적끼리는_서로_옮아가지_않는다()
        {
            // 0·2m 가 낙뢰에 같이 맞음. 0 은 곁(3m)에 안 맞은 적이 없어 멈추고, 2 → 4 → 6 (0.6, 0.36)
            var hops = EvolvedHits.FiveThunders(Evo("five_thunders"), new[] { 0, 1 }, Xs, Ys);
            Assert.Equal(new[] { "1->2 x0.6", "2->3 x0.36" }, hops.Select(h => h.ToString()));
        }

        [Fact]
        public void 무영비도는_6m_안_아직_안_맞힌_가장_가까운_적으로()
        {
            var e = Evo("muyeong_dagger");
            Assert.Equal(2, EvolvedHits.NextBounce(e, 2f, 0f, Xs, Ys, new HashSet<int> { 0, 1 }));
            Assert.Equal(-1, EvolvedHits.NextBounce(e, 0f, 0f, new[] { 0f, 6.5f }, new[] { 0f, 0f }, new HashSet<int> { 0 }));
        }

        [Fact]
        public void 만천성지는_치명타일_때만_파산장은_늘()
        {
            Assert.False(EvolvedHits.Explodes(Evo("mancheon_star"), false));
            Assert.True(EvolvedHits.Explodes(Evo("mancheon_star"), true));
            var pasan = _d.Evolutions.FirstOrDefault(x => x.Params != null && x.Params.ContainsKey("explodeRadius") && !x.Params.ContainsKey("explodeOnCrit"));
            if (pasan != null) Assert.True(EvolvedHits.Explodes(pasan, false));
        }

        [Fact]
        public void 벽력천화는_세_번_0_2초_간격_반경_0_9배_피해_0_8배_마지막에_불바다()
        {
            var e = Evo("byeokryeok_fire");
            var bombs = EvolvedHits.ChainBombs(e, 0f, 0f, 2f, () => 0.0);   // 늘 오른쪽
            Assert.Equal(3, bombs.Count);
            Assert.Equal(0.4f, bombs[2].Delay, 4);
            // 다음 폭발은 지금 반경만큼 떨어진 자리: 0 → 2 → 2 + 1.8
            Assert.Equal(2f, bombs[1].X, 4); Assert.Equal(3.8f, bombs[2].X, 4);
            Assert.Equal(1.62f, bombs[2].Radius, 4);
            Assert.Equal(0.64f, bombs[2].DamageScale, 4);
            var (r, s) = EvolvedHits.FireField(e, bombs[^1]);
            Assert.Equal(1.62f, r, 4); Assert.Equal(5f, s);
        }

        [Fact]
        public void 천지인_삼검은_세_방향에_세_번째마다_검기()
        {
            var d = EvolvedHits.FrontArcDirections(0f, true, false);
            Assert.Equal(new[] { 0f, 2.0944f, -2.0944f }, d.Select(v => (float)Math.Round(v, 4)));
            Assert.Equal(2, EvolvedHits.FrontArcDirections(0f, false, true).Length);
            var e = Evo("cheonjiin");
            Assert.Equal(new[] { 3, 6 }, Enumerable.Range(1, 7).Where(n => EvolvedHits.SendsWave(e, n)));
        }

        [Fact]
        public void 백보신권은_세_갈래_0_5라디안()
        {
            var a = EvolvedHits.FanAngles(Evo("baekbo_fist"), 1f);
            Assert.Equal(new[] { 0.5f, 1f, 1.5f }, a.Select(v => (float)Math.Round(v, 4)));
        }

        [Fact]
        public void 칠보독무는_7초_머물면_처형_정예는_안_됨()
        {
            var e = Evo("chilbo_mist");
            Assert.False(EvolvedHits.PoisonExecutes(e, 6.9f, false));
            Assert.True(EvolvedHits.PoisonExecutes(e, 7f, false));
            Assert.False(EvolvedHits.PoisonExecutes(e, 99f, true));
        }

        [Fact]
        public void 같은_거리면_앞_번호()
        {
            Assert.Equal(0, EvolvedHits.NearestExcept(0f, 0f, new[] { 1f, -1f }, new[] { 0f, 0f }, 5f, null));
        }

        [Fact]
        public void 웹_프로토타입에_같은_셈이_있다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("d *= lp.lightningFalloff; hit.add(nx);", html);
            Assert.Contains("d *= ep.lightningFalloff; hit.add(nx);", html);
            Assert.Contains("bomb(x + Math.cos(a) * R, y + Math.sin(a) * R, R * ep.chainShrink, dmg * ep.chainDamage", html);
            Assert.Contains("nearestEnemies(pr.x, pr.y, 1, pr.bounceRange, pr.hit)", html);
            Assert.Contains("w.evolved ? [base, base + TAU / 3, base - TAU / 3]", html);
            Assert.Contains("w.n % ep.waveEvery === 0", html);
            Assert.Contains("a0 + (k - (ep.projectiles - 1) / 2) * ep.projectileSpread", html);
            Assert.Contains("e.poisonT >= ep.executeSeconds", html);
        }
    }
}
