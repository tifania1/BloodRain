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

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        sealed class WeaponFile { public List<WeaponDef> Weapons = new List<WeaponDef>(); }
        sealed class PassiveFile { public List<PassiveDef> Passives = new List<PassiveDef>(); }
        sealed class EvolutionFile { public List<EvolutionDef> Evolutions = new List<EvolutionDef>(); }
        sealed class CharacterFile { public List<CharacterDef> Characters = new List<CharacterDef>(); public List<UltimateDef> Ultimates = new List<UltimateDef>(); }
        sealed class EnemyFile { public List<EnemyDef> Enemies = new List<EnemyDef>(); public List<BossDef> Bosses = new List<BossDef>(); }

        /// <summary>파일 이름(확장자 없이) → JSON 문자열. weapons, passives, evolutions, characters, enemies, balance.</summary>
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
            return new GameData
            {
                Weapons = Read<WeaponFile>("weapons").Weapons,
                Passives = Read<PassiveFile>("passives").Passives,
                Evolutions = Read<EvolutionFile>("evolutions").Evolutions,
                Characters = characters.Characters,
                Ultimates = characters.Ultimates,
                Enemies = enemies.Enemies,
                Bosses = enemies.Bosses,
                Balance = Read<BalanceDef>("balance"),
            };
        }

        public static readonly string[] FileNames = { "weapons", "passives", "evolutions", "characters", "enemies", "balance" };

        public WeaponDef Weapon(string id) => Weapons.First(w => w.Id == id);
        public PassiveDef Passive(string id) => Passives.First(p => p.Id == id);
        public EnemyDef Enemy(string id) => Enemies.First(e => e.Id == id);
        public CharacterDef Character(string id) => Characters.First(c => c.Id == id);
    }
}
