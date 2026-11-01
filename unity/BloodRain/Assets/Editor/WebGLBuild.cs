using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BloodRain.Editor
{
    // WebGL 빌드. CI(game-ci)와 같은 자리(저장소 루트 build/WebGL/BloodRain)에 만든다.
    // PC 명령줄: Unity.exe -batchmode -quit -projectPath unity/BloodRain -buildTarget WebGL
    //            -executeMethod BloodRain.Editor.WebGLBuild.Build -logFile <로그>
    // 올리기는 저장소 루트에서 web/_headers 를 결과 폴더에 넣고
    //            npx wrangler pages deploy build/WebGL/BloodRain --project-name=bloodrain --branch=dev
    public static class WebGLBuild
    {
        [MenuItem("BloodRain/WebGL 빌드")]
        public static void Build()
        {
            // 아직 Boot → 객잔 → Run 장면 흐름(C-10)이 없어 Boot 로 열면 빈 종이만 보인다. 그때까지 웹 빌드는 Run 씬부터 연다.
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                .OrderBy(p => p.EndsWith("/Run.unity") ? 0 : 1).ToArray();
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/WebGL/BloodRain"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            var s = report.summary;
            UnityEngine.Debug.Log($"[WebGLBuild] {s.result} · {s.totalSize / 1048576f:F1}MB · {s.totalTime} · 장면 {scenes.Length}개 → {outDir}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
