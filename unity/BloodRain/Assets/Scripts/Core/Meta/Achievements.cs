#nullable enable
// Steam 업적(WP-43, 2026-11-20 추천대로 — decisions #40, cheonha.json achievements).
//   기획서에 이미 있는 '처음'만 업적으로 둔다(새 숫자 없음): 경지 첫 돌파 여섯, 땅의 주인 일곱 첫 처치,
//   엔딩 셋, 칭호 셋, 절기 16 다 깨움, 비전 여덟 다 찾음, 봉인석 일곱.
//   조건은 혼(Soul)만 보고 가린다. 한 번 얻으면 혼 해금 토큰 achievement:<id> 로 남아 회귀로 봉인석이 지워져도 그대로다.
//   혼이 생을 마칠 때(EndLife)·엔딩을 고를 때(ChooseEnding)·도감 쪽을 찾을 때(Discover)·저장을 읽을 때(FromJson) Refresh 를 부른다.
//   Steam 쪽(Unity)은 Earned 를 보고 아직 안 켠 것만 SetAchievement 하면 된다(같은 것을 또 켜도 해가 없다).
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public static class Achievements
    {
        public const string TokenPrefix = "achievement:";

        public static string Token(string id) => TokenPrefix + id;

        static IReadOnlyList<AchievementDef> Defs(Soul soul) => soul.Data.Cheonha.Achievements;

        /// <summary>조건 토큰을 둘로 나눈다(종류, 대상). 모르는 종류면 ArgumentException.</summary>
        public static (string kind, string arg) Parse(string when)
        {
            int i = when.IndexOf(':');
            if (i <= 0 || i == when.Length - 1) throw new ArgumentException($"업적 조건 모양이 아닙니다: {when}");
            var kind = when.Substring(0, i);
            var arg = when.Substring(i + 1);
            switch (kind)
            {
                case "realm": case "boss": case "ending": case "title": return (kind, arg);
                case "evolutions": case "secrets": case "sealstones":
                    if (arg != "all") throw new ArgumentException($"'{kind}' 업적은 all 만 씁니다: {when}");
                    return (kind, arg);
                default: throw new ArgumentException($"모르는 업적 조건입니다: {when}");
            }
        }

        /// <summary>지금 혼이 이 업적의 조건을 갖췄나(얻었는지와 따로).</summary>
        public static bool Meets(Soul soul, AchievementDef a)
        {
            var (kind, arg) = Parse(a.When);
            var data = soul.Data;
            switch (kind)
            {
                case "realm": return soul.HasReached(arg);
                case "boss": return soul.BossesKilled.Contains(arg);
                case "ending": return soul.EndingsSeen.Contains(arg);
                case "title": return soul.Titles.Contains(arg);
                case "evolutions": return data.Evolutions.All(e => soul.HasAwakened(e.Id));
                case "secrets": return data.Synergies.Secrets.All(x => soul.Codex.Contains(CodexKey.Secret(x.Id)));
                default: return soul.SealStoneCount >= data.Cheonha.Bosses.FinalRequires.SealStones; // sealstones
            }
        }

        /// <summary>얻은 업적인가.</summary>
        public static bool IsEarned(Soul soul, string id) => soul.Unlocks.Contains(Token(id));

        /// <summary>얻은 업적 id(데이터 순서). Steam 맞추기는 이것으로 한다.</summary>
        public static IReadOnlyList<string> Earned(Soul soul) => Defs(soul).Where(a => IsEarned(soul, a.Id)).Select(a => a.Id).ToList();

        /// <summary>처음 조건을 갖춘 업적을 혼에 적고 그 id 를 돌려준다(데이터 순서).</summary>
        public static IReadOnlyList<string> Refresh(Soul soul)
        {
            var opened = new List<string>();
            foreach (var a in Defs(soul))
            {
                if (IsEarned(soul, a.Id) || !Meets(soul, a)) continue;
                soul.Unlock(Token(a.Id));
                opened.Add(a.Id);
            }
            return opened;
        }
    }
}
