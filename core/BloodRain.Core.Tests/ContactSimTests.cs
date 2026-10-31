using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Spawning;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>맞는 쪽 셈(ContactSim, docs/design/sim/contact.md). 피해 식이 Core 와 같은지, 표가 지금 데이터와 같은지 본다.</summary>
    public class ContactSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static Core.Data.GameData G => Data.Value.Game;

        [Fact] public void 몸_둘레에_여섯이_붙는다()
        {
            Assert.Equal(6, ContactSim.SurroundCount);
            // 0.35초에 한 번 × 6 = 초당 약 17대
            Assert.Equal(17.14, ContactSim.SurroundedDps(G, 1), 2);
        }

        [Fact] public void 한_번_피해는_분과_농도_호신을_따른다()
        {
            Assert.Equal(5, ContactSim.PerHit(G, 5, 0, 1, 0, 0), 3);
            // 30분, 농도 4: 5 × 1.9 × 2 = 19
            Assert.Equal(19, ContactSim.PerHit(G, 5, 30, 4, 0, 0), 3);
            Assert.Equal(16, ContactSim.PerHit(G, 5, 30, 4, 3, 0), 3);
            // 호신이 피해보다 크면 최소 피해 1
            Assert.Equal(1, ContactSim.PerHit(G, 5, 0, 1, 10, 0), 3);
            // 받는 피해 -10% 는 호신을 뺀 뒤
            Assert.Equal(16 * 0.9, ContactSim.PerHit(G, 5, 30, 4, 3, 0.1f), 3);
        }

        [Fact] public void 호신_기혈_회복_빌드는_데이터에서_센다()
        {
            var a = ContactSim.ArmorCases(G);
            Assert.Equal(new[] { 0f, 3f, 10f }, a.Select(x => x.Armor).ToArray());
            Assert.Equal(0.8f, ContactSim.MaxHealthBuild(G), 3);
            Assert.Equal(1.25f, ContactSim.RecoveryMax(G), 3);
        }

        [Fact] public void 경지_보상_비율을_읽는다()
        {
            Assert.Equal(0.1f, ContactSim.RewardRatio(G, "iryu", "max_health_"), 3);
            Assert.Equal(0.1f, ContactSim.RewardRatio(G, "hwagyeong", "damage_taken_minus"), 3);
            Assert.Equal(0f, ContactSim.RewardRatio(G, "samryu", "max_health_"));
            Assert.Equal(0f, ContactSim.RewardRatio(G, "없는경지", "max_health_"));
        }

        [Fact] public void 닿는_적은_접촉_피해가_있는_적만()
        {
            var wt = WaveTable.From(G);
            var rs = new RegionSpawns(G);
            foreach (var r in G.Cheonha.Regions)
                foreach (var m in new float[] { 0, 3, 5, 8, 10, 15, 20, 30, 45 })
                {
                    var (avg, max, id) = ContactSim.BaseContact(G, wt, rs, r.Id, m);
                    Assert.True(avg > 0, $"{r.Id} {m}분");
                    Assert.True(max >= avg - 1e-9, $"{r.Id} {m}분");
                    Assert.True(G.Enemies.First(e => e.Id == id).ContactDamage > 0, id);
                }
            // 0분 숭산은 시강뿐
            var (a0, _, id0) = ContactSim.BaseContact(G, wt, rs, "songshan", 0);
            Assert.Equal("sigang", id0);
            Assert.Equal(G.Enemies.First(e => e.Id == "sigang").ContactDamage, a0, 3);
        }

        [Fact] public void 경공_숫자는_기획서_8장과_같다()
        {
            var d = G.Balance.Dash;
            Assert.Equal(4f, d.Distance);
            Assert.Equal(0.25f, d.Duration);
            Assert.Equal(0.2f, d.Iframes);
            Assert.Equal(2, d.Charges);
            Assert.Equal(4f, d.RechargeSeconds);
            var gdd = File.ReadAllText(Path.Combine(TestData.RepoRoot, "docs", "design", "gdd.md"));
            Assert.Contains("0.25초 동안 4m 대시, 시작 0.2초 무적. 기본 2회 충전, 1회 회복에 4초", gdd);
        }

        [Fact] public void 원각_금강장은_20분에_프로토타입_26()
        {
            // bosspatterns.json wongak _note: 프로토타입 20분 값 26 을 20분 배율 1.6 으로 나눠 0분 값으로 적었다.
            var p = G.BossPatterns["wongak"].Pattern("vajra_cone");
            float d = Core.Combat.BossPatterns.ScaleDamage(p.BaseDamage, 20, 1, G.Balance.EnemyScaling.DamagePerMinute);
            Assert.Equal(26f, d, 3);
        }

        [Fact] public void 보고서에_땅의_주인_일곱이_다_있다()
        {
            var text = File.ReadAllText(Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "contact.md"));
            var rows = Regex.Matches(text, @"^\| [^|]+\([^,|]+, [0-9.]+\) \| [0-9]+:[0-5][0-9]", RegexOptions.Multiline);
            Assert.Equal(G.BossPatterns.Count, rows.Count);
        }

        [Fact] public void 보고서는_지금_데이터와_같다()
        {
            var path = Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "contact.md");
            var file = File.ReadAllText(path).Replace("\r\n", "\n");
            var now = ContactSim.Report(Data.Value).Replace("\r\n", "\n");
            const string hint = " — dotnet run --project core/BloodRain.Sim -- --contact docs/design/sim/contact.md 로 다시 쓴다";
            Assert.True(file == now, "contact.md 가 지금 데이터와 다르다" + hint);
        }
    }
}
