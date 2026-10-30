using BloodRain.Core.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation.Fx
{
    // 각성 연출 시험 씬(Assets/Scenes/Test/AwakenTest)용. 임시 바닥·플레이어·적 점을 깔고 연출을 돌린다.
    // Space 긴 연출, S 짧은 연출, N 다음 절기. 플레이를 누르면 0.6초 뒤 첫 절기를 길게 한 번 보여 준다.
    public sealed class AwakeningTestDriver : MonoBehaviour
    {
        [SerializeField] AwakeningFx fx;
        [SerializeField] int evolutionIndex;
        [SerializeField] bool playOnStart = true;
        [SerializeField] int enemyCount = 80;

        Transform _player;
        float _startAt;

        void Start()
        {
            BuildStage();
            _startAt = playOnStart ? Time.unscaledTime + 0.6f : float.MaxValue;
        }

        void Update()
        {
            if (Time.unscaledTime >= _startAt) { _startAt = float.MaxValue; Play(true); }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.spaceKey.wasPressedThisFrame) Play(true);
            if (kb.sKey.wasPressedThisFrame) Play(false);
            if (kb.nKey.wasPressedThisFrame) evolutionIndex = (evolutionIndex + 1) % DataBoot.Data.Evolutions.Count;
        }

        void Play(bool longVersion)
        {
            if (fx == null || fx.IsPlaying) return;
            var evos = DataBoot.Data.Evolutions;
            var evo = evos[Mathf.Clamp(evolutionIndex, 0, evos.Count - 1)];
            fx.Play(evo, _player, firstOnAccount: longVersion, AwakeningLength.Auto, forceLong: longVersion);
        }

        /// <summary>검수용: 긴 연출을 틀고 장면마다 Temp/Shots 에 스크린샷을 남긴다(unityMCP 에서 부른다).</summary>
        public void CaptureSequence() => StartCoroutine(Capture());

        System.Collections.IEnumerator Capture()
        {
            System.IO.Directory.CreateDirectory("Temp/Shots");
            Play(true);
            float[] at = { 0.3f, 0.7f, 1.05f, 1.45f, 1.9f, 2.45f, 2.8f, 3.3f };
            float t0 = Time.unscaledTime;
            foreach (var t in at)
            {
                while (Time.unscaledTime - t0 < t) yield return null;
                ScreenCapture.CaptureScreenshot($"Temp/Shots/awaken_{t:0.00}.png");
            }
        }

        void BuildStage()
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1f - d) * 4f)));
            }
            tex.Apply();
            var dot = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
            var sq = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);

            Make("바닥(임시)", sq, new Color(0.42f, 0.5f, 0.36f), -100, Vector3.zero, new Vector3(60, 40, 1));
            var rnd = new System.Random(3);
            for (int i = 0; i < 40; i++) // 풀 얼룩
            {
                var p = new Vector3((float)rnd.NextDouble() * 30 - 15, (float)rnd.NextDouble() * 20 - 10, 0);
                Make("얼룩", dot, new Color(0.35f, 0.44f, 0.3f), -99, p, Vector3.one * (1f + (float)rnd.NextDouble() * 2f));
            }
            for (int i = 0; i < enemyCount; i++) // 적(강시) 자리
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 2f + (float)rnd.NextDouble() * 8f;
                var p = new Vector3(Mathf.Cos(a) * r * 1.6f, Mathf.Sin(a) * r, 0);
                Make("적", dot, new Color(0.45f, 0.62f, 0.55f), 0, p, Vector3.one * 0.45f);
            }
            _player = Make("플레이어", dot, new Color(0.85f, 0.25f, 0.2f), 10, Vector3.zero, Vector3.one * 0.7f).transform;
        }

        GameObject Make(string name, Sprite s, Color c, int order, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = c;
            sr.sortingOrder = order;
            return go;
        }
    }
}
