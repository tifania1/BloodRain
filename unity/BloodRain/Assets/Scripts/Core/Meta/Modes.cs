#nullable enable
// 모드와 열리는 시점(기획서 7장 '모드', cheonha.json modes).
//   조건(서장·봉인석·본 엔딩)을 갖추면 열리고, 혼 해금 토큰 mode:<id> 로 남아 회귀로 봉인석이 지워져도 닫히지 않는다.
//   혼이 생을 마칠 때(Soul.EndLife)와 엔딩을 고를 때(Soul.ChooseEnding) Refresh 를 부른다.
//   계승 엔딩의 혈마 모드(mode:blood_demon)는 출시 후 DLC 후보라 이 표에 없고 Soul.ChooseEnding 이 바로 연다.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public static class Modes
    {
        public const string TokenPrefix = "mode:";

        public static string Token(string modeId) => TokenPrefix + modeId;

        static IReadOnlyList<ModeDef> Defs(Soul soul) => soul.Data.Cheonha.Modes;

        /// <summary>모드가 열려 있다: 한 번 열려 혼에 남았거나 지금 조건을 갖췄다.</summary>
        public static bool IsOpen(Soul soul, string modeId)
        {
            var m = Defs(soul).FirstOrDefault(d => d.Id == modeId) ?? throw new KeyNotFoundException($"모드가 없습니다: {modeId}");
            return soul.Unlocks.Contains(Token(m.Id)) || MeetsRequirement(soul, m);
        }

        public static bool MeetsRequirement(Soul soul, ModeDef m)
        {
            var r = m.Require;
            if (r.Prologue && !soul.PrologueDone) return false;
            if (soul.SealStoneCount < r.SealStones) return false;
            if (r.Ending != null && !soul.EndingsSeen.Contains(r.Ending)) return false;
            return true;
        }

        /// <summary>열린 모드 id(데이터 순서).</summary>
        public static IReadOnlyList<string> OpenModes(Soul soul) => Defs(soul).Where(m => IsOpen(soul, m.Id)).Select(m => m.Id).ToList();

        /// <summary>처음 조건을 갖춘 모드를 혼에 적고 그 id 를 돌려준다.</summary>
        public static IReadOnlyList<string> Refresh(Soul soul)
        {
            var opened = new List<string>();
            foreach (var m in Defs(soul))
            {
                if (soul.Unlocks.Contains(Token(m.Id)) || !MeetsRequirement(soul, m)) continue;
                soul.Unlock(Token(m.Id));
                opened.Add(m.Id);
            }
            return opened;
        }
    }
}
