// 레벨 n → n+1 에 필요한 진기(경험치). 구간별 선형 증가.
//   n ≤ 20       : 5 + 10(n-1)
//   21 ≤ n ≤ 40  : 195 + 13(n-20)
//   n ≥ 41       : 455 + 16(n-40)
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public sealed class XpCurve
    {
        readonly XpCurveDef _def;

        public XpCurve(XpCurveDef def) { _def = def; }

        /// <summary>레벨 level에서 다음 레벨로 가는 데 필요한 진기.</summary>
        public int Required(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            foreach (var s in _def.Segments)
                if (level >= s.FromLevel && level <= s.ToLevel)
                    return s.Base + s.Step * (level - s.Anchor);
            var last = _def.Segments[_def.Segments.Count - 1];
            return last.Base + last.Step * (level - last.Anchor);
        }

        /// <summary>레벨 1에서 targetLevel에 도달하기까지 누적 진기.</summary>
        public long CumulativeTo(int targetLevel)
        {
            long sum = 0;
            for (int n = 1; n < targetLevel; n++) sum += Required(n);
            return sum;
        }

        /// <summary>누적 진기로 도달한 레벨.</summary>
        public int LevelFor(long totalXp)
        {
            int level = 1;
            long remaining = totalXp;
            while (remaining >= Required(level)) { remaining -= Required(level); level++; }
            return level;
        }
    }
}
