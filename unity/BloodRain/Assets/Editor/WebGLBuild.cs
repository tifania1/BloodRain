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
    // 올리기는 저장소 루트에서 python tools/split_webgl_data.py(data 가 Cloudflare Pages 25MiB 상한을 넘으면 조각으로 나눔, 2026-11-09)
    //            → web/_headers 를 결과 폴더에 넣고
    //            npx wrangler pages deploy build/WebGL/BloodRain --project-name=bloodrain --branch=dev
    public static class WebGLBuild
    {
        [MenuItem("BloodRain/WebGL 빌드")]
        public static void Build() => Build(false);

        /// <summary>V-04d: 브라우저에서 멈춘 함수 이름을 보려고 Development + 예외 전체 스택으로 빌드한다. 설정은 끝나면 되돌린다.</summary>
        [MenuItem("BloodRain/WebGL 빌드(개발·스택 추적)")]
        public static void BuildDevelopment() => Build(true);

        /// <summary>V-04d: 개발 빌드는 멀쩡하고 릴리스만 멈춰서, 릴리스 그대로에 함수 이름(심볼)만 넣어 빌드한다.</summary>
        [MenuItem("BloodRain/WebGL 빌드(릴리스·함수 이름)")]
        public static void BuildReleaseSymbols() => Build(false, true);

        static void Build(bool development, bool symbols = false)
        {
            var oldEx = PlayerSettings.WebGL.exceptionSupport;
            var oldSym = PlayerSettings.WebGL.debugSymbolMode;
            if (development) PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            if (symbols) PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Embedded;
            try { BuildCore(development); }
            finally { PlayerSettings.WebGL.exceptionSupport = oldEx; PlayerSettings.WebGL.debugSymbolMode = oldSym; }
        }

        static void BuildCore(bool development)
        {
            // 빌드 설정 순서 그대로(Boot → Run → Inn). Boot 에 타이틀(TT-01U)이 생겨 웹에서도 타이틀부터 연다(2026-11-09 WG-01U).
            // 11-01 까지는 장면 흐름이 없어 Run 부터 열었다.
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/WebGL/BloodRain"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None,
            });
            var s = report.summary;
            UnityEngine.Debug.Log($"[WebGLBuild] {(development ? "개발 " : "")}{(PlayerSettings.WebGL.debugSymbolMode == WebGLDebugSymbolMode.Embedded ? "심볼 " : "")}{s.result} · {s.totalSize / 1048576f:F1}MB · {s.totalTime} · 장면 {scenes.Length}개 → {outDir}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
