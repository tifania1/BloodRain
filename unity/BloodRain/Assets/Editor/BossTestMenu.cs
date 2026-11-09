using BloodRain.Presentation.Combat;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // BR-01 땅의 주인을 플레이 중에 바로 불러 패턴을 본다(지역이 아직 잠들어 있어도 깨워서 싸움을 건다).
    public static class BossTestMenu
    {
        const string Root = "BloodRain/Test/땅의 주인 부르기(플레이 중)/";

        [MenuItem(Root + "wongak")]
        static void Wongak() => Call("wongak");

        [MenuItem(Root + "2막으로(남은 기혈 30%)")]
        static void Phase2()
        {
            var r = PatternRunner.Instance;
            if (r == null || !r.InFight) { Debug.LogWarning("[BossTest] 보스전 중이 아님"); return; }
            r.SetBossHealthRatio(r.Patterns.Def.Phase2At);
            Debug.Log($"[BossTest] 남은 기혈 {r.BossHealthRatio:0.00}");
        }

        [MenuItem(Root + "wongak", true)]
        [MenuItem(Root + "2막으로(남은 기혈 30%)", true)]
        static bool Playing() => Application.isPlaying;

        static void Call(string bossId)
        {
            var r = PatternRunner.Instance;
            if (r == null) { Debug.LogWarning("[BossTest] PatternRunner 없음(Run 씬을 플레이 중인가)"); return; }
            Debug.Log($"[BossTest] {bossId}: {r.ForceFight(bossId)}");
        }
    }
}
