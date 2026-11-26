#nullable enable
// 수귀(장강 수로 고유 적, 기획서 6장 '지역별 고유 적' — 물 구역에서만 솟아 4m 안 플레이어를 초속 1.5m 끌어당김, 여럿이어도 큰 것 하나).
//   웹 합친 판(WP-13c) spawnLocalGroup·water_pull 과 같은 규칙을 Core 로 옮김(2026-11-27, R-13U 남은 것).
//   - 솟는 자리: onlyInWater 적은 고른 자리가 물(GimmickLayout.InWater)이 아니면 이번 차례를 건너뛴다(다른 적으로 바꾸지 않음 — 웹과 같음).
//     '물속에서 올라온다'는 소환(마천패 수귀 소환)처럼 자리를 꼭 물로 옮겨야 할 때만 SnapToWater 로 가장 가까운 물길 가운데 줄에 붙인다.
//     수귀 무리 우두머리의 '수귀 부르기'는 기획서 6장 표대로 물 밖에서도 솟으니 이 판정을 거치지 않는다.
//   - 끌어당김: 수귀가 물 위이고 플레이어가 pull.range m 안이면 수귀 쪽으로 초속 pull.speed m. 경공 중엔 안 끌림. 한 걸음에 한 번(가장 큰 것).
//   거리는 지금 경지 축척의 세계 m(scale 을 곱해 넘긴다). pull 값은 enemies.json 그대로이고 축척을 곱하지 않는다(웹도 안 곱함).
using System;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public static class WaterGhost
    {
        /// <summary>이 적을 p 에 솟게 해도 되나. onlyInWater 가 아니면 늘 참, 맞으면 p 가 물일 때만.
        /// 거짓이면 이번 차례는 건너뛴다(웹 spawnLocalGroup 이 null 을 돌려주는 것과 같음).</summary>
        public static bool CanSpawnAt(EnemyDef enemy, Regions regions, GimmicksDef g, float scale, Vector2 p, int? sealStones = 0) =>
            !enemy.OnlyInWater || GimmickLayout.InWater(regions, g, scale, p, sealStones);

        /// <summary>p 에서 가장 가까운 물길 가운데 줄 자리(같은 x, 띠 j 의 가운데 y). 가장 가까운 지역에 물이 없거나
        /// 옮길 거리가 maxShift m 를 넘거나, 옮긴 자리가 물이 아니면(다른 지역으로 넘어감) null.
        /// 소환처럼 '물속에서 올라온다'를 꼭 물에 놓아야 할 때 쓴다. 벌써 물이면 p 그대로.</summary>
        public static Vector2? SnapToWater(Regions regions, GimmicksDef g, float scale, Vector2 p, float maxShift, int? sealStones = 0)
        {
            if (GimmickLayout.InWater(regions, g, scale, p, sealStones)) return p;
            var r = regions.Nearest(p, sealStones);
            if (r.Gimmicks == null || string.IsNullOrEmpty(g.Water.Id) || !r.Gimmicks.Contains(g.Water.Id)) return null;
            var L = g.Layout;
            float sp = L.WaterSpacing * scale, w = L.WaterWave * scale, wl = L.WaterWaveLen * scale;
            if (sp <= 0f || wl <= 0f) return null;
            var o = regions.Center(r);
            // 띠 j 의 가운데 줄 y = o.Y + j·sp − w·sin((x − o.X)/wl) → j = (p.Y − o.Y + w·sin(...)) / sp 를 반올림
            int j = (int)Math.Round((p.Y - o.Y + w * (float)Math.Sin((p.X - o.X) / wl)) / sp);
            var q = new Vector2(p.X, GimmickLayout.BandCenterY(o, j, sp, w, wl, p.X));
            if (Math.Abs(q.Y - p.Y) > maxShift) return null;
            return GimmickLayout.InWater(regions, g, scale, q, sealStones) ? q : (Vector2?)null;
        }

        /// <summary>수귀 하나가 이번 걸음에 플레이어를 끄는 거리(세계 m, 수귀 쪽). 안 끌면 0.
        /// 여럿이면 부르는 쪽이 길이가 가장 큰 하나만 쓴다(PullStep).</summary>
        public static Vector2 PullOffset(EnemyPull? pull, Vector2 ghost, Vector2 player, bool ghostInWater, bool playerDashing, float dt)
        {
            if (pull == null || !ghostInWater || playerDashing || dt <= 0f || pull.Speed <= 0f) return Vector2.Zero;
            var d = ghost - player;
            float len = d.Length();
            if (len <= 1e-4f || len >= pull.Range) return Vector2.Zero;
            float step = Math.Min(pull.Speed * dt, len);   // 수귀 몸을 지나쳐 끌지 않는다
            return d / len * step;
        }
    }

    /// <summary>한 걸음 동안 수귀 끌기를 모아 가장 큰 하나만 남긴다(기획서 6장 '여럿이어도 큰 것 하나').
    /// 걸음마다 Reset → 수귀마다 Offer → 플레이어 자리에 Offset 을 더한다.</summary>
    public sealed class PullStep
    {
        public Vector2 Offset { get; private set; }

        public void Reset() => Offset = Vector2.Zero;

        public void Offer(Vector2 offset)
        {
            if (offset.LengthSquared() > Offset.LengthSquared()) Offset = offset;
        }
    }
}
