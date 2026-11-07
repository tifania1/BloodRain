#nullable enable
// 조작 키(기획서 8장 '조작' 표, 12장 설정 '조작 재설정'·'레벨업 입력' 단축키).
//   키는 Unity Input System 컨트롤 경로 글자 그대로("<Keyboard>/space", "<Mouse>/rightButton", "<Gamepad>/buttonSouth").
//   Unity 는 InputAction 에 ApplyBindingOverride(경로) 로 얹고, 화면 글은 InputControlPath.ToHumanReadableString 으로 만든다.
//   키보드·마우스는 행동마다 두 칸(8장 'WASD / 방향키', 'Space / Shift', 'E / 마우스 오른쪽', 'Esc / Tab'), 게임패드는 한 칸.
//   재설정: 이미 다른 행동이 쓰는 키를 고르면 둘이 키를 맞바꾼다(어떤 행동도 키를 잃지 않게). 레벨업 카드 1~4·Enter 는 못 고른다.
//   레벨업 단축키는 웹 프로토타입과 같은 R·X·Z(2026-11-07 — S 는 8장에서 이동 아래라 정진에 쓰지 않는다).
//   게임패드 이동(왼쪽 스틱)과 레벨업 고르기(방향 + A)는 고정이라 여기 없다.
//   저장은 GameSettings 처럼 낱말마다 문자열 하나: "bind.<행동>.<칸>"(칸 0·1 키보드, pad 게임패드). 기본값과 같으면 지운다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Meta
{
    public static class ControlAction
    {
        public const string Up = "up", Down = "down", Left = "left", Right = "right",
            Dash = "dash", Ultimate = "ult", Pause = "pause",
            Reroll = "reroll", Banish = "banish", Skip = "skip";

        /// <summary>설정 화면 차례(8장 표 차례, 레벨업 단축키는 뒤에).</summary>
        public static readonly IReadOnlyList<string> All = new[] { Up, Down, Left, Right, Dash, Ultimate, Pause, Reroll, Banish, Skip };
    }

    public sealed class Controls
    {
        /// <summary>게임패드 칸 번호(키보드는 0·1).</summary>
        public const int Pad = 2;
        public const string KeyPrefix = "bind.";

        /// <summary>고를 수 없는 키: 레벨업 카드 1~4(8장 '1~4')와 확인(Enter).</summary>
        public static readonly IReadOnlyCollection<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "<Keyboard>/1", "<Keyboard>/2", "<Keyboard>/3", "<Keyboard>/4", "<Keyboard>/enter", "<Keyboard>/numpadEnter",
        };

        // 8장 '조작' 표. null 은 빈 칸.
        static readonly Dictionary<string, string?[]> Default = new Dictionary<string, string?[]>(StringComparer.Ordinal)
        {
            [ControlAction.Up] = new[] { "<Keyboard>/w", "<Keyboard>/upArrow", null },
            [ControlAction.Down] = new[] { "<Keyboard>/s", "<Keyboard>/downArrow", null },
            [ControlAction.Left] = new[] { "<Keyboard>/a", "<Keyboard>/leftArrow", null },
            [ControlAction.Right] = new[] { "<Keyboard>/d", "<Keyboard>/rightArrow", null },
            [ControlAction.Dash] = new[] { "<Keyboard>/space", "<Keyboard>/leftShift", "<Gamepad>/buttonSouth" },
            [ControlAction.Ultimate] = new[] { "<Keyboard>/e", "<Mouse>/rightButton", "<Gamepad>/rightTrigger" },
            [ControlAction.Pause] = new[] { "<Keyboard>/escape", "<Keyboard>/tab", "<Gamepad>/start" },
            [ControlAction.Reroll] = new[] { "<Keyboard>/r", null, null },
            [ControlAction.Banish] = new[] { "<Keyboard>/x", null, null },
            [ControlAction.Skip] = new[] { "<Keyboard>/z", null, null },
        };

        readonly Dictionary<string, string?[]> _map;

        Controls() => _map = Default.ToDictionary(kv => kv.Key, kv => (string?[])kv.Value.Clone(), StringComparer.Ordinal);

        public static Controls Defaults() => new Controls();

        /// <summary>그 행동의 그 칸 키(없으면 null).</summary>
        public string? Get(string action, int slot) => Slots(action)[CheckSlot(slot)];

        /// <summary>그 행동의 키 전부(빈 칸 뺌). Unity 가 InputAction 에 얹을 것.</summary>
        public IEnumerable<string> Paths(string action) => Slots(action).Where(p => p != null).Select(p => p!);

        /// <summary>키를 바꾼다. 다른 행동(또는 같은 행동의 다른 칸)이 그 키를 쓰고 있으면 맞바꾼다.
        /// 못 고르는 키·게임패드 칸에 키보드 키(또는 그 반대)면 false 이고 아무것도 안 바뀐다. null 은 그 칸 비우기.</summary>
        public bool Rebind(string action, int slot, string? path)
        {
            var slots = Slots(action);
            CheckSlot(slot);
            if (path != null)
            {
                if (Reserved.Contains(path)) return false;
                if (IsPad(path) != (slot == Pad)) return false;
            }
            var old = slots[slot];
            if (path != null)
                foreach (var kv in _map)
                    for (int i = 0; i < kv.Value.Length; i++)
                        if ((kv.Key != action || i != slot) && kv.Value[i] == path) kv.Value[i] = old;
            slots[slot] = path;
            return true;
        }

        /// <summary>한 행동만 기본값으로.</summary>
        public void Reset(string action)
        {
            var def = Default[action];
            for (int i = 0; i < def.Length; i++) Rebind(action, i, def[i]);
        }

        /// <summary>같은 키가 두 칸에 있으면 그 키 목록(맞바꾸기로는 생기지 않지만 저장 파일을 손댔을 때).</summary>
        public IEnumerable<string> Conflicts() =>
            _map.Values.SelectMany(v => v).Where(p => p != null).GroupBy(p => p!).Where(g => g.Count() > 1).Select(g => g.Key);

        /// <summary>쓰기: 기본값과 다른 칸만 write(낱말, 경로 또는 ""(비움)), 같은 칸은 remove(낱말).</summary>
        public void Save(Action<string, string> write, Action<string> remove)
        {
            foreach (var a in ControlAction.All)
                for (int i = 0; i <= Pad; i++)
                {
                    var k = SaveKey(a, i);
                    var v = _map[a][i];
                    if (v == Default[a][i]) remove(k);
                    else write(k, v ?? "");
                }
        }

        /// <summary>읽기: 기본값 위에 저장된 칸을 행동 차례대로 Rebind 로 얹는다(못 고르는 키는 버림). 겹치면 기본값으로 되돌린다.</summary>
        public static Controls Load(Func<string, string?> read)
        {
            var c = new Controls();
            foreach (var a in ControlAction.All)
                for (int i = 0; i <= Pad; i++)
                {
                    var v = read(SaveKey(a, i));
                    if (v == null) continue;
                    c.Rebind(a, i, v.Length == 0 ? null : v);
                }
            return c.Conflicts().Any() ? new Controls() : c;
        }

        public static string SaveKey(string action, int slot) => KeyPrefix + action + "." + (slot == Pad ? "pad" : slot.ToString());

        static bool IsPad(string path) => path.StartsWith("<Gamepad>", StringComparison.Ordinal);

        string?[] Slots(string action) =>
            _map.TryGetValue(action, out var s) ? s : throw new ArgumentException($"모르는 조작입니다: {action}");

        static int CheckSlot(int slot) =>
            slot >= 0 && slot <= Pad ? slot : throw new ArgumentOutOfRangeException(nameof(slot), "칸은 0·1(키보드)·2(게임패드)");
    }
}
