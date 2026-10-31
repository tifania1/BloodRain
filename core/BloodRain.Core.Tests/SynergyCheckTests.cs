using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>연환·진전 화력 상한 계산(SynergyCheck, docs/design/sim/synergy.md). S-06 결정에 쓰는 표라 계산이 데이터와 어긋나지 않는지 본다.
    /// SynergyCheck 는 손잡이(Knobs)를 정적 필드로 들고 있어 이 클래스 안에서만 돌린다(같은 클래스 테스트는 차례로 돈다).</summary>
    public class SynergyCheckTests
    {
        static readonly Lazy<SimData> Data = new Lazy<SimData>(() => SimData.Load(TestData.DataDir));
        static readonly Lazy<System.Collections.Generic.List<SynergyCheck.Row>> Rows =
            new Lazy<System.Collections.Generic.List<SynergyCheck.Row>>(() => SynergyCheck.Run(Data.Value));

        static long Choose(int n, int k)
        {
            long r = 1;
            for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
            return r;
        }

        [Fact] public void 데이터의_연환은_모두_화력식이나_0으로_센다고_적혀_있다()
        {
            var known = SynergyCheck.DamageLinks.Concat(SynergyCheck.ZeroLinks).ToList();
            Assert.Equal(known.Count, known.Distinct().Count());
            foreach (var l in Data.Value.Game.Synergies.Links)
                Assert.True(known.Contains(l.Id), $"SynergyCheck 가 모르는 연환: {l.Id}. DamageLinks 나 ZeroLinks 에 넣고 Eval 을 본다");
            foreach (var id in known)
                Assert.True(Data.Value.Game.Synergies.Links.Any(l => l.Id == id), $"데이터에 없는 연환: {id}");
        }

        [Fact] public void 이름으로_찾는_무공은_데이터에_있다()
        {
            var ids = Data.Value.Game.Weapons.Select(w => w.Id).ToHashSet();
            foreach (var id in SynergyCheck.NamedWeapons) Assert.Contains(id, ids);
            Assert.Contains(Data.Value.Game.Synergies.Common.Weapon, ids);
        }

        [Fact] public void 조합은_무공_칸_수만큼_고른_모든_경우()
        {
            var g = Data.Value.Game;
            var rows = Rows.Value;
            Assert.Equal(Choose(g.Weapons.Count, g.Balance.Slots.Weapons), rows.Count);
            Assert.All(rows, r => Assert.Equal(g.Balance.Slots.Weapons, r.Weapons.Distinct().Count()));
            Assert.Equal(rows.Count, rows.Select(r => string.Join(",", r.Weapons.OrderBy(x => x))).Distinct().Count());
        }

        [Fact] public void 시너지는_화력을_깎지_않고_조건부는_늘_켜진_것_위에_얹힌다()
        {
            foreach (var r in Rows.Value)
            {
                Assert.True(r.Base > 0, string.Join(",", r.Weapons));
                Assert.True(r.Gain >= 0 && r.CondGain >= 0);
                Assert.True(r.Ratio >= 1 && r.CondRatio >= r.Ratio);
                Assert.Equal(r.Gain + r.CondGain, r.Parts.Sum(p => p.Dps), 6);
                Assert.Equal(r.CondGain, r.Parts.Where(p => p.Name.EndsWith("(조건부)")).Sum(p => p.Dps), 6);
            }
        }

        [Fact] public void 진전_개수는_칸_수를_넘지_않는다()
        {
            var g = Data.Value.Game;
            int most = g.Balance.Slots.Weapons + g.Balance.Slots.Passives + 1; // +1: 삼재검법 '기본의 극'
            foreach (var r in Rows.Value)
            {
                Assert.True(r.Schools.Values.Sum() <= most, string.Join(",", r.Weapons));
                foreach (var kv in r.Schools)
                    Assert.True(kv.Value <= g.Synergies.Schools.First(s => s.Id == kv.Key).Members.Count + 1, kv.Key);
            }
        }

        [Fact] public void 보고서_결과_줄은_지금_데이터로_돌린_값과_같다()
        {
            // 보고서 전체를 다시 쓰면 오래 걸려서(낮춰 본 안까지 두 번 돈다) 맨 위 결과 두 줄만 대조한다.
            var rows = Rows.Value;
            var text = File.ReadAllText(Path.Combine(TestData.RepoRoot, "docs", "design", "sim", "synergy.md"));
            var m = Regex.Matches(text, @"가장 센 조합 (\d+\.\d\d)배[^\n]*?넘는 조합 ([\d,]+)개");
            Assert.True(m.Count >= 2, "synergy.md 결과 줄을 못 찾음");
            string F(double x) => x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            string N(int x) => x.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            const string hint = " — dotnet run --project core/BloodRain.Sim -- --synergy docs/design/sim/synergy.md 로 다시 쓴다";
            Assert.True(F(rows.Max(r => r.Ratio)) == m[0].Groups[1].Value, "늘 켜진 최대 배수" + hint);
            Assert.True(N(rows.Count(r => r.Ratio > SynergyCheck.Limit)) == m[0].Groups[2].Value, "늘 켜진 상한 넘는 수" + hint);
            Assert.True(F(rows.Max(r => r.CondRatio)) == m[1].Groups[1].Value, "조건부 최대 배수" + hint);
            Assert.True(N(rows.Count(r => r.CondRatio > SynergyCheck.Limit)) == m[1].Groups[2].Value, "조건부 상한 넘는 수" + hint);
        }
    }
}
