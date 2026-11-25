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

        // ---- 굽이 띠(장강 물길·곤륜 빙판, 2026-11-26 — 웹 합친 판 CHG.inWater·inIce·drawWaterways·boatsW 를 옮김) ----
        // 띠 j 의 가운데 줄: y = 지역 한가운데 y + j × spacing − wave × sin((x − 한가운데 x) / waveLen). 반폭 half 안이면 띠 위.
        // 값은 gimmicks.json layout(waterSpacing·waterHalf·waterWave·waterWaveLen, ice… 같은 꼴) × 그 경지 축척.

        /// <summary>띠 j 의 가운데 줄 y(x 자리). 그리는 쪽이 x 를 훑으며 줄을 긋는다. 값은 이미 축척을 곱한 세계 m.</summary>
        public static float BandCenterY(Vector2 origin, int j, float spacing, float wave, float waveLen, float x)
            => origin.Y + j * spacing - wave * (float)Math.Sin((x - origin.X) / waveLen);

        /// <summary>가운데 줄의 기울기 dy/dx(배를 그 자리 기울기로 놓을 때).</summary>
        public static float BandSlope(Vector2 origin, float wave, float waveLen, float x)
            => -wave / waveLen * (float)Math.Cos((x - origin.X) / waveLen);

        /// <summary>p 가 그 원점의 굽이 띠 위인가(지역은 안 봄).</summary>
        public static bool InBand(Vector2 origin, float spacing, float half, float wave, float waveLen, Vector2 p)
        {
            if (spacing <= 0f || waveLen <= 0f) return false;
            float v = p.Y - origin.Y + wave * (float)Math.Sin((p.X - origin.X) / waveLen);
            float m = ((v + spacing / 2f) % spacing + spacing) % spacing - spacing / 2f;
            return Math.Abs(m) < half;
        }

        /// <summary>p 가 물인가: 가장 가까운 지역에 물(gimmicks water)이 있고 그 지역 원점의 물길 띠 위. scale 은 그 경지 축척.</summary>
        public static bool InWater(Regions regions, GimmicksDef g, float scale, Vector2 p, int? sealStones = 0)
        {
            var r = regions.Nearest(p, sealStones);
            if (!Has(r, g.Water.Id)) return false;
            var L = g.Layout;
            return InBand(regions.Center(r), L.WaterSpacing * scale, L.WaterHalf * scale, L.WaterWave * scale, L.WaterWaveLen * scale, p);
        }

        /// <summary>p 가 빙판인가(곤륜 설원, 물길과 같은 꼴 — layout ice…).</summary>
        public static bool InIce(Regions regions, GimmicksDef g, float scale, Vector2 p, int? sealStones = 0)
        {
            var r = regions.Nearest(p, sealStones);
            if (!Has(r, g.IceFloor.Id)) return false;
            var L = g.Layout;
            return InBand(regions.Center(r), L.IceSpacing * scale, L.IceHalf * scale, L.IceWave * scale, L.IceWaveLen * scale, p);
        }

        /// <summary>around 에서 radius 안을 지나는 띠 번호 j 범위(그리기용). 웹 drawWaterways 처럼 위아래 한 줄씩 더.</summary>
        public static (int From, int To) BandRows(Vector2 origin, float spacing, Vector2 around, float radius)
        {
            if (spacing <= 0f) return (0, -1);
            return ((int)Math.Floor((around.Y - radius - origin.Y) / spacing) - 1, (int)Math.Ceiling((around.Y + radius - origin.Y) / spacing) + 1);
        }

        /// <summary>굽이 물길을 따라 떠내려오는 배(웹 boatsW). 띠 가운데 줄 위 spacing 간격, 생 시작부터 lifeSeconds 흐른 만큼 밀림,
        /// 그 자리가 물(InWater)인 것만. Angle 은 라디안(그 자리 기울기), Velocity 는 줄을 따라가는 속도(세계 m/s).</summary>
        public static List<(Vector2 Pos, float Angle, Vector2 Velocity)> CurvedBoats(Regions regions, GimmicksDef g, RegionDef region, float scale,
            float lifeSeconds, Vector2 around, float radius, int? sealStones = 0)
        {
            var list = new List<(Vector2, float, Vector2)>();
            var db = g.DriftingBoats; var L = g.Layout;
            float S = db.Spacing * scale, sp = L.WaterSpacing * scale, W = L.WaterWave * scale, WL = L.WaterWaveLen * scale, speed = db.Speed * scale;
            if (S <= 0f || sp <= 0f || WL <= 0f || !Has(region, g.Water.Id)) return list;
            var o = regions.Center(region);
            float sgn = Math.Cos(db.DirectionDegrees * Math.PI / 180.0) >= 0 ? 1f : -1f;
            float shift = ((sgn * speed * lifeSeconds) % S + S) % S;
            float reach = radius + db.Length * scale;
            var (j0, j1) = BandRows(o, sp, around, reach);
            int i0 = (int)Math.Floor((around.X - reach - o.X - shift) / S), i1 = (int)Math.Ceiling((around.X + reach - o.X - shift) / S);
            if ((long)(j1 - j0 + 1) * (i1 - i0 + 1) > 2000) return list;
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x = o.X + i * S + shift, y = BandCenterY(o, j, sp, W, WL, x), slope = BandSlope(o, W, WL, x);
                var p = new Vector2(x, y);
                if (Vector2.Distance(p, around) > reach || !InWater(regions, g, scale, p, sealStones)) continue;
                list.Add((p, (float)Math.Atan(slope), new Vector2(sgn * speed, sgn * speed * slope)));
            }
            return list;
        }

        /// <summary>p 가 굽이 배 위인가(배는 Angle 쪽으로 length, 가로 width — 웹 boatUnder).</summary>
        public static bool OnCurvedBoat(DriftingBoatsGimmickDef def, Vector2 boat, float angle, Vector2 p, float scale = 1f)
        {
            var d = p - boat; float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
            return Math.Abs(d.X * c + d.Y * s) <= def.Length * scale * 0.5f && Math.Abs(-d.X * s + d.Y * c) <= def.Width * scale * 0.5f;
        }

        // ---- 엇갈린 격자(무당 태극 바닥, gimmick_start_values 2절 '16m, 줄마다 반 칸 비킴, 하나는 한가운데' — 웹 CHG.taijiPads) ----

        /// <summary>그 지역의 엇갈린 격자 칸(홀수 줄 j 는 가로 반 칸 비킴) 가운데 around 에서 radius 안. (i, j) 는 칸 번호 —
        /// 경지와 같이 열쇠로 쓰면 같은 바닥이라 쿨다운을 칸으로 센다. (0,0) 이 지역 한가운데.</summary>
        public static List<(Vector2 Pos, int I, int J)> StaggeredNear(Regions regions, RegionDef region, float spacing, Vector2 around, float radius, int? sealStones = 0)
        {
            var list = new List<(Vector2, int, int)>();
            if (spacing <= 0f) return list;
            var o = regions.Center(region);
            int j0 = (int)Math.Floor((around.Y - radius - o.Y) / spacing), j1 = (int)Math.Ceiling((around.Y + radius - o.Y) / spacing);
            int i0 = (int)Math.Floor((around.X - radius - o.X) / spacing) - 1, i1 = (int)Math.Ceiling((around.X + radius - o.X) / spacing);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                var p = new Vector2(o.X + (i + (j & 1) * 0.5f) * spacing, o.Y + j * spacing);
                if (Vector2.Distance(p, around) > radius || !regions.InsideWorld(p)) continue;
                if (regions.Nearest(p, sealStones).Id != region.Id) continue;
                list.Add((p, i, j));
            }
            return list;
        }

        static bool Has(RegionDef r, string id) => !string.IsNullOrEmpty(id) && r.Gimmicks != null && r.Gimmicks.Contains(id);
    }
}
