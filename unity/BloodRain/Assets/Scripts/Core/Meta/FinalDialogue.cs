#nullable enable
// 종장·엔딩·인트로 컷 고르기(backlog DL-10). 문장은 dialogue.json intro·final·endings, 규칙은 docs/story/intro.md·final.md·endings.md.
//
// 인트로(Intro): 12컷을 차례로. 처음 실행 때 한 번은 게임이 정한다(혼 저장이 없을 때).
// 종장 입장(FinalEnter): 봉마곡에 들어설 때 컷 둘. 땅의 주인 만남 컷처럼 회차마다 처음 한 번(final.md '아직 정하지 않은 것'의 가정).
// 종장 상대(FinalOpponent): FinalFight 가 상대를 바꿀 때마다 머리 위 한 줄. 혈영이면 그 땅의 주인 혈영 줄(lords echo),
//   혈마 단계면 단계 줄, 혈월 회차면 그 단계의 혈월 줄을 뒤에 더한다.
// 엔딩(EndingChoices·EndingCuts): 고르는 화면은 Soul.EndingsOpen 차례 그대로, 컷은 엔딩마다 다 보여 준다(봉인은 회차마다 되풀이).
//   진혈마 10% 컷 뒤 차례(진엔딩 컷 1 → 고르는 화면?)는 사람 결정 대기(backlog DL-11)라 여기서는 엔딩 컷을 통째로만 준다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    /// <summary>종장에서 상대가 바뀔 때 머리 위 한 줄.</summary>
    public sealed class FinalLine
    {
        /// <summary>speakers id. 혈영 줄과 서술 줄은 null(혈영은 이름 붓글씨 아래에 띄운다).</summary>
        public string? Speaker { get; }
        public string Text { get; }
        /// <summary>현지화 키. 혈영 dialogue.lords.list.&lt;땅의 주인&gt;.echo, 단계 dialogue.final.stages.&lt;단계&gt;.text.</summary>
        public string Key { get; }
        /// <summary>혈월 회차에 Text 뒤에 더하는 줄(없으면 null).</summary>
        public string? BloodMoon { get; }
        public string? BloodMoonKey { get; }
        /// <summary>혈영 차례인가.</summary>
        public bool Shadow { get; }

        public FinalLine(string? speaker, string text, string key, string? bloodMoon, string? bloodMoonKey, bool shadow)
        { Speaker = speaker; Text = text; Key = key; BloodMoon = bloodMoon; BloodMoonKey = bloodMoonKey; Shadow = shadow; }
    }

    public static class FinalDialogue
    {
        static IReadOnlyList<LordCut> Cuts(IEnumerable<CutDef> cuts) =>
            cuts.Select(c => new LordCut(c.Id, c.Art, c.Lines, false)).ToList();

        /// <summary>인트로 컷신 12컷(현지화 키 dialogue.intro.cuts.&lt;id&gt;.lines.&lt;n&gt;.text, 길이는 data 의 CutDef.Seconds).</summary>
        public static IReadOnlyList<LordCut> Intro(GameData data) => Cuts(data.Dialogue.Intro.Cuts);

        /// <summary>봉마곡에 들어설 때(봉인석 일곱, 현경). 회차마다 처음 한 번 컷 둘, 그 뒤로는 빈 목록.</summary>
        public static IReadOnlyList<LordCut> FinalEnter(GameData data, Soul soul, DialogueProgress p)
        {
            var key = $"final.enter.{soul.Cycle}";
            if (p.HasSeen(key)) return Array.Empty<LordCut>();
            p.See(key);
            return Cuts(data.Dialogue.Final.Enter.Cuts);
        }

        /// <summary>FinalFight 의 지금 상대에 맞는 한 줄. 싸움이 끝났으면 null. 상대가 바뀔 때마다(시작과 Defeat 뒤) 부른다.</summary>
        public static FinalLine? FinalOpponent(GameData data, Soul soul, FinalFight fight)
        {
            if (fight.Done) return null;
            if (fight.IsShadow)
            {
                var lord = data.Dialogue.Lord(fight.BossId);
                return new FinalLine(null, lord.EchoKo, $"dialogue.lords.list.{lord.Id}.echo", null, null, true);
            }
            var st = data.Dialogue.Final.Stage(fight.Stage!.Id);
            string? bm = soul.IsBloodMoon && !string.IsNullOrEmpty(st.BloodMoonKo) ? st.BloodMoonKo : null;
            return new FinalLine(st.Speaker, st.TextKo, $"dialogue.final.stages.{st.Id}.text",
                bm, bm == null ? null : $"dialogue.final.stages.{st.Id}.bloodMoon", false);
        }

        /// <summary>엔딩 고르는 화면의 칸(이름·한 줄). Soul.EndingsOpen 차례 그대로. 고를 때가 아니면 빈 목록.</summary>
        public static IReadOnlyList<EndingDialogueDef> EndingChoices(GameData data, Soul soul, bool mokTalkDone) =>
            soul.EndingsOpen(mokTalkDone).Select(data.Dialogue.Ending).ToList();

        /// <summary>엔딩 컷(현지화 키 dialogue.endings.list.&lt;엔딩&gt;.cuts.&lt;id&gt;.lines.&lt;n&gt;.text). 고른 뒤 Soul.ChooseEnding 전에 보여 준다.</summary>
        public static IReadOnlyList<LordCut> EndingCuts(GameData data, string ending) => Cuts(data.Dialogue.Ending(ending).Cuts);
    }
}
