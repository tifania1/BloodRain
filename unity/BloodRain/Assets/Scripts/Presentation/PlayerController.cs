using BloodRain.Core.Combat;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // 플레이어 이동과 경공 대시(C-01). WASD·방향키·패드 왼쪽 스틱으로 걷고, Space·Shift·패드 A 로 대시.
    //   걸음 = 기본 걸음(balance.json stats.moveBase, 경지 m/s) × 몸의 걸음 배율, 대시 = Core Dash(balance.json dash). 둘 다 WorldScale 로 세계 m 로 바꾼다.
    //   혈우 장막: 걸음에 Core PushVelocity 를 더하고 KeepInside(TU-06 과 같음). 카메라는 ScaleCamera 가 이 Transform 을 따른다.
    public sealed class PlayerController : MonoBehaviour
    {
        [Tooltip("플레이어 지름(경지 m). 시작할 때 balance.json stats.playerBodyRadius x 2 로 덮어쓴다")]
        public float bodyRealmMeters = 0.8f;
        [Tooltip("태어난 곳(Unity 좌표). 혈우 장막의 가운데")]
        public Vector2 birthplace;

        WorldScale _ws;
        float _moveMultiplier = 1f;
        float _baseMove = 4.6f;
        Vector2 _facing = Vector2.right;

        float _slowLeft, _slowMul = 1f;

        /// <summary>걸음 둔화(빙강시 닿음: enemies.json onHitSlowSeconds·onHitSlowMultiplier). 다시 걸리면 시간만 처음부터,
        /// 배율은 더 느린 쪽. 대시에는 안 걸린다.</summary>
        public void Slow(float seconds, float multiplier)
        {
            if (seconds <= 0f) return;
            if (multiplier <= 0f) multiplier = 1f;
            _slowMul = _slowLeft > 0f ? Mathf.Min(_slowMul, multiplier) : multiplier;
            _slowLeft = seconds;
        }

        public float SlowLeft => _slowLeft;

        float _bindLeft;

        /// <summary>묶임(수적 갈고리 hook_bind: enemies.json hook.bindSeconds). 남은 초 동안 걷기·경공을 못 한다(무공은 나감).
        /// 다시 걸리면 긴 쪽.</summary>
        public void Bind(float seconds) { if (seconds > _bindLeft) _bindLeft = seconds; }

        public float BindLeft => _bindLeft;

        public Dash Dash { get; private set; }
        /// <summary>경공이 실제로 시작될 때(충전이 있어 나간 때만). R-08U 도전 과제 '한 생에서 경공 N회'.</summary>
        public event System.Action Dashed;
        public Vector2 Facing => _facing;
        public bool Invulnerable => Dash != null && Dash.Invulnerable;

        /// <summary>몸 걸음 배율(StatSheet.MoveSpeed = 몸 × (1 + 신법)). C-06a LifeRunner 가 넣는다.</summary>
        public float MoveMultiplier { get => _moveMultiplier; set => _moveMultiplier = value; }
        /// <summary>C-06b 경공 거리 배율(수로채 3 dash_drag). 미끄러지는 한 걸음에 곱한다. LifeRunner 가 넣는다.</summary>
        public float DashDistanceMultiplier { get; set; } = 1f;
        /// <summary>PM-01U 금강체 최대 덧붙임(넉백 면역). 지금 Run 씬엔 플레이어를 미는 것이 없어 표시만 둔다.
        /// 정예·땅의 주인 패턴 넉백(BR-01)·기수 깃발 등 플레이어를 미는 쪽이 생기면 이 값이 참일 때 밀지 않는다.</summary>
        public bool KnockbackImmune { get; set; }

        // C-06c 수로채 3 dash_drag 끌기. 경공 중 몸 DragRadius(경지 m) 안 잡몹을 미끄러진 걸음 × DragFactor 만큼 같이 옮긴다.
        // 정예·보스(EnemyDef.Family elite·boss)는 안 끌린다. LifeRunner 가 넣는다(0 이면 끔).
        [System.NonSerialized] public EnemyField dragField;
        public float DragRadius { get; set; }
        public float DragFactor { get; set; }
        /// <summary>끌려간 적 수 합(시험·디버그용).</summary>
        public int Dragged { get; private set; }
        readonly System.Collections.Generic.List<int> _dragNear = new System.Collections.Generic.List<int>();

        void Drag(Vector2 step)
        {
            if (DragRadius <= 0f || DragFactor <= 0f || dragField == null || dragField.Sim == null) return;
            Vector2 p = transform.position;
            dragField.Sim.QueryCircle(new Unity.Mathematics.float2(p.x, p.y), _ws.Length(DragRadius), _dragNear);
            var move = new Unity.Mathematics.float2(step.x, step.y) * DragFactor;
            foreach (int i in _dragNear)
            {
                var a = dragField.Sim.Get(i);
                var def = dragField.DefOf(a.Kind);
                if (def != null && (def.Family == "elite" || def.Family == "boss")) continue;
                a.Pos += move;
                dragField.Sim.Set(i, a);
                Dragged++;
            }
        }

        void Awake()
        {
            var data = DataBoot.Data;
            if (WorldScale.Current == null)
            {
                var ws = new WorldScale(data);
                ws.SetLevel(1, snap: true);
                WorldScale.Current = ws;
            }
            _ws = WorldScale.Current;
            var body = RebirthHandoff.Body ?? Rebirth.Prologue(data);
            _moveMultiplier = body.Character.MoveSpeed; // C-06a 한 생이 있으면 LifeRunner 가 ApplyStats 로 덮는다
            _baseMove = data.Balance.Stats.MoveBase;
            bodyRealmMeters = data.Balance.Stats.PlayerBodyRadius * 2f;
            Dash = new Dash(data.Balance.Dash);
            var cam = Camera.main != null ? Camera.main.GetComponent<ScaleCamera>() : null;
            if (cam != null) cam.target = transform;
            // 캐릭터 그림이 오기 전 임시 원
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite == null) sr.sprite = MakeDisc();
        }

        static Sprite MakeDisc()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(c - d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var move = ReadMove();
            bool bound = _bindLeft > 0f;
            if (bound) { _bindLeft = Mathf.Max(0f, _bindLeft - dt); move = Vector2.zero; }
            if (move.sqrMagnitude > 1e-4f) _facing = move.normalized;
            if (!bound && DashPressed() && Dash.TryStart(_facing.x, _facing.y)) Dashed?.Invoke(); // R-08U 경공 수(LifeRunner 가 Log.Dash)

            if (_slowLeft > 0f) { _slowLeft -= dt; if (_slowLeft <= 0f) _slowMul = 1f; }
            var (ddx, ddy) = Dash.Tick(dt);
            var pos = new System.Numerics.Vector2(transform.position.x, transform.position.y);
            var birth = new System.Numerics.Vector2(birthplace.x, birthplace.y);
            Vector2 step;
            if (Dash.IsDashing || ddx != 0f || ddy != 0f)
            {
                step = new Vector2(_ws.Length(ddx), _ws.Length(ddy)) * DashDistanceMultiplier; // 대시 중엔 걸음 대신 미끄러짐(웹과 같다)
                Drag(step); // C-06c 수로채 3
            }
            else
            {
                float speed = _ws.Speed(_baseMove * _moveMultiplier * _slowMul);
                var push = _ws.Scale.PushVelocity(pos, birth, _ws.Realm, speed);
                step = (Vector2.ClampMagnitude(move, 1f) * speed + new Vector2(push.X, push.Y)) * dt;
            }
            if (step != Vector2.zero)
            {
                var kept = _ws.Scale.KeepInside(pos + new System.Numerics.Vector2(step.x, step.y), birth, _ws.Realm);
                transform.position = new Vector3(kept.X, kept.Y, transform.position.z);
            }
            transform.localScale = Vector3.one * _ws.Length(bodyRealmMeters);
        }

        static Vector2 ReadMove()
        {
            var v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
            }
            var pad = Gamepad.current;
            if (pad != null) v += pad.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        static bool DashPressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
        }
    }
}
