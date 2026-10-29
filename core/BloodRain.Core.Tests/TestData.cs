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

        public static string Gdd => File.ReadAllText(Path.Combine(DataDir, "..", "..", "..", "..", "..", "docs", "design", "gdd.md"));

        /// <summary>기획서에서 "### 제목" 부터 다음 제목 전까지. 마크다운 이스케이프(\~)는 풀어 둔다.</summary>
        public static string GddSection(string heading)
        {
            var gdd = Gdd.Replace("\\~", "~");
            int start = gdd.IndexOf("### " + heading);
            if (start < 0) throw new KeyNotFoundException($"기획서에 '{heading}' 절이 없습니다");
            int end = gdd.IndexOf("\n#", start + 4);
            return end < 0 ? gdd.Substring(start) : gdd.Substring(start, end - start);
        }

        /// <summary>절 안 표의 본문 줄을 칸으로 나눈다(머리줄과 구분줄은 뺀다). 칸 앞뒤 공백은 지운다.</summary>
        public static List<string[]> GddTable(string section)
        {
            var rows = new List<string[]>();
            bool header = true;
            foreach (var line in section.Split('\n'))
            {
                if (!line.StartsWith("|")) { header = true; continue; }
                if (header) { header = false; continue; }
                if (line.StartsWith("| ---")) continue;
                var cells = line.Split('|');
                var trimmed = new string[cells.Length - 2];
                for (int i = 1; i < cells.Length - 1; i++) trimmed[i - 1] = cells[i].Trim();
                rows.Add(trimmed);
            }
            return rows;
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
