using System.Collections.Generic;
using BloodRain.Presentation.Fx;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // 축척 시험 씬(TU-01). 경지를 키 하나로 올려 삼류~현경 일곱 화면을 본다.
    //   B  다음 경지로 돌파 연출(TU-02)   R  다음 경지(바로)   F  앞 경지   L / K  레벨 +1 / -1   1~7  그 경지로 바로   WASD  이동(경지 m/s 를 세계 m/s 로)
    // 그림은 임시 도형이다. 경지마다 색이 다른 허수아비를 그 경지 첫 화면 안에 뿌려 두어,
    // 경지가 오르면 아래 경지 것들이 작아져 바닥 무늬처럼 되는지 본다. 붉은 원은 그 경지 혈우 장막 반경(씬에 BloodCurtain 이 있으면 그 셰이더로 그린다).
    public sealed class ScaleTestDriver : MonoBehaviour
    {
        public static ScaleTestDriver Instance { get; private set; }

        [SerializeField] int dummiesPerRealm = 40;
        [SerializeField] float playerRealmMeters = 0.8f;
        [SerializeField] float dummyRealmMeters = 1.0f;
        [SerializeField] float weaponRangeRealmMeters = 3f;
        [SerializeField] float moveRealmMetersPerSecond = 5f;
        [SerializeField] int seed = 7;

        static readonly Color[] RealmColors =
        {
            new Color(0.25f, 0.25f, 0.25f), new Color(0.20f, 0.35f, 0.55f), new Color(0.20f, 0.50f, 0.35f),
            new Color(0.60f, 0.45f, 0.15f), new Color(0.55f, 0.25f, 0.55f), new Color(0.70f, 0.30f, 0.15f),
            new Color(0.10f, 0.10f, 0.10f),
        };

        WorldScale _ws;
        Transform _player;
        LineRenderer _range, _boundary;
        Material _mat;
        Sprite _circle;
        Vector2 _birth;
        BreakthroughFx _fx;
        BloodCurtain _curtain;

        public BreakthroughFx Fx => _fx;
        public Transform Player => _player;
        public WorldScale World => _ws;
        public Sprite Circle => _circle;
        public Material Mat => _mat;
        /// <summary>플레이어 반지름(세계 m).</summary>
        public float PlayerRadius => _ws.Length(playerRealmMeters) * 0.5f;

        void Awake()
        {
            Instance = this;
            _ws = new WorldScale(DataBoot.Data);
            _ws.SetLevel(1, snap: true);
            WorldScale.Current = _ws;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            _mat = new Material(shader);
            _circle = MakeCircle();

            var rng = new System.Random(seed);
            for (int r = 0; r < _ws.Scale.Realms.Count; r++)
            {
                float spread = _ws.Scale.ViewDiagonalAtRealm(r) * 0.6f;
                float size = _ws.Length(dummyRealmMeters, r);
                var parent = new GameObject($"허수아비 {_ws.Scale.Realms[r].NameKo}").transform;
                for (int i = 0; i < dummiesPerRealm; i++)
                {
                    var pos = new Vector2((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1)) * spread;
                    var d = MakeDisc($"{i}", RealmColors[r % RealmColors.Length], size, -r * 0.01f);
                    d.SetParent(parent, false);
                    d.localPosition = new Vector3(pos.x, pos.y, 0f);
                }
            }

            _player = MakeDisc("플레이어", new Color(0.70f, 0.15f, 0.12f), 1f, -1f);
            _range = MakeRing("무공 사거리", new Color(0.70f, 0.15f, 0.12f, 0.8f));
            _range.transform.SetParent(_player, false);
            _boundary = MakeRing("혈우 장막", new Color(0.70f, 0.15f, 0.12f, 0.9f));
            _birth = Vector2.zero;

            _fx = gameObject.AddComponent<BreakthroughFx>();
            _fx.RealmFactorChanged += f => Debug.Log($"[돌파] 공간 해시 격자 다시 짓기 자리: 배율 ×{f:0.##}");
            _fx.Swept += bt => Debug.Log($"[돌파] 쓸림 {bt.Elapsed:0.00}초, 반경 {bt.SweepRadius:0.0}m");
            _fx.Ended += bt => Debug.Log($"[돌파] 끝 {bt.Elapsed:0.00}초 → {_ws.RealmName}. 깨달음 3택1 자리");

            // TU-05 혈우 장막이 씬에 있으면 붉은 원 대신 그것으로 본다
            _curtain = GetComponentInChildren<BloodCurtain>();
            if (_curtain != null) { _curtain.birthplace = _birth; _curtain.breakthrough = _fx; }

            var cam = Camera.main != null ? Camera.main.GetComponent<ScaleCamera>() : null;
            if (cam != null) cam.target = _player;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (WorldScale.Current == _ws) WorldScale.Current = null;
        }

        /// <summary>그 경지 첫 레벨로. snap 이면 보간 없이 바로 그 화면.</summary>
        /// <summary>다음 경지로 돌파 연출. 이미 연출 중이거나 현경이면 무시.</summary>
        public bool BreakToNext(bool revisit = false)
        {
            int next = _ws.Realm + 1;
            if (next > _ws.Scale.TopRealm) return false;
            return _fx.Play(_ws, _ws.Level, _ws.Scale.Realms[next].Level, _player, revisit);
        }

        public void JumpToRealm(int realm, bool snap = false)
        {
            if (_fx != null && _fx.IsPlaying) return;
            realm = Mathf.Clamp(realm, 0, _ws.Scale.TopRealm);
            _ws.SetLevel(_ws.Scale.Realms[realm].Level, snap);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.bKey.wasPressedThisFrame) BreakToNext();
                if (kb.rKey.wasPressedThisFrame) JumpToRealm(_ws.Realm + 1);
                if (kb.fKey.wasPressedThisFrame) JumpToRealm(_ws.Realm - 1);
                if (kb.lKey.wasPressedThisFrame) _ws.SetLevel(_ws.Level + 1);
                if (kb.kKey.wasPressedThisFrame) _ws.SetLevel(_ws.Level - 1);
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7 };
                for (int i = 0; i < digits.Length; i++)
                    if (kb[digits[i]].wasPressedThisFrame) JumpToRealm(i);

                var move = Vector2.zero;
                if (kb.wKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed) move.y -= 1;
                if (kb.aKey.isPressed) move.x -= 1;
                if (kb.dKey.isPressed) move.x += 1;
                if (move != Vector2.zero)
                {
                    var p = (Vector2)_player.position + move.normalized * _ws.Speed(moveRealmMetersPerSecond) * Time.deltaTime;
                    var kept = _ws.Scale.KeepInside(new System.Numerics.Vector2(p.x, p.y),
                        new System.Numerics.Vector2(_birth.x, _birth.y), _ws.Realm);
                    _player.position = new Vector3(kept.X, kept.Y, _player.position.z);
                }
            }

            float playerSize = _ws.Length(playerRealmMeters);
            _player.localScale = Vector3.one * playerSize;
            float line = _ws.ViewDiagonal * 0.0015f;
            // 사거리 고리는 플레이어 자식이라 반경만 플레이어 크기로 나눠 둔다(선 굵기는 스케일을 안 받는다)
            SetRing(_range, Vector2.zero, _ws.Length(weaponRangeRealmMeters) / playerSize, line);
            var br = _ws.Scale.BoundaryRadius(_ws.Realm);
            if (_fx.IsPlaying)
            {
                // 장막 반경은 줌과 같은 진행값으로 밀려난다(빗줄기 갈라짐은 BloodCurtain)
                var bt = _fx.Core;
                br = bt.ToBoundaryRadius.HasValue && bt.FromBoundaryRadius.HasValue
                    ? Mathf.Lerp(bt.FromBoundaryRadius.Value, bt.ToBoundaryRadius.Value, bt.ZoomProgress)
                    : bt.ToBoundaryRadius;
            }
            _boundary.enabled = br.HasValue && _curtain == null;
            if (br.HasValue) SetRing(_boundary, _birth, br.Value, line * 2f);
        }

        void OnGUI()
        {
            GUI.color = new Color(0.15f, 0.1f, 0.08f);
            GUI.Label(new Rect(12, 8, 900, 24),
                $"{_ws.RealmName}  레벨 {_ws.Level:0}  화면 대각선 {_ws.ViewDiagonal:0.0}m  배율 ×{_ws.RealmFactor:0.##}   [B 돌파  R/F 경지  L/K 레벨  1~7  WASD]");
        }

        public Transform MakeDisc(string name, Color color, float size, float z)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _circle;
            sr.color = color;
            sr.sharedMaterial = _mat;
            go.transform.localScale = Vector3.one * size;
            go.transform.position = new Vector3(0, 0, z);
            return go.transform;
        }

        LineRenderer MakeRing(string name, Color color)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 96;
            lr.sharedMaterial = _mat;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = 10;
            return lr;
        }

        static void SetRing(LineRenderer lr, Vector2 center, float radius, float width)
        {
            if (lr.transform.parent == null) lr.transform.position = new Vector3(center.x, center.y, -2f);
            lr.startWidth = lr.endWidth = width;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }

        static Sprite MakeCircle()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                byte a = (byte)(Mathf.Clamp01(c - d + 0.5f) * 255);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
