using System.Linq;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>대장간 안(SM-01, 2026-11-20 추천대로): 몸 색 셋·바꾸기 공짜·무기 외형은 해금으로 얻어 고름.</summary>
    public class SmithyTests
    {
        readonly Data.GameData _d = TestData.Load();

        [Fact] public void 몸_색은_문파_빛깔_먹_담묵_셋()
        {
            Assert.Equal(new[] { "sect", "ink", "wash" }, _d.Cheonha.Inn.Smithy.Tints.Select(t => t.Id));
            Assert.Equal(new[] { 1f, 1f, 1f }, _d.Cheonha.Inn.Smithy.Tints[0].Rgb);
            Assert.All(_d.Cheonha.Inn.Smithy.Tints, t => Assert.Equal(3, t.Rgb.Length));
        }

        [Fact] public void 짓기_전엔_못_바꾸고_지은_뒤엔_공짜로_바꾸고_저장된다()
        {
            var soul = new Soul(_d);
            Assert.Equal("sect", soul.BodyTint);
            Assert.False(soul.TrySetBodyTint("ink"));       // 대장간이 없다
            soul.AddBloodCrystal(20);
            Assert.True(Inn.TryBuild(soul, "smithy"));
            int crystal = soul.BloodCrystal;
            Assert.True(soul.TrySetBodyTint("ink"));
            Assert.False(soul.TrySetBodyTint("vermilion")); // 없는 색
            Assert.Equal(crystal, soul.BloodCrystal);        // 바꾸기는 공짜
            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.Equal("ink", back.BodyTint);
        }

        [Fact] public void 무기_외형은_skin_해금에서_나온다()
        {
            var soul = new Soul(_d);
            Assert.Empty(soul.WeaponSkins);
            soul.Unlock("skin:plum_sword");
            Assert.Equal(new[] { "plum_sword" }, soul.WeaponSkins);
        }
    }
}
