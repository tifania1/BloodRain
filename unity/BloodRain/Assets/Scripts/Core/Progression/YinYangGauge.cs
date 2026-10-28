// 태극(음양) 게이지. 양 무공·심법을 올리면 +, 음이면 -.
//   |g| ≤ harmony(기본 25, 운화 40)  → 태극(조화)
//   harmony < |g| < 70               → 편향
//   |g| ≥ 70                         → 극성(기혈역류 위험)
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public enum YinYangBand { Harmony, Bias, Extreme }

    public sealed class YinYangGauge
    {
        readonly YinYangDef _def;
        readonly int _harmonyRange;

        public int Value { get; private set; }

        public YinYangGauge(YinYangDef def, int start = 0, int? harmonyRangeOverride = null)
        {
            _def = def;
            _harmonyRange = harmonyRangeOverride ?? def.HarmonyRange;
            Value = Clamp(start);
        }

        int Clamp(int v) => Math.Max(_def.Min, Math.Min(_def.Max, v));

        static int Sign(YinYang aspect) => aspect == YinYang.Yang ? 1 : aspect == YinYang.Yin ? -1 : 0;

        public void OnWeaponLevelUp(WeaponDef w) => Value = Clamp(Value + Sign(w.Aspect) * _def.WeaponPerLevel);

        public void OnPassiveLevelUp(PassiveDef p)
        {
            int amount = p.YinyangPerLevelOverride ?? _def.PassivePerLevel;
            Value = Clamp(Value + Sign(p.Aspect) * amount);
        }

        /// <summary>깨달음 '역근': 게이지를 0으로.</summary>
        public void Reset() => Value = 0;

        public YinYangBand Band
        {
            get
            {
                int a = Math.Abs(Value);
                if (a <= _harmonyRange) return YinYangBand.Harmony;
                if (a >= _def.ExtremeFrom) return YinYangBand.Extreme;
                return YinYangBand.Bias;
            }
        }

        /// <summary>지금 기운이 기운 쪽 속성. 태극이면 Neutral.</summary>
        public YinYang Leaning => Band == YinYangBand.Harmony ? YinYang.Neutral : (Value > 0 ? YinYang.Yang : YinYang.Yin);
    }
}
