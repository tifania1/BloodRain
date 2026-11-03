using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // 효과 간소화(FxSettings)를 에디터에서 켜고 끈다. 휴대폰이 아니어도 플레이로 줄어든 모습을 보려고.
    public static class FxSettingsMenu
    {
        const string Path = "BloodRain/Test/효과 간소화";

        [MenuItem(Path)]
        static void Toggle()
        {
            FxSettings.Simplified = !FxSettings.Simplified;
            Debug.Log("[FxSettings] 효과 간소화 " + (FxSettings.Simplified ? "켬" : "끔"));
        }

        [MenuItem(Path, true)]
        static bool Check()
        {
            Menu.SetChecked(Path, FxSettings.Simplified);
            return true;
        }
    }
}