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

        // ---- R-07b 허상·제자 숫자 ----

        [Fact] public void 허상_제자_숫자는_같은_무공과_각성_파동에서_왔고_기획서에_있다()
        {
            var a = T("arhat_ring"); var b = T("beggar_allies");
            var nahan = _d.Weapons.Single(w => w.Id == "nahan_fist");
            var palm = _d.Weapons.Single(w => w.Id == "palm_wind");
            Assert.Equal(nahan.Range, a.Param("radius"), 3);
            Assert.Equal(_d.Balance.Awakening.EliteKnockback, a.Param("knockback"), 3);
            Assert.Equal(palm.DamageLv1, b.Param("damage"), 3);
            Assert.Equal(palm.Knockback!.Value, b.Param("knockback"), 3);
            Assert.Equal(1f, b.Param("attackSeconds"));
            var sec = TestData.GddSection("진전(眞傳) 9문파");
            foreach (var w in new[] { "몸 둘레 1.8m", "넉백 8", "땅의 주인은 밀리지 않습니다", "적이 노리지 않는 동료", "1초마다", "장풍 Lv1 한 대(20 x 공력, 넉백 5)" })
                Assert.Contains(w, sec);
        }

        [Fact] public void 허상은_둘레_안_잡몹_정예만_밀고_보스는_안_민다()
        {
            var fx = Shaolin4;
            var s = new SchoolSummons();
            Assert.Equal(0f, s.ArhatPush(fx, 1f, false));          // 아직 안 켜짐
            s.Tick(fx, 0.8f, 0.1f); s.Tick(fx, 0.4f, 0.1f);
            Assert.True(s.ArhatActive);
            Assert.Equal(8f, s.ArhatPush(fx, 1.7f, false), 3);
            Assert.Equal(0f, s.ArhatPush(fx, 1.9f, false), 3);
            Assert.Equal(8f, s.ArhatPush(fx, 4f, false, 2.25f), 3);
            Assert.Equal(0f, s.ArhatPush(fx, 1f, true), 3);
            s.Tick(fx, 0.4f, 5f);
            Assert.Equal(0f, s.ArhatPush(fx, 1f, false), 3);       // 5초 뒤 꺼짐
        }

        [Fact] public void 제자는_1초마다_장풍_한_대씩_10초_동안()
        {
            var fx = Gaebang4;
            var s = new SchoolSummons();
            var e = s.Tick(fx, 1f, 180f);
            Assert.Equal(3, e.AlliesSpawned);
            Assert.Equal(0, e.AllyVolleys);                         // 나타난 순간엔 아직
            Assert.Equal(3, s.AlliesPresent);
            Assert.Equal(0, s.Tick(fx, 1f, 0.5f).AllyVolleys);
            Assert.Equal(1, s.Tick(fx, 1f, 0.5f).AllyVolleys);      // 1초
            int total = 1;
            for (int i = 0; i < 100; i++) total += s.Tick(fx, 1f, 0.1f).AllyVolleys;
            Assert.Equal(10, total);                                // 10초에 열 대
            Assert.Equal(0, s.AlliesPresent);
            Assert.Equal((40f, 5f), SchoolSummons.AllyShot(fx, 2f));
            Assert.Equal((0f, 0f), SchoolSummons.AllyShot(Shaolin4, 2f));
        }
    }
}
