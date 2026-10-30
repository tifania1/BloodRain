using BloodRain.Presentation.Fx;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // 덩이 렌더러 시험 씬(TU-04). 경지 하나를 골라 그 경지에서 생긴 덩이를 화면 안에 뿌린다.
    //   1~7  경지(덩이 크기 1·4·9·16·28·44·64)   H  덩이 30개에 기혈 -20%   G  자동 피해 켜고 끄기
    //   ↑/↓  덩이 수 ±100   C  화면 밖 거르기 켜고 끄기
    // 기본은 현경 덩이 600개(64명씩, 38,400명). 위에 평균 fps 를 찍는다(에디터 플레이라 빌드보다 낮게 나온다).
    public sealed class ClusterTestDriver : MonoBehaviour
    {
        public static ClusterTestDriver Instance { get; private set; }

        public int realm = 6;
        public int clusters = 600;
        public bool autoDamage = true;
        public int seed = 5;

        WorldScale _ws;
        ClusterRenderer _r;
        System.Random _rng;
        float _hitTimer;
        int _frames;
        float _time;
        float _ema = 1f / 60f;

        public ClusterRenderer Renderer => _r;
        public float AvgFps => _time > 0 ? _frames / _time : 0f;
        public void ResetStats() { _frames = 0; _time = 0f; }

        void Awake()
        {
            Instance = this;
            _ws = new WorldScale(DataBoot.Data);
            WorldScale.Current = _ws;
            _r = GetComponent<ClusterRenderer>();
            if (_r.atlas == null) _r.atlas = ClusterRenderer.MakePlaceholderAtlas();
            _r.World = _ws;
            Respawn();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (WorldScale.Current == _ws) WorldScale.Current = null;
        }

        public void Respawn()
        {
            realm = Mathf.Clamp(realm, 0, _ws.Scale.TopRealm);
            _ws.SetLevel(_ws.Scale.Realms[realm].Level, snap: true);
            _rng = new System.Random(seed);
            _r.Clear();
            float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
            float h = WorldScale.OrthoSize(_ws.Scale.ViewDiagonalAtRealm(realm), aspect) * 0.92f, w = h * aspect;
            for (int i = 0; i < clusters; i++)
            {
                _r.Add(new ClusterView
                {
                    Position = new Vector2((float)(_rng.NextDouble() * 2 - 1) * w, (float)(_rng.NextDouble() * 2 - 1) * h),
                    BornRealm = realm,
                    Seed = _rng.Next(),
                    Health = 1f,
                });
            }
            ResetStats();
        }

        /// <summary>덩이 count 개에 기혈 -amount. 0 이 된 덩이는 디졸브가 끝난 뒤 다시 채운다.</summary>
        public void Hit(int count, float amount)
        {
            var list = _r.Clusters;
            for (int k = 0; k < count && list.Count > 0; k++)
            {
                var c = list[_rng.Next(list.Count)];
                c.Health = c.Health <= 0f ? 1f : Mathf.Max(0f, c.Health - amount);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7 };
                for (int i = 0; i < digits.Length; i++)
                    if (kb[digits[i]].wasPressedThisFrame) { realm = i; Respawn(); }
                if (kb.hKey.wasPressedThisFrame) Hit(30, 0.2f);
                if (kb.gKey.wasPressedThisFrame) autoDamage = !autoDamage;
                if (kb.cKey.wasPressedThisFrame) { _r.cull = !_r.cull; ResetStats(); }
                if (kb.upArrowKey.wasPressedThisFrame) { clusters += 100; Respawn(); }
                if (kb.downArrowKey.wasPressedThisFrame) { clusters = Mathf.Max(100, clusters - 100); Respawn(); }
            }

            if (autoDamage)
            {
                _hitTimer += Time.deltaTime;
                if (_hitTimer >= 0.25f) { _hitTimer = 0f; Hit(Mathf.Max(1, clusters / 40), (float)(0.05 + _rng.NextDouble() * 0.3)); }
            }

            float dt = Time.unscaledDeltaTime;
            _ema += (dt - _ema) * 0.05f;
            _frames++;
            _time += dt;
        }

        void OnGUI()
        {
            GUI.color = new Color(0.15f, 0.1f, 0.08f);
            GUI.Label(new Rect(12, 8, 1200, 24),
                $"{_ws.RealmName} 덩이 {clusters}개 × {_ws.Scale.ClusterSize(realm)}명  그린 사람 {_r.LastInstances}  호출 {_r.LastDrawCalls}  " +
                $"fps {1f / Mathf.Max(0.0001f, _ema):0} (평균 {AvgFps:0})  거르기 {(_r.cull ? "켬" : "끔")}  자동 피해 {(autoDamage ? "켬" : "끔")}   [1~7 경지  H 피해  G 자동  ↑↓ 수  C 거르기]");
        }
    }
}
