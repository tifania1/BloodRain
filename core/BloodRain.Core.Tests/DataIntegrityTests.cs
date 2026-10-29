using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>데이터 파일끼리 서로 어긋나지 않는지. 무공·심법·절기는 1:1:1로 짝지어진다.</summary>
    public class DataIntegrityTests
    {
        readonly Data.GameData _d = TestData.Load();

        [Fact] public void 무공_심법_절기는_각각_16종이다()
        {
            Assert.Equal(16, _d.Weapons.Count);
            Assert.Equal(16, _d.Passives.Count);
            Assert.Equal(16, _d.Evolutions.Count);
        }

        [Fact] public void 아이디는_겹치지_않는다()
        {
            Assert.Equal(_d.Weapons.Count, _d.Weapons.Select(w => w.Id).Distinct().Count());
            Assert.Equal(_d.Passives.Count, _d.Passives.Select(p => p.Id).Distinct().Count());
            Assert.Equal(_d.Evolutions.Count, _d.Evolutions.Select(e => e.Id).Distinct().Count());
            Assert.Equal(_d.Enemies.Count, _d.Enemies.Select(e => e.Id).Distinct().Count());
        }

        [Fact] public void 무공과_심법의_짝이_양쪽에서_일치한다()
        {
            foreach (var w in _d.Weapons)
            {
                var p = _d.Passives.SingleOrDefault(x => x.Id == w.PairPassive);
                Assert.True(p != null, $"{w.NameKo}의 짝 심법 {w.PairPassive}가 없음");
                Assert.Equal(w.Id, p!.PairWeapon);
            }
        }

        [Fact] public void 절기마다_무공과_심법이_실제_짝이다()
        {
            foreach (var e in _d.Evolutions)
            {
                var w = _d.Weapons.Single(x => x.Id == e.Weapon);
                Assert.Equal(w.PairPassive, e.Passive);
            }
            Assert.Equal(16, _d.Evolutions.Select(e => e.Weapon).Distinct().Count());
        }

        [Fact] public void 절기마다_각성_연출에_쓸_한자_이름이_있다()
        {
            foreach (var e in _d.Evolutions)
            {
                Assert.False(string.IsNullOrWhiteSpace(e.Hanja), $"{e.NameKo} 한자 이름 없음");
                // 한글 이름에서 띄어쓰기를 뺀 글자 수와 한자 수가 같아야 한다(천지인 삼검 = 天地人三劍)
                Assert.Equal(e.NameKo.Replace(" ", "").Length, e.Hanja.Length);
            }
        }

        [Fact] public void 처음부터_쓸_수_있는_무공_수가_기획서와_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"처음엔 공격 무공 (\d+)종으로 시작하고 나머지 (\d+)종은");
            Assert.True(m.Success, "9장 첫 문단에서 처음 무공 수를 못 찾았습니다");
            Assert.Equal(int.Parse(m.Groups[1].Value), _d.Weapons.Count(w => w.Unlock == "start"));
            Assert.Equal(int.Parse(m.Groups[2].Value), _d.Weapons.Count(w => w.Unlock != "start"));
        }

        /// <summary>기획서 9장 '공격 무공 16종' 표의 마지막 칸(처음 쓸 수 있는 조건)이 weapons.json unlock 과 같은지.
        /// 꼴은 넷: '처음부터', '처음부터(문파 시작 무공)', '보스 첫 처치(문파로 태어나면 처음부터)', 도전 과제(뒤에 같은 괄호가 붙을 수 있음).</summary>
        [Fact] public void 공격_무공_해금_칸이_데이터와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("공격 무공 16종"));
            Assert.Equal(_d.Weapons.Count, rows.Count);
            var c = _d.Cheonha;
            var bornWith = new System.Collections.Generic.HashSet<string>(); // 표에 '태어나면 처음부터'로 적힌 무공
            foreach (var r in rows)
            {
                var w = _d.Weapons.Single(x => x.NameKo == r[0]);
                string cell = r[r.Length - 1];

                var born = Regex.Match(cell, @"^(.+)\((.+?)(?:으로|로) 태어나면 처음부터\)$");
                if (born.Success)
                {
                    var sect = c.Sects.Single(s => s.NameKo == born.Groups[2].Value);
                    Assert.True(sect.StartWeapon == w.Id, $"{w.NameKo}: {sect.NameKo} 시작 무공이 아님");
                    bornWith.Add(w.Id);
                    cell = born.Groups[1].Value;
                }

                var startOf = Regex.Match(cell, @"^처음부터\((.+) 시작 무공\)$");
                var boss = Regex.Match(cell, @"^(.+) 첫 처치$");
                var kills = Regex.Match(cell, @"^한 (?:판|생)에서 적 ([\d,]+) 처치$");
                var dash = Regex.Match(cell, @"^한 (?:판|생)에서 경공 (\d+)회$");
                var talisman = Regex.Match(cell, @"^정신부 누적 (\d+)회 사용$");
                if (cell == "처음부터") Assert.Equal("start", w.Unlock);
                else if (startOf.Success)
                {
                    Assert.Equal("start", w.Unlock);
                    Assert.Equal(w.Id, c.Sects.Single(s => s.NameKo == startOf.Groups[1].Value).StartWeapon);
                }
                else if (boss.Success)
                {
                    Assert.True(w.UnlockBossRegion != null, $"{w.NameKo}: 땅의 주인 해금인데 unlock 이 {w.Unlock}");
                    var region = c.Region(w.UnlockBossRegion!);
                    var bossName = _d.Bosses.Single(b => b.Id == region.Boss).NameKo;
                    Assert.True(bossName.EndsWith(boss.Groups[1].Value), $"{w.NameKo}: '{boss.Groups[1].Value}' 는 {region.NameKo} 땅의 주인({bossName})이 아님");
                    Assert.True(born.Success, $"{w.NameKo}: 땅의 주인 해금이면 '(그 문파로 태어나면 처음부터)'가 붙어야 한다");
                    Assert.Equal(region.Sect, c.Sects.Single(s => s.StartWeapon == w.Id).Id);
                }
                else if (kills.Success) Assert.Equal($"kills_{kills.Groups[1].Value.Replace(",", "")}_one_run", w.Unlock);
                else if (dash.Success) Assert.Equal($"dash_{dash.Groups[1].Value}_one_run", w.Unlock);
                else if (talisman.Success) Assert.Equal($"seal_talisman_{talisman.Groups[1].Value}_total", w.Unlock);
                else Assert.Fail($"{w.NameKo}: 모르는 해금 칸 '{r[r.Length - 1]}'");
            }
            // 문파 시작 무공이 처음부터가 아니면 표에 '그 문파로 태어나면 처음부터'가 적혀 있어야 한다
            foreach (var s in c.Sects)
                if (_d.Weapon(s.StartWeapon).Unlock != "start")
                    Assert.True(bornWith.Contains(s.StartWeapon), $"{s.NameKo} 시작 무공 {_d.Weapon(s.StartWeapon).NameKo}: 표에 '태어나면 처음부터'가 없음");
        }

        [Fact] public void 진소운은_처음부터이고_대표_인물은_그_문파_해금과_같다()
        {
            Assert.Equal("start", _d.Character("jin_sowoon").Unlock);
            var leaders = _d.Cheonha.Sects.Where(s => s.Leader != null).ToList();
            foreach (var s in leaders) Assert.Equal(s.Unlock, _d.Character(s.Leader!).Unlock);
            // 진소운 말고는 모두 어느 문파의 대표 인물이다
            Assert.Equal(_d.Characters.Count - 1, leaders.Count);
            Assert.All(_d.Characters, ch => Assert.True(ch.Unlock == "start" || ch.UnlockBossRegion != null, $"{ch.NameKo}: {ch.Unlock}"));
        }

        [Fact] public void 캐릭터_8명의_시작_무공과_오의가_존재한다()
        {
            Assert.Equal(8, _d.Characters.Count);
            foreach (var c in _d.Characters)
            {
                Assert.Contains(_d.Weapons, w => w.Id == c.StartWeapon);
                Assert.Contains(_d.Ultimates, u => u.Id == c.Ultimate);
            }
        }

        [Fact] public void 무공_피해는_Lv8이_Lv1보다_크다()
        {
            foreach (var w in _d.Weapons) Assert.True(w.DamageLv8 > w.DamageLv1, w.NameKo);
        }

        [Fact] public void 음양_값은_세_가지_중_하나다()
        {
            var ok = new[] { "yang", "yin", "neutral" };
            foreach (var w in _d.Weapons) Assert.Contains(w.Yinyang, ok);
            foreach (var p in _d.Passives) Assert.Contains(p.Yinyang, ok);
        }
    }
}
