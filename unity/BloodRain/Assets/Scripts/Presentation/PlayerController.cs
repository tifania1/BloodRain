using BloodRain.Core.Combat;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // 플레이어 이동과 경공 대시(C-01). WASD·방향키·패드 왼쪽 스틱으로 걷고, Space·Shift·패드 A 로 대시.
    //   걸음 = 기본 걸음(경지 m/s) × 몸의 걸음 배율, 대시 = Core Dash(balance.json dash). 둘 다 WorldScale 로 세계 m 로 바꾼다.
    //   혈우 장막: 걸음에 Core PushVelocity 를 더하고 KeepInside(TU-06 과 같음). 카메라는 ScaleCamera 가 이 Transform 을 따른다.
    public sealed class PlayerController : MonoBehaviour
    {
        [Tooltip("기본 걸음(경지 m/s). 웹 프로토타입 4.6. balance.json 에 아직 없다")]
        public float baseMoveRealmMetersPerSecond = 4.6f;
        [Tooltip("플레이어 지름(경지 m)")]
        public float bodyRealmMeters = 0.8f;
        [Tooltip("태어난 곳(Unity 좌표). 혈우 장막의 가운데")]
        public Vector2 birthplace;

        WorldScale _ws;
        float _moveMultiplier = 1f;
        Vector2 _facing = Vector2.right;

        public Dash Dash { get; private set; }
        public Vector2 Facing => _facing;
        public bool Invulnerable => Dash != null && Dash.Invulnerable;

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
            _moveMultiplier = body.Character.MoveSpeed;
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
            var move = ReadMove();
            if (move.sqrMagnitude > 1e-4f) _facing = move.normalized;
            if (DashPressed()) Dash.TryStart(_facing.x, _facing.y);

            float dt = Time.deltaTime;
            var (ddx, ddy) = Dash.Tick(dt);
            var pos = new System.Numerics.Vector2(transform.position.x, transform.position.y);
            var birth = new System.Numerics.Vector2(birthplace.x, birthplace.y);
            Vector2 step;
            if (Dash.IsDashing || ddx != 0f || ddy != 0f)
                step = new Vector2(_ws.Length(ddx), _ws.Length(ddy)); // 대시 중엔 걸음 대신 미끄러짐(웹과 같다)
            else
            {
                float speed = _ws.Speed(baseMoveRealmMetersPerSecond * _moveMultiplier);
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
