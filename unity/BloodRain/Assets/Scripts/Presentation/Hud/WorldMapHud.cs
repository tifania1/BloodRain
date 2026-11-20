using BloodRain.Core.Data;
using BloodRain.Core.World;
using BloodRain.Presentation.Fx;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation.Hud
{
    // 천하도(2026-11-17 사람: "중원 전체 맵 적용이 하나도 안 돼 있다" — 그린 수묵 지도가 낮은 경지에선 바닥 밑에 깔려 한 번도 안 보였다).
    //   ① 구석 지도: 오른쪽 위에 중원 한 장과 내 자리(붉은 점). 누르거나 M(패드 Select)이면 큰 지도.
    //   ② 큰 지도: 세상을 멈추고 지도 한 장, 땅 이름·한자·혈운 농도, 내 자리·태어난 곳·혈우 장막. M·Esc·누름으로 닫는다.
    //   ③ 한 생 첫머리(WorldIntro)에는 태어난 땅 이름을 크게 띄운다.
    // 그림은 MapLayer.MapTexture(지역 자료로 그린 천하도) 그대로, 땅 자리는 cheonha.json regions.pos. 규칙은 건드리지 않는다(보기만).
    // LifeRunner 가 붙인다(메뉴 없음). 글은 IMGUI — 다른 디버그 줄처럼 ImguiFont 로 한글을 쓴다.
    public sealed class WorldMapHud : MonoBehaviour
    {
        public LifeRunner runner;

        static readonly Color Ink = new Color(0.10f, 0.09f, 0.08f, 1f);
        static readonly Color Paper = new Color(0.95f, 0.91f, 0.82f, 1f);
        static readonly Color Crimson = new Color32(0xB3, 0x26, 0x1E, 255);
        static readonly Color GoldInk = new Color32(0x8A, 0x63, 0x14, 255);

        Texture2D _map, _white, _dot, _ring;
        MapLayer _mapLayer;
        bool _open, _lookedForMap;
        float _prevScale = 1f;
        int _openedFrame = -1;
        GUIStyle _label;
        readonly System.Collections.Generic.List<Rect> _placed = new System.Collections.Generic.List<Rect>();
        readonly System.Collections.Generic.List<RegionDef> _order = new System.Collections.Generic.List<RegionDef>();

        public bool IsOpen => _open;
        /// <summary>큰 지도에서 땅을 눌렀을 때(시험 메뉴가 그 문파 몸으로 새 생을 연다). 비어 있으면 누름은 닫기.</summary>
        public System.Action<RegionDef> RegionClicked;

        void Awake()
        {
            _white = Solid();
            _dot = Disc(32, false);
            _ring = Disc(64, true);
        }

        void OnDestroy()
        {
            if (_open) Time.timeScale = _prevScale <= 0f ? 1f : _prevScale;
            foreach (var t in new[] { _white, _dot, _ring }) if (t != null) Destroy(t);
        }

        Texture2D Map
        {
            get
            {
                if (_map == null)
                {
                    // MapLayer 가 Start 에서 지역 자료로 천하도를 그린다. 아직이면 다음 프레임에 다시 본다
                    if (_mapLayer == null && !_lookedForMap) { _lookedForMap = true; _mapLayer = FindFirstObjectByType<MapLayer>(); }
                    if (_mapLayer != null) _map = _mapLayer.MapTexture;
                }
                return _map;
            }
        }

        void Update()
        {
            if (runner == null || runner.Life == null || Map == null) return;
            var kb = Keyboard.current; var pad = Gamepad.current;
            bool toggle = (kb != null && kb.mKey.wasPressedThisFrame && !DebugOverlay.On) || (pad != null && pad.selectButton.wasPressedThisFrame);
            if (_open)
            {
                if (Time.timeScale != 0f) Time.timeScale = 0f;
                bool esc = kb != null && kb.escapeKey.wasPressedThisFrame;
                if ((toggle || esc) && Time.frameCount != _openedFrame) Close();
            }
            else if (toggle) Open();
            // 웹 빌드에서는 IMGUI 가 마우스 누름을 못 받아(2026-11-17 dev 판에서 확인) Input System 포인터로 직접 본다
            var ptr = Pointer.current;
            if (ptr != null && ptr.press.wasPressedThisFrame)
            {
                var pp = ptr.position.ReadValue();
                Press(new Vector2(pp.x, Screen.height - pp.y));
            }
        }

        Rect _miniRect;
        readonly System.Collections.Generic.List<(Rect r, RegionDef reg)> _regionHits = new System.Collections.Generic.List<(Rect, RegionDef)>();

        /// <summary>GUI 좌표(왼쪽 위가 0,0)를 눌렀다. 구석 지도면 큰 지도를 열고, 큰 지도에선 땅(시험)이나 닫기.</summary>
        public void Press(Vector2 gui)
        {
            if (_open)
            {
                if (Time.frameCount == _openedFrame) return;
                RegionDef hit = null;
                foreach (var (r, reg) in _regionHits) if (r.Contains(gui)) { hit = reg; break; }
                Close();
                if (hit != null && RegionClicked != null) RegionClicked(hit);
            }
            else if (_miniRect.width > 0f && _miniRect.Contains(gui) && Time.timeScale > 0f) Open();
        }

        public void Open()
        {
            if (_open || runner == null || runner.Life == null || Map == null) return;
            if (runner.Life.Record != null || Time.timeScale <= 0f || WorldIntro.Playing) return; // 다른 화면이 세상을 멈췄으면 안 연다
            _prevScale = BloodRain.Presentation.Fx.HitFeel.ScaleToRestore(); // 타격 멈춤(0.02)을 기억하지 않게(2026-11-20)
            Time.timeScale = 0f;
            _open = true;
            _openedFrame = Time.frameCount;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            Time.timeScale = _prevScale <= 0f ? 1f : _prevScale;
        }

        // ── 그리기 ─────────────────────────────────────────────
        void OnGUI()
        {
            if (runner == null || runner.Life == null || Map == null) return;
            if (Event.current.type != EventType.Repaint) return;
            ImguiFont.Use();
            if (_label == null) _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false };
            float k = Screen.height / 720f;
            var life = runner.Life;
            _miniRect = default;
            if (WorldIntro.Playing) { DrawIntroTitle(life, k); return; }
            if (life.Record != null) return;
            if (_open) DrawBig(life, k);
            else if (Time.timeScale > 0f) DrawMini(life, k);
        }

        Vector2 PlayerUv(BloodRain.Core.Run.Life life)
        {
            var regions = life.Regions;
            var w = runner.player != null ? WorldCoords.FromUnity(runner.player.position) : life.Position;
            return new Vector2(Mathf.Clamp01(w.X / regions.WidthMeters), Mathf.Clamp01(w.Y / regions.HeightMeters));
        }

        void DrawMini(BloodRain.Core.Run.Life life, float k)
        {
            float mw = 168f * k, mh = mw * Map.height / Map.width;
            var r = new Rect(Screen.width - mw - 14f * k, 86f * k, mw, mh);
            Fill(new Rect(r.x - 2f * k, r.y - 2f * k, r.width + 4f * k, r.height + 4f * k), new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            GUI.color = new Color(1f, 1f, 1f, 0.92f);
            GUI.DrawTexture(r, Map, ScaleMode.StretchToFill);
            foreach (var lr in life.Bosses.Awake) // EB-01a 깨어난 주인 자리
                Blit(_dot, r.x + lr.Pos[0] * r.width, r.y + lr.Pos[1] * r.height, 5f * k, new Color(Crimson.r, Crimson.g, Crimson.b, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f)));
            var uv = PlayerUv(life);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            Blit(_ring, r.x + uv.x * r.width, r.y + uv.y * r.height, (13f + 5f * pulse) * k, new Color(Crimson.r, Crimson.g, Crimson.b, 0.9f - 0.4f * pulse));
            Blit(_dot, r.x + uv.x * r.width, r.y + uv.y * r.height, 6f * k, Crimson);
            _label.fontSize = Mathf.RoundToInt(12f * k); _label.fontStyle = FontStyle.Normal; _label.alignment = TextAnchor.UpperRight;
            Outlined(new Rect(r.x, r.yMax + 2f * k, r.width, 20f * k), Loc.T("ui.map.hint"), new Color(Ink.r, Ink.g, Ink.b, 0.8f), new Color(Paper.r, Paper.g, Paper.b, 0.7f), k);
            GUI.color = Color.white;
            _miniRect = r;
        }

        void DrawBig(BloodRain.Core.Run.Life life, float k)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.07f, 0.06f, 0.05f, 0.82f));
            float aspect = (float)Map.width / Map.height;
            float w = Mathf.Min(Screen.width * 0.9f, Screen.height * 0.8f * aspect), h = w / aspect;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f + 10f * k, w, h);
            Fill(new Rect(r.x - 5f * k, r.y - 5f * k, r.width + 10f * k, r.height + 10f * k), Paper);
            Fill(new Rect(r.x - 2f * k, r.y - 2f * k, r.width + 4f * k, r.height + 4f * k), Ink);
            GUI.color = Color.white;
            GUI.DrawTexture(r, Map, ScaleMode.StretchToFill);

            var regions = life.Regions;
            var here = regions.Region(life.RegionId);
            _regionHits.Clear();
            // 땅 이름이 서로 겹치지 않게: 점 아래 → 위 → 오른쪽 → 왼쪽 가운데 먼저 비는 자리에 둔다(지금 선 땅부터)
            _placed.Clear();
            _order.Clear();
            foreach (var reg in regions.Visible(life.SealStones)) { if (reg.Id == here.Id) _order.Insert(0, reg); else _order.Add(reg); }
            float lw = 190f * k, lh = 44f * k;
            foreach (var reg in _order)
            {
                float x = r.x + reg.Pos[0] * r.width, y = r.y + reg.Pos[1] * r.height;
                bool cur = reg.Id == here.Id, born = reg.Id == life.Body.Region.Id;
                Rect box = default;
                for (int tryAt = 0; tryAt < 5; tryAt++)
                {
                    box = tryAt == 0 ? new Rect(x - lw / 2f, y + 9f * k, lw, lh)
                        : tryAt == 1 ? new Rect(x - lw / 2f, y - 9f * k - lh, lw, lh)
                        : tryAt == 2 ? new Rect(x + 12f * k, y - lh / 2f, lw, lh)
                        : tryAt == 3 ? new Rect(x - 12f * k - lw, y - lh / 2f, lw, lh)
                        : new Rect(x - lw / 2f, y + 9f * k + lh, lw, lh);
                    bool hit = false;
                    foreach (var o in _placed) if (o.Overlaps(box)) { hit = true; break; }
                    if (!hit) break;
                }
                _placed.Add(box);
                bool lord = reg.Boss != null && System.Linq.Enumerable.Contains(life.Bosses.BossRegions, reg) && life.Bosses.State(reg.Id) == BloodRain.Core.World.BossRegionState.Awake;
                if (lord) // EB-01a 깨어난 땅의 주인: 붉게 뛰는 고리
                {
                    float lp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                    Blit(_ring, x, y, (26f + 10f * lp) * k, new Color(Crimson.r, Crimson.g, Crimson.b, 0.95f - 0.5f * lp));
                }
                Blit(_dot, x, y, (cur ? 13f : 9f) * k, lord ? Crimson : new Color(Paper.r, Paper.g, Paper.b, 0.95f));
                Blit(_ring, x, y, (cur ? 17f : 12f) * k, cur || lord ? Crimson : Ink);
                _label.alignment = TextAnchor.UpperCenter;
                _label.fontSize = Mathf.RoundToInt((cur ? 18f : 16f) * k); _label.fontStyle = FontStyle.Bold;
                string han = Loc.Hanja(reg.Hanja);
                OutlinedPair(new Rect(box.x, box.y, box.width, 26f * k), Loc.Region(reg), han, cur ? Crimson : Ink, Paper, k);
                _label.fontSize = Mathf.RoundToInt(12f * k); _label.fontStyle = FontStyle.Normal;
                string sub = Loc.F("ui.density", reg.Density.ToString("0.0")) + (born ? "  ·  " + Loc.T("ui.map.born") : "") + (lord ? "  ·  " + Loc.T("ui.map.lord_awake") : "");
                Outlined(new Rect(box.x, box.y + 22f * k, box.width, 20f * k), sub, lord ? Crimson : born ? GoldInk : new Color(Ink.r, Ink.g, Ink.b, 0.85f), Paper, k);
                _regionHits.Add((box, reg));
                _regionHits.Add((new Rect(x - 14f * k, y - 14f * k, 28f * k, 28f * k), reg));
            }

            // 혈우 장막(이 경지에 갈 수 있는 끝)과 내 자리
            var ws = WorldScale.Current;
            var origin = WorldCoords.Origin;
            float ox = r.x + origin.X / regions.WidthMeters * r.width, oy = r.y + origin.Y / regions.HeightMeters * r.height;
            var br = ws != null ? ws.Scale.BoundaryRadius(ws.Realm) : null;
            if (br.HasValue)
                Blit(_ring, ox, oy, Mathf.Max(22f * k, br.Value * 2f / regions.WidthMeters * r.width), new Color(Crimson.r, Crimson.g, Crimson.b, 0.75f));
            var uv = PlayerUv(life);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
            float px = r.x + uv.x * r.width, py = r.y + uv.y * r.height;
            Blit(_ring, px, py, (20f + 10f * pulse) * k, new Color(Crimson.r, Crimson.g, Crimson.b, 0.9f - 0.5f * pulse));
            Blit(_dot, px, py, 9f * k, Crimson);

            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontSize = Mathf.RoundToInt(26f * k); _label.fontStyle = FontStyle.Bold;
            Outlined(new Rect(0, r.y - 46f * k, Screen.width, 36f * k), Loc.T("ui.map.title"), Paper, Ink, k);
            _label.fontSize = Mathf.RoundToInt(14f * k); _label.fontStyle = FontStyle.Normal;
            string foot = Loc.T("ui.map.foot");
            if (RegionClicked != null) foot += "  ·  " + "(시험) 땅을 누르면 그 문파 몸으로 새 생";
            Outlined(new Rect(0, r.yMax + 8f * k, Screen.width, 24f * k), foot, Paper, Ink, k);

        }

        void DrawIntroTitle(BloodRain.Core.Run.Life life, float k)
        {
            float p = WorldIntro.Progress;
            float a = Mathf.Clamp01(p / 0.08f) * (1f - Mathf.Clamp01((p - 0.62f) / 0.25f));
            if (a <= 0.01f) return;
            var reg = life.Body.Region;
            float cy = Screen.height * 0.22f;   // 2026-11-21 0.3 이면 아래 줄(어느 문파 제자)이 태어난 자리 고리·머리 위 기혈 바에 걸렸다
            var ink = new Color(Ink.r, Ink.g, Ink.b, a); var paper = new Color(Paper.r, Paper.g, Paper.b, a * 0.9f);
            _label.alignment = TextAnchor.MiddleCenter; _label.fontStyle = FontStyle.Bold;
            string han = Loc.Hanja(reg.Hanja);
            if (!string.IsNullOrEmpty(han))
            {
                _label.fontSize = Mathf.RoundToInt(66f * k);
                if (ImguiFont.Lacks(han)) _label.font = ImguiFont.Hanja; // 丐 같은 KR 글꼴에 없는 한자
                Outlined(new Rect(0, cy - 60f * k, Screen.width, 84f * k), han, ink, paper, k * 1.6f);
                _label.font = null;
            }
            _label.fontSize = Mathf.RoundToInt(26f * k);
            Outlined(new Rect(0, cy + 28f * k, Screen.width, 36f * k), Loc.Region(reg), ink, paper, k);
            _label.fontSize = Mathf.RoundToInt(17f * k); _label.fontStyle = FontStyle.Normal;
            string who = life.Body.Sect != null ? Loc.Disciple(life.Body.Sect) : Loc.Character(life.Body.Character);
            Outlined(new Rect(0, cy + 64f * k, Screen.width, 28f * k), who, new Color(Crimson.r, Crimson.g, Crimson.b, a), paper, k);
        }

        // ── 도구 ─────────────────────────────────────────────
        void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = Color.white; }

        void Blit(Texture2D t, float cx, float cy, float size, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(cx - size / 2f, cy - size / 2f, size, size), t);
            GUI.color = Color.white;
        }

        /// <summary>'이름 한자'를 가운데 맞춰 한 줄로. 한자에 KR 글꼴에 없는 글자가 있으면 한자만 한자 글꼴(ImguiFont.Hanja)로 따로 그린다.</summary>
        void OutlinedPair(Rect r, string ko, string han, Color fg, Color bg, float k)
        {
            var hf = ImguiFont.Hanja;
            if (string.IsNullOrEmpty(han) || hf == null || !ImguiFont.Lacks(han))
            {
                Outlined(r, string.IsNullOrEmpty(han) ? ko : ko + " " + han, fg, bg, k);
                return;
            }
            var align = _label.alignment;
            float wKo = _label.CalcSize(new GUIContent(ko + " ")).x;
            _label.font = hf;
            float wHan = _label.CalcSize(new GUIContent(han)).x;
            _label.font = null;
            float x0 = r.center.x - (wKo + wHan) * 0.5f;
            _label.alignment = TextAnchor.UpperLeft;
            Outlined(new Rect(x0, r.y, wKo + 4f, r.height), ko + " ", fg, bg, k);
            _label.font = hf;
            Outlined(new Rect(x0 + wKo, r.y, wHan + 4f, r.height), han, fg, bg, k);
            _label.font = null;
            _label.alignment = align;
        }

        /// <summary>테두리 두른 글: 바탕색으로 여덟 방향에 한 번씩, 그 위에 글자색.</summary>
        void Outlined(Rect r, string text, Color fg, Color bg, float k)
        {
            float d = Mathf.Max(1f, 1.4f * k);
            _label.normal.textColor = bg;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                GUI.Label(new Rect(r.x + Mathf.Cos(ang) * d, r.y + Mathf.Sin(ang) * d, r.width, r.height), text, _label);
            }
            _label.normal.textColor = fg;
            GUI.Label(r, text, _label);
        }

        static Texture2D Solid()
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, Color.white); t.Apply();
            return t;
        }

        /// <summary>흰 원(ring 이면 고리). 가장자리는 한 픽셀 부드럽게.</summary>
        static Texture2D Disc(int n, bool ring)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            float c = (n - 1) / 2f, rad = n / 2f - 1f, thick = Mathf.Max(2f, n * 0.09f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(rad - d + 0.5f);
                    if (ring) a *= Mathf.Clamp01(d - (rad - thick) + 0.5f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }
    }
}
