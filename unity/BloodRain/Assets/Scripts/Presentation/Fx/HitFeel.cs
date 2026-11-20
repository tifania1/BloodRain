using UnityEngine;
using UnityEngine.SceneManagement;

namespace BloodRain.Presentation.Fx
{
    /// <summary>
    /// 타격감 바탕의 화면 쪽(HF-02U, 2026-11-13): 히트스톱과 방향 있는 화면 흔들림.
    /// 값은 시안(web/lookdev/sects.html)과 vfx.md '화면이 어지러워지지 않게'를 따른다.
    /// 히트스톱은 0.3초에 한 번까지, 다른 연출(돌파·각성·레벨업)이 시간을 쥐고 있으면 끼어들지 않는다.
    /// 흔들림은 설정의 '화면 흔들림' 배율을 곱한다. ScaleCamera 가 프레임마다 Tick 을 불러 카메라에 더한다.
    /// 2026-11-16 '더 세게': 큰 타격에 화면이 한 번 눌리고(당겨 보기 5%까지) 종이가 잠깐 어두워진다(Jolt). 시안의 jolt 와 같은 값.
    /// </summary>
    public static class HitFeel
    {
        const float StopScale = 0.02f;   // 완전히 0 이면 dt 로 나누는 코드가 놀랄 수 있어 아주 느리게만
        const float LockSeconds = 0.3f;
        const float MaxStop = 0.08f;

        const float ZoomMax = 0.05f;     // 눌림 1 이면 5% 당겨 본다
        const float DimMax = 0.3f;       // 어두움 1 이면 먹색 알파 0.3

        static float _stopUntil = -1f, _lockUntil = -1f, _trauma, _zoom, _dim, _zoomScale = 1f;
        static Vector2 _kick;
        static bool _ours;

        static HitFeel()
        {
            SceneManager.sceneUnloaded += _ => Release();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _stopUntil = _lockUntil = -1f; _trauma = _zoom = _dim = 0f; _zoomScale = 1f; _kick = Vector2.zero; _ours = false; }

        static void Release()
        {
            if (_ours && Mathf.Abs(Time.timeScale - StopScale) < 1e-4f) Time.timeScale = 1f;
            _ours = false;
        }

        /// <summary>화면(레벨업·깨달음·보갑·상인·검총·절기 고르기·멈춤·천하도)이 세상을 멈추기 전에 '닫을 때 되돌릴 배속'을 여기서 받는다.
        /// 타격 멈춤(0.02) 중에 화면이 열리면 전에는 0.02 를 기억했다가 닫을 때 되돌려, 세상이 거의 멈춘 채로 남았다
        /// (2026-11-20 플레이 테스트: 이류 돌파 뒤 레벨업이 잦을 때 일어남 — 적은 멈추고 시계만 가서 기혈이 깎이는 것처럼 보임).
        /// 타격 멈춤 중이면 그 멈춤을 거두고 1 을 돌려준다.</summary>
        public static float ScaleToRestore()
        {
            if (Mathf.Abs(Time.timeScale - StopScale) < 1e-4f) { _ours = false; _stopUntil = -1f; return 1f; }
            return Time.timeScale > 0f ? Time.timeScale : 1f;
        }

        /// <summary>아주 짧게 멈춘다(실제 시간 seconds 초).</summary>
        public static void Stop(float seconds)
        {
            float now = Time.unscaledTime;
            if (now < _lockUntil || Time.timeScale < 0.99f) return;
            _lockUntil = now + LockSeconds;
            _stopUntil = now + Mathf.Min(seconds, MaxStop);
            _ours = true;
            Time.timeScale = StopScale;
        }

        /// <summary>흔들림을 더한다. trauma 0~1, dir(라디안)이 있으면 그쪽으로 한 번 밀린다.</summary>
        public static void Shake(float trauma, float dir = float.NaN)
        {
            // 2026-11-17 사람: 흔들림이 너무 거세다 → 5분의 1로, 0.28 을 넘지 않게. 방향으로 살짝 밀리는 것만 남긴다(시안의 '절제')
            trauma *= 0.2f;
            _trauma = Mathf.Min(0.28f, Mathf.Max(_trauma, trauma) + trauma * 0.15f);
            if (!float.IsNaN(dir)) _kick += new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * trauma * 0.5f;
            if (_kick.sqrMagnitude > 0.16f) _kick = _kick.normalized * 0.4f;
        }

        /// <summary>큰 타격에 화면이 한 번 눌리고(zoom 0~1) 종이가 잠깐 어두워진다(dim 0~1). 쌓이되 1 까지.</summary>
        public static void Jolt(float zoom, float dim)
        {
            // 2026-11-17 사람: 흔들림이 거세고 번쩍임이 많다 → 눌림은 3분의 1, 종이 어둡게는 뺀다(시안의 '절제')
            _zoom = Mathf.Min(0.5f, _zoom + zoom * 0.35f);
        }

        /// <summary>카메라 직교 크기에 곱할 값(1 이 평소, 눌리면 0.95 까지). 설정 흔들림 배율을 따른다(0 이면 늘 1).</summary>
        public static float Zoom => 1f - _zoom * ZoomMax * _zoomScale;

        /// <summary>화면에 깔 먹색 알파(0 ~ 0.3). SectFx 가 바닥 위·캐릭터 밑에 한 장 깐다.</summary>
        public static float DimAlpha => _dim * DimMax * _zoomScale;

        /// <summary>프레임마다 한 번(실제 시간). 카메라에 더할 어긋남을 미터로 돌려준다.</summary>
        public static Vector2 Tick(float unscaledDt)
        {
            if (_ours && Time.unscaledTime >= _stopUntil) Release();
            _trauma = Mathf.Max(0f, _trauma - unscaledDt * 2.2f);
            _kick *= Mathf.Pow(0.0001f, unscaledDt);
            _zoom *= Mathf.Pow(0.0006f, unscaledDt);
            _dim = Mathf.Max(0f, _dim - unscaledDt * 5f);
            var s = BloodRain.Presentation.Meta.SettingsScreen.Current;
            float scale = s != null ? s.ShakeScale : 1f;
            _zoomScale = Mathf.Clamp01(scale);
            if (scale <= 0f) return Vector2.zero;
            float t2 = _trauma * _trauma, n = Time.unscaledTime * 1000f;
            var noise = new Vector2(Mathf.Sin(n * 0.071f) + Mathf.Sin(n * 0.113f), Mathf.Cos(n * 0.083f) + Mathf.Sin(n * 0.127f)) * 0.14f * t2;
            return (_kick + noise) * scale;
        }
    }
}
