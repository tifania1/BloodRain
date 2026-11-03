using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 휴대폰용 '효과 간소화'(vfx.md 이펙트 단계 아래 줄): 반투명 겹침이 휴대폰 성능을 가장 많이 잡아먹으니
    // 켜면 담채 층을 그리지 않고 먹 자국·먹 튐 양을 3분의 1로 줄인다. 본 획·금빛·AI 재료는 그대로.
    // 값은 PlayerPrefs 에 남고, 한 번도 정한 적 없으면 휴대폰에서만 켜진 채로 시작한다. 설정 화면이 생기면 거기서 Simplified 를 바꾼다.
    public static class FxSettings
    {
        const string Key = "fx_simplified";
        static int _cached = -1;

        public static bool Simplified
        {
            get
            {
                if (_cached < 0) _cached = PlayerPrefs.GetInt(Key, Application.isMobilePlatform ? 1 : 0);
                return _cached == 1;
            }
            set
            {
                _cached = value ? 1 : 0;
                PlayerPrefs.SetInt(Key, _cached);
                PlayerPrefs.Save();
            }
        }

        /// <summary>간소화면 n 의 3분의 1(최소 1), 아니면 n.</summary>
        public static int Scale(int n) => Simplified ? Mathf.Max(1, n / 3) : n;
    }
}