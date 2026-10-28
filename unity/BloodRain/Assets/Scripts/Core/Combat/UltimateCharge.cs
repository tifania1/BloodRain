// 진기(오의) 게이지. 100이 차면 오의를 쓴다(기획서 '8. 전투 시스템' 오의 상세).
//   처치 0.4, 피격 3, 정예 처치 15 → 충전 배율을 곱한다.
//   충전 배율 = (1 + 연무장·심법 UltimateCharge) × (태극이면 1.25)
//   정진(건너뛰기) 20, 기혈역류 끊기 50, 공청석유 시작 100 같은 '정해진 양'은 배율 없이 그대로 더한다.
using System;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;

namespace BloodRain.Core.Combat
{
    public sealed class UltimateCharge
    {
        readonly UltimateChargeDef _def;
        readonly YinYangHarmonyDef _harmony;

        public float Value { get; private set; }
        /// <summary>연무장·심법에서 온 충전 보너스(0.2 = +20%).</summary>
        public float ChargeBonus { get; set; }

        public UltimateCharge(UltimateChargeDef def, YinYangHarmonyDef harmony, float chargeBonus = 0f, float start = 0f)
        {
            _def = def;
            _harmony = harmony;
            ChargeBonus = chargeBonus;
            Value = Clamp(start);
        }

        public float Max => _def.Max;
        public bool IsFull => Value >= _def.Max;
        public float Fraction => Value / _def.Max;

        float Clamp(float v) => Math.Max(0f, Math.Min(_def.Max, v));

        public float Rate(YinYangBand band) =>
            (1f + ChargeBonus) * (band == YinYangBand.Harmony ? 1f + _harmony.UltimateChargeBonus : 1f);

        public void OnKill(YinYangBand band, int count = 1) => Value = Clamp(Value + _def.PerKill * count * Rate(band));
        public void OnHit(YinYangBand band) => Value = Clamp(Value + _def.PerHit * Rate(band));
        /// <summary>정예 처치. 일반 처치분은 따로 더하지 않는다.</summary>
        public void OnEliteKill(YinYangBand band) => Value = Clamp(Value + _def.PerElite * Rate(band));

        /// <summary>정진(레벨업 건너뛰기).</summary>
        public void OnSkip() => AddFlat(_def.PerSkip);

        /// <summary>배율 없이 정해진 양을 더한다(역류 끊기 50 등).</summary>
        public void AddFlat(float amount) => Value = Clamp(Value + amount);

        /// <summary>가득 찼으면 비우고 true. 아니면 그대로 false.</summary>
        public bool TryUse()
        {
            if (!IsFull) return false;
            Value = 0f;
            return true;
        }
    }
}
