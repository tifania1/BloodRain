// C-10b 서장 끝 컷. Prologue.CurrentCut(art id)을 한 장씩 보여 주고 누르면 Prologue.NextCut, FirstRebirth 가 되면 끝을 알린다.
// 그림은 Assets/Art/Cutscenes 의 같은 이름 텍스처(메뉴 'BloodRain/Run 씬에 서장 끝 컷 화면 붙이기' 가 채운다). 없으면 검은 칸 + 자막.
// 자막은 dialogue.json prologue.cuts 의 같은 art 줄(현지화 키 dialogue.prologue.cuts.<id>.lines.<i>.text).
// 세상은 멈춘 채(timeScale 0)라 실제 시간으로 센다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BloodRain.Core.Run;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BloodRain.Presentation.Meta
{
    public sealed class CutScreen : MonoBehaviour
    {
        public TMP_FontAsset font;
        public List<Texture2D> art = new List<Texture2D>();
        /// <summary>한 컷을 넘길 수 있게 되기까지(실제 초). 쓰러뜨린 순간 누르던 키가 컷을 건너뛰지 않게.
        /// 웹 컷과 같이 balance levelUp.inputGuardSeconds(12장 '레벨업 입력')를 쓴다.</summary>
        static float InputGuardSeconds => DataBoot.Data.Balance.LevelUp.InputGuardSeconds;

        public bool IsOpen { get; private set; }
        public string Shown { get; private set; } = "";
        public int ShownCount { get; private set; }

        Prologue _prologue;
        Action _done;
        Canvas _canvas;
        RawImage _image;
        AspectRatioFitter _fit;
        TextMeshProUGUI _text;
        float _openedAt;

        public void Play(Prologue prologue, Action done)
        {
            _prologue = prologue;
            _done = done;
            ShownCount = 0;
            if (_canvas == null) Build();
            _canvas.gameObject.SetActive(true);
            IsOpen = true;
            ShowCurrent();
        }

        /// <summary>지금 컷을 넘긴다(시험 메뉴·누름).</summary>
        public void Next()
        {
            if (!IsOpen) return;
            _prologue.NextCut();
            ShowCurrent();
        }

        void ShowCurrent()
        {
            string cut = _prologue.Step == PrologueStep.Cutscene ? _prologue.CurrentCut : null;
            if (cut == null) { Finish(); return; }
            Shown = cut;
            ShownCount++;
            _openedAt = Time.unscaledTime;
            var tex = art.FirstOrDefault(t => t != null && t.name == cut);
            _image.texture = tex;
            _image.color = tex != null ? Color.white : Color.black;
            _fit.aspectRatio = tex != null ? (float)tex.width / tex.height : 16f / 9f;
            _text.text = Subtitle(cut);
        }

        static string Subtitle(string art)
        {
            var data = DataBoot.Data;
            var def = data.Dialogue.Prologue.Cuts.FirstOrDefault(c => c.Art == art);
            if (def == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < def.Lines.Count; i++)
            {
                var l = def.Lines[i];
                string line = Loc.T($"dialogue.prologue.cuts.{def.Id}.lines.{i}.text", l.TextKo);
                if (sb.Length > 0) sb.Append('\n');
                if (l.Speaker != null)
                {
                    var sp = data.Dialogue.Speakers.FirstOrDefault(s => s.Id == l.Speaker);
                    string name = sp != null ? Loc.T($"dialogue.speakers.{sp.Id}.name", sp.NameKo) : l.Speaker;
                    sb.Append("<color=#C9A86A>").Append(name).Append("</color>  ").Append(line);
                }
                else sb.Append("<i>").Append(line).Append("</i>");
            }
            return sb.ToString();
        }

        void Finish()
        {
            IsOpen = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            var d = _done;
            _done = null;
            d?.Invoke();
        }

        void Update()
        {
            if (!IsOpen || Time.unscaledTime - _openedAt < InputGuardSeconds) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool pressed = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (pressed) Next();
        }

        void Build()
        {
            var root = new GameObject("서장 끝 컷", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("바탕", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = Color.black;

            var img = new GameObject("그림", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            img.transform.SetParent(root.transform, false);
            Stretch(img.GetComponent<RectTransform>());
            _image = img.GetComponent<RawImage>();
            _fit = img.GetComponent<AspectRatioFitter>();
            _fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;   // 2026-11-21 인트로처럼 화면을 꽉 채운다(위아래 검은 띠 없이)

            // 자막 밑: 납작한 검은 띠 대신 아래로 갈수록 짙어지는 그늘(인트로와 같은 결)
            var band = new GameObject("자막 띠", typeof(RectTransform), typeof(RawImage));
            band.transform.SetParent(root.transform, false);
            var br = band.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 0f); br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(0.5f, 0f);
            br.sizeDelta = new Vector2(0f, 300f);
            br.anchoredPosition = Vector2.zero;
            var shade = band.GetComponent<RawImage>();
            shade.texture = Shade(); shade.color = new Color(0f, 0f, 0f, 0.8f); shade.raycastTarget = false;

            var txt = new GameObject("자막", typeof(RectTransform));
            txt.transform.SetParent(band.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            Stretch(tr);
            tr.offsetMin = new Vector2(160f, 30f); tr.offsetMax = new Vector2(-160f, -150f);   // 그늘 아래쪽(짙은 데)에 줄
            _text = txt.AddComponent<TextMeshProUGUI>();
            if (font != null) _text.font = font;
            _text.fontSize = 38;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = new Color(0.94f, 0.91f, 0.85f);
            _text.richText = true;
            var mat = _text.fontMaterial;   // 그림 위에서도 읽히게 글 둘레 어두운 번짐
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.85f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.5f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.6f);
            _text.UpdateMeshPadding();
            root.SetActive(false);
        }

        static Texture2D _shade;
        static Texture2D Shade()
        {
            if (_shade != null) return _shade;
            const int h = 64;
            _shade = new Texture2D(1, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "CutShade" };
            for (int y = 0; y < h; y++) _shade.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, y / (h - 1f))));
            _shade.Apply();
            return _shade;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
