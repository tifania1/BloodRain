using UnityEngine;

namespace BloodRain.Presentation.Hud
{
    // 색각 보정(UI-03, 2026-11-20): 위험 표시(적 탄·땅의 주인·정예 예고)의 붉은빛만 설정(cvd)에 맞게 바꾼다.
    //   내 효과(흰·금·청록)와 수묵 바탕은 그대로. 셈은 Core GameSettings.DangerColor.
    public static class DangerColors
    {
        public static Color Of(Color c)
        {
            var (r, g, b) = Meta.SettingsScreen.Current.DangerColor(c.r, c.g, c.b);
            return new Color(r, g, b, c.a);
        }
    }
}
