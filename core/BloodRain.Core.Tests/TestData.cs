using System.Collections.Generic;
using System.IO;
using BloodRain.Core.Data;

namespace BloodRain.Core.Tests
{
    /// <summary>Unity가 읽는 바로 그 JSON 파일을 읽는다.</summary>
    public static class TestData
    {
        static GameData? _cached;

        public static string DataDir
        {
            get
            {
                var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "unity")))
                    dir = dir.Parent;
                if (dir == null) throw new DirectoryNotFoundException("저장소 루트(unity 폴더)를 찾지 못했습니다");
                return Path.Combine(dir.FullName, "unity", "BloodRain", "Assets", "Resources", "Data");
            }
        }

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
