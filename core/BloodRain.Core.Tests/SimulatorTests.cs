using System;
using System.Linq;
using BloodRain.Sim;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// B-07 밸런스 시뮬레이터(core/BloodRain.Sim). 화면 없이 1장 20분을 평균 정책, 시드 1로 돌려 레벨을 본다.
    /// 숫자와 가정의 자세한 설명은 docs/design/sim/level-curve.md.
    /// </summary>
    public class SimulatorTests
    {
        static readonly Lazy<SimResult> Current = new Lazy<SimResult>(() =>
            RunSimulator.Run(SimData.Load(TestData.DataDir), new SimOptions { Policy = "average", Seed = 1 }));

        static readonly Lazy<SimResult> Recommended = new Lazy<SimResult>(() =>
        {
            var sc = Scenarios.Find(Report.RecommendedId);
            return RunSimulator.Run(sc.LoadData(TestData.DataDir), sc.ApplyOptions(new SimOptions { Policy = "average", Seed = 1 }));
        });

        static int TargetAt(int minute) =>
            SimData.Load(TestData.DataDir).LevelTargets.Single(t => t.Minute == minute).Level;

        // 목표(기획서 11장, balance.json xpCurve.targets): 5분 Lv15 / 10분 Lv30 / 15분 Lv45 / 20분 Lv60.
        // 허용 띠는 목표 ±25%(Report.Tolerance). 시뮬레이터가 재 볼 수 없는 무리 가정(반경·도착 시간·밀도)을 크게 흔들면
        // 같은 데이터에서도 레벨이 이만큼 움직인다(보고서 '가정 민감도'). 이보다 좁으면 가정 하나에 깨지고, 넓으면 어긋남을 못 잡는다.
        //
        // ※ 지금은 목표 띠가 아니라 '지금 잰 값'을 지킨다.
        //   지금 데이터(처치 진기는 웹 프로토타입 값)로는 시드 1에서 5분 Lv16, 10분 Lv39, 15분 Lv65, 20분 Lv89가 나와
        //   10분부터 목표 띠(23~37, 34~56, 45~75)를 넘는다. 목표 띠를 검사하면 CI가 계속 빨갛게 되므로,
        //   대신 지금 잰 값 ±10%(최소 2레벨)를 지켜 어긋남이 있다는 사실을 테스트에 남긴다.
        //   - 이 범위를 벗어나면 데이터나 시뮬레이터가 바뀐 것이다. 보고서를 다시 만들고(--report) 여기 숫자도 고친다.
        //   - 데이터를 고쳐(보고서 '조정 제안') 목표 띠 안에 들어오면 이 테스트를 아래 '추천안' 테스트처럼 목표 띠 검사로 바꾸고 이 주석을 지운다.
        //   - 2026-10-29 T-01: 20분 Lv85 → Lv89(범위 77~93 → 80~98). 한 생 곡선에는 19분 생성 중지가 없어서(balance.json spawnTarget
        //     18분 뒤 400 유지) 19~20분에도 적이 채워지고, 그만큼 진기를 더 줍는다. 5·10·15분은 그대로다.
        [Theory]
        [InlineData(5, 14, 18)]
        [InlineData(10, 35, 43)]
        [InlineData(15, 59, 71)]
        [InlineData(20, 80, 98)]
        public void 평균_빌드_레벨은_지금_잰_범위_안(int minute, int lo, int hi)
        {
            Assert.InRange(Current.Value.LevelAt(minute), lo, hi);
        }

        [Fact]
        public void 지금_데이터는_10분부터_목표_띠를_넘는다()
        {
            // 위 테스트와 같은 사실을 목표 쪽에서 적어 둔다. 데이터를 고쳐 이 테스트가 깨지면 잘 된 것이다:
            // 이 테스트를 지우고 위 테스트를 목표 띠 검사로 바꾼다.
            var r = Current.Value;
            Assert.True(Report.InBand(r.LevelAt(5), TargetAt(5)), "5분은 아직 목표 띠 안이어야 한다");
            foreach (int m in new[] { 10, 15, 20 })
                Assert.True(r.LevelAt(m) > Report.Hi(TargetAt(m)), $"{m}분 Lv{r.LevelAt(m)}: 목표 띠 위쪽(>{Report.Hi(TargetAt(m))})이어야 한다");
        }

        // 추천안(보고서 '조정 제안' 1+2: 처치 진기 1·1·1·1·2·2, 짝 심법 Lv2)을 메모리에서만 얹으면 목표 띠 안에 든다.
        // 추천의 근거가 시뮬레이터 수정으로 사라지지 않게 지킨다.
        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(20)]
        public void 추천안이면_목표_띠_안(int minute)
        {
            int target = TargetAt(minute);
            Assert.InRange(Recommended.Value.LevelAt(minute), Report.Lo(target), Report.Hi(target));
        }

        [Fact]
        public void 같은_시드면_같은_결과()
        {
            var data = SimData.Load(TestData.DataDir);
            var a = RunSimulator.Run(data, new SimOptions { Seed = 7, Minutes = 6 });
            var b = RunSimulator.Run(data, new SimOptions { Seed = 7, Minutes = 6 });
            Assert.Equal(a.Minutes.Select(m => (m.Level, m.Kills, m.Alive)), b.Minutes.Select(m => (m.Level, m.Kills, m.Alive)));
        }

        [Fact]
        public void 레벨과_처치는_줄지_않고_경지는_순서대로()
        {
            var r = Current.Value;
            for (int i = 1; i < r.Minutes.Count; i++)
            {
                Assert.True(r.Minutes[i].Level >= r.Minutes[i - 1].Level);
                Assert.True(r.Minutes[i].Kills >= r.Minutes[i - 1].Kills);
            }
            var order = new[] { "iryu", "ilryu", "jeoljeong", "chojeoljeong" };
            var reached = order.Where(r.RealmMinute.ContainsKey).Select(id => r.RealmMinute[id]).ToList();
            Assert.Equal(reached.OrderBy(x => x), reached);
        }
    }
}
