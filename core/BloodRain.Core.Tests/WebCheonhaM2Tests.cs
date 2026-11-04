using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>RM-12~14: 웹 천하 시안이 M2 줄(목인나한 나뭇조각, 정산의 혈정, 한 생 사건)을 데이터에서 읽는지,
    /// 그리고 RM-10 때처럼 맞은 뒤 무적 줄이 주석 안으로 들어가 사라지지 않았는지 본다.</summary>
    public class WebCheonhaM2Tests
    {
        readonly GameData _d = Load();
        static string Html() => ReadRepoFile("web", "lookdev", "cheonha.html");
        static string Build() => ReadRepoFile("tools", "build_web_prototype.py");

        [Fact] public void 맞은_뒤_무적_줄이_주석_밖에_있다()
        {
            var html = Html();
            var fn = html.Substring(html.IndexOf("function hitPlayer("));
            fn = fn.Substring(0, fn.IndexOf("\n}\n"));
            // 주석(// 뒤)을 걷어 낸 코드에 무적 시간을 거는 문장이 있어야 한다
            var code = string.Join("\n", fn.Split('\n').Select(l => Regex.Replace(l, @"//.*$", "")));
            Assert.Contains("p.iframe = LD.hitIframe;", code.Substring(code.IndexOf("p.hp -=")));
        }

        [Fact] public void 목인나한_나뭇조각은_enemies_json_값을_쓴다()
        {
            var m = _d.Enemy("mogin_nahan");
            Assert.Equal("splinter_burst", m.OnDeath);
            Assert.NotNull(m.Death);
            var html = Html();
            Assert.Contains("d.onDeath === 'splinter_burst'", html);
            Assert.Contains("d.death.enemyDamage * f.maxHp / d.health", html);
            Assert.Contains("d.death.playerDamage * f.dmg / d.contactDamage", html);
        }

        [Fact] public void 정산_혈정은_Core_와_같은_값을_쓴다()
        {
            Assert.Equal(1, _d.Balance.Drops.EliteBloodCrystal);
            Assert.Equal(5, _d.Cheonha.Bosses.FirstKill.BloodCrystal);
            Assert.Equal(5, _d.Cheonha.Bosses.RepeatKill.BloodCrystal);
            var html = Html();
            Assert.Contains("BR.firstKill.bloodCrystal", html);
            Assert.Contains("BR.repeatKill.bloodCrystal", html);
            Assert.Contains("BAL.drops.eliteBloodCrystal", html);
            Assert.Matches(@"for k in \([^)]*""drops""[^)]*\)\}", Build());   // balance 묶음 칸 목록 안에
        }

        [Fact] public void 한_생_사건은_chapters_json_에서_오고_나오는_적이_다_있다()
        {
            var life = _d.Chapter("life");
            var evs = life.Events.Where(e => e.Kind == "charge" || e.Kind == "group" || e.Kind == "ring").ToList();
            Assert.Equal(8, evs.Count(e => e.Kind == "charge"));
            Assert.Single(evs, e => e.Kind == "group" && e.Local);
            Assert.Single(evs, e => e.Kind == "ring");
            foreach (var e in evs) Assert.Contains(_d.Enemies, x => x.Id == e.Enemy);
            Assert.Contains("events=[", Build());
            Assert.Contains("\"eventFoes\"", Build());
            Assert.Contains("function lifeEvents()", Html());
        }
    }
}
