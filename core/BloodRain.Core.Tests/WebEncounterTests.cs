using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-27: 웹 한 생 판 기연 추첨이 Core EncounterRoller 와 같은 규칙인지 글로 본다
    /// (사건마다 판에 한 번, 확률 = chance + 기연 × luck.chancePerLuck, chapters·minMinute·needsChapterSchool).
    /// </summary>
    public class WebEncounterTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 확률과_나올_형편이_Core_와_같다()
        {
            var html = Proto;
            Assert.Contains("Math.max(0, Math.min(1, e.chance + c.luck * SYN.luck.chancePerLuck))", html);
            Assert.Contains("!(e.chapters && (!c.chapter || !e.chapters.includes(c.chapter))) && c.minute >= (e.minMinute || 0) && !(e.needsChapterSchool && !c.school)", html);
            var core = ReadRepoFile("unity", "BloodRain", "Assets", "Scripts", "Core", "Spawning", "Encounters.cs");
            Assert.Contains("double p = e.Chance + ctx.Luck * _syn.Luck.ChancePerLuck;", core);
            Assert.Contains("if (e.NeedsChapterSchool && ctx.ChapterSchool == null) return false;", core);
        }

        [Fact] public void 한_생_판은_한_번만_굴리고_유해는_그_땅_문파()
        {
            var html = Proto;
            Assert.Contains("G.body && !ev.rolled", html);
            Assert.Contains("if (!encCanAppear(e, c)) { ev.at = G.gm + 1; continue; }", html);
            Assert.Contains("S.bonus[sc.id] = (S.bonus[sc.id] || 0) + ENC_BY.relic.params.schoolBonus", html);
            Assert.DoesNotContain("S.bonus.shaolin = (S.bonus.shaolin || 0) + 1", html);
        }

        [Fact] public void 은거기인_곁_시간은_기획서_20장대로()
        {
            Assert.Contains("| 은거기인 | 맵 구석에서 바둑 두는 노인 | 곁에서 6초 동안 떠나지 않는다", Gdd);
            Assert.Contains("if (ev.prog >= ENC_BY.hermit.params.staySeconds && !overlay) { ev.done = true; ev.met = true; discover('enc:hermit');", Proto);
            Assert.Equal(6f, Load().Synergies.Encounter("hermit").Param("staySeconds"));
        }
    
        [Fact] public void 영약은_한_생_판에서_오의_가득_음양_0()
        {
            var html = Proto;
            Assert.Contains("for (const key of ['relic', 'hermit', 'elixir', 'letter', 'whisper', 'sword_tomb', 'spiritbeast'])", html);
            Assert.Contains("((key === 'elixir' || key === 'letter' || key === 'whisper' || key === 'sword_tomb' || key === 'spiritbeast') && !G.body)", html);
            Assert.Contains("discover('enc:elixir'); G.ult = B.ultimateCharge.max; G.yy = 0;", html);
            Assert.Contains("| 영약 | 빛나는 덩굴·바위 틈(천년하수오, 공청석유) | 줍는다 | 오의 게이지 가득, 음양 0 |", Gdd);
        }
    
        [Fact] public void 밀서는_다음_등롱에서_못_찾은_비전_조건_한_줄()
        {
            var html = Proto;
            Assert.Contains("if (key === 'letter') { ev.done = true; G.letterArmed = true; continue; }", html);
            Assert.Contains("if (G.letterArmed && !l.silver) { G.letterArmed = false; readLetter(); }", html);
            Assert.Contains("SYN.secrets.find(q => !dex.has('secret:' + q.id) && !letters.has(q.id))", html);
            Assert.Contains("letters.has(x.id) ? `<span class=\"lore\">밀서: ${x.condition}</span>`", html);
            Assert.Contains("| 하오문 밀서 | 운낭이 등롱 속에 숨긴 쪽지 | 줍는다 | 못 찾은 비전 하나의 조건이 한 줄 더 드러남 | 20% |", Gdd);
        }
    
        [Fact] public void 속삭임은_석상_앞_오의로_받고_회복이_절반()
        {
            var html = Proto;
            Assert.Contains("G.ult = 0; sfx('ult'); whisperUlt();", html);
            Assert.Contains("G.syn.bonus.demonic = (G.syn.bonus.demonic || 0) + ENC_BY.whisper.params.schoolBonus", html);
            Assert.Contains("(G && G.whisperTaken ? ENC_BY.whisper.params.healingMultiplier : 1)", html);
            Assert.Contains("if (st.whisper) m.whispers = (m.whispers || 0) + 1;", html);
            Assert.Equal(15f, Load().Synergies.Encounter("whisper").MinMinute);
        }

        [Fact] public void 검총은_화산에서_무공을_꽂고_레벨당_공력_3퍼센트()
        {
            string html = Proto;
            Assert.Contains("| 검총 | 5장 화산 절벽 위 검 무덤 | 검 하나를 꽂는다(공격 무공 하나를 이번 판에서 버림) | 버린 무공 레벨당 남은 무공 공력 +3% | 50% |", Gdd);
            var e = Load().Synergies.Encounter("sword_tomb");
            Assert.Equal(0.5f, e.Chance);
            Assert.Contains("chapter5", e.Chapters!);
            Assert.Contains("const TOMB_PER_LEVEL = ENC_BY.sword_tomb.params.mightPerLevel;", html);
            Assert.Equal(0.03f, e.Param("mightPerLevel"), 5);
            Assert.Contains("G.weapons.splice(k, 1); G.banished.add(o.w.id); G.tombMight = (G.tombMight || 0) + lv * TOMB_PER_LEVEL;", html);
            Assert.Contains("(G.stats.might + sectBonus('Might') + (G.tombMight || 0) + drunkMight())", html);
            Assert.Contains("if (G.weapons.length > 1) showSwordTomb();", html);
        }

        [Fact] public void 영물은_무당_곤륜에서_달아나고_잡으면_영약과_혈정()
        {
            string html = Proto;
            Assert.Contains("| 영물 | 장별 영물(4장 백학, 6장 설삼 동자) | 달아나는 것을 잡는다 | 영약 하나 + 혈정 | 25% |", Gdd);
            var e = Load().Synergies.Encounter("spiritbeast");
            Assert.Equal(0.25f, e.Chance);
            Assert.Equal(new[] { "chapter4", "chapter6" }, e.Chapters);
            Assert.Contains("const BEAST_NAME = { chapter4: '백학', chapter6: '설삼 동자' };", html);
            Assert.Contains("if (key === 'spiritbeast') { ev.done = true; spawnSpiritBeast(c.chapter); continue; }", html);
            Assert.Contains("discover('enc:spiritbeast'); G.ult = B.ultimateCharge.max; G.yy = 0;", html);
            Assert.Contains("+ Math.round((G.beastCrystal || 0) * moonR())", html);
        }
    }
}
