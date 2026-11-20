using System.Linq;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>하오문 게시판 힌트(EL-06, 2026-11-20 추천대로): 300 × (산 수 + 1), 사면 비전 하나가 밀서처럼 드러남.</summary>
    public class HaomunBoardTests
    {
        readonly Data.GameData _d = TestData.Load();

        Soul OpenBoard(int qi)
        {
            var soul = Soul.FromJson(_d, new Soul(_d).ToJson().Replace("\"rebirthQi\": 0", $"\"rebirthQi\": {qi}"));
            soul.Unlock("facility:haomun_board");
            return soul;
        }

        [Fact] public void 값은_300에_산_수_더하기_1을_곱한다()
        {
            Assert.Equal(300, _d.Cheonha.Inn.HaomunBoard.HintBaseRebirthQi);
            var soul = OpenBoard(2000);
            Assert.Equal(300, soul.NextHintCost);
            var first = soul.TryBuyHint();
            Assert.Equal(_d.Synergies.Secrets[0].Id, first);
            Assert.Contains(first!, soul.Letters);
            Assert.Equal(1700, soul.RebirthQi);
            Assert.Equal(600, soul.NextHintCost);
            var second = soul.TryBuyHint();
            Assert.NotEqual(first, second);
            Assert.Equal(1100, soul.RebirthQi);
            Assert.Equal(2, Soul.FromJson(_d, soul.ToJson()).HaomunHints);   // 저장
        }

        [Fact] public void 게시판이_없거나_기가_모자라면_못_산다()
        {
            var soul = Soul.FromJson(_d, new Soul(_d).ToJson().Replace("\"rebirthQi\": 0", "\"rebirthQi\": 5000"));
            Assert.Null(soul.TryBuyHint());              // 게시판이 안 열림
            var poor = OpenBoard(299);
            Assert.Null(poor.TryBuyHint());
            Assert.Equal(299, poor.RebirthQi);
        }

        [Fact] public void 드러낼_비전이_없으면_멈춘다()
        {
            var soul = OpenBoard(1_000_000);
            int n = _d.Synergies.Secrets.Count;
            for (int i = 0; i < n; i++) Assert.NotNull(soul.TryBuyHint());
            Assert.Null(soul.NextHintSecret);
            Assert.Null(soul.TryBuyHint());
            Assert.Equal(n, soul.Letters.Count);
        }
    }
}
