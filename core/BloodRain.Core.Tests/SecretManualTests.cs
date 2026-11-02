using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>비급 해금(S-06, 기획서 10·20장): 센 시너지는 비급 층으로 잠긴 채 시작하고, 갖춘 채 생을 마치면 찢긴 비급, 장경각에서 윤회의 기로 복원.</summary>
    public class SecretManualTests
    {
        readonly GameData _d = TestData.Load();
        static readonly string[] None = new string[0];

        static Loadout L(params (string, int)[] items) => TestData.Build("jin_sowoon", items);

        // ---- 데이터 ----

        [Fact] public void 비급_id는_겹치지_않고_장경각이_이름_한자_설명을_모두_찾는다()
        {
            // 장경각·정산 화면(Presentation/Meta/ManualNames)은 연환 id 를 먼저 찾고, 없으면 "문파:단계" 로 나눠 읽는다.
            // 연환 id 에 ':' 가 들어가거나 문파 id 와 겹치면 엉뚱한 이름이 뜬다.
            var ids = _d.Synergies.SecretManualIds.ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(_d.Synergies.Links, l => Assert.DoesNotContain(':', l.Id));
            Assert.Empty(_d.Synergies.Links.Select(l => l.Id).Intersect(_d.Synergies.Schools.Select(s => s.Id)));
            foreach (var id in ids)
            {
                var link = _d.Synergies.Links.FirstOrDefault(l => l.ManualId == id);
                if (link != null)
                {
                    Assert.False(string.IsNullOrWhiteSpace(link.NameKo), id);
                    Assert.False(string.IsNullOrWhiteSpace(link.Hanja), id);
                    Assert.False(string.IsNullOrWhiteSpace(link.Desc), id);
                    continue;
                }
                var parts = id.Split(':');
                Assert.Equal(2, parts.Length);
                var school = _d.Synergies.Schools.Single(s => s.Id == parts[0]);
                var tier = school.Tiers.Single(t => t.Count == int.Parse(parts[1]));
                Assert.True(tier.IsSecret, id);
                Assert.False(string.IsNullOrWhiteSpace(school.NameKo), id);
                Assert.False(string.IsNullOrWhiteSpace(school.Hanja), id);
                Assert.False(string.IsNullOrWhiteSpace(tier.Desc), id);
            }
        }

        [Fact] public void 비급_층은_점혈_쌍검합벽_취중_개방3_곤륜3이다()
        {
            Assert.Equal(new[] { "ssanggeom", "jeomhyeol", "chwijung", "gaebang:3", "kunlun:3" }, _d.Synergies.SecretManualIds.ToArray());
            Assert.All(_d.Synergies.Links, l => Assert.Contains(l.Tier, new[] { SynergyDefs.BaseTier, SynergyDefs.SecretTier }));
            Assert.All(_d.Synergies.Schools.SelectMany(s => s.Tiers), t => Assert.Contains(t.Tier, new[] { SynergyDefs.BaseTier, SynergyDefs.SecretTier }));
        }

        [Fact] public void 복원_값은_봉인석_2개부터이고_뒤로_갈수록_줄지_않는다()
        {
            var m = _d.Synergies.SecretManuals;
            int n = _d.Synergies.SecretManualIds.Count();
            Assert.Equal(n, m.SealStones.Count);
            Assert.Equal(n, m.RebirthQi.Count);
            Assert.Equal(2, m.SealStonesFor(0));
            Assert.Equal(new[] { 3, 4, 5 }, m.SealStones.Skip(1).Take(3));
            for (int i = 1; i < n; i++)
            {
                Assert.True(m.SealStones[i] >= m.SealStones[i - 1]);
                Assert.True(m.RebirthQi[i] > m.RebirthQi[i - 1]);
            }
            Assert.True(m.SealStones.Max() <= _d.Cheonha.Bosses.FinalRequires.SealStones);
            Assert.Equal(m.RebirthQi.Last(), m.RebirthQiFor(99)); // 목록보다 많으면 마지막 값
        }

        [Fact] public void 기획서_20장_복원_값_표가_데이터와_같다()
        {
            var m = _d.Synergies.SecretManuals;
            var rows = System.Text.RegularExpressions.Regex.Matches(TestData.Gdd, @"^\| (\d) \| (\d)개 이상 \| ([\d,]+) \|$", System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.Equal(m.RebirthQi.Count, rows.Count);
            foreach (System.Text.RegularExpressions.Match r in rows)
            {
                int i = int.Parse(r.Groups[1].Value) - 1;
                Assert.Equal(m.SealStones[i], int.Parse(r.Groups[2].Value));
                Assert.Equal(m.RebirthQi[i], int.Parse(r.Groups[3].Value.Replace(",", "")));
            }
            foreach (var l in _d.Synergies.Links.Where(l => l.IsSecret))
                Assert.Contains($"| {l.NameKo} | 연환 |", TestData.Gdd);
        }

        // ---- 판정 ----

        [Fact] public void 잠긴_연환은_켜지지_않고_잠긴_목록에만_있다()
        {
            var l = L(("samjae_sword", 4), ("changcheon_qi", 4));
            var locked = Synergy.Compute(_d, l, null, None);
            Assert.False(locked.HasLink("ssanggeom"));
            Assert.Equal(new[] { "ssanggeom" }, locked.LockedLinks.Select(x => x.Id));
            Assert.Equal(new[] { "ssanggeom" }, locked.LockedManualIds);

            var open = Synergy.Compute(_d, l, null, new[] { "ssanggeom" });
            Assert.True(open.HasLink("ssanggeom"));
            Assert.Empty(open.LockedLinks);
            Assert.True(Synergy.Compute(_d, l).HasLink("ssanggeom")); // restored 를 안 넘기면 모두 열림
        }

        [Fact] public void 기본_층_연환은_복원_없이도_켜진다()
        {
            var s = Synergy.Compute(_d, L(("samjae_sword", 4), ("falling_petals", 4)), null, None);
            Assert.True(s.HasLink("geomhwa"));
            Assert.Empty(s.LockedManualIds);
        }

        [Fact] public void 잠긴_진전_단계는_건너뛰고_그_위_단계는_켜진다()
        {
            var three = L(("drunken_staff", 1), ("palm_wind", 1), ("juseon", 1));
            var s3 = Synergy.Compute(_d, three, null, None).School("gaebang");
            Assert.Equal(3, s3.Count);
            Assert.Equal(2, s3.TierCount);
            Assert.Equal(new[] { 3 }, s3.LockedTiers.Select(t => t.Count));
            Assert.Equal(4, s3.Next!.Count);
            Assert.False(SynergyEffects.Of(Synergy.Compute(_d, three, null, None)).Has("low_health_might"));
            Assert.True(SynergyEffects.Of(Synergy.Compute(_d, three, null, new[] { "gaebang:3" })).Has("low_health_might"));

            var four = L(("drunken_staff", 1), ("palm_wind", 1), ("juseon", 1), ("pasan", 1));
            var s4 = Synergy.Compute(_d, four, null, None);
            Assert.Equal(4, s4.School("gaebang").TierCount);
            Assert.Equal(new[] { 2, 4 }, s4.School("gaebang").ReachedTiers.Select(t => t.Count));
            Assert.Equal(new[] { "gaebang:3" }, s4.LockedManualIds);
            // 2단계 음식 회복은 그대로 들어간다
            Assert.Equal(0.5f, s4.StatBonus()[StatKind.FoodHeal], 3);
        }

        [Fact] public void 카드_미리보기는_잠긴_연환을_잠겼다고_알리고_켜진다고_하지_않는다()
        {
            var l = L(("samjae_sword", 4), ("changcheon_qi", 3));
            var p = Synergy.Preview(_d, l, new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"), null, None);
            var h = Assert.Single(p.Links);
            Assert.True(h.Locked);
            Assert.True(h.Completes); // 조건은 채운다('?'가 뒤집힐 자리)
            var opened = Synergy.Preview(_d, l, new Offer(OfferKind.UpgradeWeapon, "changcheon_qi"), null, new[] { "ssanggeom" });
            Assert.False(Assert.Single(opened.Links).Locked);
        }

        // ---- 혼: 찢긴 비급과 복원 ----

        Soul AfterPrologue()
        {
            var s = new Soul(_d);
            var life = s.BeginLife(Rebirth.Prologue(_d));
            s.EndLife(life.Finish("fallen", 0));
            return s;
        }

        Soul With(Soul s, int qi, int sealStones)
        {
            var j = JObject.Parse(s.ToJson());
            j["rebirthQi"] = qi;
            j["sealStones"] = new JArray(_d.Cheonha.BossRegions.Where(r => r.SealStone != null).Take(sealStones).Select(r => r.Id));
            return Soul.FromJson(_d, j.ToString());
        }

        [Fact] public void 조건을_갖춘_채_생을_마치면_찢긴_비급으로_오른다()
        {
            var soul = AfterPrologue();
            var life = soul.BeginLife(Rebirth.Body(_d.Cheonha, soul.OpenRebirth(new Random(1)).Candidates[0]));
            var l = L(("samjae_sword", 4), ("changcheon_qi", 4));
            life.NoteSynergy(Synergy.Compute(_d, l, null, soul.RestoredManuals));
            life.NoteSynergy(Synergy.Compute(_d, l, null, soul.RestoredManuals)); // 두 번 적어도 하나
            Assert.Equal(new[] { "ssanggeom" }, life.TornManuals);
            var sum = soul.EndLife(life.Finish("fallen", 0));
            Assert.Equal(new[] { "ssanggeom" }, sum.NewTornManuals);
            Assert.Contains("ssanggeom", soul.TornManuals);
            Assert.False(soul.IsRestored("ssanggeom"));
        }

        [Fact] public void 복원은_봉인석과_윤회의_기가_넉넉해야_하고_값은_복원할수록_오른다()
        {
            var m = _d.Synergies.SecretManuals;
            var soul = AfterPrologue();
            var rec = new LifeRecord(null, "fallen", new RebirthQiLedger(_d.Cheonha.RebirthQi).Settle(0), None, new[] { "cheongun" }, "samryu", None, 0,
                new[] { "ssanggeom", "jeomhyeol" });
            soul = With(soul, 0, 0);
            Assert.Throws<ArgumentException>(() => soul.EndLife(new LifeRecord(null, "fallen", rec.RebirthQi, None, new[] { "cheongun" }, "samryu", None, 0, new[] { "geomhwa" })));

            soul.EndLife(rec);
            Assert.False(soul.CanRestore("ssanggeom")); // 봉인석도 기도 없음
            Assert.False(soul.CanRestore("chwijung"));  // 찢긴 비급이 아님

            soul = With(soul, m.RebirthQiFor(0) - 1, 2);
            Assert.False(soul.TryRestore("ssanggeom")); // 기가 하나 모자람
            soul = With(soul, m.RebirthQiFor(0) + m.RebirthQiFor(1), 1);
            Assert.False(soul.TryRestore("ssanggeom")); // 봉인석 하나
            soul = With(soul, m.RebirthQiFor(0) + m.RebirthQiFor(1), 2);
            Assert.True(soul.TryRestore("ssanggeom"));
            Assert.Equal(m.RebirthQiFor(1), soul.RebirthQi);
            Assert.False(soul.TryRestore("ssanggeom")); // 이미 함
            Assert.Equal((m.SealStonesFor(1), m.RebirthQiFor(1)), soul.NextRestoreCost);
            Assert.False(soul.TryRestore("jeomhyeol")); // 둘째는 봉인석 3개
            soul = With(soul, soul.RebirthQi, 3);
            Assert.True(soul.TryRestore("jeomhyeol"));
            Assert.Equal(0, soul.RebirthQi);

            // 저장하고 읽어도 그대로, 복원한 비급은 다시 찢긴 비급으로 오르지 않는다
            var back = Soul.FromJson(_d, soul.ToJson());
            Assert.Equal(new[] { "jeomhyeol", "ssanggeom" }, back.RestoredManuals.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(new[] { "jeomhyeol", "ssanggeom" }, back.TornManuals.OrderBy(x => x, StringComparer.Ordinal));
            var again = back.EndLife(new LifeRecord(null, "fallen", rec.RebirthQi, None, new[] { "cheongun" }, "samryu", None, 0, new[] { "ssanggeom" }));
            Assert.Empty(again.NewTornManuals);
            Assert.True(Synergy.Compute(_d, L(("samjae_sword", 4), ("changcheon_qi", 4)), null, back.RestoredManuals).HasLink("ssanggeom"));
        }

        [Fact] public void 옛_저장에는_비급이_없다()
        {
            var j = JObject.Parse(new Soul(_d).ToJson());
            j.Remove("tornManuals");
            j.Remove("restoredManuals");
            var s = Soul.FromJson(_d, j.ToString());
            Assert.Empty(s.TornManuals);
            Assert.Empty(s.RestoredManuals);
        }
    }
}
