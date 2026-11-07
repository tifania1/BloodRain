#nullable enable
// 장경각 비급 도감(기획서 20장 '장경각 비급 도감과 발견 연출': 연환 16·진전 9·비전 8·기연 8, 모두 41쪽).
//   찾은 쪽은 그림과 이야기, 못 찾은 쪽은 '???'와 시구(비전은 밀서로 본 조건 한 줄이 더 붙는다).
//   찾은 쪽은 혼(Soul.Codex)에 남는다. 키는 웹 프로토타입 dex 와 같다: link:<id>, school:<id>, secret:<id>, enc:<id>.
//   글(이름·이야기·시구)은 Unity 가 synergies 이름 키로 얹는다. 여기서는 어느 쪽을 찾았는지만.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public enum CodexGroup { Link, School, Secret, Encounter }

    public static class CodexKey
    {
        public static string Link(string id) => "link:" + id;
        public static string School(string id) => "school:" + id;
        public static string Secret(string id) => "secret:" + id;
        public static string Encounter(string id) => "enc:" + id;

        public static string Of(CodexGroup g, string id) => g switch
        {
            CodexGroup.Link => Link(id),
            CodexGroup.School => School(id),
            CodexGroup.Secret => Secret(id),
            _ => Encounter(id),
        };

        /// <summary>데이터에 있는 쪽의 키인가.</summary>
        public static bool IsValid(SynergyDefs s, string key) => Codex.Ids(s).Any(p => Of(p.group, p.id) == key);
    }

    /// <summary>도감 한 쪽. LetterSeen: 비전을 못 찾았어도 하오문 밀서로 조건을 봤나(Soul.Letters).</summary>
    public readonly struct CodexPage
    {
        public readonly CodexGroup Group;
        public readonly string Id;
        public readonly bool Found;
        public readonly bool LetterSeen;
        public CodexPage(CodexGroup group, string id, bool found, bool letterSeen) { Group = group; Id = id; Found = found; LetterSeen = letterSeen; }
        public string Key => CodexKey.Of(Group, Id);
    }

    public static class Codex
    {
        /// <summary>도감 차례(연환 → 진전 → 비전 → 기연, 그 안은 데이터 차례). 웹 renderDex 와 같다.</summary>
        public static IEnumerable<(CodexGroup group, string id)> Ids(SynergyDefs s) =>
            s.Links.Select(x => (CodexGroup.Link, x.Id))
                .Concat(s.Schools.Select(x => (CodexGroup.School, x.Id)))
                .Concat(s.Secrets.Select(x => (CodexGroup.Secret, x.Id)))
                .Concat(s.Encounters.Select(x => (CodexGroup.Encounter, x.Id)));

        public static List<CodexPage> Pages(Soul soul) =>
            Ids(soul.Data.Synergies).Select(p => new CodexPage(p.group, p.id,
                soul.Codex.Contains(CodexKey.Of(p.group, p.id)),
                p.group == CodexGroup.Secret && soul.Letters.Contains(p.id))).ToList();

        /// <summary>무리마다 (찾은 수, 모두). 도감 머리의 '찾은 비급 n / 41' 과 무리 제목 옆 숫자.</summary>
        public static Dictionary<CodexGroup, (int found, int total)> Counts(Soul soul) =>
            Pages(soul).GroupBy(p => p.Group).ToDictionary(g => g.Key, g => (g.Count(p => p.Found), g.Count()));

        /// <summary>도감에서 찾은 비전 id. 하오문 밀서(Soul.ReadLetter·Life.ReadLetter)의 '못 찾은 비전' 셈에 넘긴다.</summary>
        public static IEnumerable<string> FoundSecrets(Soul soul) =>
            soul.Data.Synergies.Secrets.Where(x => soul.Codex.Contains(CodexKey.Secret(x.Id))).Select(x => x.Id);
    }
}
