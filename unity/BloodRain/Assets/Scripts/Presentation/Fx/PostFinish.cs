using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BloodRain.Presentation.Fx
{
    // PP-01U 화면 마감: 약한 Bloom + 가장자리 어둡게(시안 web/lookdev/finish.html).
    // 프로필은 실행 때 코드로 만든다(에셋 없음 — 값은 아래 필드에서만 바꾼다).
    // 기·금빛만 번지게: SectFx 더하기 겹이 GlowHdr 배로 1 을 넘고, 원화·바닥·UI 는 1 이하라 문턱 1 에 안 걸린다.
    // 혈우 장막 쪽으로 밀릴수록(ScaleTestDriver.PushStrength) Bloom·Vignette 가 Rain 값까지 오른다.
    // 효과 간소화(FxSettings.Simplified)면 Bloom 을 끈다. MapLayer 가 따로 깔던 판 비네트는 이게 붙으면 끈다(하나로 합침).
    [DisallowMultipleComponent]
    public sealed class PostFinish : MonoBehaviour
    {
        [Header("Bloom")]
        public float bloomThreshold = 1f;
        public float bloomIntensity = 0.35f;
        public float bloomIntensityRain = 0.6f;
        [Range(0f, 1f)] public float bloomScatter = 0.55f;

        [Header("Vignette")]
        public Color vignetteColor = new Color32(0x1C, 0x1A, 0x17, 0xFF);
        [Range(0f, 1f)] public float vignetteIntensity = 0.22f;
        [Range(0f, 1f)] public float vignetteIntensityRain = 0.3f;
        [Range(0.01f, 1f)] public float vignetteSmoothness = 0.45f;

        Volume _volume;
        VolumeProfile _profile;
        Bloom _bloom;
        Vignette _vignette;
        Camera _cam;

        public float BloomNow => _bloom != null && _bloom.active ? _bloom.intensity.value : 0f;
        public float VignetteNow => _vignette != null ? _vignette.intensity.value : 0f;

        void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "화면 마감(실행 중)";
            _bloom = _profile.Add<Bloom>(true);
            _bloom.threshold.Override(bloomThreshold);
            _bloom.intensity.Override(bloomIntensity);
            _bloom.scatter.Override(bloomScatter);
            _bloom.highQualityFiltering.Override(false);
            _vignette = _profile.Add<Vignette>(true);
            _vignette.color.Override(vignetteColor);
            _vignette.intensity.Override(vignetteIntensity);
            _vignette.smoothness.Override(vignetteSmoothness);
            _vignette.rounded.Override(true);

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.sharedProfile = _profile;
        }

        void Start()
        {
            foreach (var ml in FindObjectsByType<MapLayer>(FindObjectsSortMode.None)) ml.vignette = 0f;
        }

        void LateUpdate()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
                _cam.allowHDR = true;
                var data = _cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = true;
            }

            float k = RainDepth();
            _bloom.active = !FxSettings.Simplified;
            _bloom.intensity.value = Mathf.Lerp(bloomIntensity, bloomIntensityRain, k);
            _vignette.intensity.value = Mathf.Lerp(vignetteIntensity, vignetteIntensityRain, k);
        }

        BloodCurtain _curtain;
        PlayerController _player;

        /// <summary>장막 안으로 들어간 깊이 0~1. 시험 드라이버가 있으면 Core PushStrength,
        /// Run 씬에선 장막이 보이는 반지름의 80% 부터 오르기 시작해 장막 가장자리(몸이 더 못 나가는 곳)에서 1(화면 값, 기획 숫자 아님).</summary>
        float RainDepth()
        {
            var drv = ScaleTestDriver.Instance;
            if (drv != null) return Mathf.Clamp01(drv.PushStrength);
            if (_curtain == null) _curtain = FindFirstObjectByType<BloodCurtain>();
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
            if (_curtain == null || _player == null || _curtain.ShownRadius <= 0f) return 0f;
            float d = Vector2.Distance(_player.transform.position, _curtain.birthplace), r = _curtain.ShownRadius;
            return Mathf.Clamp01((d - r * 0.8f) / (r * 0.2f));
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }
    }
}
