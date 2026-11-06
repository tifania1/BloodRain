using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 한 생 판의 비급 층(WP-44, 20장 '비급 층과 해금')이 Core Synergy.Compute(restored)·Soul.TryRestore 와 같은 규칙인지 글로 본다.</summary>
    public class WebSecretManualTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 혼_기록이_있는_판만_복원한_비급을_켠다()
        {
            var html = Proto;
            // Core: restored null 이면 다 열림. 웹은 1장·서장 판(혼 기록 없음)이 그 자리
            Assert.Contains("const restored = G.soul0 ? new Set(G.soul0.restored || []) : null;", html);
            Assert.Contains("const tierOpen = (sc, tr) => tr.tier !== 'secret' || !restored || restored.has(manualIdOf(sc, tr));", html);
            Assert.Contains("const links = new Set(met.filter(l => l.tier !== 'secret' || !restored || restored.has(l.id)).map(l => l.id));", html);
        }

        [Fact] public void 비급_id_는_Core_와_같은_꼴()
        {
            // Core SynergyDefs.ManualIdOf: 문파 id + ':' + 개수, 연환은 그 id
            Assert.Contains("const manualIdOf = (sc, tr) => sc.id + ':' + tr.count;", Proto);
        }

        [Fact] public void 잠긴_진전_단계는_건너뛰고_위_단계는_켜진다()
        {
            Assert.Contains("const sch = (id, n) => !!G && !!G.syn && (G.syn.tier[id] || 0) >= n && !(G.syn.locked[id] || []).includes(n);", Proto);
        }

        [Fact] public void 찢긴_비급은_정산에서_혼에_오르고_복원은_Core_값표대로()
        {
            var html = Proto;
            Assert.Contains("const newTorn = [...G.syn.torn].filter(id => !(s0.torn || []).includes(id) && !(s0.restored || []).includes(id));", html);
            Assert.Contains("for (const id of st.newTorn || []) if (!m.torn.includes(id)) m.torn.push(id);", html);
            // Core SecretManualsDef: 몇 번째 복원인지, 표를 넘으면 마지막 값. 봉인석은 지닌 수만
            Assert.Contains("k = Math.min((m.restored || []).length, sm.rebirthQi.length - 1); return [sm.sealStones[k], sm.rebirthQi[k]];", html);
            Assert.Contains("return (m.stones || []).length >= rs && (m.qi || 0) >= rq;", html);
            // 수리 중이면(Core Inn.CanEnter) 복원 못 함
            Assert.Contains("if (!lf || lf.underRepair || !facOpen(m, lf)) return false;", html);
        }

        [Fact] public void 침방_벽_지도_장막_원은_7장_식()
        {
            // WP-45, 21장 '침방의 중원 지도': 절정·초절정 장막 원 = view0 × scalePerRealm^경지 × boundaryViewMultiple
            var html = Proto;
            Assert.Contains("const MAP_RINGS = ['jeoljeong', 'chojeoljeong'];", html);
            Assert.Contains("WD.view0Meters * Math.pow(WD.scalePerRealm, i) * WD.boundaryViewMultiple", html);
            Assert.Contains("taken = !!r.sealStone && stones.has(r.sealStone)", html);
            Assert.Contains("b.onmouseenter = b.onfocus = () => drawInnMap(m, i);", html);
        }

        [Fact] public void 화면_가장자리_화살표와_장막_번짐()
        {
            // WP-46, 12장 'HUD 배치' 화면 가장자리 줄: 정예·땅의 주인·금두꺼비 화살표, 장막이 가까우면 그쪽 가장자리가 붉게
            var html = Proto;
            Assert.Contains("const kind = e.boss ? 'boss' : e.elite ? 'elite' : e.type === 'golden_toad' ? 'toad' : null;", html);
            Assert.Contains("const gap = R - Math.hypot(px - hx, py - hy), k = clamp(1 - gap / half, 0, 1);", html);
            Assert.Contains("drawEdgeHints();", html);
        }

        [Fact] public void 다시_오른_경지는_짧은_연출과_깨달음_하나_더()
        {
            // WP-47, 21장 '한 번 깨친 길'(cheonha.json mastery, Core Breakthrough.Start revisit)
            Assert.Contains("\"mastery\": drop_notes(load(\"cheonha\")[\"mastery\"]),", ReadRepoFile("tools", "build_web_prototype.py"));
            var html = Proto;
            Assert.Contains("const speed = revisit && MASTERY.shortBreakthroughWhenRevisited && BRK.revisitSeconds > 0 ? BRK.zoomToSeconds / BRK.revisitSeconds : 1;", html);
            Assert.Contains("const more = revisit ? MASTERY.revisitInsightChoicesBonus || 0 : 0;", html);
            Assert.Contains("B.t += rdt * (B.speed || 1);", html);
        }
    }
}
