using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>기획서 6장 '공통 적' 표의 기혈·속도·접촉 피해 칸과 '행동' 칸에 적힌 숫자가 enemies.json 과 같은지.
    /// 처음 등장 시각은 ChapterDataTests, 지역 고유 적의 기혈·속도·피해는 EliteDataTests 가 본다.</summary>
    public class EnemyTableTests
    {
        readonly GameData _d = Load();

        EnemyDef ByName(string cell) => _d.Enemies.Single(e => e.Chapter == null && e.NameKo == Regex.Replace(cell, @"\(.*\)", "").Trim());

        [Fact] public void 공통_적_기혈_속도_접촉_피해가_데이터와_같다()
        {
            var rows = GddTable(GddSection("공통 적"));
            Assert.Equal(_d.Enemies.Count(e => e.Chapter == null && e.Family != "elite"), rows.Count);
            foreach (var r in rows)
            {
                var e = ByName(r[0]);
                Assert.Equal(Num(e.Health), r[2].Replace(",", ""));

                // '순간이동'(속도 0, 표식 뒤 순간이동), '120 (도망)'
                if (r[3] == "순간이동") { Assert.Equal(0f, e.Speed); Assert.Equal("mark_teleport", e.Behavior); }
                else Assert.Equal(Num(e.Speed), Regex.Match(r[3], @"^\d+").Value);

                // '0 (화살 8)', '0 (장판 초당 6)'
                var m = Regex.Match(r[4], @"^(\d+)(?: \((화살|장판 초당) (\d+)\))?$");
                Assert.True(m.Success, $"{e.NameKo}: 접촉 피해 칸 '{r[4]}'");
                Assert.Equal(Num(e.ContactDamage), m.Groups[1].Value);
                string kind = m.Groups[2].Value, extra = m.Groups[3].Value;
                Assert.Equal(kind == "화살" ? extra : "0", Num(e.ProjectileDamage));
                Assert.Equal(kind == "장판 초당" ? extra : "0", Num(e.ZoneDamagePerSecond));
            }
        }

        /// <summary>'행동' 칸에 숫자로 적힌 것: 넉백 저항 70%, 0.6초 예고선, 1초 뒤 순간이동, 반경 5m +20%, 은자 100.</summary>
        [Fact] public void 공통_적_행동_칸의_숫자가_데이터와_같다()
        {
            var rows = GddTable(GddSection("공통 적")).ToDictionary(r => ByName(r[0]).Id, r => r[6]);

            Assert.Contains($"넉백 저항 {Num(_d.Enemy("donggap").KnockbackResist * 100)}%", rows["donggap"]);
            Assert.Contains($"{Num(_d.Enemy("hyeol_musa").Telegraph)}초 예고선", rows["hyeol_musa"]);
            Assert.Contains($"{Num(_d.Enemy("hyeolyeong_assassin").Telegraph)}초 뒤 순간이동", rows["hyeolyeong_assassin"]);
            var gisu = _d.Enemy("gisu");
            Assert.Contains($"반경 {Num(gisu.AuraRadius)}m 적 이동속도·피해 +{Num(gisu.AuraBonus * 100)}%", rows["gisu"]);
            Assert.True(_d.Enemy("bigang").IgnoresTerrain);
            Assert.Contains("지형 무시", rows["bigang"]);
            var toad = _d.Enemy("golden_toad");
            Assert.NotNull(toad.Reward);
            Assert.Contains($"은자 {toad.Reward!.Silver}과 진기 구슬 다량", rows["golden_toad"]);
            Assert.True(toad.Reward.OrbBurst);

            // 표 아래 문단: 사라지는 초와 혈무사 돌진(2026-11-01 프로토타입에서 옮김)
            var sec = GddSection("공통 적");
            Assert.Contains($"혈인은 나온 뒤 {Num(_d.Enemy("hyeolin").LifeSeconds)}초면 사라지고, 금두꺼비는 {Num(toad.LifeSeconds)}초 안에 못 잡으면 달아납니다", sec);
            var dash = _d.Enemy("hyeol_musa").Dash;
            Assert.NotNull(dash);
            Assert.Contains($"혈무사는 {Num(dash!.Range)}m 안에 들면 예고선을 긋고, {Num(dash.Seconds)}초 동안 초당 {Num(dash.Speed)}m로 찌르며 돌진한 뒤 {Num(dash.RestMin)}~{Num(dash.RestMax)}초 쉬었다가", sec);
            Assert.True(dash.Reach < dash.Range * 1.5f, "돌진이 예고 거리보다 너무 멀리 간다");
            Assert.All(_d.Enemies.Where(e => e.Behavior == "telegraph_dash"), e => { Assert.NotNull(e.Dash); Assert.True(e.Telegraph > 0); });
            var html = ReadRepoFile("web", "prototype", "template.html");
            foreach (var use in new[] { "e.life > E_BY_ID[e.type].lifeSeconds", "d < dash.range", "e.timer = dash.seconds", "e.ax * dash.speed", "rand(dash.restMin, dash.restMax)" })
                Assert.Contains(use, html);

            // 행동 값이 없는 적은 기본값이다.
            var sigang = _d.Enemy("sigang");
            Assert.Equal(0f, sigang.Telegraph);
            Assert.Null(sigang.Reward);
            Assert.Null(sigang.OnDeath);
            Assert.Equal(0f, sigang.LifeSeconds);
            Assert.Null(sigang.Dash);
        }

        /// <summary>지역 고유 적 표의 '행동' 칸: 흑백 쌍강시 '10초 뒤 되살림', 빙강시 '2초 둔화', 수귀 '물 구역에서만', 쓰러질 때 남기는 것.</summary>
        [Fact] public void 지역_고유_적_행동_칸의_숫자가_데이터와_같다()
        {
            var rows = GddTable(GddSection("지역별 고유 적"));
            string Row(string id) => rows.Single(r => r[1].StartsWith(_d.Enemy(id).NameKo))[2];

            Assert.Contains($"{Num(_d.Enemy("twin_gangsi").ReviveSeconds)}초 뒤 다른 쪽이 되살림", Row("twin_gangsi"));
            Assert.Contains($"닿으면 {Num(_d.Enemy("ice_gangsi").OnHitSlowSeconds)}초 둔화", Row("ice_gangsi"));
            Assert.Contains($"이동속도 {Num(_d.Enemy("ice_gangsi").OnHitSlowMultiplier)}배", Row("ice_gangsi"));
            Assert.InRange(_d.Enemy("ice_gangsi").OnHitSlowMultiplier, 0.3f, 0.95f);
            Assert.True(_d.Enemy("water_ghost").OnlyInWater);
            Assert.Contains("물 구역에서만", Row("water_ghost"));
            Assert.Equal("splinter_burst", _d.Enemy("mogin_nahan").OnDeath);
            Assert.Contains("나뭇조각이 터져", Row("mogin_nahan"));
            Assert.Equal("poison_pool", _d.Enemy("poison_toad").OnDeath);
            Assert.Contains("죽을 때 독 장판", Row("poison_toad"));
        }

        /// <summary>2026-11-02: PC 세션(C-02b)이 숫자가 없어 멈춘 행동 갈래. 표 아래 문단과 enemies.json 이 같은지.</summary>
        [Fact] public void 화살_표식_웅덩이_구슬_숫자가_기획서_문단과_같다()
        {
            var sec = GddSection("공통 적");
            var s = _d.Enemy("hyeol_gungsu").Shoot;
            Assert.NotNull(s);
            Assert.Contains($"혈궁수는 플레이어와 {Num(s!.KeepMin)}~{Num(s.KeepMax)}m를 지키며", sec);
            Assert.Contains($"{Num(s.Interval)}초마다 화살 한 발", sec);
            Assert.Contains($"화살은 초속 {Num(s.Speed)}m로 {Num(s.Range)}m 날아가고 반경 {Num(s.Radius)}m", sec);
            Assert.True(s.KeepMin < s.KeepMax && s.KeepMax < s.Range, "지키는 거리보다 화살이 멀리 가야 맞는다");

            var a = _d.Enemy("hyeolyeong_assassin");
            Assert.NotNull(a.Mark);
            Assert.Contains($"반경 {Num(a.Mark!.Radius)}m 표식을 찍고 {Num(a.Telegraph)}초 뒤", sec);
            Assert.Contains($"표식부터 다음 표식까지 {Num(a.Mark.Interval)}초", sec);
            Assert.True(a.Mark.Interval > a.Telegraph);

            var p = _d.Enemy("hyeol_sulsa").Pool;
            Assert.NotNull(p);
            Assert.Contains($"{Num(p!.Interval)}초마다 플레이어 발밑에 반경 {Num(p.Radius)}m 피 웅덩이를 깔고 {Num(p.Seconds)}초 남깁니다", sec);
            Assert.Contains($"웅덩이는 {p.Max}개까지", sec);
            Assert.True(p.Seconds / p.Interval <= p.Max, "깔리는 빠르기에 비해 상한이 낮아 늘 지워진다");

            var r = _d.Enemy("golden_toad").Reward!;
            Assert.Contains($"진기 구슬 {r.Orbs}알(한 알 {Num(r.OrbXp)})", sec);

            // 다른 적에는 붙지 않는다
            Assert.All(_d.Enemies.Where(e => e.Shoot != null), e => Assert.Contains(e.Behavior, new[] { "kite_shoot", "petal_shot", "ice_throw" }));
            Assert.All(_d.Enemies.Where(e => e.Mark != null), e => Assert.StartsWith("mark_teleport", e.Behavior));
            Assert.All(_d.Enemies.Where(e => e.Pool != null), e => Assert.StartsWith("blood_pool", e.Behavior));
        }

        [Fact] public void 덧붙는_레벨업_카드_몫이_기획서와_같다()
        {
            var fc = _d.Balance.LevelUp.FallbackCard;
            Assert.Contains($"이 카드는 은자 {fc.Silver}과 기혈 회복 {Num(fc.Heal)}을 함께 줍니다", TestData.Gdd);
            Assert.Contains("B.levelUp.fallbackCard", System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot, "web", "prototype", "template.html")));
        }

        [Fact] public void 쓰러질_때_남기는_것과_곡선_비행_숫자가_기획서와_같다()
        {
            var sec = GddSection("지역별 고유 적");
            var m = _d.Enemy("mogin_nahan").Death!;
            Assert.Contains($"반경 {Num(m.Radius)}m 안의 다른 적에게 {Num(m.EnemyDamage)}, 플레이어에게 {Num(m.PlayerDamage)} 피해", sec);
            var t = _d.Enemy("poison_toad").Death!;
            Assert.Contains($"반경 {Num(t.Radius)}m 독 장판이 {Num(t.Seconds)}초 남고, 밟으면 초당 {Num(t.PerSecond)} 피해", sec);
            var c = _d.Enemy("bigang").Curve!;
            Assert.Contains($"옆으로 {Num(c.Amplitude)}m씩 {Num(c.Period)}초 주기로", sec);
            // onDeath 가 있는 적은 모두 크기·피해가 있다
            Assert.All(_d.Enemies.Where(e => e.OnDeath != null), e => { Assert.NotNull(e.Death); Assert.True(e.Death!.Radius > 0); });
            Assert.All(_d.Enemies.Where(e => e.Behavior == "fly_curve"), e => Assert.NotNull(e.Curve));
        }
        /// <summary>C-13: 지역 고유 적 나머지 행동 숫자 표(6장 '지역별 고유 적' 둘째 표)와 enemies.json.</summary>
        [Fact] public void 지역_고유_적_행동_숫자_표가_데이터와_같다()
        {
            var sec = GddSection("지역별 고유 적");
            string Row(string name) => sec.Split('\n').Single(l => l.StartsWith($"- {name}: "));

            var pull = _d.Enemy("water_ghost").Pull!;
            Assert.Contains($"{Num(pull.Range)}m 안이면 플레이어를 초속 {Num(pull.Speed)}m로 끌어당김", Row("수귀"));
            var wb = _d.Enemy("water_bandit");
            Assert.Contains($"{Num(wb.Hook!.Range)}m 안이면 {Num(wb.Telegraph)}초 예고 뒤 초속 {Num(wb.Hook.Speed)}m 갈고리. 맞으면 {Num(wb.Hook.BindSeconds)}초", Row("수적"));
            Assert.Contains($"{Num(wb.Hook.Interval)}초마다", Row("수적"));
            var plum = _d.Enemy("plum_sword_ghost");
            var ps = plum.Shoot!;
            Assert.Contains($"{Num(ps.Range)}m 안이면 {Num(ps.Interval)}초마다 꽃잎 {ps.Count}발을 {Num(ps.Spread)}도로 펼쳐 쏨. 초속 {Num(ps.Speed)}m, 한 발 {Num(plum.ProjectileDamage)}", Row("매화검귀"));
            Assert.Contains($"꽃잎 탄 {ps.Count}발", GddTable(sec).Single(r => r[1] == "매화검귀")[2]);
            var yeti = _d.Enemy("yeti");
            var ys = yeti.Shoot!;
            Assert.Contains($"{Num(ys.Range)}m 안이면 {Num(ys.Interval)}초마다 얼음덩이 한 발. 초속 {Num(ys.Speed)}m, 반경 {Num(ys.Radius)}m, {Num(yeti.ProjectileDamage)}", Row("설인"));
            var wolf = _d.Enemy("snow_wolf").Group!;
            Assert.Contains($"{wolf.Size}마리가 함께 나와 양옆 {Num(wolf.FlankAngle)}도로 돌아 들어오다 {Num(wolf.CommitRange)}m 안에서", Row("설랑 떼"));
            var cult = _d.Enemy("demon_cultist").Group!;
            Assert.Contains($"{cult.Size}명이 {Num(cult.Spacing)}m 간격 한 줄", Row("마교도"));
            var cent = _d.Enemy("golden_centipede").Group!;
            Assert.Contains($"{Num(cent.Spacing)}m 간격 마디 {cent.Size}개", Row("금선지네 떼"));
            var mim = _d.Enemy("blood_shadow_clone").Mimic!;
            Assert.Contains($"피해 {Num(mim.DamageScale)}배, 쿨다운 {Num(mim.CooldownScale)}배", Row("혈영 분신"));
            var lion = _d.Enemy("blood_lion");
            Assert.Contains($"{Num(lion.Dash!.Range)}m 안이면 {Num(lion.Telegraph)}초 예고 뒤 {Num(lion.Dash.Seconds)}초 동안 초속 {Num(lion.Dash.Speed)}m 돌진", Row("혈사자"));
            Assert.Contains($"반경 {Num(lion.AuraRadius)}m 적 이동속도·피해 +{Num(lion.AuraBonus * 100)}%를 {Num(lion.RoarSeconds)}초. {Num(lion.Dash.RestMin)}~{Num(lion.Dash.RestMax)}초 쉼", Row("혈사자"));
            var dogin = _d.Enemy("dogin");
            Assert.Contains($"반경 {Num(dogin.ZoneRadius)}m 독안개, 초당 {Num(dogin.ZoneDamagePerSecond)}", Row("독인"));

            // 지역 고유 적은 행동마다 숫자가 다 있다(쫓기만 하는 hop_chase 와 표에 없는 정예는 뺌)
            foreach (var e in _d.Enemies.Where(e => e.Chapter != null && e.Behavior != "hop_chase"))
                Assert.True(e.Shoot != null || e.Pull != null || e.Hook != null || e.Group != null || e.Mimic != null || e.Dash != null
                            || e.ZoneRadius > 0 || e.ReviveSeconds > 0 || e.Death != null, $"{e.NameKo}({e.Behavior}): 행동 숫자가 없다");
        }
    }
}
