// 시뮬레이터가 읽는 데이터. Core GameData 에 더해, weapons.json 에는 있지만 Core WeaponDef 에 아직 없는
// 값(타수·투사체 수·관통·초당 발수 등)과 balance.json 음양 편향·극성 피해 보너스를 따로 읽는다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Newtonsoft.Json.Linq;

namespace BloodRain.Sim
{
    /// <summary>무공 한 줄의 [Lv1, Lv8] 범위값들. 없으면 null.</summary>
    public sealed class WeaponExtras
    {
        public float[]? Hits;
        public float[]? Projectiles;
        public float[]? Pierce;
        public float[]? ShotsPerSecond;
        public float[]? Duration;
        public float[]? BurnPerSecond;
        public float? RangeLv8;
    }

    public sealed class SimData
    {
        public GameData Game { get; }
        public IReadOnlyDictionary<string, WeaponExtras> Extras { get; }
        public float BiasAspectBonus { get; }
        public float ExtremeAspectBonus { get; }
        /// <summary>balance.json xpCurve.targets(예: level15AtMinute 5) → (분, 레벨), 분 순서.</summary>
        public IReadOnlyList<(int Minute, int Level)> LevelTargets { get; }
        public string DataDir { get; }

        SimData(GameData game, IReadOnlyDictionary<string, WeaponExtras> extras, float bias, float extreme,
            IReadOnlyList<(int Minute, int Level)> targets, string dir)
        {
            Game = game; Extras = extras; BiasAspectBonus = bias; ExtremeAspectBonus = extreme; LevelTargets = targets; DataDir = dir;
        }

        /// <summary>데이터 폴더를 읽는다. 매번 새로 읽으므로 돌려받은 값을 고쳐도(조정 시험) 파일은 그대로다.</summary>
        public static SimData Load(string? dataDir = null)
        {
            string dir = dataDir ?? FindDataDir();
            var files = new Dictionary<string, string>();
            foreach (var name in GameData.FileNames)
                files[name] = File.ReadAllText(Path.Combine(dir, name + ".json"));
            var game = GameData.FromJson(files);

            var extras = new Dictionary<string, WeaponExtras>();
            foreach (var w in (JArray)JObject.Parse(files["weapons"])["weapons"]!)
            {
                var id = (string?)w["id"];
                if (id == null) continue;
                extras[id] = new WeaponExtras
                {
                    Hits = Range(w["hits"]),
                    Projectiles = Range(w["projectiles"]),
                    Pierce = Range(w["pierce"]),
                    ShotsPerSecond = Range(w["shotsPerSecond"]),
                    Duration = Range(w["duration"]),
                    BurnPerSecond = Range(w["burnPerSecond"]),
                    RangeLv8 = (float?)w["rangeLv8"],
                };
            }

            var balance = JObject.Parse(files["balance"]);
            var yy = balance["yinyang"];
            float bias = (float?)yy?["bias"]?["aspectDamageBonus"] ?? 0.15f;
            float extreme = (float?)yy?["extreme"]?["aspectDamageBonus"] ?? 0.40f;

            var targets = new List<(int Minute, int Level)>();
            if (balance["xpCurve"]?["targets"] is JObject t)
                foreach (var kv in t)
                {
                    var m = Regex.Match(kv.Key, @"^level(\d+)AtMinute$");
                    if (m.Success && kv.Value != null) targets.Add(((int)kv.Value, int.Parse(m.Groups[1].Value)));
                }
            targets.Sort((a, b) => a.Minute.CompareTo(b.Minute));
            return new SimData(game, extras, bias, extreme, targets, dir);
        }

        static float[]? Range(JToken? t)
        {
            if (t is not JArray a || a.Count != 2) return null;
            return new[] { (float)a[0], (float)a[1] };
        }

        /// <summary>현재 폴더나 실행 파일 위치에서 위로 올라가며 unity/BloodRain/Assets/Resources/Data 를 찾는다.</summary>
        public static string FindDataDir()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "unity", "BloodRain", "Assets", "Resources", "Data");
                    if (Directory.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new DirectoryNotFoundException("데이터 폴더(unity/BloodRain/Assets/Resources/Data)를 찾지 못했습니다. --data 로 알려 주세요.");
        }
    }
}
