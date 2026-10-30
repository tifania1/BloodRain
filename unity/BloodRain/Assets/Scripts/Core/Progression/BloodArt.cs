// 마교 몸의 성장 '혈공(血功)'(기획서 5장). 값은 cheonha.json sects[demonic].bloodArt.
//   - 진기 구슬에서 받는 진기 × 그 자리 혈운 농도(다른 문파는 1배). 윤회의 기는 구슬 값 그대로 센다(이 규칙으로 늘지 않음).
//   - 농도 덕에 더 받은 진기가 300 쌓일 때마다 음양 게이지가 음 쪽으로 1 기운다(심마).
//   - 천마해체를 쓰면 게이지가 몸의 시작값으로 돌아온다. 돌파 때는 그대로.
// 마공 깨달음(돌파 셋 중 하나)은 insights.json demonicInsights 와 Insights.RollWithDemonic. Run/Life 가 마교 몸이면 이 클래스를 만든다.
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public sealed class BloodArt
    {
        readonly BloodArtDef _def;
        readonly YinYangGauge _gauge;
        readonly int _start;
        double _extra;

        public BloodArt(BloodArtDef def, YinYangGauge gauge, int yinyangStart)
        {
            _def = def;
            _gauge = gauge;
            _start = yinyangStart;
        }

        /// <summary>지금까지 농도 덕에 더 받은 진기(심마 계산용, 쌓인 채로 둔다).</summary>
        public double ExtraXp => _extra;
        /// <summary>심마로 기운 횟수.</summary>
        public int Steps { get; private set; }

        /// <summary>구슬 하나. RunXp.Absorb 에 넘길 진기를 돌려주고, 심마로 게이지를 옮긴다.</summary>
        public double OrbXp(double orbValue, float density)
        {
            if (!_def.XpTimesDensity) return orbValue;
            double got = orbValue * density;
            double extra = got - orbValue; // 농도 1 아래(청운진 0.8)면 음수: 심마는 줄지 않는다
            if (extra > 0)
            {
                _extra += extra;
                int steps = _def.ExtraXpPerStep > 0 ? (int)Math.Floor(_extra / _def.ExtraXpPerStep) : 0;
                if (steps > Steps)
                {
                    _gauge.Shift(-(steps - Steps) * _def.YinPerStep);
                    Steps = steps;
                }
            }
            return got;
        }

        /// <summary>오의를 썼다. 천마해체면 게이지를 시작값으로.</summary>
        public bool OnUltimate(string ultimateId)
        {
            if (ultimateId != _def.ResetUltimate) return false;
            _gauge.ResetTo(_start);
            return true;
        }
    }
}
