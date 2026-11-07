using System;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Run;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>한 생이 기획서 11장 '적 능력치 증가'(경과 분 × 지금 자리 혈운 농도)를 내주는지(EN-02).</summary>
    public class LifeEnemyScalingTests
    {
        readonly GameData _d = TestData.Load();

        Life NewLife(string sect, bool bloodmoon = false)
        {
            var soul = new Soul(_d);
            soul.EndLife(soul.BeginLife(Rebirth.Prologue(_d)).Finish(LifeEndWay.Fallen, 0));
            if (bloodmoon) soul.Regress();
            var c = _d.Cheonha;
            var cand = new RebirthCandidate(c.Sect(sect), c.Region(c.Sect(sect).Region), Array.Empty<BirthTraitDef>());
            return new Life(soul, Rebirth.Body(c, cand), new Random(1));
        }

        [Fact] public void 생_시작엔_기준_곱하기_농도()
        {
            var life = NewLife("shaolin");
            Assert.Equal(0f, life.Minutes);
            Assert.Equal(8f * life.Density, life.EnemyHealth(8f), 3);
            Assert.Equal(10f * (float)Math.Sqrt(life.Density), life.EnemyDamage(10f), 3);
        }

        [Fact] public void 시간이_가면_분마다_오른다()
        {
            var life = NewLife("shaolin");
            for (int i = 0; i < 120 && life.Minutes < 1f; i++) life.Tick(1f, life.Position, 0);
            float m = life.Minutes;
            Assert.True(m > 0f);
            var s = _d.Balance.EnemyScaling;
            Assert.Equal(8f * (1f + s.HealthPerMinute * m) * life.Density, life.EnemyHealth(8f), 3);
            Assert.Equal(10f * (1f + s.DamagePerMinute * m) * (float)Math.Sqrt(life.Density), life.EnemyDamage(10f), 3);
        }

        [Fact] public void 짙은_땅에서_나면_더_단단하다()
        {
            // 마교 몸은 십만대산(혈운 4.3)에서 난다 — 같은 시각 숭산 몸보다 적 기혈이 농도 비만큼 높다
            var thin = NewLife("shaolin");
            var thick = NewLife("demonic");
            Assert.True(thick.Density > thin.Density);
            Assert.Equal(thick.Density / thin.Density, thick.EnemyHealth(8f) / thin.EnemyHealth(8f), 3);
        }

        [Fact] public void 혈월이면_기혈만_더_곱한다()
        {
            var plain = NewLife("shaolin");
            var moon = NewLife("shaolin", bloodmoon: true);
            Assert.Equal(plain.EnemyHealth(8f) * _d.Cheonha.Cycle.Bloodmoon.EnemyHealth, moon.EnemyHealth(8f), 3);
            Assert.Equal(plain.EnemyDamage(10f), moon.EnemyDamage(10f), 3);
        }

        [Fact] public void 기획서_11장_숭산_20분_시강_예와_같다()
        {
            // "숭산(1.0) 20분의 시강은 기혈 8 x 2.6 = 약 21"
            var sc = new EnemyScaling(_d.Balance.EnemyScaling);
            Assert.Equal(20.8f, sc.HealthAt(_d.Enemy("sigang").Health, 20f, 1f), 1);
            Assert.Contains("숭산(1.0) 20분의 시강은 기혈 8 x 2.6", TestData.ReadRepoFile("docs", "design", "gdd.md"));
        }
        [Fact] public void 웹_한_생_판도_농도로_곱한다()
        {
            // 웹 spawnEnemy·spawnElite·정예 패턴이 Core 와 같은 식(한 생 판은 G.dens, 피해는 √)
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains("(1 + B.enemyScaling.healthPerMinute * G.gm) * (G.body ? Math.max(0, G.dens || 1)", html);
            Assert.Contains("(1 + B.enemyScaling.damagePerMinute * G.gm) * (G.body ? Math.sqrt(Math.max(0, G.dens || 1))", html);
            Assert.Contains("const hp = def.health * foeHpMul();", html);
            Assert.Contains("const hp = d.hp * foeHpMul();", html);
            Assert.Contains("dmg: def.contactDamage * foeDmgMul()", html);
        }
    }
}
