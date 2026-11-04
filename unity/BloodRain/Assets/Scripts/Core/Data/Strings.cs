#nullable enable
// 현지화 문구. 한국어 원문은 데이터 JSON(nameKo·desc 등)과 strings/ko.json(UI 문구)에 있고,
// 다른 언어는 Resources/Data/strings/<lang>.json 에 키 = 값으로 둔다(backlog L-01, 용어는 docs/design/glossary.md).
//
// 키 만드는 법(StringCatalog.FromData):
//   파일 이름 . 경로 . 필드. 목록 원소는 id 가 있으면 id, 없으면 0부터 센 자리. 필드 끝의 Ko 는 뗀다.
//   맨 앞 칸이 파일 이름과 같으면 한 번만 쓴다(weapons.json 의 weapons[] → weapons.<id>).
//   '_' 로 시작하는 칸(_note 등)은 기획 메모라 옮기지 않는다. 한글이 든 문자열만 옮길 문구로 친다(hanja 칸은 그대로 쓴다).
//   예) weapons.samjae_sword.name, weapons.samjae_sword.milestones.4, cheonha.regions.songshan.name,
//       bosspatterns.bosses.wongak.patterns.vajra_cone.name, synergies.secrets.mumyeong.hint.0
// UI 문구 키는 ui. 로 시작하고 한국어 원문은 strings/ko.json 에 있다. 값 안의 {0} {1} 은 string.Format 자리.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace BloodRain.Core.Data
{
    /// <summary>옮길 문구 전부(키 → 한국어 원문). 데이터 JSON 에서 뽑은 것 + strings/ko.json 의 UI 문구.</summary>
    public sealed class StringCatalog
    {
        /// <summary>strings 폴더(Resources/Data/strings) 안 파일 이름. 한국어 UI 원문.</summary>
        public const string KoreanFile = "ko";
        public const string UiPrefix = "ui.";

        readonly Dictionary<string, string> _ko;
        public IReadOnlyDictionary<string, string> Korean => _ko;

        StringCatalog(Dictionary<string, string> ko) { _ko = ko; }

        /// <summary>데이터 파일(GameData.FileNames, 이름 → JSON)과 strings/ko.json 내용으로 만든다.</summary>
        public static StringCatalog FromData(IReadOnlyDictionary<string, string> dataFiles, string koUiJson)
        {
            var ko = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in GameData.FileNames)
            {
                if (!dataFiles.TryGetValue(file, out var text))
                    throw new InvalidOperationException($"데이터 파일이 없습니다: {file}.json");
                Walk(JToken.Parse(text), file, file, ko, top: true);
            }
            foreach (var kv in ReadTable(koUiJson))
            {
                if (!kv.Key.StartsWith(UiPrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException($"strings/ko.json 에는 ui. 키만 둔다: {kv.Key}");
                Add(ko, kv.Key, kv.Value);
            }
            return new StringCatalog(ko);
        }

        static readonly Regex Hangul = new Regex("[가-힣]", RegexOptions.Compiled);

        static void Walk(JToken token, string file, string path, Dictionary<string, string> into, bool top)
        {
            switch (token)
            {
                case JObject obj:
                    foreach (var p in obj.Properties())
                    {
                        if (p.Name.StartsWith("_", StringComparison.Ordinal)) continue;
                        bool skip = top && p.Name == file && p.Value is JArray;
                        Walk(p.Value, file, skip ? path : path + "." + FieldName(p.Name), into, false);
                    }
                    break;
                case JArray arr:
                    for (int i = 0; i < arr.Count; i++)
                    {
                        var id = (arr[i] as JObject)?["id"];
                        var seg = id != null && id.Type == JTokenType.String ? (string)id! : i.ToString(CultureInfo.InvariantCulture);
                        Walk(arr[i], file, path + "." + seg, into, false);
                    }
                    break;
                case JValue v when v.Type == JTokenType.String:
                    var s = (string)v!;
                    if (Hangul.IsMatch(s)) Add(into, path, s);
                    break;
            }
        }

        /// <summary>nameKo → name. 끝이 Ko(대문자 K)일 때만 뗀다.</summary>
        public static string FieldName(string jsonName) =>
            jsonName.Length > 2 && jsonName.EndsWith("Ko", StringComparison.Ordinal) ? jsonName.Substring(0, jsonName.Length - 2) : jsonName;

        static void Add(Dictionary<string, string> into, string key, string value)
        {
            if (into.ContainsKey(key)) throw new InvalidOperationException($"현지화 키가 겹칩니다: {key}");
            into[key] = value;
        }

        /// <summary>strings/<lang>.json 을 키 → 값으로. '_' 로 시작하는 칸(_meta)은 뺀다. 값은 문자열만.</summary>
        public static Dictionary<string, string> ReadTable(string json)
        {
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in JObject.Parse(json).Properties())
            {
                if (p.Name.StartsWith("_", StringComparison.Ordinal)) continue;
                if (p.Value.Type != JTokenType.String) throw new InvalidOperationException($"현지화 값은 문자열이어야 합니다: {p.Name}");
                table[p.Name] = (string)p.Value!;
            }
            return table;
        }

        /// <summary>키의 첫 칸(파일 이름 또는 ui). 언어 파일의 _meta.complete 가 이 단위로 '다 옮김'을 적는다.</summary>
        public static string Section(string key)
        {
            int dot = key.IndexOf('.');
            return dot < 0 ? key : key.Substring(0, dot);
        }

        public IEnumerable<string> Sections => _ko.Keys.Select(Section).Distinct().OrderBy(s => s, StringComparer.Ordinal);

        /// <summary>문구 안 {0} {1} 자리(순서 무관, 겹친 것 하나로).</summary>
        public static IReadOnlyCollection<int> Placeholders(string text) =>
            new SortedSet<int>(Regex.Matches(text, @"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}").Cast<Match>().Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)));
    }

    /// <summary>한 언어의 문구 표. 옮긴 값이 없으면 한국어 원문, 원문도 없으면 키를 그대로 돌려준다(빠진 것이 화면에서 보이게).</summary>
    public sealed class Strings
    {
        /// <summary>지원 언어(기획서 16장 현지화 우선순위 1순위 한·영·간체, 2순위 번체). 파일 이름이 곧 코드다.</summary>
        public static readonly string[] Languages = { "ko", "en", "zh-Hans", "zh-Hant" };

        public string Lang { get; }
        readonly StringCatalog _catalog;
        readonly IReadOnlyDictionary<string, string> _table;

        Strings(string lang, StringCatalog catalog, IReadOnlyDictionary<string, string> table)
        {
            Lang = lang; _catalog = catalog; _table = table;
        }

        /// <summary>langJson 은 strings/<lang>.json 내용. 한국어이거나 파일이 없으면 null.</summary>
        public static Strings Load(string lang, StringCatalog catalog, string? langJson) =>
            new Strings(lang, catalog, lang == "ko" || langJson == null
                ? new Dictionary<string, string>() : StringCatalog.ReadTable(langJson));

        public string Get(string key)
        {
            if (_table.TryGetValue(key, out var v) && v.Length > 0) return v;
            return _catalog.Korean.TryGetValue(key, out var ko) ? ko : key;
        }

        /// <summary>데이터에서 바로 꺼낸 한국어(w.NameKo 등)를 함께 넘기는 꼴. 키가 카탈로그에 없어도 한국어가 나온다.</summary>
        public string Get(string key, string korean) =>
            _table.TryGetValue(key, out var v) && v.Length > 0 ? v : korean;

        public string Format(string key, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, Get(key), args);

        public bool Has(string key) => _table.TryGetValue(key, out var v) && v.Length > 0;

        /// <summary>옮긴 값이 없는 키(한국어는 비어 있음).</summary>
        public IEnumerable<string> Missing() =>
            Lang == "ko" ? Enumerable.Empty<string>() : _catalog.Korean.Keys.Where(k => !Has(k));

        /// <summary>카탈로그에 없는 키(원문이 지워졌거나 키 철자가 틀림).</summary>
        public IEnumerable<string> Extra() => _table.Keys.Where(k => !_catalog.Korean.ContainsKey(k));
    }
}
