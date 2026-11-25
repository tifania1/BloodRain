using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 지역마다 다른 바닥과, 지역 자료로 그린 천하도(2026-11-17 밤 사람: "누런 끼 빼고 각 지역마다의 특색을 살린 그래픽으로,
    // 지역을 이동할 때마다 맞는 이미지가 나와야", "지도가 현실 지역 위치랑 안 맞는다").
    //   - 바닥: 지역 하나에 512px 이음매 없는 타일 한 장을 코드로 그린다. 빛깔은 종이 누런빛을 버리고 지역 빛(돌·대숲·눈·물가…)으로.
    //     원화 타일이 있는 지역(ArtOf 표 — 열하나 다)은 지역 바탕빛 위에 원화를 옅게 얹어 쓴다(Resources/Art/Tiles).
    //   - 천하도: 옛 수묵 지도 그림은 땅 자리(cheonha.json regions.pos)와 맞지 않았다. 땅 모양(PropField.InLand)·지역 자리·산 자리·
    //     두 큰 강을 자료에서 바로 그려 지도와 실제 자리가 늘 맞게 한다.
    // 처음 쓸 때 한 번 그려 두고 다시 쓴다(지역은 열하나, 한 장 0.1초 안쪽).
    public static class Biomes
    {
        public const int TileSize = 512;

        public sealed class Look
        {
            public Color Base;      // 바닥 바탕
            public Color Shade;     // 얼룩이 기우는 빛
            public Color Map;       // 지도에서 이 땅의 빛
            public string Kind;     // 무늬
        }

        static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            // 마을 흙마당: 다진 흙에 박힌 돌
            ["cheongun"] = new Look { Base = Hex(0xC6C3BC), Shade = Hex(0x9C978E), Map = Hex(0xC9C4B8), Kind = "cobble" },
            // 숭산 소림: 네모난 돌판, 줄눈에 이끼
            ["songshan"] = new Look { Base = Hex(0xBFC5BA), Shade = Hex(0x8C9A84), Map = Hex(0xB4C0AC), Kind = "slab" },
            // 개방 총타(저잣거리): 수레 자국 난 흙길, 짚
            ["gaebang_hq"] = new Look { Base = Hex(0xBEBAB3), Shade = Hex(0x918880), Map = Hex(0xC8BBAA), Kind = "dirt" },
            // 남궁세가: 마름모로 깐 청석
            ["namgung_manor"] = new Look { Base = Hex(0xC9D0D4), Shade = Hex(0x97A4AD), Map = Hex(0xBFCAD2), Kind = "diamond" },
            // 당가타(사천 대숲): 풀빛 땅에 댓잎
            ["danggata"] = new Look { Base = Hex(0xB5C6AC), Shade = Hex(0x7C9A74), Map = Hex(0xA3BE9A), Kind = "bamboo" },
            // 장강 수로: 젖은 모래톱, 물결 자국
            ["janggang"] = new Look { Base = Hex(0xB9CECD), Shade = Hex(0x86AAAB), Map = Hex(0xA9CACB), Kind = "shore" },
            // 무당산: 옥빛 돌, 구름 무늬
            ["wudangsan"] = new Look { Base = Hex(0xC0CFC9), Shade = Hex(0x8EABA2), Map = Hex(0xAFC8BF), Kind = "cloud" },
            // 화산: 찬 화강암, 금, 매화 꽃잎
            ["huashan"] = new Look { Base = Hex(0xC8C6CC), Shade = Hex(0x9A96A2), Map = Hex(0xC3BDC8), Kind = "granite" },
            // 곤륜: 눈밭, 푸른 그늘
            ["kunlun"] = new Look { Base = Hex(0xE4EAEF), Shade = Hex(0xB9CBDB), Map = Hex(0xE3EAF0), Kind = "snow" },
            // 십만대산: 어두운 풀빛 땅, 뿌리
            ["sipmandaesan"] = new Look { Base = Hex(0xAEB8A2), Shade = Hex(0x74806C), Map = Hex(0x97AC8C), Kind = "jungle" },
            // 봉마곡: 그을린 땅, 핏빛 금
            ["bongmagok"] = new Look { Base = Hex(0xB3A3A2), Shade = Hex(0x7E6868), Map = Hex(0xB59C9C), Kind = "scorched" },
        };
        static readonly Look Fallback = new Look { Base = Hex(0xC8C8C4), Shade = Hex(0x9A9A96), Map = Hex(0xC8C8C4), Kind = "cobble" };

        public static Look Of(string regionId) => regionId != null && Looks.TryGetValue(regionId, out var l) ? l : Fallback;

        static readonly Dictionary<string, Texture2D> Tiles = new Dictionary<string, Texture2D>();

        /// <summary>그 지역 바닥 타일(코드로 그린 것). 원화가 있는 지역은 MapLayer 가 Art 를 대신 깐다. 지역 알림의 띠는 늘 이것.</summary>
        public static Texture2D Tile(string regionId)
        {
            regionId = regionId ?? "";
            if (Tiles.TryGetValue(regionId, out var t) && t != null) return t;
            t = MakeTile(regionId, Of(regionId));
            Tiles[regionId] = t;
            return t;
        }

        // 원화 타일(Resources/Art/Tiles, 2026-10~11 에 뽑아 둔 수묵 바닥)과 얹는 세기. 원화는 어두워서(평균 밝기 0.4) 그대로 깔면
        // 먹 이펙트와 적이 묻힌다(2026-11-13 검토에서 벽돌 바닥을 걷어낸 까닭). 그래서 지역 바탕빛 위에 이 세기만큼만 얹어 밝게 씻어 쓴다.
        // 흙바닥은 세게 얹으면 누렇게 돌아가니 약하게, 돌·대숲은 무늬가 살게 세게. 원화가 새로 오면 여기 한 줄.
        static readonly Dictionary<string, (string file, float opacity)> ArtOf = new Dictionary<string, (string, float)>
        {
            ["cheongun"] = ("tile_cheongun_yard", 0.36f),
            ["songshan"] = ("tile_ch1_courtyard", 0.5f),
            ["gaebang_hq"] = ("tile_gaebang_lane", 0.26f),
            ["namgung_manor"] = ("tile_namgung_court", 0.5f),
            ["danggata"] = ("tile_danggata_bamboo", 0.48f),
            ["janggang"] = ("tile_changjiang_bank", 0.42f),
            // 2026-11-21 들인 셋(Artlist 01:47 차례). 십만대산 돌은 아주 짙어 옅게 얹는다(먹 이펙트가 묻히지 않게)
            ["huashan"] = ("tile_huashan_rock", 0.4f),
            ["kunlun"] = ("tile_kunlun_snow", 0.45f),
            ["sipmandaesan"] = ("tile_sipmandaesan_stone", 0.24f),
            // 2026-11-24 RG-01c: 무당산 돌 바닥(Art/Tiles 에 있던 것을 옮김). 세기는 화산 바위와 같게 두고 Unity 에서 눈으로 맞춤
            ["wudangsan"] = ("tile_wudang_stone", 0.4f),
            // 2026-11-25 봉마곡 혈지(종장) 바닥: Art/Tiles 에 들어와 있던 것을 옮김(RG-01c 와 같은 길). 세기는 Unity 에서 눈으로(R4-03U)
            ["bongmagok"] = ("tile_bongmagok_ground", 0.4f),
        };
        static readonly Dictionary<string, Texture2D> Arts = new Dictionary<string, Texture2D>();

        /// <summary>그 지역의 원화 타일과 얹는 세기. 없으면 null(코드 타일을 쓴다).</summary>
        public static Texture2D Art(string regionId, out float opacity)
        {
            opacity = 0f;
            if (regionId == null || !ArtOf.TryGetValue(regionId, out var a)) return null;
            opacity = a.opacity;
            if (!Arts.TryGetValue(regionId, out var t)) { t = Resources.Load<Texture2D>("Art/Tiles/" + a.file); Arts[regionId] = t; }
            return t;
        }

        // ---- 그리는 판(가장자리에서 감긴다) ----
        sealed class Canvas
        {
            public readonly int T;
            public readonly Color[] Px;
            readonly System.Random _rng;
            public Canvas(int size, int seed) { T = size; Px = new Color[size * size]; _rng = new System.Random(seed); }
            public float R() => (float)_rng.NextDouble();
            public float R(float a, float b) => a + (b - a) * R();

            public void Blend(int x, int y, Color c, float a)
            {
                if (a <= 0f) return;
                x = ((x % T) + T) % T; y = ((y % T) + T) % T;
                int i = y * T + x;
                var p = Px[i];
                a = Mathf.Min(1f, a);
                Px[i] = new Color(p.r + (c.r - p.r) * a, p.g + (c.g - p.g) * a, p.b + (c.b - p.b) * a, 1f);
            }

            /// <summary>가장자리가 부드러운 타원. soft 0 이면 딱 끊기고 1 이면 가운데부터 풀린다.</summary>
            public void Blob(float cx, float cy, float rx, float ry, float rot, Color c, float a, float soft = 0.5f)
            {
                float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
                int r = Mathf.CeilToInt(Mathf.Max(rx, ry)) + 1;
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        float u = (x * cs + y * sn) / rx, v = (-x * sn + y * cs) / ry;
                        float d = u * u + v * v;
                        if (d >= 1f) continue;
                        float k = soft <= 0f ? 1f : Mathf.Clamp01((1f - Mathf.Sqrt(d)) / soft);
                        Blend(Mathf.RoundToInt(cx) + x, Mathf.RoundToInt(cy) + y, c, a * k);
                    }
            }

            /// <summary>굽는 획. 굵기는 가운데가 굵고 끝이 가늘다(taper 1) 또는 고르다(0).</summary>
            public void Stroke(float x, float y, float ang, float len, float width, float bend, Color c, float a, float taper = 1f, float wobble = 0f)
            {
                for (float t = 0; t < len; t += 0.7f)
                {
                    float k = t / len;
                    float w = width * (taper > 0f ? Mathf.Lerp(1f, Mathf.Sin(k * Mathf.PI), taper) : 1f);
                    if (w <= 0.75f) Blend(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c, a * Mathf.Max(0.35f, w));
                    else Blob(x, y, w, w, 0f, c, a, 0.7f);
                    x += Mathf.Cos(ang) * 0.7f; y += Mathf.Sin(ang) * 0.7f;
                    ang += bend + (wobble > 0f ? (R() - 0.5f) * wobble : 0f);
                }
            }

            public float[] Lattice(int n) { var l = new float[n * n]; for (int i = 0; i < l.Length; i++) l[i] = R(); return l; }
            public float Sample(float[] l, int n, float x, float y)
            {
                float fx = x * n / T, fy = y * n / T;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0;
                tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                x0 = ((x0 % n) + n) % n; y0 = ((y0 % n) + n) % n;
                int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;
                return Mathf.Lerp(Mathf.Lerp(l[y0 * n + x0], l[y0 * n + x1], tx), Mathf.Lerp(l[y1 * n + x0], l[y1 * n + x1], tx), ty);
            }
        }

        static Texture2D MakeTile(string id, Look look)
        {
            const int T = TileSize;
            int seed = 17;
            foreach (char ch in id) seed = seed * 31 + ch;
            var cv = new Canvas(T, seed);
            var n1 = cv.Lattice(5); var n2 = cv.Lattice(17); var n3 = cv.Lattice(61);
            // 바탕: 지역 빛에 큰 얼룩·잔 얼룩. 얼룩은 Shade 쪽으로 기운다
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    float v = cv.Sample(n1, 5, x, y) * 0.5f + cv.Sample(n2, 17, x, y) * 0.32f + cv.Sample(n3, 61, x, y) * 0.18f;
                    float k = Mathf.Clamp01((v - 0.36f) * 1.5f) * 0.34f;
                    cv.Px[y * T + x] = Color.Lerp(look.Base, look.Shade, k);
                }
            var ink = new Color(0.16f, 0.15f, 0.15f);
            var light = Color.Lerp(look.Base, Color.white, 0.6f);
            switch (look.Kind)
            {
                case "slab":
                {
                    // 돌판 128px(3m), 줄마다 반 칸 어긋남. 줄눈은 먹, 판마다 밝기가 조금 다르다. 줄눈 따라 이끼
                    const int S = 128;
                    for (int gy = 0; gy < T / S; gy++)
                        for (int gx = 0; gx < T / S; gx++)
                        {
                            float tone = cv.R(-0.05f, 0.05f);
                            int ox = gx * S + (gy % 2) * S / 2, oy = gy * S;
                            var c = tone > 0 ? light : look.Shade;
                            for (int y = 3; y < S - 3; y++) for (int x = 3; x < S - 3; x++) cv.Blend(ox + x, oy + y, c, Mathf.Abs(tone) * 2.2f);
                            for (int i = 0; i < S; i++)
                            {
                                float a = 0.30f + cv.R() * 0.16f;
                                cv.Blend(ox + i, oy, ink, a); cv.Blend(ox + i, oy + 1, ink, a * 0.5f);
                                cv.Blend(ox, oy + i, ink, a); cv.Blend(ox + 1, oy + i, ink, a * 0.5f);
                            }
                            // 모서리 깨짐
                            if (cv.R() < 0.5f) cv.Stroke(ox + cv.R(8, S - 8), oy + cv.R(8, S - 8), cv.R(0, 6.28f), cv.R(14, 46), 0.7f, 0f, ink, 0.22f, 0f, 0.5f);
                        }
                    var moss = new Color(0.42f, 0.52f, 0.36f);
                    for (int i = 0; i < 60; i++)
                    {
                        bool onRow = cv.R() < 0.5f;
                        float x = onRow ? cv.R(0, T) : Mathf.Round(cv.R(0, T) / 64f) * 64f + cv.R(-4, 4);
                        float y = onRow ? Mathf.Round(cv.R(0, T) / S) * S + cv.R(-4, 4) : cv.R(0, T);
                        cv.Blob(x, y, cv.R(5, 16), cv.R(3, 9), cv.R(0, 3.14f), moss, cv.R(0.14f, 0.3f), 0.9f);
                    }
                    break;
                }
                case "cobble":
                {
                    // 납작한 돌: 가는 먹 테두리(아래쪽만 조금 진하게) 안을 밝게. 얼룩처럼 보이지 않게 진한 속은 쓰지 않는다
                    for (int i = 0; i < 120; i++)
                    {
                        float x = cv.R(0, T), y = cv.R(0, T), rx = cv.R(8, 19), ry = rx * cv.R(0.62f, 0.9f), rot = cv.R(0, 3.14f);
                        cv.Blob(x + 0.8f, y + 1.6f, rx + 1.2f, ry + 1.2f, rot, ink, 0.20f, 0.12f);         // 테두리·아래 그늘
                        cv.Blob(x, y, rx, ry, rot, Color.Lerp(look.Base, light, cv.R(0.35f, 0.9f)), 0.95f, 0.12f);
                        if (cv.R() < 0.4f) cv.Stroke(x - rx * 0.4f, y, rot + cv.R(-0.5f, 0.5f), rx * 0.8f, 0.6f, 0f, look.Shade, 0.35f, 0f, 0.3f); // 돌결
                    }
                    for (int i = 0; i < 380; i++) cv.Blend((int)cv.R(0, T), (int)cv.R(0, T), ink, cv.R(0.05f, 0.16f));
                    break;
                }
                case "dirt":
                {
                    // 수레 자국 두 줄(가로로 길게, 타일을 넘어 이어짐), 짚, 잔돌
                    foreach (float y0 in new[] { 150f, 196f, 392f, 430f })
                        for (int x = 0; x < T; x++)
                        {
                            float y = y0 + Mathf.Sin(x / (float)T * Mathf.PI * 2f) * 7f;
                            cv.Blob(x, y, 3.2f, 3.2f, 0f, look.Shade, 0.10f, 0.9f);
                            if (x % 3 == 0) cv.Blend(x, Mathf.RoundToInt(y) + 3, ink, 0.07f);
                        }
                    var straw = new Color(0.86f, 0.82f, 0.66f);
                    for (int i = 0; i < 260; i++) cv.Stroke(cv.R(0, T), cv.R(0, T), cv.R(0, 6.28f), cv.R(6, 20), 0.6f, cv.R(-0.02f, 0.02f), straw, cv.R(0.25f, 0.5f), 0f);
                    for (int i = 0; i < 70; i++) { float r = cv.R(1.5f, 4f); cv.Blob(cv.R(0, T), cv.R(0, T), r, r * 0.8f, cv.R(0, 3f), ink, cv.R(0.12f, 0.24f), 0.4f); }
                    break;
                }
                case "diamond":
                {
                    // 마름모 깔기: 대각 줄눈 64px. 칸마다 밝기 조금
                    const int S = 64;
                    for (int y = 0; y < T; y++)
                        for (int x = 0; x < T; x++)
                        {
                            int a = (x + y) / S, b = (x - y + T * 4) / S;
                            float tone = ((a * 7 + b * 13) % 5 - 2) * 0.022f;
                            cv.Blend(x, y, tone > 0 ? light : look.Shade, Mathf.Abs(tone) * 2f);
                            int da = (x + y) % S, db = (x - y + T * 4) % S;
                            if (da < 2 || db < 2) cv.Blend(x, y, ink, da == 0 || db == 0 ? 0.30f : 0.14f);
                        }
                    for (int i = 0; i < 40; i++) cv.Blob(cv.R(0, T), cv.R(0, T), cv.R(8, 22), cv.R(5, 12), cv.R(0, 3f), light, 0.16f, 1f);
                    break;
                }
                case "bamboo":
                {
                    // 댓잎: 가늘고 뾰족한 획, 어두운 풀빛 셋에 마른 잎 하나. 사이에 대 그림자 줄
                    var leaf = new Color(0.30f, 0.44f, 0.28f); var dry = new Color(0.78f, 0.76f, 0.56f);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = cv.R(0, T), ang = 1.2f + cv.R(-0.12f, 0.12f);
                        cv.Stroke(x, 0, ang, T * 1.08f, 2.6f, 0f, look.Shade, 0.16f, 0f);
                    }
                    for (int i = 0; i < 520; i++)
                    {
                        bool d = i % 4 == 0;
                        cv.Stroke(cv.R(0, T), cv.R(0, T), cv.R(0, 6.28f), cv.R(10, 26), d ? 1.3f : 1.6f, cv.R(-0.03f, 0.03f), d ? dry : leaf, d ? cv.R(0.3f, 0.55f) : cv.R(0.22f, 0.5f), 1f);
                    }
                    break;
                }
                case "shore":
                {
                    // 물결 자국: 가로로 길고 느린 물결 줄, 밝은 줄과 어두운 줄이 번갈아. 조약돌
                    for (int row = 0; row < 16; row++)
                    {
                        float y0 = row * (T / 16f) + cv.R(-5, 5), ph = cv.R(0, 6.28f), amp = cv.R(4, 9);
                        var c = row % 2 == 0 ? light : look.Shade;
                        for (int x = 0; x < T; x++)
                        {
                            float y = y0 + Mathf.Sin(x / (float)T * Mathf.PI * 4f + ph) * amp + Mathf.Sin(x / (float)T * Mathf.PI * 10f + ph * 2f) * 2f;
                            float a = 0.22f + 0.18f * Mathf.Sin(x / (float)T * Mathf.PI * 6f + ph);
                            cv.Blob(x, y, 1.6f, 1.6f, 0f, c, Mathf.Max(0.05f, a), 0.8f);
                        }
                    }
                    for (int i = 0; i < 46; i++) { float r = cv.R(2.5f, 7f); float x = cv.R(0, T), y = cv.R(0, T); cv.Blob(x + 1, y + 1.5f, r + 1, r * 0.75f + 1, 0f, ink, 0.14f, 0.4f); cv.Blob(x, y, r, r * 0.75f, cv.R(0, 3f), light, 0.5f, 0.35f); }
                    break;
                }
                case "cloud":
                {
                    // 구름 무늬: 말려 드는 옅은 획, 이끼 얼룩
                    for (int i = 0; i < 26; i++)
                    {
                        float x = cv.R(0, T), y = cv.R(0, T), ang = cv.R(0, 6.28f);
                        float bend = cv.R(0.018f, 0.034f) * (cv.R() < 0.5f ? 1 : -1);
                        cv.Stroke(x, y, ang, cv.R(90, 190), 1.8f, bend, light, 0.42f, 1f);
                        cv.Stroke(x + 3, y + 3, ang, cv.R(60, 120), 1.2f, bend * 1.25f, look.Shade, 0.26f, 1f);
                    }
                    var moss = new Color(0.40f, 0.55f, 0.47f);
                    for (int i = 0; i < 38; i++) cv.Blob(cv.R(0, T), cv.R(0, T), cv.R(6, 20), cv.R(4, 12), cv.R(0, 3f), moss, cv.R(0.08f, 0.2f), 1f);
                    for (int i = 0; i < 12; i++) cv.Stroke(cv.R(0, T), cv.R(0, T), cv.R(0, 6.28f), cv.R(30, 80), 0.7f, 0f, ink, 0.2f, 0f, 0.4f);
                    break;
                }
                case "granite":
                {
                    // 금 간 화강암: 꺾이는 먹 금, 잔 반점, 분홍 매화 꽃잎
                    for (int i = 0; i < 22; i++) cv.Stroke(cv.R(0, T), cv.R(0, T), cv.R(0, 6.28f), cv.R(50, 170), cv.R(0.7f, 1.3f), 0f, ink, cv.R(0.24f, 0.42f), 0.6f, 0.55f);
                    for (int i = 0; i < 900; i++) cv.Blend((int)cv.R(0, T), (int)cv.R(0, T), cv.R() < 0.6f ? ink : light, cv.R(0.06f, 0.2f));
                    var petal = new Color(0.86f, 0.52f, 0.62f); var petal2 = new Color(0.95f, 0.78f, 0.82f);
                    for (int i = 0; i < 90; i++) { float r = cv.R(2.2f, 4.2f); cv.Blob(cv.R(0, T), cv.R(0, T), r, r * 0.62f, cv.R(0, 3.14f), cv.R() < 0.5f ? petal : petal2, cv.R(0.55f, 0.9f), 0.35f); }
                    break;
                }
                case "snow":
                {
                    // 눈 두둑: 크고 부드러운 푸른 그늘, 그 위쪽에 흰 볕. 드문 바위, 발자국 같은 점
                    for (int i = 0; i < 34; i++)
                    {
                        float x = cv.R(0, T), y = cv.R(0, T), rx = cv.R(30, 80), ry = rx * cv.R(0.3f, 0.5f), rot = cv.R(-0.4f, 0.4f);
                        cv.Blob(x, y + ry * 0.5f, rx, ry, rot, look.Shade, 0.30f, 1f);
                        cv.Blob(x, y - ry * 0.3f, rx * 0.9f, ry * 0.7f, rot, Color.white, 0.5f, 1f);
                    }
                    for (int i = 0; i < 14; i++) { float r = cv.R(4, 11); float x = cv.R(0, T), y = cv.R(0, T); cv.Blob(x, y, r, r * 0.7f, cv.R(0, 3f), new Color(0.36f, 0.38f, 0.42f), 0.6f, 0.3f); cv.Blob(x - 1, y - r * 0.4f, r * 0.8f, r * 0.35f, 0f, Color.white, 0.8f, 0.5f); }
                    for (int i = 0; i < 300; i++) cv.Blend((int)cv.R(0, T), (int)cv.R(0, T), look.Shade, cv.R(0.2f, 0.5f));
                    break;
                }
                case "jungle":
                {
                    // 뿌리와 넓은 잎: 굵게 굽는 어두운 뿌리, 둥근 잎 얼룩, 보랏빛 그늘
                    var root = new Color(0.30f, 0.26f, 0.28f); var leafD = new Color(0.30f, 0.42f, 0.30f); var violet = new Color(0.45f, 0.38f, 0.5f);
                    for (int i = 0; i < 30; i++) cv.Blob(cv.R(0, T), cv.R(0, T), cv.R(20, 60), cv.R(14, 36), cv.R(0, 3f), violet, cv.R(0.06f, 0.14f), 1f);
                    for (int i = 0; i < 10; i++) cv.Stroke(cv.R(0, T), cv.R(0, T), cv.R(0, 6.28f), cv.R(80, 200), cv.R(1.4f, 2.6f), cv.R(-0.02f, 0.02f), root, 0.2f, 0.8f, 0.12f);
                    for (int i = 0; i < 170; i++) { float r = cv.R(5, 12); cv.Blob(cv.R(0, T), cv.R(0, T), r, r * cv.R(0.4f, 0.6f), cv.R(0, 3.14f), leafD, cv.R(0.2f, 0.42f), 0.45f); }
                    break;
                }
                case "scorched":
                {
                    // 그을린 땅: 갈라진 금, 금 속에 핏빛, 재 얼룩
                    var ash = new Color(0.24f, 0.2f, 0.2f); var blood = new Color(0.70f, 0.15f, 0.12f);
                    for (int i = 0; i < 40; i++) cv.Blob(cv.R(0, T), cv.R(0, T), cv.R(16, 50), cv.R(10, 30), cv.R(0, 3f), ash, cv.R(0.08f, 0.2f), 1f);
                    for (int i = 0; i < 26; i++)
                    {
                        float x = cv.R(0, T), y = cv.R(0, T), ang = cv.R(0, 6.28f), len = cv.R(60, 200);
                        cv.Stroke(x, y, ang, len, 2.2f, 0f, blood, 0.30f, 0.7f, 0.5f);
                        cv.Stroke(x, y, ang, len, 0.9f, 0f, ink, 0.6f, 0.4f, 0.5f);
                    }
                    for (int i = 0; i < 500; i++) cv.Blend((int)cv.R(0, T), (int)cv.R(0, T), ash, cv.R(0.1f, 0.3f));
                    break;
                }
            }
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, true) { name = "바닥 " + id, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            tex.SetPixels(cv.Px);
            tex.Apply(true, true);
            return tex;
        }

        // ---- 천하도 ----
        // 두 큰 강을 지역 자리에서 잡는다(실제 지리의 큰 줄기: 황하는 곤륜 북쪽에서 화산·개방 총타·숭산 북쪽을 지나 동쪽 바다에,
        // 장강은 당가타(사천)에서 장강 수로를 지나 남궁세가 남쪽으로 바다에). 지역 자리가 바뀌면 강도 따라간다.
        static readonly (string id, float dx, float dy)[] YellowRiver = { ("kunlun", 0.03f, -0.10f), ("huashan", -0.06f, -0.10f), ("huashan", 0.03f, -0.07f), ("gaebang_hq", 0.02f, -0.06f), ("songshan", 0.08f, -0.10f), ("songshan", 0.24f, -0.14f), ("songshan", 0.42f, -0.17f) };
        static readonly (string id, float dx, float dy)[] LongRiver = { ("danggata", -0.10f, 0.02f), ("danggata", 0.03f, 0.05f), ("janggang", -0.10f, 0.03f), ("janggang", 0f, 0f), ("janggang", 0.10f, -0.02f), ("namgung_manor", 0f, 0.07f), ("namgung_manor", 0.16f, 0.04f), ("namgung_manor", 0.40f, 0.02f) };

        /// <summary>지역 자료로 그린 천하도(가로 w px, 세로는 세계 비율). 위가 북.</summary>
        public static Texture2D MakeMap(Regions regions, PropField field, int w = 768)
        {
            float W = regions.WidthMeters, H = regions.HeightMeters;
            int h = Mathf.RoundToInt(w * H / W);
            var px = new Color[w * h];
            var all = regions.All;
            int n = all.Count;
            var cx = new float[n]; var cy = new float[n]; var col = new Color[n];
            for (int i = 0; i < n; i++) { var c = regions.Center(all[i]); cx[i] = c.X; cy[i] = c.Y; col[i] = Of(all[i].Id).Map; }
            var rng = new System.Random(5);
            int ln = 24; var lat = new float[ln * ln]; for (int i = 0; i < lat.Length; i++) lat[i] = (float)rng.NextDouble();
            float Noise(float u, float v)
            {
                float fx = u * (ln - 1), fy = v * (ln - 1); int x0 = Mathf.Clamp((int)fx, 0, ln - 2), y0 = Mathf.Clamp((int)fy, 0, ln - 2);
                float tx = fx - x0, ty = fy - y0;
                return Mathf.Lerp(Mathf.Lerp(lat[y0 * ln + x0], lat[y0 * ln + x0 + 1], tx), Mathf.Lerp(lat[(y0 + 1) * ln + x0], lat[(y0 + 1) * ln + x0 + 1], tx), ty);
            }
            var sea = Hex(0xC4D3D9); var seaDeep = Hex(0xAFC3CC); var paper = Hex(0xE2E1DC); var ink = new Color(0.14f, 0.13f, 0.13f);
            var land = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    land[y * w + x] = field.InLand((x + 0.5f) / w * W, (y + 0.5f) / h * H);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float wx = (x + 0.5f) / w * W, wy = (y + 0.5f) / h * H;
                    float nz = Noise((float)x / w, (float)y / h);
                    Color c;
                    if (!land[y * w + x]) c = Color.Lerp(sea, seaDeep, nz * 0.8f);
                    else
                    {
                        // 지역 빛을 거리의 네제곱 반비례로 섞는다 — 땅마다 제 빛이 고이고 사이는 부드럽게 넘어간다
                        float sum = 0f; Color acc = new Color(0, 0, 0, 0);
                        for (int i = 0; i < n; i++)
                        {
                            float dx = wx - cx[i], dy = wy - cy[i];
                            float d2 = dx * dx + dy * dy + 900f;
                            float wgt = 1f / (d2 * d2);
                            acc += col[i] * wgt; sum += wgt;
                        }
                        c = acc / sum;
                        c = Color.Lerp(c, paper, 0.18f + nz * 0.14f);
                        // 바닷가: 물 옆 칸이면 먹 선
                        bool shore = (x > 0 && !land[y * w + x - 1]) || (x < w - 1 && !land[y * w + x + 1]) || (y > 0 && !land[(y - 1) * w + x]) || (y < h - 1 && !land[(y + 1) * w + x]);
                        if (shore) c = Color.Lerp(c, ink, 0.55f);
                    }
                    c.a = 1f;
                    px[(h - 1 - y) * w + x] = c; // 텍스처는 아래가 0
                }
            void Dot(float fx, float fy, Color c, float a, float r)
            {
                int ir = Mathf.CeilToInt(r);
                for (int yy = -ir; yy <= ir; yy++)
                    for (int xx = -ir; xx <= ir; xx++)
                    {
                        float d = Mathf.Sqrt(xx * xx + yy * yy); if (d > r) continue;
                        int X = Mathf.RoundToInt(fx) + xx, Y = Mathf.RoundToInt(fy) + yy;
                        if (X < 0 || Y < 0 || X >= w || Y >= h || !land[Y * w + X]) continue;
                        int i = (h - 1 - Y) * w + X;
                        px[i] = Color.Lerp(px[i], c, a * Mathf.Clamp01(r - d + 0.5f));
                    }
            }
            // 강: 지역 자리에서 잡은 점들을 부드럽게 이어 물빛 줄로
            var water = Hex(0x7FA6B8);
            void River((string id, float dx, float dy)[] pts, float width)
            {
                var p = new List<Vector2>();
                foreach (var (id, dx, dy) in pts)
                {
                    RegionDef r = null;
                    foreach (var a in all) if (a.Id == id) { r = a; break; }
                    if (r == null) return;
                    p.Add(new Vector2((r.Pos[0] + dx) * w, (r.Pos[1] + dy) * h));
                }
                for (int i = 0; i < p.Count - 1; i++)
                {
                    Vector2 a = p[Mathf.Max(0, i - 1)], b = p[i], c2 = p[i + 1], d = p[Mathf.Min(p.Count - 1, i + 2)];
                    int steps = Mathf.CeilToInt(Vector2.Distance(b, c2) * 1.5f);
                    for (int s = 0; s <= steps; s++)
                    {
                        float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                        var q = 0.5f * ((2f * b) + (-a + c2) * t + (2f * a - 5f * b + 4f * c2 - d) * t2 + (-a + 3f * b - 3f * c2 + d) * t3);
                        float wob = Mathf.Sin((i + t) * 5.3f) * 2.2f;
                        Dot(q.x, q.y + wob, water, 0.85f, width);
                    }
                }
            }
            River(YellowRiver, 1.5f);
            River(LongRiver, 1.8f);
            // 산: 소품 자료의 산 자리마다 먹 봉우리 하나(세 겹 획)
            float sx = w / W, sy = h / H;
            foreach (var p in field.Props)
            {
                if (p.Kind != PropKind.Mount) continue;
                float mx = p.Pos.X * sx, my = p.Pos.Y * sy, s = Mathf.Clamp(p.S * sx * 0.5f, 2.5f, 7f);
                for (float t = -1f; t <= 1f; t += 0.12f)
                {
                    float yy = my - (1f - Mathf.Abs(t)) * s * 1.2f;
                    Dot(mx + t * s, yy, ink, 0.5f, 0.8f);
                    if (t > 0f) Dot(mx + t * s * 0.6f, yy + s * 0.35f, ink, 0.18f, 1.2f); // 볕 안 드는 쪽 그늘
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "천하도(자료로 그림)", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, false);
            return tex;
        }
    }
}
