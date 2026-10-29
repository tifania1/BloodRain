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

        public static GameData Load() => _cached ??= GameData.FromJson(ReadFiles());

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
