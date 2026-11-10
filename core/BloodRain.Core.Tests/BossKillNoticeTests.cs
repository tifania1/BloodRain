using System.IO;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>땅의 주인 처치 알림(BR-02 붓글씨). 웹 lordDown toast 와 같은 글인지.</summary>
    public class BossKillNoticeTests
    {
        static readonly StringCatalog Catalog =
            StringCatalog.FromData(ReadFiles(), ReadText(Path.Combine(DataDir, "strings", "ko.json")));

        static Strings Lang(string lang) => Strings.Load(lang, Catalog,
            lang == "ko" ? null : ReadText(Path.Combine(DataDir, "strings", lang + ".json")));

        static BossKill Wongak(bool first) => new BossKill
        {
            RegionId = "songshan", BossId = "wongak", First = first,
            SealStone = first ? "천추석" : null, UnlockSect = null, RebirthQi = 300, BloodCrystal = 2, Chest = true,
        };

        [Fact]
        public void 첫_처치는_이름과_봉인석과_보상()
        {
            var n = BossKillNotice.Of(Load(), Lang("ko"), Wongak(true))!;
            Assert.Equal("혈나한 원각 — 쓰러뜨렸다", n.Title);
            Assert.Equal("봉인석 천추석 · 윤회의 기 +300 · 혈정 +2", n.Body);
        }

        [Fact]
        public void 다시_쓰러뜨리면_혈영_줄이_앞에()
        {
            var n = BossKillNotice.Of(Load(), Lang("ko"), Wongak(false))!;
            Assert.Equal("혈영으로 다시 일어날 것이다 · 윤회의 기 +300 · 혈정 +2", n.Body);
        }

        [Fact]
        public void 문파가_열리면_그_줄()
        {
            var k = new BossKill { RegionId = "danggata", BossId = "dang_sohye", First = true, SealStone = "천선석", UnlockSect = "tang", RebirthQi = 300 };
            var n = BossKillNotice.Of(Load(), Lang("ko"), k)!;
            Assert.Contains("당가의 몸으로 환생할 수 있다", n.Lines);
            var en = BossKillNotice.Of(Load(), Lang("en"), k)!;
            Assert.Contains("Seal Stone: Heavenly Jade Stone", en.Lines);
        }

        [Fact]
        public void 종장은_알림이_없다()
        {
            Assert.Null(BossKillNotice.Of(Load(), Lang("ko"), new BossKill { IsFinal = true, BossId = "blood_demon" }));
        }

        [Fact]
        public void 네_언어_모두_키가_있다()
        {
            foreach (var lang in Strings.Languages)
            {
                var s = Lang(lang);
                foreach (var key in new[] { "ui.bosskill.title", "ui.bosskill.shadow", "ui.bosskill.seal_stone", "ui.bosskill.unlock_sect", "ui.bosskill.rewards" })
                    Assert.True(s.Has(key) || lang == "ko", $"{lang}: {key}");
            }
        }
    }
}
