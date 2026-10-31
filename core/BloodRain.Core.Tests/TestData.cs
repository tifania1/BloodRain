using System.Collections.Generic;
using System.IO;
using System.Linq;
using BloodRain.Core.Build;
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
            ReadText(Path.Combine(RepoRoot, Path.Combine(parts)));

        /// <summary>기획서·프로토타입 글과 비교할 때 쓰는 숫자 표기. 15, 0.04, 1.5 처럼 쓸데없는 0을 뺀다.</summary>
        public static string Num(float v) => System.Math.Round(v, 4).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);


        public static GameData Load() => _cached ??= GameData.FromJson(ReadFiles());

        /// <summary>문서 파일을 읽고 줄바꿈을 \n 으로 맞춘다. PC(git autocrlf)에서 받은 사본은 \r\n 이라
        /// 줄 단위 정규식이 엇나가던 것(2026-10-31 PC 세션, ArtFrameTests).</summary>
        public static string ReadText(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        public static string Gdd => ReadText(Path.Combine(DataDir, "..", "..", "..", "..", "..", "docs", "design", "gdd.md"));

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

        /// <summary>GameData.FileNames 에 적힌 파일을 모두 읽는다. 하나라도 없으면 여기서 터진다.</summary>
        public static Dictionary<string, string> ReadFiles()
        {
            var files = new Dictionary<string, string>();
            foreach (var name in GameData.FileNames)
                files[name] = File.ReadAllText(Path.Combine(DataDir, name + ".json"));
            return files;
        }

        /// <summary>캐릭터로 판을 시작해 (id, 레벨)대로 무공·심법을 올린 로드아웃. 무공인지 심법인지는 데이터로 가린다.</summary>
        public static Loadout Build(string character, params (string id, int level)[] items)
        {
            var d = Load();
            var l = new Loadout(d, d.Character(character));
            foreach (var (id, level) in items)
            {
                bool weapon = d.Weapons.Any(w => w.Id == id);
                var owned = weapon ? l.Weapons : l.Passives;
                int cur = owned.TryGetValue(id, out var lv) ? lv : 0;
                if (cur == 0) { l.Apply(new Offer(weapon ? OfferKind.NewWeapon : OfferKind.NewPassive, id)); cur = 1; }
                for (; cur < level; cur++) l.Apply(new Offer(weapon ? OfferKind.UpgradeWeapon : OfferKind.UpgradePassive, id));
            }
            return l;
        }
    }
}
