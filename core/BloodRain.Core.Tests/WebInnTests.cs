using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>
    /// WP-14d: 1장 프로토타입 침방·연무장이 천하 시안과 같은 셈(후보 수·환혼 강화 값·연무장 비용 식·타고난 성질)을 데이터에서 읽는지 글로 본다.
    /// </summary>
    public class WebInnTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");
        static string Sian => ReadRepoFile("web", "lookdev", "cheonha.html");

        [Fact] public void 빌드가_환혼과_타고난_성질을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"rebirth\": {k: v for k, v in drop_notes(load(\"cheonha\")[\"rebirth\"]).items() if k in (\"candidates\", \"firstRebirth\", \"firstRebirthTraits\", \"upgrades\", \"femaleRatio\", \"maleOnlySects\")}", build);
            Assert.Contains("\"birthTraits\": drop_notes(load(\"cheonha\")[\"birthTraits\"])", build);
        }

        [Fact] public void 연무장_비용_식이_시안과_같다()
        {
            Assert.Contains("Math.round(it.baseCost * (lv + 1) * (1 + B.training.costGrowthPerBought * bought))", Proto);
            Assert.Contains("Math.round(it.baseCost * (lv + 1) * (1 + BAL.training.costGrowthPerBought * bought))", Sian);
        }

        [Fact] public void 환혼_강화_값과_후보_수가_시안과_같다()
        {
            Assert.Contains("lv >= u.maxLevel ? null : u.costPerLevel ? u.baseCost * (lv + 1) : u.baseCost", Proto);
            Assert.Contains("lv >= u.maxLevel ? null : u.costPerLevel ? u.baseCost * (lv + 1) : u.baseCost", Sian);
            Assert.Contains("RB.candidates + upgEffect(m, 'candidates')", Proto);
            Assert.Contains("RB.firstRebirth.map(id => ({ sect: id, traits: RB.firstRebirthTraits ? rollTraits(rnd) : [] }))", Proto);
        }

        [Fact] public void 타고난_성질은_겹치지_않게_굴리고_윤회의_기_보너스를_정산에()
        {
            Assert.Contains("BT.excludes.some(", Proto);
            Assert.Contains("qiBonus += t.rebirthQi || 0;", Proto);
            Assert.Contains("Math.round(rows.reduce((a, r) => a + r[2], 0) * G.body.qiBonus)", Proto);
        }

        [Fact] public void 몸_성별과_카드_글과_목_노인_줄을_데이터에서()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"femaleRatio\", \"maleOnlySects\"", build);
            Assert.Contains("\"bedroom\": drop_notes(load(\"dialogue\")[\"bedroom\"][\"lines\"])", build);
            Assert.Contains("\"cards\": drop_notes(load(\"dialogue\")[\"cards\"][\"list\"])", build);
            var html = Proto;
            Assert.Contains("!(RB.maleOnlySects || []).includes(sect) && rnd() < (RB.femaleRatio || 0)", html);
            Assert.Contains("c.female && (card.femaleBodiesKo || []).length > idx", html);
            // 첫 환혼 줄은 늘, 나머지는 한 침방에 한 마디(Core BedroomTalk)
            Assert.Contains("if (!l || talk.key || talk.extra) return false;", html);
            Assert.Contains("bedLine('first:open')", html);
            Assert.Contains("bedLine('pick:female')", html);
        }

        [Fact] public void 서장_끝은_이_판의_침방으로_잇고_서장_정산은_시안과_같은_셈()
        {
            var html = Proto;
            Assert.Contains("if (SCALE_MODE) { $('profirst').hidden = true; overlay = null; recordPrologue(G.orbRaw, G.realm.id); openInn(); return; }", html);
            Assert.Contains("Math.round(QI.perOrbValue * Math.max(0, orbRaw || 0) * home.density + visits.length * QI.firstVisitRegion + realms.length * QI.firstReachRealm)", html);
            Assert.Contains("Math.round(orbQi + visits.length * QI.firstVisitRegion + realms.length * QI.firstReachRealm)", Sian);
            Assert.Contains("__hyeolu.prologueEnd(900, 'ilryu')", ReadRepoFile("tools", "web_autocheck.js"));
        }

        [Fact] public void 대표_인물_마당_줄은_처음_0번이고_여자_몸이면_사매_칸()
        {
            Assert.Contains("\"leaders\": [{k: l.get(k) for k in (\"id\", \"sect\", \"yardUnknown\", \"yardKnown\", \"sister\", \"join\", \"inn\")}", ReadRepoFile("tools", "build_web_prototype.py"));
            var html = Proto;
            Assert.Contains("if (!m.seen.includes(key)) { m.seen.push(key); return kind + '.0'; }", html);
            Assert.Contains("body.female && l.sister && l.sister[slot] ? l.sister[slot] : l[kind][+i]", html);
        }

        [Fact] public void 귀환하면_목_노인_운낭_대표_인물_줄을_Core_OnReturn_처럼()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"returns\": drop_notes(load(\"dialogue\")[\"returns\"])", build);
            var html = Proto;
            // 한 생에 한 번, 서장 끝 귀환엔 없음
            Assert.Contains("if (!m.lives || m.retAt === m.lives) return [];", html);
            // 밀린 줄을 갈래 순서 → 파일 순서로 max 개
            Assert.Contains("retTake(m, 'mok', RET_MOK, RET.mokOrder, RET.mokMax)", html);
            Assert.Contains("retTake(m, 'unnang', RET_UN, RET.unnangOrder, RET.unnangMax)", html);
            // 합류는 자기 땅 봉인석, 땅의 주인 없는 문파는 그 몸으로 땅의 주인을 쓰러뜨린 생 뒤. 합류 줄이 먼저
            Assert.Contains("if (home ? stones.includes(home.sealStone) : !!lifeSect && lifeSect.id === l.sect && ((G && G.lordKills) || []).length > 0) m.pjoin.push(l.id);", html);
            // 처치 자리에서 하던 합류 알림은 뺐다(Core 는 귀환 때 합류)
            Assert.DoesNotContain("합류`, leader.join)", html);
        }

        [Fact] public void 귀환_현경_short_sealed_와_밀서_줄도_Core_처럼()
        {
            var html = Proto;
            // Core: realm:X:short = summary.NewRealms 에 X && !HasAllSealStones, realm:X:sealed = record.HighestRealm == X && HasAllSealStones
            Assert.Contains("kind === 'realm' && a[1] === 'short' ? newR.includes(a[0]) && !allStones", html);
            Assert.Contains("kind === 'realm' && a[1] === 'sealed' ? m.lastTop === a[0] && allStones", html);
            Assert.Contains("stones.length >= ((LR && LR.finalRequires && LR.finalRequires.sealStones) || 7)", html);
            // Core: letter:<before|after> = PickedHaomunLetter && (after == 마천패 처치)
            Assert.Contains("kind === 'letter' ? !!m.lastLetter && (a[0] === 'after') === afterMa", html);
            Assert.Contains("G.letterPicked = true;", html);
            Assert.Contains("m.lastNewRealms = st.newRealms || []; m.lastTop = st.topId || null; m.lastLetter = !!st.letter;", html);
            // 데이터에 그 줄이 있어야 뜻이 있다
            var d = TestData.Load();
            Assert.Contains(d.Dialogue.Mok.Lines, l => l.Kind == "realm" && l.Args.Length == 2 && l.Args[1] == "short");
            Assert.Contains(d.Dialogue.Mok.Lines, l => l.Kind == "realm" && l.Args.Length == 2 && l.Args[1] == "sealed");
            Assert.Contains(d.Dialogue.Unnang.Lines, l => l.Kind == "letter");
        }

        [Fact] public void 객잔_시설은_Core_Inn_처럼_열리고_지어지며_귀환_시설_줄로()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"innFacilities\": drop_notes(load(\"cheonha\")[\"inn\"][\"facilities\"])", build);
            var html = Proto;
            // IsOpen: 혼에 남았거나, 짓지 않는 시설이고 조건을 갖춤
            Assert.Contains("const facOpen = (m, f) => (m.unlocks || []).includes(facTok(f.id)) || (!(f.buildBloodCrystal > 0) && facMeets(m, f));", html);
            // Refresh: 짓는 시설·이미 적은 것·조건 못 갖춘 것은 건너뛰고, 조건 없는 시설은 새로 연 것으로 치지 않는다
            Assert.Contains("if (f.buildBloodCrystal > 0 || m.unlocks.includes(facTok(f.id)) || !facMeets(m, f)) continue;", html);
            // TryBuild: 혈정을 쓰고 다음 귀환 시설 줄에
            Assert.Contains("m.crystal -= f.buildBloodCrystal;", html);
            Assert.Contains("m.newFac = facRefresh(m).concat(Array.isArray(m.builtFac) ? m.builtFac : []); m.builtFac = [];", html);
            Assert.Contains("kind === 'facility' ? (m.newFac || []).includes(a[0])", html);
            // 대사로 여는 시설은 그 시설이 기다리는 해금 토큰을
            Assert.Contains("if (l.opens) { const f = INN_F.find(x => x.id === l.opens), tok = f && f.require && f.require.unlock;", html);
            var d = Load();
            Assert.Contains(d.Dialogue.Unnang.Lines, l => l.Kind == "facility");
        }
    }
}
