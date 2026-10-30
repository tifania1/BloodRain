// 화면 위 목표 적 수(분 → 마리)를 선형 보간한다. 마지막 점 뒤로는 마지막 값을 지킨다(한 생: 18분부터 400).
// 생성 디렉터는 현재 적 수가 목표보다 적으면 매초 부족분의 30%를 화면 바깥 고리에 만든다.
// 곡선은 모든 흐름이 같이 쓰고, 생성을 잠깐 멈추는 구간(서장 장삼 경고 전)만 흐름마다 넘겨받는다.
// 땅의 주인과 싸우는 동안 목표를 줄이는 배율(cheonha.json bosses.spawnTargetScale)은 한 생의 생성(T-09)이 곱한다.
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Spawning
{
    public sealed class SpawnCurve
    {
        readonly SpawnTargetDef _def;
        readonly float? _pauseFrom;
        readonly float? _pauseUntil;

        /// <param name="pauseFromMinute">이 분부터 생성을 멈춘다. 없으면 멈추지 않는다.</param>
        /// <param name="pauseUntilMinute">이 분부터 다시 만든다(서장은 보스 시각). 없으면 멈춘 채로 있는다.</param>
        public SpawnCurve(SpawnTargetDef def, float? pauseFromMinute = null, float? pauseUntilMinute = null)
        {
            if (def.Points.Count < 2) throw new ArgumentException("spawnTarget.points에 점이 2개 이상 필요합니다");
            if (pauseFromMinute != null && pauseUntilMinute != null && pauseUntilMinute < pauseFromMinute)
                throw new ArgumentException("생성을 다시 시작하는 시각이 멈추는 시각보다 이릅니다");
            _def = def;
            _pauseFrom = pauseFromMinute;
            _pauseUntil = pauseUntilMinute;
        }

        public float TargetAt(float minutes)
        {
            var pts = _def.Points;
            if (minutes <= pts[0][0]) return pts[0][1];
            for (int i = 1; i < pts.Count; i++)
            {
                if (minutes <= pts[i][0])
                {
                    float x0 = pts[i - 1][0], y0 = pts[i - 1][1], x1 = pts[i][0], y1 = pts[i][1];
                    float t = (minutes - x0) / (x1 - x0);
                    return y0 + (y1 - y0) * t;
                }
            }
            return pts[pts.Count - 1][1];
        }

        public bool SpawningStopped(float minutes) =>
            _pauseFrom != null && minutes >= _pauseFrom && (_pauseUntil == null || minutes < _pauseUntil);

        /// <summary>이번 1초 동안 새로 만들 적 수.</summary>
        public int SpawnThisSecond(float minutes, int aliveNow)
        {
            if (SpawningStopped(minutes)) return 0;
            float deficit = TargetAt(minutes) - aliveNow;
            if (deficit <= 0) return 0;
            // float 오차(180 × 0.3 = 54.000002)로 하나 더 올라가지 않게 아주 작은 값을 빼고 올림한다.
            return (int)Math.Ceiling(deficit * _def.SpawnFractionPerSecond - 1e-4f);
        }
    }
}
