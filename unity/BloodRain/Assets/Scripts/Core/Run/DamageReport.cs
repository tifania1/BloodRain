#nullable enable
// 무공별 피해(기획서 12장 '화면 편의 규칙' 무공별 피해 — 일시정지 화면, '정산의 무공별 피해 비중과 같은 셈').
//   웹 프로토타입 멈춤 화면 pauselist 와 같은 셈: 지금까지 피해, 초당 피해 = 피해 / max(1초, 지금 - 얻은 시각), 비중 = 피해 / 합.
//   합이 0 이면 비중은 0. 정산 막대는 ByDamage(6) — 큰 것부터 여섯.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Run
{
    public readonly struct WeaponDamageLine
    {
        public readonly string Id;
        public readonly double Dealt;
        public readonly double PerSecond;
        /// <summary>0~1.</summary>
        public readonly float Share;
        public WeaponDamageLine(string id, double dealt, double perSecond, float share) { Id = id; Dealt = dealt; PerSecond = perSecond; Share = share; }
    }

    public static class DamageReport
    {
        /// <summary>
        /// 무공마다 한 줄(dealt 의 차례 그대로). acquiredAt 은 그 무공을 얻은 판 시각(초), 없으면 0.
        /// now 는 지금 판 시각(초).
        /// </summary>
        public static List<WeaponDamageLine> Lines(IEnumerable<KeyValuePair<string, double>> dealt, float now,
            IReadOnlyDictionary<string, float>? acquiredAt = null)
        {
            var list = dealt.ToList();
            double sum = 0;
            foreach (var kv in list) sum += Math.Max(0, kv.Value);
            var lines = new List<WeaponDamageLine>(list.Count);
            foreach (var kv in list)
            {
                double d = Math.Max(0, kv.Value);
                float t0 = acquiredAt != null && acquiredAt.TryGetValue(kv.Key, out var a) ? a : 0f;
                double perSecond = d / Math.Max(1.0, now - t0);
                lines.Add(new WeaponDamageLine(kv.Key, d, perSecond, sum > 0 ? (float)(d / sum) : 0f));
            }
            return lines;
        }

        /// <summary>큰 피해부터 take 줄(정산 막대는 6). 같은 피해면 원래 차례.</summary>
        public static List<WeaponDamageLine> ByDamage(IEnumerable<KeyValuePair<string, double>> dealt, int take, float now = 0f,
            IReadOnlyDictionary<string, float>? acquiredAt = null) =>
            Lines(dealt, now, acquiredAt).Select((l, i) => (l, i)).OrderByDescending(x => x.l.Dealt).ThenBy(x => x.i)
                .Take(Math.Max(0, take)).Select(x => x.l).ToList();
    }
}
