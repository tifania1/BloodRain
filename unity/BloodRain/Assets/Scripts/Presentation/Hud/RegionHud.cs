using BloodRain.Core.World;
using BloodRain.Sim;
using TMPro;
using UnityEngine;

namespace BloodRain.Presentation.Hud
{
    // 전투 HUD 위 가운데(TU-09, 기획서 12장 '전투 HUD 배치'): 경과 시간, 그 아래 지금 지역 이름(한글·한자)과 혈운 농도.
    // 서 있는 지역과 농도는 Core Regions(Nearest·Density)가 정한다. 지역 사이에선 가장 가까운 두 지역 값을 섞은 농도.
    // 같은 농도를 화면 혈우(ScreenRain)에도 넘긴다. 농도 2 이상이면 웹 시안처럼 주홍 굵은 글씨.
    public sealed class RegionHud : MonoBehaviour
    {
        [Tooltip("플레이어. 비우면 ScaleTestDriver 의 플레이어")]
        public Transform target;
        public TMP_Text clockText;
        public TMP_Text regionText;
        public ScreenRain rain;
        [Tooltip("모은 봉인석 수. 봉마곡은 다 모이기 전엔 지역에도 농도에도 안 든다")]
        public int sealStones;
        [Tooltip("0 이상이면 이 값을 경과 초로 쓴다(시험용). 음수면 씬을 연 뒤 흐른 시간")]
        public float elapsedOverride = -1f;

        const string CrimsonHex = "#B3261E";

        Regions _regions;
        string _shownId;
        int _shownDensity10 = int.MinValue, _shownSeconds = -1;

        public string RegionId => _shownId;
        public float Density { get; private set; } = 1f;

        void Awake() => _regions = new Regions(DataBoot.Data);

        void LateUpdate()
        {
            var t = target != null ? target : (ScaleTestDriver.Instance != null ? ScaleTestDriver.Instance.Player : null);
            if (t != null)
            {
                var world = _regions.ClampToWorld(WorldCoords.FromUnity(t.position));
                var region = _regions.Nearest(world, sealStones);
                Density = _regions.Density(world, sealStones);
                int d10 = Mathf.RoundToInt(Density * 10f);
                if (region.Id != _shownId || d10 != _shownDensity10)
                {
                    _shownId = region.Id;
                    _shownDensity10 = d10;
                    if (regionText != null) regionText.text = RegionLine(region.NameKo, region.Hanja, Density);
                }
            }
            if (rain != null) rain.density = Density;

            int sec = Mathf.FloorToInt(elapsedOverride >= 0f ? elapsedOverride : Time.timeSinceLevelLoad);
            if (sec != _shownSeconds && clockText != null)
            {
                _shownSeconds = sec;
                clockText.text = $"{sec / 60}:{sec % 60:00}";
            }
        }

        /// <summary>"청운진 靑雲鎭 · 혈운 0.8". 농도 2 이상은 주홍 굵게(웹 시안 .thick).</summary>
        public static string RegionLine(string nameKo, string hanja, float density)
        {
            string dens = $"혈운 {density:0.0}";
            if (density >= 2f) dens = $"<color={CrimsonHex}><b>{dens}</b></color>";
            string han = string.IsNullOrEmpty(hanja) ? "" : $" <size=80%><alpha=#99>{hanja}<alpha=#FF></size>";
            return $"{nameKo}{han}  ·  {dens}";
        }
    }
}
