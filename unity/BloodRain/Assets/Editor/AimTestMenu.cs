using BloodRain.Core.Combat;
using BloodRain.Presentation.Combat;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // WP-52U 조준 방식 시험. 설정 화면(WP-57U)이 생기기 전까지 '가까운 적'·'이동 방향'을 여기서 바꾼다(PlayerPrefs "aimMode" 에 남음).
    public static class AimTestMenu
    {
        const string Root = "BloodRain/Test/조준 방식/";

        [MenuItem(Root + "가까운 적")]
        public static void Near() => Set(AimMode.Nearest);

        [MenuItem(Root + "이동 방향")]
        public static void Move() => Set(AimMode.Move);

        static void Set(AimMode mode)
        {
            WeaponRunner.SetAimMode(mode);
            Debug.Log($"[조준] 방식 = {Aim.ToKey(mode)}");
        }
    }
}
