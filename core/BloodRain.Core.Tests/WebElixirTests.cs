using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>웹 침방 약방 영약(EL-03)이 Core Apothecary·Life.TakeElixirs 와 같은 규칙인지 글로 본다.</summary>
    public class WebElixirTests
    {
        static string Proto => ReadRepoFile("web", "prototype", "template.html");

        [Fact] public void 빌드가_영약을_넣는다()
        {
            var build = ReadRepoFile("tools", "build_web_prototype.py");
            Assert.Contains("\"innElixirs\": drop_notes(load(\"cheonha\")[\"inn\"][\"elixirs\"])", build);
            Assert.Contains("\"elixirsPerLife\": load(\"cheonha\")[\"inn\"][\"elixirsPerLife\"]", build);
        }

        [Fact] public void 약방에_들어갈_수_있을_때만_고르고_몸을_고를_때_혈정을_쓴다()
        {
            var html = Proto;
            Assert.Contains("const elixAvail = m => { const f = INN_F.find(x => x.id === 'apothecary'); return !!f && !f.underRepair && facOpen(m, f); };", html);
            Assert.Contains("if (pick.length && elixAvail(m) && (m.crystal || 0) >= cost) { m.crystal -= cost; soulSave(m); } else pick.length = 0;", html);
        }

        [Fact] public void 효과는_Core_TakeElixirs_와_같은_자리()
        {
            var html = Proto;
            Assert.Contains("s.maxHealth += e.maxHealth || 0;   // 만년설삼", html);
            Assert.Contains("while (G.level < lv) { G.level++; G.pendingLevels++; }   // 대환단", html);
            Assert.Contains("G.ult = Math.min(B.ultimateCharge.max, (G.ult || 0) + e.startUltimate);   // 공청석유", html);
            Assert.Contains("(G.elixCrystal || 1) * moonR()", html);
            Assert.Contains("if (aspect === 'neutral' || G.yyFreeze > 0) return;   // 태극환", html);
            // 소환단은 재기보다 먼저
            Assert.True(html.IndexOf("if (G.body && G.elixRevive > 0)") < html.IndexOf("if (G.body && G.revivals > 0)"));
        }
    }
}
