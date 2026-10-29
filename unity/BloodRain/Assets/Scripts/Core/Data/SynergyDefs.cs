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
        public string Desc = "";
        public bool Proto;
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
        /// <summary>판 밖 기록: 이 장들을 깬 적이 있어야 한다(worldmap.json chapter 값).</summary>
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
        /// <summary>나올 수 있는 장(worldmap.json chapter 값). 없으면 어느 장이든.</summary>
        public List<string>? Chapters;
        /// <summary>이 게임 시간(분)부터 나온다.</summary>
        public float MinMinute;
        /// <summary>그 땅에 문파가 있어야 나온다(고수의 유해).</summary>
        public bool NeedsChapterSchool;
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
