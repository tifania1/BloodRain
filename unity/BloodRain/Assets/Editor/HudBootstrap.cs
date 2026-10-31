using System.IO;
using BloodRain.Presentation.Hud;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace BloodRain.Editor
{
    // 전투 HUD(TU-09 지역 HUD부터). 글꼴과 HUD 캔버스를 코드로 만든다. 다시 돌려도 있는 글꼴은 그대로 쓴다.
    public static class HudBootstrap
    {
        const string BodyFontPath = "Assets/Art/Fonts/NotoSerifKR-Regular.otf";
        const string BodyFontAssetPath = "Assets/Art/Fonts/NotoSerifKR SDF.asset";
        const string BrushFontPath = "Assets/Art/Fonts/YujiBoku-Regular.ttf";
        // 각성 연출용 YujiBoku SDF 는 절기 한자 56자 고정 아틀라스라, 본문 대체 글꼴은 동적으로 따로 만든다(丐·罩)
        const string BrushFallbackAssetPath = "Assets/Art/Fonts/YujiBoku Dynamic SDF.asset";

        static readonly Color Ink = new Color(0.12f, 0.10f, 0.09f);

        /// <summary>본문 글꼴 Noto Serif KR 동적 SDF. 대체 글꼴로 Yuji Boku 동적 SDF 를 건다(기획서 12장 '글꼴').</summary>
        [MenuItem("BloodRain/Bootstrap/본문 글꼴(Noto Serif KR SDF)")]
        public static TMP_FontAsset EnsureBodyFont()
        {
            var body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontAssetPath);
            if (body == null) body = CreateDynamic(BodyFontPath, BodyFontAssetPath, "NotoSerifKR SDF");
            var brush = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BrushFallbackAssetPath);
            if (brush == null) brush = CreateDynamic(BrushFontPath, BrushFallbackAssetPath, "YujiBoku Dynamic SDF");
            if (body != null && brush != null && body.fallbackFontAssetTable == null)
                body.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (body != null && brush != null && !body.fallbackFontAssetTable.Contains(brush))
            {
                body.fallbackFontAssetTable.Add(brush);
                EditorUtility.SetDirty(body);
                AssetDatabase.SaveAssets();
            }
            return body;
        }

        static TMP_FontAsset CreateDynamic(string fontPath, string assetPath, string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null) { Debug.LogError($"[HudBootstrap] {fontPath} 이 없다"); return null; }
            var fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = name + " Atlas";
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HudBootstrap] {assetPath} 만듦(동적)");
            return fa;
        }

        /// <summary>지금 열린 씬에 전투 HUD 캔버스(위 가운데 시간·지역·혈운, 화면 혈우)를 만든다.</summary>
        public static RegionHud CreateHud()
        {
            var font = EnsureBodyFont();

            var canvasGo = new GameObject("전투 HUD", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 화면 혈우는 맨 뒤(글자 아래)
            var rainGo = new GameObject("화면 혈우", typeof(RectTransform), typeof(CanvasRenderer), typeof(ScreenRain));
            rainGo.transform.SetParent(canvasGo.transform, false);
            var rr = (RectTransform)rainGo.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

            var clock = MakeText(canvasGo.transform, "경과 시간", font, 40, -44, FontStyles.Bold);
            clock.text = "0:00";
            var region = MakeText(canvasGo.transform, "지역과 혈운", font, 26, -92, FontStyles.Normal);
            region.text = "";

            var hud = canvasGo.AddComponent<RegionHud>();
            hud.clockText = clock;
            hud.regionText = region;
            hud.rain = rainGo.GetComponent<ScreenRain>();
            return hud;
        }

        static TextMeshProUGUI MakeText(Transform parent, string name, TMP_FontAsset font, float size, float y, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900, size * 1.6f);
            rt.anchoredPosition = new Vector2(0, y);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Ink;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }
    }
}
