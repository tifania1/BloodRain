using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // C-09b 시험: 등롱을 기다리지 않고 플레이어 둘레에 열 개 놓는다. LanternLayer 는 LifeRunner 가 플레이 때 붙인다.
    public static class LanternBootstrap
    {
        [MenuItem("BloodRain/Test/등롱 놓기(플레이 중)")]
        public static void PlaceLanterns()
        {
            var l = LanternLayer.Instance;
            if (l == null || LifeRunner.Instance == null) { Debug.LogWarning("[LanternBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            l.PlaceRing(10);
            Debug.Log("[LanternBootstrap] 등롱 열 개를 놓음(둘레 2m, 삼재검법 사거리 안)");
        }

        [MenuItem("BloodRain/Test/등롱 놓기(플레이 중)", true)]
        static bool PlaceLanternsValid() => Application.isPlaying;
    }
}
