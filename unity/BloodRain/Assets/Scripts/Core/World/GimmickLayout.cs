#nullable enable
// 지역 기믹을 지역 안 어디에 몇 개 놓는지(2026-11-20 R-10 '표대로', docs/design/gimmick_start_values.md 2절, gimmicks.json layout).
//
//   - 격자는 지역 한가운데를 원점으로 간격(화면 기준 m × 그 경지 축척)마다. halfOffset 이면 가로·세로 반 칸 비킨다(한가운데는 빔).
//   - 칸 자리에서 가장 가까운 지역이 그 땅일 때만 놓는다(Regions.Nearest, 봉마곡은 봉인석이 모이기 전엔 숨은 채로).
//   - 반 칸 격자는 경지가 올라 칸이 모두 지역 밖으로 나가면 한가운데에 하나를 둔다(웹 시안 탑림 pagodaPatchAt 규칙).
//   - 경지가 바뀌면 그 축척으로 새로 깐다(7장 '지금 경지 축척으로 생깁니다'). 부르는 쪽이 돌파 때 다시 부른다.
//
// Unity 쪽: 돌파·지역 바뀜 때 Near(플레이어 둘레 반경 안 칸만)를 받아 범종·탑림 무더기를 놓는다. 화면 밖 먼 칸은 만들지 않는다.
using System;
using System.Collections.Generic;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public static class GimmickLayout
    {
        /// <summary>그 지역의 격자 칸 가운데 around 에서 radius(세계 m) 안인 것. spacing 은 이미 축척을 곱한 세계 m.</summary>
        public static List<Vector2> Near(Regions regions, RegionDef region, float spacing, bool halfOffset, Vector2 around, float radius, int? sealStones = 0)
        {
            var list = new List<Vector2>();
            if (spacing <= 0f) return list;
            var o = regions.Center(region);
            float off = halfOffset ? 0.5f : 0f;
            int i0 = (int)Math.Floor((around.X - radius - o.X) / spacing - off), i1 = (int)Math.Ceiling((around.X + radius - o.X) / spacing - off);
            int j0 = (int)Math.Floor((around.Y - radius - o.Y) / spacing - off), j1 = (int)Math.Ceiling((around.Y + radius - o.Y) / spacing - off);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                var p = new Vector2(o.X + (i + off) * spacing, o.Y + (j + off) * spacing);
                if (Vector2.Distance(p, around) > radius || !regions.InsideWorld(p)) continue;
                if (regions.Nearest(p, sealStones).Id != region.Id) continue;
                list.Add(p);
            }
            if (halfOffset && list.Count == 0 && Vector2.Distance(o, around) <= radius && !HasAny(regions, region, spacing, true, sealStones))
                list.Add(o);
            return list;
        }

        /// <summary>그 지역 안 격자 칸 전부(반 칸 격자가 다 밖이면 한가운데 하나). 시험·지도용 — 삼류에선 수백 개다.</summary>
        public static List<Vector2> All(Regions regions, RegionDef region, float spacing, bool halfOffset, int? sealStones = 0)
        {
            var all = Near(regions, region, spacing, halfOffset, new Vector2(regions.WidthMeters / 2f, regions.HeightMeters / 2f),
                           (float)Math.Sqrt(regions.WidthMeters * regions.WidthMeters + regions.HeightMeters * regions.HeightMeters), sealStones);
            return all;
        }

        /// <summary>반 칸이든 아니든 그 지역 안에 칸이 하나라도 있나(가운데 하나 규칙을 쓸지).</summary>
        public static bool HasAny(Regions regions, RegionDef region, float spacing, bool halfOffset, int? sealStones = 0)
        {
            if (spacing <= 0f) return false;
            var o = regions.Center(region);
            float off = halfOffset ? 0.5f : 0f;
            // 지역(가장 가까운 한가운데 칸)은 지도 안이니 지도 끝까지만 보면 된다
            int ni = (int)Math.Ceiling(regions.WidthMeters / spacing) + 1, nj = (int)Math.Ceiling(regions.HeightMeters / spacing) + 1;
            for (int j = -nj; j <= nj; j++)
            for (int i = -ni; i <= ni; i++)
            {
                var p = new Vector2(o.X + (i + off) * spacing, o.Y + (j + off) * spacing);
                if (!regions.InsideWorld(p)) continue;
                if (regions.Nearest(p, sealStones).Id == region.Id) return true;
            }
            return false;
        }

        /// <summary>범종 칸 가운데 탑림 무더기(반경 patchRadius) 안에 든 것은 뺀다(웹 범종 규칙: 막는 무더기 안의 손대는 것은 뺌).</summary>
        public static List<Vector2> Without(List<Vector2> points, List<Vector2> patches, float patchRadius)
        {
            var keep = new List<Vector2>();
            foreach (var p in points)
            {
                bool inside = false;
                foreach (var c in patches) if (Vector2.Distance(p, c) <= patchRadius) { inside = true; break; }
                if (!inside) keep.Add(p);
            }
            return keep;
        }
    }
}
