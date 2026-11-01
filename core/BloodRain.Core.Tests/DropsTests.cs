using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>음식·은자·혈정·등롱(기획서 8장 '드롭', balance.json drops).</summary>
    public class DropsTests
    {
        readonly GameData _d = TestData.Load();
        DropsDef R => _d.Balance.Drops;

        [Fact] public void 음식_회복은_기획서_드롭_표와_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"\| 만두 / 오리구이 \| 기혈 (\d+) / (\d+) 회복");
            Assert.True(m.Success, "기획서 8장 드롭 표에 음식 줄이 없다");
            Assert.Equal(float.Parse(m.Groups[1].Value), R.BunHeal);
            Assert.Equal(float.Parse(m.Groups[2].Value), R.DuckHeal);
        }

        [Fact] public void 은자_주머니와_잡몹_확률은_기획서와_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"\| 은자 주머니 \| 은자 (\d+)\\~(\d+) \| [^|]*잡몹이 ([\d.]+)% 확률로");
            Assert.True(m.Success, "기획서 8장 드롭 표에 은자 주머니 줄이 없다");
            Assert.Equal(new[] { int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value) }, R.SilverBag.ToArray());
            Assert.Equal(double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) / 100, R.EnemySilverChance, 4);
        }

        [Fact] public void 정신부_정지_시간은_기획서와_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"정신부\([^)]*화면 안 적 (\d+)초 정지\)");
            Assert.True(m.Success, "기획서 7장 부적 드롭에 정신부 정지 시간이 없다");
            Assert.Equal(float.Parse(m.Groups[1].Value), R.JeongsinFreezeSeconds);
            Assert.Contains("G.freeze = DR.jeongsinFreezeSeconds", TestData.ReadRepoFile("web", "prototype", "template.html"));
        }

        [Fact] public void 정예_혈정은_기획서와_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"\| 혈정 \| [^|]+\| 정예 (\d+)개, 보스 (\d+)개");
            Assert.True(m.Success);
            Assert.Equal(int.Parse(m.Groups[1].Value), R.EliteBloodCrystal);
            // 보스 몫은 cheonha.json bosses 가 들고 있다(첫 처치·다시 처치 모두).
            int boss = int.Parse(m.Groups[2].Value);
            Assert.Equal(boss, _d.Cheonha.Bosses.FirstKill.BloodCrystal);
            Assert.Equal(boss, _d.Cheonha.Bosses.RepeatKill.BloodCrystal);
        }

        [Fact] public void 등롱_비율은_기획서와_같고_합이_1이다()
        {
            var m = Regex.Match(TestData.Gdd, @"등롱 하나를 부수면 만두 (\d+)%, 오리구이 (\d+)%, 취기부 (\d+)%, 정신부 (\d+)%, 은자 주머니 (\d+)%");
            Assert.True(m.Success, "기획서 8장 드롭 절에 등롱 비율 문단이 없다");
            var kinds = new[] { Drops.Bun, Drops.Duck, Drops.ChwigiTalisman, Drops.JeongsinTalisman, Drops.SilverBag };
            Assert.Equal(kinds, R.Lantern.Select(l => l.Kind).ToArray());
            for (int i = 0; i < kinds.Length; i++) Assert.Equal(int.Parse(m.Groups[i + 1].Value) / 100f, R.Lantern[i].Weight, 4);
            Assert.Equal(1.0, R.Lantern.Sum(l => (double)l.Weight), 4);
        }

        [Fact] public void 등롱은_비율대로_나온다()
        {
            var rng = new Random(11);
            const int n = 40000;
            var counts = R.Lantern.ToDictionary(l => l.Kind, _ => 0);
            for (int i = 0; i < n; i++) counts[Drops.RollLantern(R, rng)]++;
            foreach (var l in R.Lantern) Assert.InRange(counts[l.Kind] / (double)n, l.Weight - 0.015, l.Weight + 0.015);
        }

        [Fact] public void 등롱_표가_비거나_음수면_막는다()
        {
            var rng = new Random(1);
            Assert.Throws<InvalidOperationException>(() => Drops.RollLantern(new DropsDef(), rng));
            var bad = new DropsDef();
            bad.Lantern.Add(new LanternDropDef { Kind = Drops.Bun, Weight = -1 });
            Assert.Throws<InvalidOperationException>(() => Drops.RollLantern(bad, rng));
        }

        [Fact] public void 음식_회복에_배율을_곱하고_음식이_아니면_0()
        {
            Assert.Equal(20f, Drops.FoodHeal(R, Drops.Bun));
            Assert.Equal(100f, Drops.FoodHeal(R, Drops.Duck, 2f));
            Assert.Equal(0f, Drops.FoodHeal(R, Drops.SilverBag, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Drops.FoodHeal(R, Drops.Bun, -1f));
        }

        [Fact] public void 은자_주머니는_양끝을_포함한_범위다()
        {
            var rng = new Random(3);
            var seen = Enumerable.Range(0, 5000).Select(_ => Drops.RollSilverBag(R, rng)).ToList();
            Assert.Equal(R.SilverBag[0], seen.Min());
            Assert.Equal(R.SilverBag[1], seen.Max());
        }

        [Fact] public void 잡몹_은자_확률()
        {
            var rng = new Random(5);
            int hit = Enumerable.Range(0, 100000).Count(_ => Drops.EnemyDropsSilver(R, rng));
            Assert.InRange(hit / 100000.0, R.EnemySilverChance - 0.002, R.EnemySilverChance + 0.002);
        }
    }
}
