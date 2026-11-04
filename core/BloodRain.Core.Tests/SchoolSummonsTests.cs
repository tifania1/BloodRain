using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>진전 소림 4 나한 허상·개방 4 개방 제자(synergies.json arhat_ring·beggar_allies, Build/SchoolSummons.cs).</summary>
    public class SchoolSummonsTests
    {
        readonly GameData _d = TestData.Load();

        SynergyEffects Of(params (string, int)[] items) => SynergyEffects.Of(Synergy.Compute(_d, TestData.Build("jin_sowoon", items)));
        SynergyEffects Shaolin4 => Of(("samjae_sword", 1), ("nahan_fist", 1), ("sonic_qin", 1), ("geumgang", 1), ("cheongsim", 1));
        SynergyEffects Gaebang4 => Of(("samjae_sword", 1), ("drunken_staff", 1), ("palm_wind", 1), ("juseon", 1), ("pasan", 1));
        SchoolTierDef T(string effect) => _d.Synergies.Schools.SelectMany(s => s.Tiers).Single(t => t.Effect == effect);

        [Fact] public void 숫자는_기획서_20장과_desc_와_같다()
        {
            var a = T("arhat_ring");
            Assert.Equal(4, a.Count);
            Assert.Equal("shaolin", _d.Synergies.Schools.Single(s => s.Tiers.Contains(a)).Id);
            Assert.Equal(0.5f, a.Param("healthThreshold"));
            Assert.Equal(5f, a.Param("seconds"));
            Assert.Equal(18f, a.Param("images"));
            Assert.Equal(3f, a.Param("timesPerRun"));
            foreach (var w in new[] { "50%", "5초", "열여덟", "세 번" }) Assert.Contains(w, a.Desc);

            var b = T("beggar_allies");
            Assert.Equal(4, b.Count);
            Assert.Equal("gaebang", _d.Synergies.Schools.Single(s => s.Tiers.Contains(b)).Id);
            Assert.Equal(180f, b.Param("intervalSeconds"));
            Assert.Equal(3f, b.Param("allies"));
            Assert.Equal(10f, b.Param("seconds"));
            foreach (var w in new[] { "3분마다", "셋", "10초" }) Assert.Contains(w, b.Desc);

            var row = TestData.GddTable(TestData.GddSection("진전(眞傳) 9문파"));
            Assert.Contains("판마다 3번", row.Single(r => r[0].StartsWith("소림"))[4]);
            Assert.Contains("3분마다 개방 제자 셋이 10초", row.Single(r => r[0].StartsWith("개방"))[4]);
        }

        [Fact] public void 넷을_모아야_켜진다()
        {
            Assert.True(Shaolin4.Has("arhat_ring"));
            Assert.False(Of(("samjae_sword", 1), ("nahan_fist", 1), ("geumgang", 1), ("cheongsim", 1)).Has("arhat_ring"));
            Assert.True(Gaebang4.Has("beggar_allies"));
            Assert.False(Of(("samjae_sword", 1), ("drunken_staff", 1), ("palm_wind", 1), ("juseon", 1)).Has("beggar_allies"));
        }

        [Fact] public void 나한_허상은_50퍼센트_아래로_떨어지는_순간_5초()
        {
            var fx = Shaolin4;
            var s = new SchoolSummons();
            Assert.False(s.Tick(fx, 0.8f, 0.1f).ArhatStarted);
            Assert.False(s.Tick(fx, 0.5f, 0.1f).ArhatStarted);     // 문턱 위(50%)는 아직
            Assert.True(s.Tick(fx, 0.49f, 0.1f).ArhatStarted);
            Assert.True(s.ArhatActive);
            Assert.Equal(1, s.ArhatUsed);
            Assert.False(s.Tick(fx, 0.3f, 4.9f).ArhatStarted);     // 아래에 머물러도 다시 켜지지 않는다
            Assert.True(s.ArhatActive);
            s.Tick(fx, 0.3f, 0.2f);
            Assert.False(s.ArhatActive);                            // 5초 뒤 꺼짐
            Assert.False(s.Tick(fx, 0.2f, 1f).ArhatStarted);       // 올라갔다 떨어져야 다시
            s.Tick(fx, 0.6f, 1f);
            Assert.True(s.Tick(fx, 0.4f, 1f).ArhatStarted);
            Assert.Equal(2, s.ArhatUsed);
        }

        [Fact] public void 나한_허상은_판마다_세_번이고_켜진_동안엔_다시_안_켜진다()
        {
            var fx = Shaolin4;
            var s = new SchoolSummons();
            s.Tick(fx, 0.4f, 0.1f);
            s.Tick(fx, 0.9f, 1f);                                  // 켜진 동안 올라갔다
            Assert.False(s.Tick(fx, 0.4f, 1f).ArhatStarted);       // 켜진 동안 다시 떨어져도 그대로
            Assert.Equal(1, s.ArhatUsed);
            for (int i = 0; i < 5; i++) { s.Tick(fx, 0.9f, 6f); s.Tick(fx, 0.1f, 0.1f); }
            Assert.Equal(3, s.ArhatUsed);
        }

        [Fact] public void 단계가_없으면_허상도_제자도_없다()
        {
            var s = new SchoolSummons();
            var none = Of(("samjae_sword", 1));
            Assert.False(s.Tick(none, 0.1f, 1f).ArhatStarted);
            Assert.Equal(0, s.Tick(none, 0.1f, 1000f).AlliesSpawned);
            Assert.Null(s.SecondsToNextAllies(none));
            // 단계를 얻을 때 이미 문턱 아래면 그때는 켜지지 않는다
            Assert.False(s.Tick(Shaolin4, 0.1f, 0.1f).ArhatStarted);
        }

        [Fact] public void 개방_제자는_3분마다_셋이_10초()
        {
            var fx = Gaebang4;
            var s = new SchoolSummons();
            Assert.Equal(0, s.Tick(fx, 1f, 179f).AlliesSpawned);
            Assert.Equal(1f, s.SecondsToNextAllies(fx)!.Value, 3);
            Assert.Equal(3, s.Tick(fx, 1f, 1f).AlliesSpawned);
            Assert.True(s.AlliesActive);
            s.Tick(fx, 1f, 9.9f);
            Assert.True(s.AlliesActive);
            s.Tick(fx, 1f, 0.2f);
            Assert.False(s.AlliesActive);
            Assert.Equal(3, s.Tick(fx, 1f, 170f).AlliesSpawned);    // 180초 뒤 다음 무리
        }

        [Fact] public void 단계가_꺼지면_개방_시계는_멈췄다가_이어_간다()
        {
            var s = new SchoolSummons();
            s.Tick(Gaebang4, 1f, 100f);
            s.Tick(Of(("samjae_sword", 1)), 1f, 500f);
            Assert.Equal(0, s.Tick(Gaebang4, 1f, 79f).AlliesSpawned);
            Assert.Equal(3, s.Tick(Gaebang4, 1f, 1f).AlliesSpawned);
        }
    }
}
