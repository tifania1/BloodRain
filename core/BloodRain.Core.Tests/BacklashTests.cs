using System.Collections.Generic;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>기혈역류(B-05): 극성 60초마다, 3초 동안 이동속도 -50%, 끝날 때 최대 기혈 5%, 1초 안 경공으로 끊으면 오의 +50.</summary>
    public class BacklashTests
    {
        readonly GameData _d = Load();
        YinYangExtremeDef X => _d.Balance.Yinyang.Extreme;

        Backlash New(UltimateCharge? charge = null) => new Backlash(X, charge);

        UltimateCharge NewCharge(float bonus = 0f) =>
            new UltimateCharge(_d.Balance.UltimateCharge, _d.Balance.Yinyang.Harmony, bonus);

        /// <summary>극성에서 역류가 시작될 때까지 민다(쌓인 시간이 0이라는 전제).</summary>
        static void StartOne(Backlash b) => Assert.Equal(BacklashEvents.Started, b.Tick(60f, true));

        // ---- 데이터 ----

        [Fact] public void 극성과_편향_수치를_balance_json에서_읽는다()
        {
            Assert.Equal(60f, X.BacklashIntervalSeconds);
            Assert.Equal(3f, X.BacklashSeconds);
            Assert.Equal(0.5f, X.BacklashSlow);
            Assert.Equal(0.05f, X.BacklashDamageMaxHealthRatio);
            Assert.Equal(1f, X.DashCancelWindowSeconds);
            Assert.Equal(50f, X.DashCancelUltimateCharge);
            Assert.Equal(0.40f, X.AspectDamageBonus);
            Assert.Equal(0.15f, _d.Balance.Yinyang.Bias.AspectDamageBonus);
        }

        [Fact] public void 기혈역류_수치가_기획서와_같다()
        {
            var gdd = ReadRepoFile("docs", "design", "gdd.md");
            int start = gdd.IndexOf("### 음양과 주화입마");
            Assert.True(start >= 0);
            var section = gdd.Substring(start, gdd.IndexOf("### ", start + 5) - start);
            Assert.Contains($"피해 +{Num(_d.Balance.Yinyang.Bias.AspectDamageBonus * 100)}%", section);
            Assert.Contains($"피해 +{Num(X.AspectDamageBonus * 100)}%. 대신 {Num(X.BacklashIntervalSeconds)}초마다 '기혈역류'", section);
            Assert.Contains($"{Num(X.BacklashSeconds)}초 동안 이동속도 -{Num(X.BacklashSlow * 100)}%", section);
            Assert.Contains($"최대 기혈의 {Num(X.BacklashDamageMaxHealthRatio * 100)}% 피해", section);
            Assert.Contains($"{Num(X.DashCancelWindowSeconds)}초 안에 경공", section);
            Assert.Contains($"진기 게이지가 {Num(X.DashCancelUltimateCharge / _d.Balance.UltimateCharge.Max * 100)}% 찹니다", section);
        }

        // ---- 시작 ----

        [Fact] public void 극성에_60초_머물면_역류가_시작된다()
        {
            var b = New();
            Assert.Equal(BacklashEvents.None, b.Tick(59.5f, true));
            Assert.False(b.IsActive);
            Assert.Equal(0.5f, b.SecondsUntilNext, 4);
            Assert.Equal(BacklashEvents.Started, b.Tick(0.5f, true));
            Assert.True(b.IsActive);
        }

        [Fact] public void 양_무공으로_극성까지_올리고_프레임마다_밀면_60초에_온다()
        {
            var g = new YinYangGauge(_d.Balance.Yinyang);
            var yang = _d.Weapon("nahan_fist");
            for (int i = 0; i < 14; i++) g.OnWeaponLevelUp(yang); // 70
            Assert.Equal(YinYangBand.Extreme, g.Band);

            var b = New();
            const float dt = 1f / 60f;
            float t = 0f;
            while (!b.IsActive && t < 70f)
            {
                b.Tick(dt, g.Band == YinYangBand.Extreme);
                t += dt;
            }
            Assert.True(b.IsActive);
            Assert.InRange(t, 60f, 60f + dt);
        }

        [Fact] public void 편향이나_태극에서는_역류가_오지_않는다()
        {
            var b = New();
            Assert.Equal(BacklashEvents.None, b.Tick(600f, false));
            Assert.False(b.IsActive);
        }

        [Fact] public void 극성을_벗어나면_쌓인_시간이_0으로()
        {
            var b = New();
            b.Tick(50f, true);
            Assert.Equal(10f, b.SecondsUntilNext, 3);
            b.Tick(0.5f, false);
            Assert.Equal(60f, b.SecondsUntilNext, 3);
            Assert.Equal(BacklashEvents.None, b.Tick(50f, true));
            Assert.Equal(BacklashEvents.Started, b.Tick(10f, true));
        }

        [Fact] public void 역류는_시작부터_60초마다_온다()
        {
            // 역류가 이어지는 3초도 극성이면 다음 역류까지의 시간에 들어간다
            var b = New();
            var starts = new List<float>();
            var ends = new List<float>();
            const float dt = 0.125f; // 2진수로 딱 떨어지는 값이라 누적 오차가 없다
            float t = 0f;
            for (int i = 0; i < 1600; i++) // 200초
            {
                t += dt;
                var e = b.Tick(dt, true);
                if ((e & BacklashEvents.Started) != 0) starts.Add(t);
                if ((e & BacklashEvents.Ended) != 0) ends.Add(t);
            }
            Assert.Equal(new[] { 60f, 120f, 180f }, starts);
            Assert.Equal(new[] { 63f, 123f, 183f }, ends);
        }

        [Fact] public void 프레임이_길게_튀어도_역류는_하나씩_오고_남은_시간은_넘긴다()
        {
            var b = New();
            Assert.Equal(BacklashEvents.Started, b.Tick(150f, true));
            Assert.Equal(30f, b.SecondsUntilNext, 3);   // 60초 두 번을 넘겼어도 역류는 하나. 남은 30초는 다음 몫
            Assert.Equal(BacklashEvents.Ended, b.Tick(3f, true));
            Assert.Equal(BacklashEvents.Started, b.Tick(27f, true));
        }

        // ---- 역류 중 ----

        [Fact] public void 역류_중에는_이동속도가_절반()
        {
            var b = New();
            Assert.Equal(1f, b.MoveSpeedMultiplier);
            StartOne(b);
            Assert.Equal(0.5f, b.MoveSpeedMultiplier);
            b.Tick(3f, true);
            Assert.Equal(1f, b.MoveSpeedMultiplier);
        }

        [Fact] public void 끝까지_3초를_버티면_최대_기혈의_5퍼센트를_받는다()
        {
            var b = New();
            int ended = 0;
            b.Ended += () => ended++;
            StartOne(b);
            Assert.Equal(BacklashEvents.None, b.Tick(2.5f, true));
            Assert.True(b.IsActive);
            Assert.Equal(2.5f, b.Elapsed, 4);
            Assert.Equal(BacklashEvents.Ended, b.Tick(0.5f, true));
            Assert.False(b.IsActive);
            Assert.Equal(1, ended);
            Assert.Equal(5f, b.EndDamage(100f), 4);
            Assert.Equal(12f, b.EndDamage(240f), 4);
        }

        [Fact] public void 시작된_역류는_극성을_벗어나도_끝까지_간다()
        {
            var b = New();
            StartOne(b);
            b.Tick(1f, false);
            Assert.True(b.IsActive);
            Assert.Equal(BacklashEvents.Ended, b.Tick(2f, false));
        }

        // ---- 경공으로 끊기 ----

        [Fact] public void 시작하고_1초_안에_경공을_쓰면_끊기고_오의_50()
        {
            var u = NewCharge();
            var b = New(u);
            int canceled = 0, ended = 0;
            b.Canceled += () => canceled++;
            b.Ended += () => ended++;
            StartOne(b);
            b.Tick(0.75f, true);
            Assert.True(b.CanCancel);
            Assert.True(b.OnDash());
            Assert.False(b.IsActive);
            Assert.Equal(1f, b.MoveSpeedMultiplier);
            Assert.Equal(50f, u.Value, 3);
            Assert.Equal(1, canceled);
            b.Tick(5f, true);
            Assert.Equal(0, ended); // 끊었으니 피해도 없다
        }

        [Fact] public void 시작한_바로_그_순간에도_끊을_수_있다()
        {
            var b = New();
            StartOne(b);
            Assert.Equal(0f, b.Elapsed);
            Assert.True(b.OnDash());
        }

        [Fact] public void 꼭_1초가_지나면_더는_끊을_수_없다()
        {
            var u = NewCharge();
            var b = New(u);
            StartOne(b);
            b.Tick(1f, true);
            Assert.False(b.CanCancel);
            Assert.False(b.OnDash());
            Assert.True(b.IsActive);
            Assert.Equal(0f, u.Value);
            Assert.Equal(BacklashEvents.Ended, b.Tick(2f, true));
        }

        [Fact] public void 역류가_없을_때_경공은_아무_일도_하지_않는다()
        {
            var u = NewCharge();
            var b = New(u);
            b.Tick(30f, true);
            Assert.False(b.OnDash());
            Assert.Equal(0f, u.Value);
            Assert.Equal(0, b.CancelStreak);
            Assert.Equal(30f, b.SecondsUntilNext, 3); // 쌓인 시간도 그대로
        }

        [Fact] public void 끊기_보상은_연무장_배율을_받지_않는다()
        {
            var u = NewCharge(bonus: 0.5f);
            var b = New(u);
            StartOne(b);
            b.OnDash();
            Assert.Equal(50f, u.Value, 3);
        }

        [Fact] public void 오의_게이지가_없어도_끊기는_된다()
        {
            var b = New();
            StartOne(b);
            Assert.True(b.OnDash());
            Assert.False(b.IsActive);
        }

        // ---- 역천 ----

        [Fact] public void 연달아_끊은_횟수를_세고_한_번_버티면_0으로()
        {
            var b = New();
            for (int i = 0; i < 3; i++)
            {
                StartOne(b);
                Assert.True(b.OnDash());
            }
            Assert.Equal(3, b.CancelStreak); // 비전 '역천' 조건(판정은 S-02)
            StartOne(b);
            b.Tick(3f, true);
            Assert.Equal(0, b.CancelStreak);
        }

        [Fact] public void 역천을_얻으면_역류가_다시_오지_않는다()
        {
            var b = New();
            b.Tick(50f, true);
            b.Immune = true;
            Assert.Equal(BacklashEvents.None, b.Tick(600f, true));
            Assert.False(b.IsActive);
            Assert.Equal(1f, b.MoveSpeedMultiplier);
            Assert.Equal(60f, b.SecondsUntilNext, 3); // 쌓이지도 않는다
        }

        // ---- 이벤트 ----

        [Fact] public void 이벤트는_반환값과_같은_순서로_온다()
        {
            var b = New();
            var log = new List<string>();
            b.Started += () => log.Add("시작");
            b.Ended += () => log.Add("끝");
            b.Canceled += () => log.Add("끊음");
            b.Tick(60f, true);
            b.Tick(3f, true);
            b.Tick(57f, true);
            b.OnDash();
            Assert.Equal(new[] { "시작", "끝", "시작", "끊음" }, log);
        }

        [Fact] public void 끝난_이벤트를_받을_때는_이미_역류가_풀려_있다()
        {
            var b = New();
            bool activeInHandler = true;
            float speedInHandler = 0f;
            b.Ended += () => { activeInHandler = b.IsActive; speedInHandler = b.MoveSpeedMultiplier; };
            StartOne(b);
            b.Tick(3f, true);
            Assert.False(activeInHandler);
            Assert.Equal(1f, speedInHandler);
        }
    }
}
