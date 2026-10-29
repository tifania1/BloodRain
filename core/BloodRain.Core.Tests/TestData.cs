using System.Collections.Generic;
using System.IO;
using BloodRain.Core.Data;

namespace BloodRain.Core.Tests
{
    /// <summary>Unity가 읽는 바로 그 JSON 파일을 읽는다.</summary>
    public static class TestData
    {
        static GameData? _cached;

        public static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "unity")))
                    dir = dir.Parent;
                if (dir == null) throw new DirectoryNotFoundException("저장소 루트(unity 폴더)를 찾지 못했습니다");
                return dir.FullName;
            }
        }

        public static string DataDir => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Resources", "Data");

        /// <summary>저장소 안 다른 파일(기획서, 웹 프로토타입)을 읽는다. 경로는 저장소 루트 기준.</summary>
        public static string ReadRepoFile(params string[] parts) =>
            File.ReadAllText(Path.Combine(RepoRoot, Path.Combine(parts)));

        /// <summary>기획서·프로토타입 글과 비교할 때 쓰는 숫자 표기. 15, 0.04, 1.5 처럼 쓸데없는 0을 뺀다.</summary>
        public static string Num(float v) => System.Math.Round(v, 4).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        public static GameData Load()
        {
            if (_cached != null) return _cached;
            var files = new Dictionary<string, string>();
            foreach (var name in GameData.FileNames)
                files[name] = File.ReadAllText(Path.Combine(DataDir, name + ".json"));
            return _cached = GameData.FromJson(files);
        }
    }
}
