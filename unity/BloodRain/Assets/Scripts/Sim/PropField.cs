using System;
using System.Collections.Generic;
using System.Numerics;
using BloodRain.Core.World;

namespace BloodRain.Sim
{
    public enum PropKind { Tree, House, Temple, Mount }

    /// <summary>땅 위 소품 하나. 위치는 Core 세계 m(왼쪽 위 0,0, 아래로 y 증가), 크기 S 는 세계 m.</summary>
    public struct Prop
    {
        public PropKind Kind;
        public Vector2 Pos;
        public float S;
        public int Variant;
    }

    // 지형 단계 1, 소품 층(기획서 15장, TU-07). 웹 시안 web/lookdev/cheonha.html buildProps 를 옮겼다.
    // 크기가 세계 m 로 고정이라 경지가 오를수록 화면에서 작아진다. 소품 종류가 곧 경지별 층이다:
    //   나무 3~5.5m(삼류·이류 마당), 집 6~8m(일류), 전각 22m(절정), 산 90~320m(초절정 이후).
    // 화면에서 2픽셀보다 작아진 것은 그리는 쪽(PropLayer)이 거른다.
    // 지역 둘레(지역 한가운데에서 8~140m)에 시드로 뿌리고, 마을 70곳·흩어진 나무·산줄기를 더한다. 바다(땅 다각형 밖)는 뺀다.
    // 찾기는 200m 격자.
    public sealed class PropField
    {
        public const float Cell = 200f;

        // 중원 땅 모양(지도 비율). 웹 시안 LAND 그대로. 지도 그림이 오면 그 그림에 맞춰 다시 딴다.
        static readonly float[] Land =
        {
            0.80f,0.10f, 0.88f,0.16f, 0.86f,0.24f, 0.80f,0.26f, 0.84f,0.33f, 0.80f,0.40f, 0.86f,0.47f, 0.88f,0.56f, 0.84f,0.66f, 0.80f,0.74f,
            0.72f,0.82f, 0.62f,0.88f, 0.52f,0.93f, 0.44f,0.96f, 0.36f,0.91f, 0.30f,0.85f, 0.22f,0.81f, 0.12f,0.75f, 0.05f,0.62f, 0.03f,0.48f,
            0.06f,0.36f, 0.04f,0.24f, 0.12f,0.16f, 0.24f,0.12f, 0.34f,0.17f, 0.44f,0.13f, 0.56f,0.11f, 0.66f,0.06f, 0.74f,0.04f,
        };

        // 산줄기: x0, x1, y0, y1(지도 비율), 개수
        static readonly float[] Ranges =
        {
            0.03f,0.22f,0.18f,0.62f,70, 0.30f,0.50f,0.78f,0.94f,40, 0.40f,0.52f,0.28f,0.50f,28, 0.28f,0.60f,0.10f,0.17f,26, 0.20f,0.34f,0.40f,0.52f,18,
        };

        readonly List<Prop> _props = new List<Prop>();
        readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();
        readonly List<int> _landmarks = new List<int>(); // 산·전각(멀리서도 보이는 것)
        readonly float _w, _h;

        public IReadOnlyList<Prop> Props => _props;
        public IReadOnlyList<int> Landmarks => _landmarks;

        public PropField(Regions regions, int seed = 7)
        {
            _w = regions.WidthMeters; _h = regions.HeightMeters;
            var r = new Random(seed);
            float R() => (float)r.NextDouble();
            const float Tau = (float)(Math.PI * 2);
            void Add(PropKind k, float x, float y, float s)
            {
                int v = r.Next(3);
                if (InLand(x, y)) _props.Add(new Prop { Kind = k, Pos = new Vector2(x, y), S = s, Variant = v });
            }

            foreach (var g in regions.All)
            {
                if (g.Final) continue; // 봉마곡은 봉인석이 모이기 전엔 보이면 안 되니 따로 뿌리지 않는다
                var c = regions.Center(g);
                Add(PropKind.Temple, c.X, c.Y - 14, 22);
                for (int i = 0; i < 14; i++) { float a = R() * Tau, d = 16 + R() * 34; Add(PropKind.House, c.X + MathF.Cos(a) * d, c.Y + MathF.Sin(a) * d, 6 + R() * 2); }
                for (int i = 0; i < 160; i++) { float a = R() * Tau, d = 8 + R() * 140; Add(PropKind.Tree, c.X + MathF.Cos(a) * d, c.Y + MathF.Sin(a) * d, 3 + R() * 2.5f); }
            }
            for (int v = 0; v < 70; v++)
            {
                float cx = R() * _w, cy = R() * _h;
                if (!InLand(cx, cy)) continue;
                int n = (int)(6 + R() * 8);
                for (int i = 0; i < n; i++) { float a = R() * Tau, d = R() * 26; Add(PropKind.House, cx + MathF.Cos(a) * d, cy + MathF.Sin(a) * d, 6 + R() * 2); }
            }
            for (int i = 0; i < 6000; i++) Add(PropKind.Tree, R() * _w, R() * _h, 3 + R() * 2.5f);
            for (int k = 0; k < Ranges.Length; k += 5)
                for (int i = 0; i < (int)Ranges[k + 4]; i++)
                    Add(PropKind.Mount, Lerp(Ranges[k], Ranges[k + 1], R()) * _w, Lerp(Ranges[k + 2], Ranges[k + 3], R()) * _h, 90 + R() * 230);

            // 먼 것(위쪽, y 작은 것)부터 그리게 정렬
            _props.Sort((a, b) => a.Pos.Y.CompareTo(b.Pos.Y));
            for (int i = 0; i < _props.Count; i++)
            {
                var p = _props[i];
                long key = Key((int)MathF.Floor(p.Pos.X / Cell), (int)MathF.Floor(p.Pos.Y / Cell));
                if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = new List<int>();
                list.Add(i);
                if (p.Kind == PropKind.Mount || p.Kind == PropKind.Temple) _landmarks.Add(i);
            }
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        public bool InLand(float x, float y)
        {
            float mx = x / _w, my = y / _h;
            bool c = false;
            int n = Land.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = Land[i * 2], yi = Land[i * 2 + 1], xj = Land[j * 2], yj = Land[j * 2 + 1];
                if ((yi > my) != (yj > my) && mx < (xj - xi) * (my - yi) / (yj - yi) + xi) c = !c;
            }
            return c;
        }

        /// <summary>세계 m 사각형(min~max)에 걸치는 격자 칸의 소품 번호를 모은다. 칸이 maxCells 를 넘으면 산·전각만.
        /// 번호는 정렬 순서(먼 것 먼저)로 돌려준다.</summary>
        public void Query(Vector2 min, Vector2 max, List<int> result, int maxCells = 900)
        {
            result.Clear();
            int x0 = (int)MathF.Floor(min.X / Cell), x1 = (int)MathF.Floor(max.X / Cell);
            int y0 = (int)MathF.Floor(min.Y / Cell), y1 = (int)MathF.Floor(max.Y / Cell);
            if ((long)(x1 - x0) * (y1 - y0) >= maxCells) { result.AddRange(_landmarks); return; }
            for (int gx = x0; gx <= x1; gx++)
                for (int gy = y0; gy <= y1; gy++)
                    if (_grid.TryGetValue(Key(gx, gy), out var list)) result.AddRange(list);
            result.Sort();
        }
    }

    /// <summary>Core 세계 m(아래로 y 증가) ↔ Unity 좌표(위로 y 증가). Origin 의 세계 m 자리가 Unity (0,0) 이 된다.</summary>
    public static class WorldCoords
    {
        public static Vector2 Origin;

        public static UnityEngine.Vector2 ToUnity(Vector2 world) => new UnityEngine.Vector2(world.X - Origin.X, -(world.Y - Origin.Y));
        public static Vector2 FromUnity(UnityEngine.Vector2 u) => new Vector2(u.x + Origin.X, Origin.Y - u.y);
    }
}
