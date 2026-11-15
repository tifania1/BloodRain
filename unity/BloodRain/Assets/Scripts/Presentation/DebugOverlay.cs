using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation
{
    // DBG-01U 디버그 글 스위치. 기본은 꺼짐, F1 로 켜고 끈다.
    // 개발 빌드가 아닌 빌드(Debug.isDebugBuild 가 거짓)에서는 F1 도 안 먹는다. 에디터는 늘 개발 빌드로 친다.
    // OnGUI 디버그 줄(LifeRunner·EnemyField·WeaponRunner·OrbLayer·PlayerVitals·BloodShadowRunner·배경음·효과음)과
    // 시험 키(Ctrl+숫자 익히기, [ ] C K, O M)는 On 이 참일 때만 그리고 받는다. 각 컴포넌트의 showDebug·showStats 는
    // '그 줄을 띄워도 되나'로 남겨 두고, 실제로 띄우는 것은 이 스위치와 둘 다 참일 때다.
    public static class DebugOverlay
    {
        public static bool Allowed => Debug.isDebugBuild;

        static bool _on;
        public static bool On => _on && Allowed;

        public static void Set(bool on) => _on = on && Allowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            _on = false;
            if (!Allowed) return;
            var go = new GameObject("DebugOverlay") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Poller>();
        }

        sealed class Poller : MonoBehaviour
        {
            void Update()
            {
                var kb = Keyboard.current;
                if (kb != null && kb.f1Key.wasPressedThisFrame) _on = !_on;
            }
        }
    }
}
