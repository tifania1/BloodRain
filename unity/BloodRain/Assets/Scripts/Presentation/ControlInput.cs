#nullable enable
using System.Collections.Generic;
using BloodRain.Core.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // WP-69U 조작 재설정: Core `Controls`(8장 '조작' 표)의 키를 InputAction 으로 얹어 판 코드가 행동 이름으로 읽게 한다.
    //   .inputactions 파일 대신 코드로 맵 하나를 만든다(행동 열이 Core 에 있으니 두 곳에 적지 않게).
    //   처음 쓸 때 PlayerPrefs 에서 Load, 설정 화면이 바꾸면 Apply(c) 로 다시 얹고 Save.
    //   게임패드 이동(왼쪽 스틱)·레벨업 고르기(1~4·Enter·방향 + A)는 고정이라 여기서 읽지 않는다.
    public static class ControlInput
    {
        static Controls? _controls;
        static InputActionMap? _map;
        static readonly Dictionary<string, InputAction> _actions = new Dictionary<string, InputAction>();

        /// <summary>지금 키 배치(처음 읽을 때 PlayerPrefs 에서).</summary>
        public static Controls Current
        {
            get
            {
                if (_controls == null) Apply(Controls.Load(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null), save: false);
                return _controls!;
            }
        }

        /// <summary>키 배치를 바꿔 얹는다. save 면 PlayerPrefs 에도.</summary>
        public static void Apply(Controls c, bool save = true)
        {
            _controls = c;
            if (_map != null) { _map.Disable(); _map.Dispose(); }
            _map = new InputActionMap("BloodRain");
            _actions.Clear();
            foreach (var a in ControlAction.All)
            {
                var act = _map.AddAction(a, InputActionType.Button);
                foreach (var p in c.Paths(a)) act.AddBinding(p);
                _actions[a] = act;
            }
            _map.Enable();
            if (save) { c.Save(PlayerPrefs.SetString, PlayerPrefs.DeleteKey); PlayerPrefs.Save(); }
        }

        static InputAction? Act(string action)
        {
            _ = Current;
            return _actions.TryGetValue(action, out var a) ? a : null;
        }

        /// <summary>그 행동 키를 누르고 있나.</summary>
        public static bool Held(string action) => Act(action)?.IsPressed() ?? false;

        /// <summary>이 프레임에 눌렀나.</summary>
        public static bool Pressed(string action) => Act(action)?.WasPressedThisFrame() ?? false;

        /// <summary>키보드 이동(상하좌우 행동). 스틱은 부르는 쪽이 더한다.</summary>
        public static Vector2 Move()
        {
            var v = Vector2.zero;
            if (Held(ControlAction.Up)) v.y += 1;
            if (Held(ControlAction.Down)) v.y -= 1;
            if (Held(ControlAction.Left)) v.x -= 1;
            if (Held(ControlAction.Right)) v.x += 1;
            return v;
        }

        /// <summary>그 칸 키의 화면 글(빈 칸이면 null).</summary>
        public static string? Label(string action, int slot)
        {
            var p = Current.Get(action, slot);
            return p == null ? null : InputControlPath.ToHumanReadableString(p, InputControlPath.HumanReadableStringOptions.OmitDevice);
        }

        /// <summary>그 행동의 첫 키 글(레벨업 단축키 표시용). 키가 없으면 null.</summary>
        public static string? FirstLabel(string action)
        {
            for (int i = 0; i <= Controls.Pad; i++) { var l = Label(action, i); if (l != null) return l; }
            return null;
        }
    }
}
