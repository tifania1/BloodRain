using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>비전 구성 시뮬레이션(SecretSim, docs/design/sim/secrets.md). 시간 바꾸기, 구성 판정, 게이지 셈, 무명검 막힘을 본다.</summary>
    public class SecretSimTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static GameData G => Data.Value.Game;
        static SecretDef Secret(string id) => G.Synergies.Secrets.First(s => s.Id == id);

        // 1분에 레벨 4씩 오르는 곧은 곡선(RunSimulator 를 안 돌려도 되게)
        static readonly List<(double, double)> Line = Enumerable.Range(0, 31).Select(m => ((double)m, 1.0 + 4 * m)).ToList();

        [Fact] public void 레벨_곡선을_분으로_바꾼다()
        {
            Assert.Equal(0, SecretSim.MinuteOfLevel(Line, 1), 9);
            Assert.Equal(2.5, SecretSim.MinuteOfLevel(Line, 11), 9);
            Assert.Equal(30, SecretSim.MinuteOfLevel(Line, 121), 9);
            Assert.True(double.IsNaN(SecretSim.MinuteOfLevel(Line, 122)));
            // 레벨이 그대로인 구간은 다음 칸 끝 분
            var flat = new List<(double, double)> { (0, 1), (1, 5), (2, 5), (3, 9) };
            Assert.Equal(2.5, SecretSim.MinuteOfLevel(flat, 7), 9);
        }

        [Fact] public void 못_보는_조건이_붙은_비전만_부분_판정()
        {
            Assert.False(SecretSim.HasUnseen(Secret("maehwa_samnong")));
            Assert.False(SecretSim.HasUnseen(Secret("mugeuk")));
            Assert.False(SecretSim.HasUnseen(Secret("mumyeong")));   // 경과 시간은 시뮬레이터가 센다
            Assert.True(SecretSim.HasUnseen(Secret("nakmae_geomseon")));
            Assert.True(SecretSim.HasUnseen(Secret("eogang")));
            Assert.True(SecretSim.HasUnseen(Secret("chwiseon")));
        }

        [Fact] public void 구성만_보는_비전은_구성_분과_Core_가_연_분이_같다()
        {
            var s = Secret("maehwa_samnong");
            int both = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var r = SecretSim.RunOnce(G, s, "jin_sowoon", Line, seed);
                Assert.Equal(double.IsNaN(r.Build), double.IsNaN(r.Open));
                if (!double.IsNaN(r.Build)) { Assert.Equal(r.Build, r.Open, 9); both++; }
                else Assert.True(r.Blocked, $"시드 {seed}: 못 갖췄는데 칸이 막히지도 않았다");
            }
            Assert.True(both > 0);
        }

        [Fact] public void 게이지_셈이_Core_와_같다()
        {
            foreach (var ch in G.Characters)
            {
                var l = new Loadout(G, ch);
                var rng = new Random(5);
                var unlocked = new HashSet<string>(G.Weapons.Select(w => w.Id));
                for (int i = 0; i < 40; i++)
                {
                    var offers = l.RollLevelUp(rng, 0f, unlocked);
                    var o = offers[rng.Next(offers.Count)];
                    int before = l.Gauge.Value, expect = before + SecretSim.GaugeDelta(G, o);
                    l.Apply(o);
                    int clamped = Math.Max(G.Balance.Yinyang.Min, Math.Min(G.Balance.Yinyang.Max, expect));
                    Assert.Equal(clamped, l.Gauge.Value);
                }
            }
        }

        [Fact] public void 무명검은_다시_고르기가_없으면_새_무공을_억지로_받는다()
        {
            var s = Secret("mumyeong");
            Assert.Contains("jin_sowoon", SecretSim.CharactersFor(G, s));
            for (int seed = 1; seed <= 10; seed++)
            {
                var r = SecretSim.RunOnce(G, s, "jin_sowoon", Line, seed);
                Assert.True(r.Forced);
                Assert.True(double.IsNaN(r.Open));
            }
        }

        [Fact] public void 다시_고르기가_칸_막힘을_줄이고_같은_시드는_같은_결과()
        {
            var s = Secret("maehwa_samnong");
            var a = SecretSim.Measure(G, s, "jin_sowoon", Line, 60);
            var b = SecretSim.Measure(G, s, "jin_sowoon", Line, 60);
            var x = SecretSim.Measure(G, s, "jin_sowoon", Line, 60, SecretSim.MaxRerolls(G));
            Assert.Equal(a.BuildRuns, b.BuildRuns);
            Assert.Equal(a.BuildMinute, b.BuildMinute);
            Assert.True(x.BlockedRuns <= a.BlockedRuns);
            Assert.True(x.BuildRuns >= a.BuildRuns);
            Assert.Equal(5, SecretSim.MaxRerolls(G));   // balance.json training Rerolls 1 × 5
        }

        [Fact] public void 시작_무공이_조건에_든_캐릭터를_고른다()
        {
            foreach (var s in G.Synergies.Secrets.Where(s => s.Require.Levels.Count > 0))
                foreach (var c in SecretSim.CharactersFor(G, s))
                {
                    var start = G.Character(c).StartWeapon;
                    Assert.True(s.Require.Levels.ContainsKey(start) || c == "jin_sowoon", $"{s.Id}: {c}");
                }
            Assert.Equal(new[] { "unhwa" }, SecretSim.CharactersFor(G, Secret("mugeuk")));
        }

        [Fact] public void 무명검_새_무공뿐일_때_은자_음식이_섞이면_억지로_받지_않고_열린다()
        {
            var s = Secret("mumyeong");
            for (int seed = 1; seed <= 10; seed++)
            {
                var r = SecretSim.RunOnce(G, s, "jin_sowoon", Line, seed, SecretSim.Minutes, 0, escape: true);
                Assert.False(r.Forced);
                Assert.False(double.IsNaN(r.Open));
                Assert.True(r.Open >= s.Require.Counts["elapsedSeconds"] / 60 - 1e-6);
            }
        }

        [Fact] public void 버틸_시간만_바꾼_복사본은_원본을_건드리지_않는다()
        {
            var s = Secret("mumyeong");
            double before = s.Require.Counts["elapsedSeconds"];
            var c = SecretSim.WithSeconds(s, 300);
            Assert.Equal(300, c.Require.Counts["elapsedSeconds"]);
            Assert.Equal(before, s.Require.Counts["elapsedSeconds"]);
            Assert.Equal(s.Require.OnlyWeapon, c.Require.OnlyWeapon);
            Assert.Equal(s.Require.Levels, c.Require.Levels);
            Assert.Equal(before, SecretSim.MumyeongOptions(s, 5)[0].Seconds);
        }
    }
}
