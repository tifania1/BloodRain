using System.Globalization;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// 기획서 11장 '플레이어 화력 목표' 표와 6장 '땅의 주인' 기준 체력 표가 서로, 그리고 enemies.json bosses 와 맞는지.
    /// 11장: 기준 체력 = 20분 화력 x 목표 처치 시간, 원각은 메타 초반 열, 먼 땅의 주인일수록 뒤쪽 열.
    /// </summary>
    public class FirepowerTargetTests
    {
        readonly GameData _d = TestData.Load();

        static float Num(string s) => float.Parse(s.Replace(",", "").Replace("초", "").Trim(), CultureInfo.InvariantCulture);

        (float early, float late)[] Firepower()
        {
            var rows = TestData.GddTable(TestData.GddSection("플레이어 화력 목표"));
            Assert.Equal(new[] { "5분", "10분", "15분", "20분" }, rows.Select(r => r[0]).ToArray());
            return rows.Select(r => (Num(r[1]), Num(r[2]))).ToArray();
        }

        [Fact] public void 화력_목표는_시간이_갈수록_오르고_중후반이_초반보다_크다()
        {
            var f = Firepower();
            for (int i = 0; i < f.Length; i++)
            {
                Assert.True(f[i].late > f[i].early, $"{(i + 1) * 5}분 중후반 화력이 초반보다 작다");
                if (i > 0) { Assert.True(f[i].early > f[i - 1].early); Assert.True(f[i].late > f[i - 1].late); }
            }
        }

        [Fact] public void 땅의_주인_표는_enemies_json_과_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("땅의 주인(지역 보스)"));
            Assert.Equal(8, rows.Count); // 장삼 + 일곱 땅의 주인
            foreach (var r in rows)
            {
                var b = _d.Bosses.SingleOrDefault(x => x.NameKo == r[0]);
                Assert.True(b != null, $"enemies.json bosses 에 '{r[0]}' 이 없다");
                Assert.Equal(Num(r[2]), b!.Health);
                Assert.Equal(Num(r[3]), b.TargetKillSeconds);
            }
        }

        [Fact] public void 기준_체력은_20분_화력_x_목표_처치_시간이다()
        {
            var f20 = Firepower()[3];
            var lords = _d.Bosses.Where(b => b.Chapter.StartsWith("chapter")).OrderBy(b => b.Chapter).ToList();
            Assert.Equal(7, lords.Count);
            var implied = lords.Select(b => b.Health / b.TargetKillSeconds).ToList();

            // 숭산 원각은 메타 초반 열 그대로
            Assert.Equal(f20.early, implied[0], 3);
            // 먼 땅일수록 뒤쪽 열: 오르기만 하고, 마지막 혁련휘는 중후반 열 근처(반올림 1% 안)
            for (int i = 1; i < implied.Count; i++) Assert.True(implied[i] > implied[i - 1], $"{lords[i].NameKo} 화력 기준이 앞 땅보다 낮다");
            Assert.InRange(implied[^1], f20.late * 0.99f, f20.late * 1.01f);
        }
    }
}
