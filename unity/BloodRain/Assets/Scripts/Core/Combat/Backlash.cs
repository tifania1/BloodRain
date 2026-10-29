#nullable enable
// 기혈역류(기획서 3장 '음양과 주화입마'). 숫자는 balance.json yinyang.extreme.
//   극성(|게이지| ≥ 70)에 머문 시간이 60초 쌓일 때마다 역류가 온다. 극성을 벗어나면 쌓인 시간은 0으로.
//   역류는 3초 동안 이동속도 -50%, 끝까지 버티면 최대 기혈 5% 피해.
//   시작하고 1초 안에 경공을 쓰면 끊기고 오의 게이지 +50(배율 없이 그대로).
//   60초는 역류가 시작될 때부터 다시 센다. 역류가 이어지는 3초도 극성이면 다음 역류까지의 시간에 들어간다.
//   이미 시작된 역류는 극성을 벗어나도 끝까지 간다(웹 프로토타입과 같다).
//   비전 '역천'을 얻으면 Immune = true. 그 판 동안 역류가 다시 오지 않는다(극성 피해 보너스는 그대로).
//   역천 조건(세 번 연달아 끊기)을 판정하는 일은 비전 쪽(S-02)이 하고, 여기서는 CancelStreak만 센다.
//
// Unity 쪽은 매 프레임 Tick(Time.deltaTime, gauge.Band == YinYangBand.Extreme)을 부르고,
// 경공을 쓸 때 OnDash(), 이동속도에 MoveSpeedMultiplier 를 곱한다.
// Ended 를 받으면 EndDamage(최대 기혈)를 피격 처리로 넘기되 호신은 빼지 않는다(프로토타입은 호신만큼 더해 상쇄한다).
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    /// <summary>Tick 한 번에 일어난 일. 한 번에 둘 다 올 수도 있어서 묶음으로 돌려준다.</summary>
    [Flags]
    public enum BacklashEvents
    {
        None = 0,
        /// <summary>역류가 시작됐다.</summary>
        Started = 1,
        /// <summary>끝까지 버텼다. 지금 EndDamage 만큼 피해를 준다.</summary>
        Ended = 2,
    }

    public sealed class Backlash
    {
        readonly YinYangExtremeDef _def;
        readonly UltimateCharge? _charge;
        float _extremeTime;
        float _elapsed;

        /// <summary>역류가 시작됐다.</summary>
        public event Action? Started;
        /// <summary>끝까지 버텼다. EndDamage(최대 기혈)만큼 피해를 준다.</summary>
        public event Action? Ended;
        /// <summary>경공으로 끊었다(오의 게이지는 이미 더해졌다).</summary>
        public event Action? Canceled;

        /// <param name="charge">끊었을 때 오의 게이지를 채울 곳. 없으면 Canceled 만 알린다.</param>
        public Backlash(YinYangExtremeDef def, UltimateCharge? charge = null)
        {
            _def = def;
            _charge = charge;
        }

        /// <summary>지금 역류 중인가.</summary>
        public bool IsActive { get; private set; }

        /// <summary>비전 '역천': 켜져 있으면 새 역류가 오지 않는다.</summary>
        public bool Immune { get; set; }

        /// <summary>연달아 끊은 횟수. 역류를 끝까지 버티면 0으로 돌아간다(역천 판정용).</summary>
        public int CancelStreak { get; private set; }

        /// <summary>진행 중인 역류가 시작된 지 몇 초 지났나. 역류 중이 아니면 0.</summary>
        public float Elapsed => IsActive ? _elapsed : 0f;

        /// <summary>지금 경공을 쓰면 끊기는가.</summary>
        public bool CanCancel => IsActive && _elapsed < _def.DashCancelWindowSeconds;

        /// <summary>이동속도에 곱할 값. 역류 중이면 0.5.</summary>
        public float MoveSpeedMultiplier => IsActive ? Math.Max(0f, 1f - _def.BacklashSlow) : 1f;

        /// <summary>극성에 계속 머문다면 다음 역류까지 남은 시간(HUD 예고용).</summary>
        public float SecondsUntilNext => Math.Max(0f, _def.BacklashIntervalSeconds - _extremeTime);

        /// <summary>끝까지 버텼을 때 받는 피해. 최대 기혈 × 0.05.</summary>
        public float EndDamage(float maxHealth) => maxHealth * _def.BacklashDamageMaxHealthRatio;

        /// <param name="dt">세상 시간(초). 각성 연출처럼 시간이 느려지면 역류도 느려진다.</param>
        /// <param name="inExtreme">음양 게이지가 지금 극성 구간인가.</param>
        public BacklashEvents Tick(float dt, bool inExtreme)
        {
            if (dt < 0f) dt = 0f;
            var events = BacklashEvents.None;

            if (IsActive)
            {
                _elapsed += dt;
                if (_elapsed >= _def.BacklashSeconds)
                {
                    IsActive = false;
                    _elapsed = 0f;
                    CancelStreak = 0;
                    events |= BacklashEvents.Ended;
                    Ended?.Invoke();
                }
            }

            if (inExtreme && !Immune)
            {
                _extremeTime += dt;
                float interval = _def.BacklashIntervalSeconds;
                if (!IsActive && _extremeTime >= interval)
                {
                    // 남은 시간은 다음 역류 몫으로 넘긴다. 프레임 길이와 상관없이 60초마다 오게.
                    _extremeTime = interval > 0f ? _extremeTime % interval : 0f;
                    IsActive = true;
                    _elapsed = 0f;
                    events |= BacklashEvents.Started;
                    Started?.Invoke();
                }
            }
            else
            {
                _extremeTime = 0f;
            }

            return events;
        }

        /// <summary>경공을 썼다. 역류가 시작되고 1초가 안 됐으면 끊고 오의 +50, true.</summary>
        public bool OnDash()
        {
            if (!CanCancel) return false;
            IsActive = false;
            _elapsed = 0f;
            CancelStreak++;
            _charge?.AddFlat(_def.DashCancelUltimateCharge);
            Canceled?.Invoke();
            return true;
        }
    }
}
