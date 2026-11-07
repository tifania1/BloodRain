#nullable enable
// synergies.json 구조(기획서 '20. 시너지와 기연'). 연환·진전·기본의 극·비전·기연 사건·기연 능력치.
// 판정은 Build/Synergy.cs, Build/Secrets.cs, Spawning/Encounters.cs 가 한다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    /// <summary>연환: 공격 무공 둘이 모두 MinLevel 이상이면 켜진다.</summary>
    public sealed class LinkDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        public string A = "";
        public string B = "";
        public int MinLevel = 4;
        /// <summary>base(늘 열림) 또는 secret(비급 층: 장경각에서 복원해야 켜진다, 기획서 20장).</summary>
        public string Tier = SynergyDefs.BaseTier;
        public bool IsSecret => Tier == SynergyDefs.SecretTier;
        /// <summary>비급 복원 id. 연환은 연환 id 그대로.</summary>
        public string ManualId => Id;
        public bool Proto;
        public string Desc = "";
        public string Lore = "";

        public bool Has(string weaponId) => A == weaponId || B == weaponId;
        /// <summary>짝 무공. 이 연환에 없는 무공이면 null.</summary>
        public string? Partner(string weaponId) => A == weaponId ? B : B == weaponId ? A : null;
    }

    public sealed class SchoolTierDef
    {
        public int Count;
        /// <summary>base 또는 secret(비급 층). 잠긴 단계는 건너뛰고 그 위 단계는 그대로 켜진다.</summary>
        public string Tier = SynergyDefs.BaseTier;
        public bool IsSecret => Tier == SynergyDefs.SecretTier;
        public string Desc = "";
        public bool Proto;
        /// <summary>이 단계에 닿으면 보갑 강화가 이만큼 더(장강수로채 4단계 1). Build.Chest.ExtraUpgrades 가 더한다.</summary>
        public int ChestUpgrades;
        /// <summary>이 단계에 닿으면 태극 구간이 ±이만큼으로 넓어진다(무당 2단계 35). 몸이 원래 더 넓으면(운화 40) 그대로 둔다.</summary>
        public int? HarmonyRange;
        /// <summary>이 단계에 닿으면 더하는 능력치(StatKind 이름 → 값, 비율은 0.15 = 15%). 닿은 단계까지 모두 더한다.
        /// 조건이 붙거나 곱하는 효과(개방 3단계, 마교 2단계)는 여기 없고 desc 에만 있다.</summary>
        public Dictionary<string, float> Stats = new Dictionary<string, float>();
        /// <summary>능력치 덧셈으로 안 되는 효과 이름(Build/SynergyEffects.cs 가 읽는다). 없으면 null.</summary>
        public string? Effect;
        public Dictionary<string, float> Params = new Dictionary<string, float>();

        public float Param(string key)
        {
            if (!Params.TryGetValue(key, out var v))
                throw new KeyNotFoundException($"진전 단계 '{Desc}' 에 {key} 값이 없습니다");
            return v;
        }
    }

    /// <summary>진전: 같은 문파 무공·심법을 몇 개 모았는지로 단계가 정해진다.</summary>
    public sealed class SchoolDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        public string Motto = "";
        public string? Character;
        public List<string> Members = new List<string>();
        public List<SchoolTierDef> Tiers = new List<SchoolTierDef>();

        /// <summary>가장 높은 단계의 개수. 이만큼 모으면 더 채울 것이 없다.</summary>
        public int MaxTierCount => Tiers.Count == 0 ? 0 : Tiers.Max(t => t.Count);
    }

    /// <summary>기본의 극: 삼재검법이 AtLevel 이상이면 진전을 하나 더 채워 준다.</summary>
    public sealed class CommonDef
    {
        public string Weapon = "";
        public int AtLevel = 8;
        public string NameKo = "";
        public string Desc = "";
        /// <summary>어느 문파에 더하나. best(가장 많이 모은 문파 하나, 기획서 그대로) 또는 all(모든 문파).</summary>
        public string Target = "best";
    }

    /// <summary>비전 조건을 판정용으로 옮긴 것. 글로 된 condition 과 같은 내용이어야 한다.</summary>
    public sealed class SecretRequireDef
    {
        /// <summary>이 캐릭터로 해야 한다(없으면 누구든).</summary>
        public string? Character;
        /// <summary>이 문파 몸으로 태어나야 한다(없으면 누구든). 환생한 몸의 CharacterDef Id 가 문파 id 다(Meta.Rebirth.CharacterOf).</summary>
        public string? Sect;
        /// <summary>판 밖 기록: 이 장들을 깬 적이 있어야 한다(cheonha.json chapter 값).</summary>
        public List<string> Cleared = new List<string>();
        /// <summary>무공·심법 id → 최소 레벨. 1이면 지니고만 있으면 된다. 절기가 된 무공은 최고 단계로 친다.</summary>
        public Dictionary<string, int> Levels = new Dictionary<string, int>();
        /// <summary>가진 공격 무공이 이것 하나뿐이어야 한다.</summary>
        public string? OnlyWeapon;
        /// <summary>판 기록 수치 이름(RunCounters.Names) → 최소값. Event 가 있으면 그 사건이 난 순간의 값으로 본다.</summary>
        public Dictionary<string, double> Counts = new Dictionary<string, double>();
        public SecretEventDef? Event;
        public SecretHoldDef? Hold;
    }

    /// <summary>사건(보갑 열기, 정신부 쓰기 …)이 다른 조건을 갖춘 채로 일어나야 열리는 비전.</summary>
    public sealed class SecretEventDef
    {
        /// <summary>talisman, chest, silverLantern.</summary>
        public string Kind = "";
        /// <summary>그 순간 기혈 비율이 이 값 이하여야 한다.</summary>
        public float? MaxHpRatio;
        /// <summary>조건을 갖춘 채 몇 번 일어나야 하나.</summary>
        public int Times = 1;
    }

    /// <summary>다른 조건을 갖춘 채로 판 기록 수치(연속 시간)가 Seconds 이상 이어져야 한다.</summary>
    public sealed class SecretHoldDef
    {
        public string Counter = "";
        public float Seconds;
    }

    public sealed class SecretDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        public bool Proto;
        public string Condition = "";
        public string Reward = "";
        public List<string> Hint = new List<string>();
        public SecretRequireDef Require = new SecretRequireDef();
        /// <summary>얻는 것을 코드가 읽는 이름(Build/SecretEffects.Known). reward 글의 숫자는 Params 에.</summary>
        public string Effect = "";
        /// <summary>효과가 붙는 무공 id(낙매검선의 피 낙화검우, 매화삼롱 음공, 무명검 삼재검법). 없으면 null.</summary>
        public string? Target;
        public Dictionary<string, float> Params = new Dictionary<string, float>();

        public float Param(string key) => Params.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"비전 {Id} params 에 {key} 가 없습니다");
    }

    /// <summary>기연 사건. Where 는 글, Chapters·MinMinute·NeedsChapterSchool 은 그 글을 규칙으로 옮긴 것.</summary>
    public sealed class EncounterDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Where = "";
        public string How = "";
        public string Reward = "";
        /// <summary>한 판에서 한 번이라도 나올 기본 확률.</summary>
        public double Chance;
        public bool Proto;
        /// <summary>나올 수 있는 장(cheonha.json chapter 값). 없으면 어느 장이든.</summary>
        public List<string>? Chapters;
        /// <summary>이 게임 시간(분)부터 나온다.</summary>
        public float MinMinute;
        /// <summary>그 땅에 문파가 있어야 나온다(고수의 유해).</summary>
        public bool NeedsChapterSchool;
        /// <summary>보상·조건 숫자(검총 mightPerLevel, 속삭임 healingMultiplier 등). 기획서 20장 글의 숫자만.</summary>
        public Dictionary<string, float>? Params;
        /// <summary>판 위에 언제·어디 놓고 몇 m 에서 닿나(웹 프로토타입 값, Spawning.EncounterSpots). 없으면 놓는 자리가 없는 사건(벼랑).</summary>
        public EncounterSpotDef? Spot;

        public float Param(string key) => Params != null && Params.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"{Id} params 에 {key} 가 없습니다");
    }

    /// <summary>기연 사건을 판에 놓는 값(synergies.json encounters[].spot). 길이는 경지 m, 시간은 게임 분·초. 웹 프로토타입에서 돌던 값.</summary>
    public sealed class EncounterSpotDef
    {
        /// <summary>나올 때를 이 분~AppearToMinute 사이에서 고르게 뽑는다.</summary>
        public float AppearFromMinute;
        public float AppearToMinute;
        /// <summary>플레이어 둘레 이 거리 고리에 놓는다. 0 이면 땅에 놓지 않는 사건(밀서는 등롱 속, 영물은 금두꺼비 고리).</summary>
        public float RingRadius;
        /// <summary>이 거리 안이면 닿았다(머물기·예·줍기·석상 앞·무덤에 서기).</summary>
        public float ReachRadius;
        /// <summary>한 번 닿은 뒤 이 거리 밖으로 가면 떠났다(속삭임 거절, 검총 다시 뜨기). 0 이면 없음.</summary>
        public float LeaveRadius;
        /// <summary>닿아 쌓던 초가 벗어나면 초당 이만큼 준다(은거기인 0.5, 유해 1). 0 이면 안 준다.</summary>
        public float DecayPerSecond;
    }

    /// <summary>비급 복원 값. n번째 복원(0부터)은 sealStones[n]·rebirthQi[n], 목록보다 많으면 마지막 값.</summary>
    public sealed class SecretManualsDef
    {
        public List<int> SealStones = new List<int>();
        public List<int> RebirthQi = new List<int>();

        static int At(List<int> list, int n) => list.Count == 0 ? 0 : list[Math.Min(Math.Max(n, 0), list.Count - 1)];
        public int SealStonesFor(int restoredBefore) => At(SealStones, restoredBefore);
        public int RebirthQiFor(int restoredBefore) => At(RebirthQi, restoredBefore);
    }

    public sealed class LuckDef
    {
        /// <summary>기연 능력치 1당 사건 확률에 더하는 값(곱이 아니라 더하기).</summary>
        public double ChancePerLuck = 0.005;
    }

    public sealed class SynergyDefs
    {
        public List<LinkDef> Links = new List<LinkDef>();
        public List<SchoolDef> Schools = new List<SchoolDef>();
        public CommonDef Common = new CommonDef();
        public List<SecretDef> Secrets = new List<SecretDef>();
        public List<EncounterDef> Encounters = new List<EncounterDef>();
        public LuckDef Luck = new LuckDef();
        public SecretManualsDef SecretManuals = new SecretManualsDef();

        public const string BaseTier = "base";
        public const string SecretTier = "secret";

        /// <summary>진전 단계의 비급 복원 id: "문파id:개수"(개방 3단계 = gaebang:3).</summary>
        public static string ManualIdOf(SchoolDef school, SchoolTierDef tier) => school.Id + ":" + tier.Count;

        /// <summary>비급 층에 든 모든 것의 복원 id(연환 먼저, 데이터 순서).</summary>
        public IEnumerable<string> SecretManualIds =>
            Links.Where(l => l.IsSecret).Select(l => l.ManualId)
                .Concat(Schools.SelectMany(sc => sc.Tiers.Where(t => t.IsSecret).Select(t => ManualIdOf(sc, t))));

        public bool IsSecretManual(string id) => SecretManualIds.Contains(id);

        Dictionary<string, SchoolDef>? _schoolOf;

        /// <summary>무공·심법이 속한 문파. 삼재검법처럼 어디에도 없으면 null.</summary>
        public SchoolDef? SchoolOf(string id)
        {
            if (_schoolOf == null)
            {
                var map = new Dictionary<string, SchoolDef>();
                foreach (var sc in Schools)
                    foreach (var m in sc.Members) map[m] = sc;
                _schoolOf = map;
            }
            return _schoolOf.TryGetValue(id, out var s) ? s : null;
        }

        public SchoolDef School(string id) => Schools.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"없는 문파: {id}");
        public LinkDef Link(string id) => Links.FirstOrDefault(l => l.Id == id) ?? throw new ArgumentException($"없는 연환: {id}");
        public SecretDef Secret(string id) => Secrets.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"없는 비전: {id}");
        public EncounterDef Encounter(string id) => Encounters.FirstOrDefault(e => e.Id == id) ?? throw new ArgumentException($"없는 기연 사건: {id}");
    }
}
