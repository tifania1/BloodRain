using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>R4-04: 엔딩 세 갈래 고르기와 진엔딩 조건(기획서 4장 '엔딩 세 갈래', 10장 화폐 표 의서 7권).</summary>
    public class EndingTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;
        static RebirthQiResult NoQi(GameData d) => new RebirthQiLedger(d.Cheonha.RebirthQi).Settle(0);

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        LifeRecord Life(string sect, string way, int manuscripts = 0) =>
            new LifeRecord(sect, way, NoQi(_d), Array.Empty<string>(), Array.Empty<string>(), "hyeongyeong", Array.Empty<string>(), manuscripts);

        [Fact] public void 의서_권_수가_기획서와_같다()
        {
            Assert.Equal(7, C.Cycle.TrueEndingManuscripts);
            Assert.Contains($"| 의서 | 혈월 천하의 은빛 등롱(낮은 확률) | 진엔딩 조건 | {C.Cycle.TrueEndingManuscripts}권 |", Gdd);
            Assert.Contains($"의서 {"일곱"} 권", Gdd);
        }

        [Fact] public void 종장을_깨기_전에는_엔딩을_고를_수_없다()
        {
            var s = AfterPrologue();
            Assert.False(s.EndingPending);
            Assert.Empty(s.EndingsOpen(true));
            Assert.Throws<InvalidOperationException>(() => s.ChooseEnding(Soul.EndingSeal, true));
            s.EndLife(Life("shaolin", "fallen"));
            Assert.False(s.EndingPending);
        }

        [Fact] public void 첫_회차는_봉인만이고_고르면_회귀한다()
        {
            var s = AfterPrologue();
            s.EndLife(Life("shaolin", "sealed"));
            Assert.True(s.EndingPending);
            Assert.Equal(new[] { Soul.EndingSeal }, s.EndingsOpen(true));
            Assert.Throws<InvalidOperationException>(() => s.ChooseEnding(Soul.EndingInherit, true));
            s.ChooseEnding(Soul.EndingSeal, true);
            Assert.False(s.EndingPending);
            Assert.Equal(2, s.Cycle);
            Assert.True(s.IsBloodMoon);
            Assert.Contains(Soul.EndingSeal, s.EndingsSeen);
            Assert.Throws<InvalidOperationException>(() => s.ChooseEnding(Soul.EndingSeal, true));   // 한 번 고르면 끝
        }

        [Fact] public void 둘째_회차엔_계승이_열리고_혈마_모드를_연다()
        {
            var s = AfterPrologue();
            s.EndLife(Life("shaolin", "sealed"));
            s.ChooseEnding(Soul.EndingSeal, false);
            s.EndLife(Life("gaebang", "sealed"));
            Assert.Equal(new[] { Soul.EndingSeal, Soul.EndingInherit }, s.EndingsOpen(false));   // 진엔딩 조건은 아직
            s.ChooseEnding(Soul.EndingInherit, false);
            Assert.Contains(Soul.BloodDemonModeToken, s.Unlocks);
            Assert.Equal(2, s.Cycle);   // 계승은 회귀하지 않는다
            Assert.False(s.JourneyOver);
        }

        [Fact] public void 진엔딩은_의서_일곱_권_문파_넷_목_노인_대화를_다_채워야_열리고_여정을_닫는다()
        {
            var s = AfterPrologue();
            s.EndLife(Life("shaolin", "sealed"));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Life("gaebang", "sealed", manuscripts: 4));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Life("namgung", "sealed", manuscripts: 3));
            Assert.True(s.TrueEndingManuscriptsMet);
            Assert.False(s.TrueEndingSectsMet);          // 셋뿐
            Assert.False(s.TrueEndingMet(true));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Life("tang", "sealed"));
            Assert.True(s.TrueEndingSectsMet);
            Assert.False(s.TrueEndingMet(false));        // 목 노인 대화를 아직
            Assert.DoesNotContain(Soul.EndingTrue, s.EndingsOpen(false));
            Assert.True(s.TrueEndingMet(true));
            Assert.Contains(Soul.EndingTrue, s.EndingsOpen(true));
            s.ChooseEnding(Soul.EndingTrue, true);
            Assert.True(s.JourneyOver);
            Assert.Throws<InvalidOperationException>(() => s.Regress());
            s.EndLife(Life("wudang", "sealed"));
            Assert.Empty(s.EndingsOpen(true));           // 여정이 끝나면 더 고를 것이 없다
        }

        [Fact] public void 엔딩_상태는_저장했다가_읽어도_같다()
        {
            var s = AfterPrologue();
            s.EndLife(Life("shaolin", "sealed"));
            s.ChooseEnding(Soul.EndingSeal, true);
            s.EndLife(Life("gaebang", "sealed"));
            var back = Soul.FromJson(_d, s.ToJson());
            Assert.True(back.EndingPending);
            Assert.Equal(s.EndingsSeen, back.EndingsSeen);
            Assert.False(back.JourneyOver);
            Assert.Equal(s.ToJson(), back.ToJson());
            // 엔딩 칸이 없는 옛 저장
            var old = Soul.FromJson(_d, "{\"version\":1,\"cycle\":2}");
            Assert.False(old.EndingPending);
            Assert.Empty(old.EndingsSeen);
        }
    }
}
