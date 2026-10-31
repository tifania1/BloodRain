// 경공 대시(기획서 11장 balance.json dash). 웹 프로토타입 doDash·update 와 같은 규칙.
//   누르면 충전 하나를 쓰고 duration 동안 바라보는 쪽으로 distance 만큼 미끄러진다(속도 = distance / duration).
//   무적은 iframes 초. 대시 중엔 다시 못 쓴다. 충전이 덜 찼으면 rechargeSeconds 마다 하나씩 찬다(대시 중에도 찬다, 차면 남은 시간은 버린다).
//   거리·속도는 경지 m 다. 축척(세계 m)은 화면 쪽이 곱한다(Sim/WorldScale).
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public sealed class Dash
    {
        readonly DashDef _def;

        public Dash(DashDef def, int extraCharges = 0)
        {
            _def = def;
            MaxCharges = Math.Max(0, def.Charges + extraCharges);
            Charges = MaxCharges;
        }

        public int MaxCharges { get; private set; }
        public int Charges { get; private set; }
        /// <summary>다음 충전까지 쌓인 초.</summary>
        public float Recharge { get; private set; }
        public float ActiveLeft { get; private set; }
        public float IframesLeft { get; private set; }
        public float DirX { get; private set; } = 1f;
        public float DirY { get; private set; }

        public bool IsDashing => ActiveLeft > 0f;
        public bool Invulnerable => IframesLeft > 0f;
        /// <summary>대시 속도(경지 m/s).</summary>
        public float Speed => _def.Duration > 0f ? _def.Distance / _def.Duration : 0f;
        /// <summary>다음 충전까지 0~1. 다 찼으면 1.</summary>
        public float RechargeProgress => Charges >= MaxCharges || _def.RechargeSeconds <= 0f ? 1f : Recharge / _def.RechargeSeconds;

        /// <summary>최대 충전이 늘면(일류 돌파, 성질 경신 …) 는 만큼 바로 채운다. 줄면 넘는 만큼 버린다.</summary>
        public void SetMaxCharges(int max)
        {
            max = Math.Max(0, max);
            if (max > MaxCharges) Charges += max - MaxCharges;
            MaxCharges = max;
            if (Charges > MaxCharges) Charges = MaxCharges;
        }

        /// <summary>(dx, dy) 쪽으로 대시. 방향이 0 이면 마지막 방향. 충전이 없거나 대시 중이면 false.</summary>
        public bool TryStart(float dx, float dy)
        {
            if (Charges <= 0 || IsDashing) return false;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-6f) { DirX = dx / len; DirY = dy / len; }
            Charges--;
            ActiveLeft = _def.Duration;
            IframesLeft = _def.Iframes;
            return true;
        }

        /// <summary>시간을 흘리고, 이번 dt 동안 대시로 움직인 거리(경지 m)를 돌려준다. 대시 중이 아니면 (0, 0).</summary>
        public (float dx, float dy) Tick(float dt)
        {
            if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            float moved = 0f;
            if (ActiveLeft > 0f)
            {
                float step = Math.Min(dt, ActiveLeft);
                moved = Speed * step;
                ActiveLeft -= step;
            }
            IframesLeft = Math.Max(0f, IframesLeft - dt);
            if (Charges < MaxCharges)
            {
                Recharge += dt;
                if (Recharge >= _def.RechargeSeconds) { Recharge = 0f; Charges++; }
            }
            else Recharge = 0f;
            return (DirX * moved, DirY * moved);
        }
    }
}
