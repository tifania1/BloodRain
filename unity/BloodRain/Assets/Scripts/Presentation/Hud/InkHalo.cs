using TMPro;
using UnityEngine;

namespace BloodRain.Presentation.Hud
{
    /// <summary>전투 HUD 먹 글자 뒤에 옅은 종이빛 번짐(TMP 밑그림). 소나무·바위 같은 짙은 소품 위를 지나도 글자가 읽히게
    /// (2026-11-20 플레이 시험: 위 가운데 '혈운'과 오른쪽 위 Lv·처치가 소나무 위에서 묻혔다).</summary>
    public static class InkHalo
    {
        static readonly Color Halo = new Color(0.93f, 0.89f, 0.80f, 0.8f);

        public static void Apply(TMP_Text t)
        {
            if (t == null) return;
            var m = t.fontMaterial; // 글자마다 제 재질(밑그림 켬)
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, Halo);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            t.UpdateMeshPadding();
        }
    }
}
