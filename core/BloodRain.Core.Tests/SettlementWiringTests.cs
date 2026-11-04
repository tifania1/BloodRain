using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>정산 화면 '무공별 피해 비중 막대'(기획서 12장 정산 줄)가 빈칸으로 남지 않게.
    /// Presentation 은 클라우드에서 컴파일하지 못하니, LifeRunner 가 정산에 넘기는 줄만 글로 본다.</summary>
    public class SettlementWiringTests
    {
        static string Read(params string[] parts) =>
            File.ReadAllText(Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Scripts", "Presentation", Path.Combine(parts)));

        [Fact]
        public void 한_생_정산이_무공별_피해를_WeaponRunner_에서_받는다()
        {
            string runner = Read("LifeRunner.cs");
            var m = Regex.Match(runner, @"DamageByWeapon\s*=\s*([^;]+?),\s*\n\s*\};", RegexOptions.Singleline);
            Assert.True(m.Success, "LifeRunner 의 SettlementStats 에 DamageByWeapon 줄이 없다");
            Assert.Contains("weapons.Dealt", m.Groups[1].Value);
            Assert.Contains("Dealt", Read("Combat", "WeaponRunner.cs"));
        }
    }
}
