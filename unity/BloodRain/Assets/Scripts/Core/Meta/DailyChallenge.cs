#nullable enable
// 일일 도전(기획서 7장 '모드': 매일 같은 시드 — 문파·타고난 성질·사건 고정, Steam 리더보드). 열리는 때는 Meta/Modes(봉인석 3개).
//   같은 날이면 누구나 같은 몸·같은 생: 날짜(UTC) → 시드 하나 → 몸(문파·태어난 땅·성질·성별)과 한 생 난수(사건 일정·적 뽑기).
//   리더보드에 같은 판을 올리려고 해금과 상관없이 아홉 문파 전부에서 뽑는다(혼마다 다르면 '같은 시드'가 아니다).
//   string.GetHashCode 는 실행마다 바뀌어 쓰지 않고, 날짜 숫자를 정수 섞기로 바꾼다. System.Random(seed) 는 .NET·Mono 가 같은 수열을 낸다.
//   혼에 남기는 것(WP-37, 2026-11-20 추천대로 — decisions #38): 연무장·환혼 강화는 받고, 혼에는 아무것도 안 남긴다.
//   그래서 한 생은 혼의 복사본(Sandbox)으로 돌리고 끝나면 버린다. 남는 것은 리더보드 기록(버틴 초, 7장 '버틴 시간이 리더보드에')뿐.
//   몇 번이든 해 볼 수 있고 리더보드엔 그날 첫 판만 올린다 — 날마다 따로 둔 리더보드(LeaderboardName)에 내 기록이 이미 있으면 안 올린다(Steamworks 쪽).
using System;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public static class DailyChallenge
    {
        public const string ModeId = "daily";

        /// <summary>그날(UTC 날짜)의 시드. 시각은 버린다.</summary>
        public static int Seed(DateTime utc)
        {
            var d = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
            uint x = (uint)(d.Year * 10000 + d.Month * 100 + d.Day);
            // 정수 섞기(lowbias32). 이웃한 날짜가 비슷한 시드를 내지 않게
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return (int)(x & 0x7fffffff);
        }

        /// <summary>그날의 몸. 아홉 문파 전부에서 하나, 성질·성별은 환혼 규칙(Rebirth.Roll) 그대로.</summary>
        public static RebirthCandidate Body(CheonhaDef c, int seed) =>
            Rebirth.Roll(c, c.Sects.Select(s => s.Id), 1, new Random(seed)).Single();

        /// <summary>그날 한 생에 넘길 난수(Life 의 rng). 몸을 뽑은 난수와 따로 둬서 몸 규칙이 바뀌어도 사건 일정이 덩달아 밀리지 않게.</summary>
        public static Random LifeRandom(int seed) => new Random(seed ^ 0x5bd1e995);

        /// <summary>일일 도전 한 생을 돌릴 혼의 복사본. 연무장·환혼 강화·해금은 그대로 받고, 생이 끝나면 이 복사본째 버린다
        /// (정산·도감·윤회의 기 어느 것도 진짜 혼에 안 남는다). 진짜 혼은 건드리지 않는다.</summary>
        public static Soul Sandbox(Soul soul) => Soul.FromJson(soul.Data, soul.ToJson());

        /// <summary>그날(UTC 날짜)의 Steam 리더보드 이름. 날마다 따로 둬서 '그날 첫 판'을 내 기록이 있나로 가린다.</summary>
        public static string LeaderboardName(DateTime utc)
        {
            var d = utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc;
            return "daily_" + d.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>리더보드 점수: 버틴 시간(초, 버림). 한 생의 Life.Minutes 를 넘긴다.</summary>
        public static int Score(float minutes) => minutes <= 0 ? 0 : (int)Math.Floor(minutes * 60.0);
    }
}
