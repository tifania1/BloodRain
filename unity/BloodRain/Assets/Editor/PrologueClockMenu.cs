using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace BloodRain.Editor
{
    // C-10b 서장 시계를 플레이 중에 앞으로 넘긴다. 사건(3:30 혈인 돌진·5:00 도부수·9:00 장삼)은 지난 시각을 보고 한 번씩 나오니
    // 시계만 옮기면 다음 초에 그 사건이 나온다. Life.Minutes 는 바깥에서 못 바꾸는 값이라 시험 메뉴에서만 리플렉션으로 넣는다.
    public static class PrologueClockMenu
    {
        const string Root = "BloodRain/Test/서장 시계 넘기기(플레이 중)/";

        [MenuItem(Root + "5분")]
        static void Five() => Set(5f);

        [MenuItem(Root + "8분30초")]
        static void EightHalf() => Set(8.5f);

        [MenuItem(Root + "8분59초(장삼 직전)")]
        static void BeforeBoss() => Set(8f + 59f / 60f);

        [MenuItem(Root + "5분", true)]
        [MenuItem(Root + "8분30초", true)]
        [MenuItem(Root + "8분59초(장삼 직전)", true)]
        static bool Playing() => Application.isPlaying;

        static void Set(float minutes)
        {
            var life = BloodRain.Presentation.LifeRunner.Instance != null ? BloodRain.Presentation.LifeRunner.Instance.Life : null;
            if (life == null || !life.IsPrologue) { Debug.LogWarning("[PrologueClock] 서장 몸이 아님"); return; }
            var f = typeof(BloodRain.Core.Run.Life).GetField("<Minutes>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) { Debug.LogWarning("[PrologueClock] Life.Minutes 자리를 못 찾음"); return; }
            if (life.Minutes >= minutes) { Debug.Log($"[PrologueClock] 이미 {life.Minutes:0.00}분"); return; }
            f.SetValue(life, minutes);
            Debug.Log($"[PrologueClock] 서장 시계 {minutes:0.00}분으로 · {life.Prologue.Step}");
        }
    }
}
