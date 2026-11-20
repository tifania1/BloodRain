#nullable enable
using System.Collections.Generic;
using BloodRain.Core.Data;
using UnityEngine;

namespace BloodRain.Presentation.Hud
{
    /// <summary>문파 상징색(synergies.json schools[].color, 2026-11-20 사람: 무공 선택지에 문파 색을 넣어 진전을 챙기기 쉽게).
    /// 레벨업 카드 책등·문파 인장, 옆 칸·HUD 칸 글자가 같은 색을 쓴다. 삼재검법처럼 문파가 없으면 null.</summary>
    public static class SchoolTint
    {
        static readonly Dictionary<string, Color> _cache = new Dictionary<string, Color>();

        public static Color Of(SchoolDef school)
        {
            if (_cache.TryGetValue(school.Id, out var c)) return c;
            if (!ColorUtility.TryParseHtmlString(school.Color, out c)) c = new Color(0.12f, 0.10f, 0.09f); // 색이 없으면 먹
            _cache[school.Id] = c;
            return c;
        }

        /// <summary>무공·심법 id 가 속한 문파의 색. 없으면 null.</summary>
        public static Color? OfMember(string? id)
        {
            if (string.IsNullOrEmpty(id) || DataBoot.Data == null) return null;
            var s = DataBoot.Data.Synergies.SchoolOf(id!);
            return s != null ? Of(s) : (Color?)null;
        }

        /// <summary>문파 id(몸의 문파 = 진전 문파 id)로 색. 없으면 null.</summary>
        public static Color? OfSchoolId(string? id)
        {
            if (string.IsNullOrEmpty(id) || DataBoot.Data == null) return null;
            foreach (var s in DataBoot.Data.Synergies.Schools) if (s.Id == id) return Of(s);
            return null;
        }
    }
}
