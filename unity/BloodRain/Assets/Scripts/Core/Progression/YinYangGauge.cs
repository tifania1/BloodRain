// 태극(음양) 게이지. 양 무공·심법을 올리면 +, 음이면 -.
//   |g| ≤ harmony(기본 25, 운화 40, 무당 진전 2단계면 35)  → 태극(조화)
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
        readonly int _baseHarmonyRange;
        int? _synergyHarmonyRange;
        double _carry; // 기울기 배율 때문에 남은 소수 몫(2.5 → 2, 다음에 3)

        public int Value { get; private set; }

        public YinYangGauge(YinYangDef def, int start = 0, int? harmonyRangeOverride = null)
        {
            _def = def;
            _baseHarmonyRange = harmonyRangeOverride ?? def.HarmonyRange;
            Value = Clamp(start);
        }

        /// <summary>지금 태극 구간 반폭. 몸의 값과 진전이 넓힌 값 중 넓은 쪽(좁히지는 않는다).</summary>
        public int HarmonyRange => _synergyHarmonyRange is int r && r > _baseHarmonyRange ? r : _baseHarmonyRange;

        /// <summary>진전이 넓혀 주는 태극 구간(SynergyState.HarmonyRange). 단계를 잃으면 null 을 넘겨 되돌린다.</summary>
        public void SetSynergyHarmonyRange(int? range) => _synergyHarmonyRange = range;

        int Clamp(int v) => Math.Max(_def.Min, Math.Min(_def.Max, v));

        static int Sign(YinYang aspect) => aspect == YinYang.Yang ? 1 : aspect == YinYang.Yin ? -1 : 0;

        /// <summary>무공·심법을 올릴 때 기우는 양에 곱하는 값. 깨달음 '역근'이면 0.5. 심마(Shift)에는 안 곱한다.</summary>
        public float TiltMultiplier { get; private set; } = 1f;

        public void SetTiltMultiplier(float multiplier)
        {
            TiltMultiplier = multiplier;
            _carry = 0;
        }

        /// <summary>참이면 기울기·심마로 움직이지 않는다(약방 영약 태극환 '처음 10분', Life 가 켜고 끈다). 되돌리기(ResetTo)는 그대로.</summary>
        public bool Frozen { get; set; }

        void Tilt(int amount)
        {
            if (Frozen) return;
            double exact = amount * (double)TiltMultiplier + _carry;
            int whole = (int)Math.Truncate(exact);
            _carry = exact - whole;
            Value = Clamp(Value + whole);
        }

        public void OnWeaponLevelUp(WeaponDef w) => Tilt(Sign(w.Aspect) * _def.WeaponPerLevel);

        public void OnPassiveLevelUp(PassiveDef p)
        {
            int amount = p.YinyangPerLevelOverride ?? _def.PassivePerLevel;
            Tilt(Sign(p.Aspect) * amount);
        }

        /// <summary>게이지를 0으로.</summary>
        public void Reset() => ResetTo(0);
        /// <summary>게이지를 그만큼 옮긴다(+ 양, - 음). 마교 혈공의 심마가 쓴다.</summary>
        public void Shift(int delta) { if (!Frozen) Value = Clamp(Value + delta); }
        /// <summary>게이지를 그 값으로 되돌린다(천마해체: 시작값으로, 깨달음 '역근': 0으로).</summary>
        public void ResetTo(int value)
        {
            Value = Clamp(value);
            _carry = 0;
        }

        public YinYangBand Band
        {
            get
            {
                int a = Math.Abs(Value);
                if (a <= HarmonyRange) return YinYangBand.Harmony;
                if (a >= _def.ExtremeFrom) return YinYangBand.Extreme;
                return YinYangBand.Bias;
            }
        }

        /// <summary>지금 기운이 기운 쪽 속성. 태극이면 Neutral.</summary>
        public YinYang Leaning => Band == YinYangBand.Harmony ? YinYang.Neutral : (Value > 0 ? YinYang.Yang : YinYang.Yin);
    }
}
