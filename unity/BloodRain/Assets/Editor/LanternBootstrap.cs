using BloodRain.Presentation;
using BloodRain.Presentation.Fx;
using BloodRain.Presentation.Meta;
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

        // QA-02U 시험: 생성 디렉터 silver_lantern 사건을 기다리지 않고 은빛 등롱 하나(1회차 혼이면 안 놓임)
        [MenuItem("BloodRain/Test/은빛 등롱 놓기(플레이 중)")]
        public static void PlaceSilver()
        {
            var l = LanternLayer.Instance;
            if (l == null) { Debug.LogWarning("[LanternBootstrap] Run 씬 플레이 중에만 됩니다"); return; }
            Debug.Log(l.PlaceSilver() ? "[LanternBootstrap] 은빛 등롱을 놓음" : "[LanternBootstrap] 의서가 안 나오는 생(1회차)이라 은빛 등롱을 놓지 않음");
        }

        [MenuItem("BloodRain/Test/은빛 등롱 놓기(플레이 중)", true)]
        static bool PlaceSilverValid() => Application.isPlaying;

        // QA-02U 시험: 저장된 혼을 한 번 회귀시킨다(혈월 천하). 원래 저장은 soul.before_regress.json 으로 남긴다.
        [MenuItem("BloodRain/Test/혼 회귀시키기")]
        public static void RegressSoul()
        {
            var data = DataBoot.Data;
            var soul = SoulStore.Load(data);
            if (SoulStore.HasSoul) System.IO.File.Copy(SoulStore.SoulPath, System.IO.Path.Combine(SoulStore.Dir, "soul.before_regress.json"), true);
            soul.Regress();
            SoulStore.Save(soul);
            Debug.Log($"[LanternBootstrap] 혼 회귀 → {soul.Cycle}회차(혈월 {soul.IsBloodMoon}) · 저장 {SoulStore.SoulPath}");
        }

        [MenuItem("BloodRain/Test/혼 회귀시키기", true)]
        static bool RegressSoulValid() => !Application.isPlaying;
    }
}
