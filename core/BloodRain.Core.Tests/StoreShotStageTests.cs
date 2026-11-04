using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Core.Run;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>스토어 스크린샷 시작 상태(ST-03): Editor/StoreShots/store_shots.json 이 trailer_screens.md 표와 맞고,
    /// Core 가 그 상태로 한 생을 세울 수 있고, 같은 시드면 몇 번을 세워도 같은 그림이 나오는지.</summary>
    public class StoreShotStageTests
    {
        readonly GameData _d = Load();

        static string ShotsPath => Path.Combine(RepoRoot, "unity", "BloodRain", "Assets", "Editor", "StoreShots", StoreShotDefs.FileName + ".json");
        static StoreShotDefs Defs() => StoreShotDefs.FromJson(File.ReadAllText(ShotsPath));
        StoreShotStage Stage(StoreShotDef s, StoreShotDefs? defs = null) => StoreShotStage.Build(_d, s, (defs ?? Defs()).Seed);
        static IEnumerable<StoreShotDef> Battles() => Defs().Shots.Where(s => s.Screen == StoreShotScreen.Battle);

        [Fact]
        public void 열_장이_표와_같은_차례이고_id가_겹치지_않는다()
        {
            var defs = Defs();
            Assert.Equal(Enumerable.Range(1, 10), defs.Shots.Select(s => s.No));
            Assert.Equal(defs.Shots.Count, defs.Shots.Select(s => s.Id).Distinct().Count());
            Assert.All(defs.Shots, s => Assert.Matches("^[a-z0-9_]+$", s.Id));
        }

        [Fact]
        public void 지역_몸_경지_무공_절기_오의가_표와_같다()
        {
            var doc = StoreShotsTests.Shots().ToDictionary(s => s.No);
            foreach (var s in Defs().Shots)
            {
                var row = doc[s.No];
                if (s.Screen == StoreShotScreen.Rebirth)
                {
                    Assert.Empty(row.Region);
                    continue;
                }
                Assert.Equal(row.Region, new[] { s.Region });
                Assert.Equal(row.Body, new[] { s.Body });
                Assert.Equal(row.Realm, new[] { s.Realm });
                Assert.Equal(row.Weapons.OrderBy(x => x), s.Weapons.Keys.OrderBy(x => x));
                Assert.Equal(row.Evolution, s.Evolution == null ? new string[0] : new[] { s.Evolution });
                Assert.Equal(row.Ultimate, s.Ultimate == null ? new string[0] : new[] { s.Ultimate });
            }
        }

        [Fact]
        public void 이펙트_칸의_무공_단계가_무공_레벨과_맞다()
        {
            // 표 '이펙트' 칸의 "나한권 1단"처럼 무공 이름 뒤 단계는 무공 레벨로 정해진다(vfx.md 단계 규칙, WeaponGrowth.VisualTier).
            var byName = _d.Weapons.ToDictionary(w => w.NameKo);
            var doc = StoreShotsTests.Shots().ToDictionary(s => s.No);
            int checkedCount = 0;
            foreach (var s in Battles())
            {
                foreach (Match m in Regex.Matches(doc[s.No].Effects, @"(\S+) ([123])단"))
                {
                    if (!byName.TryGetValue(m.Groups[1].Value, out var w)) continue; // 절기·오의 이름은 무공 레벨과 상관없다
                    Assert.True(s.Weapons.ContainsKey(w.Id), $"{s.No}번 이펙트에 {w.NameKo} 가 있는데 무공 목록에 없다");
                    Assert.True(WeaponGrowth.VisualTier(w, s.Weapons[w.Id]) == int.Parse(m.Groups[2].Value),
                        $"{s.No}번 {w.NameKo} Lv{s.Weapons[w.Id]} 은 {WeaponGrowth.VisualTier(w, s.Weapons[w.Id])}단인데 표는 {m.Groups[2].Value}단");
                    checkedCount++;
                }
            }
            Assert.True(checkedCount >= 5, $"단계를 견준 무공이 {checkedCount}개뿐이다(표 글을 못 읽는 것 같다)");
        }

        [Fact]
        public void 적_수는_PC_목표_안이고_주된_적은_데이터에_있다()
        {
            int limit = _d.Balance.Limits.EnemiesPc;
            foreach (var s in Battles())
            {
                Assert.InRange(s.Enemies, 1, limit);
                if (s.Enemy != null) Assert.Contains(s.Enemy, _d.Enemies.Select(e => e.Id));
            }
        }

        [Fact]
        public void 시각은_레벨_속도와_맞는다()
        {
            // 11장: 10분 Lv30, 20분 Lv60(분당 3). 서장은 10분 한 장이고 8분 30초부터 새 적을 만들지 않는다(chapters.json).
            var prologue = _d.Chapter("prologue");
            foreach (var s in Battles())
            {
                if (s.Body == _d.Cheonha.PrologueBody.Character)
                {
                    Assert.InRange(s.Minutes, 0f, prologue.StopSpawningAtMinute ?? prologue.DurationMinutes);
                    continue;
                }
                Assert.True(Math.Abs(s.Minutes - s.Level / 3f) <= 1f, $"{s.No}번 Lv{s.Level} 인데 {s.Minutes}분(11장 속도면 {s.Level / 3f:0.0}분)");
            }
        }

        [Fact]
        public void 싸움_장면마다_그_상태로_한_생이_선다()
        {
            int limit = _d.Balance.Limits.EnemiesPc;
            foreach (var s in Battles())
            {
                var st = Stage(s);
                var life = st.Life!;
                Assert.Equal(s.Region, life.RegionId);
                Assert.Equal(s.Level, life.Level);
                Assert.Equal(life.Scale.RealmIndex(s.Realm), life.Realm);
                Assert.InRange(life.Minutes, s.Minutes, s.Minutes + 0.05f);
                Assert.Equal(s.Weapons.OrderBy(k => k.Key), life.Loadout.Weapons.OrderBy(k => k.Key));
                Assert.Equal(s.Passives.OrderBy(k => k.Key), life.Loadout.Passives.OrderBy(k => k.Key));
                if (s.Evolution != null) Assert.Contains(_d.Evolutions.First(e => e.Id == s.Evolution).Weapon, life.Loadout.Evolved);
                else Assert.Empty(life.Loadout.Evolved);
                Assert.False(life.Awakening.IsActive, $"{s.No}번 각성 연출이 아직 돈다");
                Assert.Equal(s.Ultimate != null, life.Charge.IsFull);
                Assert.Equal(s.Breakthrough, life.Breakthrough.IsActive);
                Assert.Equal(s.Boss != null ? LifePhase.BossFight : LifePhase.Roaming, life.Phase);
                Assert.Equal(s.Body == _d.Cheonha.PrologueBody.Character, life.Body.IsPrologue);
            }
        }

        [Fact]
        public void 극성은_적어_둔_장면에서만_보인다()
        {
            // 7번(남궁세가 마당)만 태극 게이지가 극성에 닿은 그림. 나머지 장면에 붉은 극성 기운이 끼면 장면이 흐려진다.
            foreach (var s in Battles())
            {
                var gauge = Stage(s).Life!.Loadout.Gauge;
                if (s.Yinyang == StoreShotYinyang.YangExtreme)
                {
                    Assert.Equal(YinYangBand.Extreme, gauge.Band);
                    Assert.True(gauge.Value > 0);
                }
                else Assert.True(gauge.Band != YinYangBand.Extreme, $"{s.No}번이 극성(값 {gauge.Value})");
            }
            Assert.Contains(Battles(), s => s.Yinyang == StoreShotYinyang.YangExtreme);
        }

        [Fact]
        public void 보스_장면은_적은_패턴이_막_판정된_순간이다()
        {
            foreach (var s in Battles().Where(x => x.Boss != null))
            {
                var st = Stage(s);
                var fight = st.Life!.Bosses.Fight!;
                Assert.Equal(_d.Cheonha.Regions.First(r => r.Id == s.Region).Boss, fight.Boss.Id);
                var p = st.Patterns!;
                Assert.Equal(BossPatterns.IsPhase2(p.Def, s.Boss!.Health), p.InPhase2);
                Assert.Equal(fight.MaxHealth * s.Boss.Health, st.BossHealth, 3);
                Assert.True(p.Elapsed < 60f, $"{s.No}번 {s.Boss.Pattern} 까지 {p.Elapsed}초나 민다");
            }
            // 4번은 원각 2막(붉은 빛줄기), 5번은 1막의 십팔나한진.
            var defs = Defs();
            Assert.True(Stage(defs.Shot(4)).Patterns!.InPhase2);
            Assert.False(Stage(defs.Shot(5)).Patterns!.InPhase2);
        }

        [Fact]
        public void 보스_없는_장면은_다시_찍는_동안_싸움이_걸리지_않는다()
        {
            // 땅의 주인이 깨어난 땅(절정의 숭산)에서 보스 없는 그림을 찍을 때 혈운 기둥 원 밖에 세운다.
            foreach (var s in Battles().Where(x => x.Boss == null))
            {
                var life = Stage(s).Life!;
                var at = life.Position;
                for (int i = 0; i < 200; i++) life.Tick(0.05f, at, s.Enemies);
                Assert.Equal(LifePhase.Roaming, life.Phase);
                Assert.Equal(s.Region, life.RegionId);
            }
        }

        [Fact]
        public void 같은_시드면_몇_번을_세워도_같다()
        {
            foreach (var s in Battles())
            {
                var a = Stage(s);
                var b = Stage(s);
                Assert.Equal(a.Life!.Position, b.Life!.Position);
                Assert.Equal(a.Life.Minutes, b.Life.Minutes);
                Assert.Equal(a.Life.Loadout.Gauge.Value, b.Life.Loadout.Gauge.Value);
                Assert.Equal(a.Life.Charge.Value, b.Life.Charge.Value);
                Assert.Equal(a.Life.Xp.Xp, b.Life.Xp.Xp);
                Assert.Equal(a.Patterns?.Elapsed, b.Patterns?.Elapsed);
                for (int i = 0; i < 5; i++)
                {
                    var sa = a.Life.Tick(1f, a.Life.Position, s.Enemies);
                    var sb = b.Life.Tick(1f, b.Life.Position, s.Enemies);
                    Assert.Equal(sa?.Spawns.Count, sb?.Spawns.Count);
                    Assert.Equal(sa?.Events.Count, sb?.Events.Count);
                }
            }
        }

        [Fact]
        public void 레벨업_장면은_무공_둘_심법_하나()
        {
            // 표 8번: 비급 책장 카드 3장(무공 둘, 심법 하나).
            var s = Defs().Shot(8);
            var offers = Stage(s).LevelUpOffers;
            Assert.Equal(3, offers.Count);
            Assert.Equal(2, offers.Count(o => o.Kind == OfferKind.NewWeapon || o.Kind == OfferKind.UpgradeWeapon));
            Assert.Equal(1, offers.Count(o => o.Kind == OfferKind.NewPassive || o.Kind == OfferKind.UpgradePassive));
            Assert.Equal(_d.Balance.LevelUp.BaseChoices, offers.Count);
        }

        [Fact]
        public void 침방_장면은_M2_세_문파에_성질_하나씩()
        {
            // 표 9번: 소림·개방·남궁, 성질 하나씩. 같이 붙을 수 없는 성질 쌍은 한 카드에 하나라 상관없고, 카드끼리는 겹치지 않게 한다.
            var s = Defs().Shot(9);
            var cards = Stage(s).Candidates;
            Assert.Null(Stage(s).Life);
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung" }, cards.Select(c => c.Sect.Id));
            Assert.All(cards, c => Assert.Single(c.Traits));
            Assert.Equal(3, cards.Select(c => c.Traits[0].Id).Distinct().Count());
            Assert.All(cards, c => Assert.Equal(c.Sect.Region, c.Region.Id));
        }

        [Fact]
        public void 규칙으로_닿을_수_없는_상태는_세우지_않는다()
        {
            var defs = Defs();
            StoreShotDef Edit(int no, Action<StoreShotDef> f)
            {
                var s = StoreShotDefs.FromJson(File.ReadAllText(ShotsPath)).Shot(no);
                f(s);
                return s;
            }
            // 레벨업 횟수보다 많은 단계
            Assert.Throws<InvalidOperationException>(() => Stage(Edit(2, s => s.Weapons["nahan_fist"] = 8), defs));
            // 짝 심법이 모자란 절기
            Assert.Throws<InvalidOperationException>(() => Stage(Edit(1, s => s.Passives["geumgang"] = 2), defs));
            // 경지와 맞지 않는 레벨
            Assert.Throws<InvalidOperationException>(() => Stage(Edit(3, s => s.Level = 35), defs));
            // 몸과 다른 오의
            Assert.Throws<InvalidOperationException>(() => Stage(Edit(6, s => s.Ultimate = "saja_hu"), defs));
            // 땅의 주인이 없는 땅의 보스 장면
            Assert.Throws<InvalidOperationException>(() => Stage(Edit(7, s => s.Boss = new StoreShotBossDef { Pattern = "vajra_cone" }), defs));
        }
    }
}
