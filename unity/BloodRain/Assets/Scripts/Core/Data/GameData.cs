#nullable enable
// JSON 문자열 묶음을 받아 정의 객체로 만든다. 파일을 여는 일은 호출하는 쪽이 한다
// (Unity: Resources.Load<TextAsset>, 테스트: File.ReadAllText).
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BloodRain.Core.Data
{
    public sealed class GameData
    {
        public IReadOnlyList<WeaponDef> Weapons { get; private set; } = Array.Empty<WeaponDef>();
        public IReadOnlyList<PassiveDef> Passives { get; private set; } = Array.Empty<PassiveDef>();
        public IReadOnlyList<EvolutionDef> Evolutions { get; private set; } = Array.Empty<EvolutionDef>();
        public IReadOnlyList<CharacterDef> Characters { get; private set; } = Array.Empty<CharacterDef>();
        public IReadOnlyList<UltimateDef> Ultimates { get; private set; } = Array.Empty<UltimateDef>();
        public IReadOnlyList<EnemyDef> Enemies { get; private set; } = Array.Empty<EnemyDef>();
        public IReadOnlyList<BossDef> Bosses { get; private set; } = Array.Empty<BossDef>();
        public BalanceDef Balance { get; private set; } = new BalanceDef();
        public SynergyDefs Synergies { get; private set; } = new SynergyDefs();
        /// <summary>장 이름(cheonha.json regions[].chapter) → 첫째·둘째·셋째 정예 id. enemies.json elites. 지역으로 찾을 때는 Spawning.RegionSpawns.</summary>
        public IReadOnlyDictionary<string, List<string>> Elites { get; private set; } = new Dictionary<string, List<string>>();
        public IReadOnlyList<InsightDef> Insights { get; private set; } = Array.Empty<InsightDef>();
        public InsightChoicesDef InsightChoices { get; private set; } = new InsightChoicesDef();
        public IReadOnlyList<ChapterDef> Chapters { get; private set; } = Array.Empty<ChapterDef>();
        /// <summary>천하 한 장과 환혼(cheonha.json, 기획서 7·10·21장).</summary>
        public CheonhaDef Cheonha { get; private set; } = new CheonhaDef();
        /// <summary>보스 id → 땅의 주인 패턴 시간표(bosspatterns.json, 기획서 6장).</summary>
        public IReadOnlyDictionary<string, BossPatternSetDef> BossPatterns { get; private set; } = new Dictionary<string, BossPatternSetDef>();
        /// <summary>지역 기믹 수치(gimmicks.json, 기획서 7장).</summary>
        public GimmicksDef Gimmicks { get; private set; } = new GimmicksDef();

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        sealed class WeaponFile { public List<WeaponDef> Weapons = new List<WeaponDef>(); }
        sealed class PassiveFile { public List<PassiveDef> Passives = new List<PassiveDef>(); }
        sealed class EvolutionFile { public List<EvolutionDef> Evolutions = new List<EvolutionDef>(); }
        sealed class CharacterFile { public List<CharacterDef> Characters = new List<CharacterDef>(); public List<UltimateDef> Ultimates = new List<UltimateDef>(); }
        sealed class EnemyFile { public List<EnemyDef> Enemies = new List<EnemyDef>(); public Dictionary<string, List<string>> Elites = new Dictionary<string, List<string>>(); public List<BossDef> Bosses = new List<BossDef>(); }
        sealed class InsightFile { public InsightChoicesDef Choices = new InsightChoicesDef(); public List<InsightDef> Insights = new List<InsightDef>(); }
        sealed class ChapterFile { public List<ChapterDef> Chapters = new List<ChapterDef>(); }

        /// <summary>파일 이름(확장자 없이) → JSON 문자열. 받아야 하는 파일은 FileNames.</summary>
        public static GameData FromJson(IReadOnlyDictionary<string, string> files)
        {
            T Read<T>(string name) where T : new()
            {
                if (!files.TryGetValue(name, out var text))
                    throw new InvalidOperationException($"데이터 파일이 없습니다: {name}.json");
                return JsonConvert.DeserializeObject<T>(text, Settings) ?? new T();
            }

            var characters = Read<CharacterFile>("characters");
            var enemies = Read<EnemyFile>("enemies");
            var insights = Read<InsightFile>("insights");
            return new GameData
            {
                Weapons = Read<WeaponFile>("weapons").Weapons,
                Passives = Read<PassiveFile>("passives").Passives,
                Evolutions = Read<EvolutionFile>("evolutions").Evolutions,
                Characters = characters.Characters,
                Ultimates = characters.Ultimates,
                Enemies = enemies.Enemies,
                Bosses = enemies.Bosses,
                Elites = enemies.Elites,
                Balance = Read<BalanceDef>("balance"),
                Synergies = Read<SynergyDefs>("synergies"),
                Insights = insights.Insights,
                InsightChoices = insights.Choices,
                Chapters = Read<ChapterFile>("chapters").Chapters,
                Cheonha = Read<CheonhaDef>("cheonha"),
                BossPatterns = Read<BossPatternFile>("bosspatterns").Bosses,
                Gimmicks = Read<GimmicksDef>("gimmicks"),
            };
        }

        public static readonly string[] FileNames = { "weapons", "passives", "evolutions", "characters", "enemies", "balance", "synergies", "insights", "chapters", "cheonha", "bosspatterns", "gimmicks" };

        public WeaponDef Weapon(string id) => Weapons.First(w => w.Id == id);
        public PassiveDef Passive(string id) => Passives.First(p => p.Id == id);
        public EnemyDef Enemy(string id) => Enemies.First(e => e.Id == id);
        public CharacterDef Character(string id) => Characters.First(c => c.Id == id);
        public ChapterDef Chapter(string id) => Chapters.First(c => c.Id == id);
        public InsightDef Insight(string id) => Insights.First(i => i.Id == id);
        public BossPatternSetDef BossPatternSet(string bossId) =>
            BossPatterns.TryGetValue(bossId, out var set) ? set : throw new KeyNotFoundException($"패턴이 없는 보스입니다: {bossId}");
    }
}
