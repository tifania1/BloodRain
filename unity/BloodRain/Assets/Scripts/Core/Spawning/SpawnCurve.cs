// 화면 위 목표 적 수(분 → 마리)를 선형 보간한다. 생성 디렉터는
// 현재 적 수가 목표보다 적으면 매초 부족분의 30%를 화면 바깥 고리에 만든다.
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Spawning
{
    public sealed class SpawnCurve
    {
        readonly SpawnTargetDef _def;

        public SpawnCurve(SpawnTargetDef def)
        {
            if (def.Points.Count < 2) throw new ArgumentException("spawnTarget.points에 점이 2개 이상 필요합니다");
            _def = def;
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

        public bool SpawningStopped(float minutes) => minutes >= _def.StopSpawningAtMinute && minutes < _def.BossAtMinute;

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
