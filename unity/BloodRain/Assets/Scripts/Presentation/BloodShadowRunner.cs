using BloodRain.Core.Data;
using BloodRain.Core.Run;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 혈마의 그림자(C-12, 기획서 7장 '생이 끝나는 세 가지'). 현경 30분 뒤 봉인석이 모자라면 나오는, 죽지 않고 밀리지 않는 그림자 하나.
    //   단계는 Life.ShadowStep(UseLife 로 붙이면 매 프레임 읽음) 또는 step 필드(시험용). 세기는 Core BloodShadow.Strength.
    //   걸음 = MoveSpeed / 100 × 기본 걸음(balance stats.moveBase) → WorldScale.Speed. 속도 상한 없음(데이터 maxSpeed 0).
    //   닿아 있으면 HitIntervalSeconds 마다 ContactDamage 한 번(PlayerVitals.Hit — 경공 무적이면 지나감).
    //   단계가 오르면(BloodShadow.TeleportsOn) TeleportWarningSeconds 동안 플레이어 몸 뒤 TeleportGap(현경 축척 m) 자리에
    //   핏자국 예고를 번지게 한 뒤 그 자리로 옮긴다. 이미 그 거리보다 가까우면 옮기지 않는다.
//   거리는 지금 경지 축척으로 잰다 — 그림자는 현경에서만 나오니 실제 판에선 현경 축척과 같고, 낮은 경지 시험 씬에서도 화면 안에 내려앉는다.
    //   화면 위 적 피해 배율 EnemyDamageMultiplier 는 EnemyField.damageMultiplier 에 넣는다.
    //   무공은 그림자를 맞히지 못한다(EnemyField 밖에 있으니 저절로). 그림은 임시 검붉은 원.
    public sealed class BloodShadowRunner : MonoBehaviour
    {
        public Transform player;
        public EnemyField enemies;
        [Tooltip("Life 가 없을 때 쓰는 단계(0이면 없음). 시험 메뉴가 올린다.")]
        public int step;
        [Tooltip("그림자 몸 지름(경지 m).")]
        public float bodyRealmMeters = 1.1f;
        public bool showStats = true;

        public int Step => _life != null ? _life.ShadowStep : step;
        public BloodShadowStrength Strength => BloodShadow.Strength(_def, Step);
        public Vector2 Position => _pos;
        public bool Warning => _warnLeft > 0f;
        public int Teleports { get; private set; }
        public int HitsDealt { get; private set; }

        Life _life;
        BloodShadowDef _def;
        float _moveBase;
        float _playerBody;
        PlayerVitals _vitals;
        PlayerController _move;
        Vector2 _pos;
        int _lastStep;
        float _hitCool;
        float _warnLeft;
        Vector2 _warnAt;
        SpriteRenderer _body, _warn;
        static Sprite _dot;

        public void UseLife(Life life) => _life = life;

        void Awake()
        {
            var data = DataBoot.Data;
            _def = data.Cheonha.LifeEnd.Shadow;
            _moveBase = data.Balance.Stats.MoveBase;
            _playerBody = data.Balance.Stats.PlayerBodyRadius * 2f;
            if (player == null) { var pc = FindFirstObjectByType<PlayerController>(); if (pc != null) player = pc.transform; }
            if (enemies == null) enemies = FindFirstObjectByType<EnemyField>();
            if (player != null) { _vitals = player.GetComponent<PlayerVitals>(); _move = player.GetComponent<PlayerController>(); }
            _body = MakeView("혈마의 그림자", new Color(0.22f, 0.02f, 0.06f, 0.92f), 8);
            _warn = MakeView("그림자 예고", new Color(0.55f, 0.04f, 0.06f, 0.6f), 3);
            _body.enabled = _warn.enabled = false;
        }

        SpriteRenderer MakeView(string name, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Dot();
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }

        static Sprite Dot()
        {
            if (_dot != null) return _dot;
            const int N = 48;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "그림자 점" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    byte al = (byte)(Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 5f) * 255);
                    px[y * N + x] = new Color32(255, 255, 255, al);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
            return _dot;
        }

        /// <summary>그림자가 처음 나타날 자리: 플레이어 걸음 반대쪽, 화면 대각선 절반쯤 밖.</summary>
        Vector2 SpawnPoint(Vector2 p, WorldScale ws)
        {
            Vector2 back = _move != null && _move.Facing.sqrMagnitude > 0.01f ? -_move.Facing.normalized : Vector2.down;
            return p + back * (ws.ViewDiagonal * 0.55f);
        }

        void Update()
        {
            var ws = WorldScale.Current;
            if (ws == null || player == null) return;
            float dt = Time.deltaTime;
            Vector2 p = player.position;
            int s = Step;
            var st = BloodShadow.Strength(_def, s);
            if (enemies != null) enemies.damageMultiplier = st.EnemyDamageMultiplier;

            if (!st.Active)
            {
                _body.enabled = _warn.enabled = false;
                _lastStep = 0;
                _warnLeft = 0f;
                return;
            }
            if (_lastStep == 0) _pos = SpawnPoint(p, ws);
            else if (BloodShadow.TeleportsOn(_def, _lastStep, s)) BeginTeleport(p, ws);
            _lastStep = s;

            // 순간이동 예고: 그동안 그림자는 제자리에서 기다리고, 끝나면 예고 자리로(이미 더 가까우면 그대로).
            if (_warnLeft > 0f)
            {
                _warnLeft -= dt;
                if (_warnLeft <= 0f)
                {
                    float gap = ws.Length(_def.TeleportGap);
                    if ((_pos - p).sqrMagnitude > gap * gap) { _pos = _warnAt; Teleports++; }
                    _warnLeft = 0f;
                }
            }
            else
            {
                float speed = ws.Speed(_moveBase * st.MoveSpeed / 100f);
                _pos = Vector2.MoveTowards(_pos, p, speed * dt); // 밀리지 않으니 넉백·밀어내기 없음
            }

            // 접촉 피해
            float rBody = ws.Length(bodyRealmMeters) * 0.5f;
            float rPlayer = ws.Length(_playerBody) * 0.5f;
            _hitCool = Mathf.Max(0f, _hitCool - dt);
            if (_warnLeft <= 0f && _hitCool <= 0f && _vitals != null && (_pos - p).sqrMagnitude <= (rBody + rPlayer) * (rBody + rPlayer))
            {
                if (_vitals.Hit(st.ContactDamage)) HitsDealt++;
                _hitCool = st.HitIntervalSeconds;
            }

            _body.enabled = true;
            _body.transform.position = new Vector3(_pos.x, _pos.y, -0.1f);
            _body.transform.localScale = Vector3.one * (rBody * 2f);
            _warn.enabled = _warnLeft > 0f;
            if (_warn.enabled)
            {
                float t = 1f - _warnLeft / Mathf.Max(0.01f, _def.TeleportWarningSeconds); // 0→1 로 번짐
                _warn.transform.position = new Vector3(_warnAt.x, _warnAt.y, 0f);
                _warn.transform.localScale = Vector3.one * (rBody * 2f * Mathf.Lerp(0.4f, 1.3f, t));
                var c = _warn.color; c.a = Mathf.Lerp(0.25f, 0.75f, t); _warn.color = c;
            }
        }

        void BeginTeleport(Vector2 p, WorldScale ws)
        {
            float gap = ws.Length(_def.TeleportGap);
            Vector2 back = _move != null && _move.Facing.sqrMagnitude > 0.01f ? -_move.Facing.normalized : (_pos - p).normalized;
            if (back.sqrMagnitude < 0.01f) back = Vector2.down;
            _warnAt = p + back * gap;
            _warnLeft = _def.TeleportWarningSeconds;
        }

        void OnGUI()
        {
            if (!showStats || Step <= 0) return;
            var st = Strength;
            GUI.Label(new Rect(12, 92, 640, 22),
                $"혈마의 그림자 {st.Step}단계  피해 {st.ContactDamage:0.#}  걸음 {st.MoveSpeed:0}  적 피해 x{st.EnemyDamageMultiplier:0.##}  맞힘 {HitsDealt}  순간이동 {Teleports}");
        }
    }
}
