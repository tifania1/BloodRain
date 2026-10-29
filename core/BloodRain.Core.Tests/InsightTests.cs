using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Build;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>돌파 깨달음(insights.json)과 3택1 추첨(Insights, InsightPicks).</summary>
    public class InsightTests
    {
        readonly Data.GameData _d = TestData.Load();

        Insights NewInsights() => Insights.From(_d);

        /// <summary>기획서 3장 '깨달음 예시' 목록 줄: (이름, 괄호 속 한자 또는 null, 설명).</summary>
        static List<(string name, string? hanja, string text)> GddInsights()
        {
            var section = TestData.GddSection("경지표");
            int start = section.IndexOf("**깨달음 예시**");
            Assert.True(start >= 0, "기획서에 깨달음 예시가 없음");
            var list = new List<(string, string?, string)>();
            foreach (var raw in section.Substring(start).Split('\n').Skip(1))
            {
                var line = raw.TrimEnd();
                if (line.Trim().Length == 0) { if (list.Count > 0) break; continue; }
                var m = Regex.Match(line, @"^- ([^(:]+)(?:\(([^)]+)\))?: (.+)$");
                Assert.True(m.Success, $"깨달음 줄 모양이 다름: {line}");
                list.Add((m.Groups[1].Value.Trim(), m.Groups[2].Success ? m.Groups[2].Value : null, m.Groups[3].Value.Trim()));
            }
            return list;
        }

        /// <summary>문장 속 숫자. 40% → 0.4, 2배 → 2, '절반' → 0.5.</summary>
        static List<float> NumbersIn(string text)
        {
            var nums = Regex.Matches(text, @"(\d+(?:\.\d+)?)(%?)")
                .Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / (m.Groups[2].Value == "%" ? 100f : 1f))
                .ToList();
            if (text.Contains("절반")) nums.Add(0.5f);
            return nums.OrderBy(x => x).ToList();
        }

        // ---- 데이터 ----

        [Fact] public void 깨달음은_7종이고_한자와_아이콘_글자가_맞다()
        {
            Assert.Equal(7, _d.Insights.Count);
            Assert.Equal(7, _d.Insights.Select(i => i.Id).Distinct().Count());
            foreach (var i in _d.Insights)
            {
                Assert.Equal(i.NameKo.Length, i.Hanja.Length);
                Assert.Equal(1, i.Glyph.Length);
                Assert.Contains(i.Glyph, i.Hanja);
                Assert.False(string.IsNullOrWhiteSpace(i.Effect), $"{i.NameKo} 효과 종류 없음");
                Assert.NotEmpty(i.Params);
            }
            Assert.Equal(7, _d.Insights.Select(i => i.Effect).Distinct().Count());
        }

        [Fact] public void 깨달음_이름_순서_문장이_기획서와_같다()
        {
            var gdd = GddInsights();
            Assert.Equal(gdd.Select(g => g.name), _d.Insights.Select(i => i.NameKo));
            for (int k = 0; k < gdd.Count; k++)
            {
                var i = _d.Insights[k];
                Assert.Equal(gdd[k].text, i.TextKo);
                if (gdd[k].hanja != null) Assert.Equal(gdd[k].hanja, i.Hanja);
            }
        }

        [Fact] public void 깨달음_효과_숫자는_기획서_문장의_숫자와_같다()
        {
            foreach (var i in _d.Insights)
            {
                var expected = NumbersIn(i.TextKo);
                var actual = i.Params.Values.OrderBy(x => x).ToList();
                Assert.True(expected.Count == actual.Count && expected.Zip(actual).All(p => Math.Abs(p.First - p.Second) < 1e-4),
                    $"{i.NameKo}: 문장 [{string.Join(", ", expected)}] / 데이터 [{string.Join(", ", actual)}]");
            }
            Assert.Equal(0.4f, _d.Insight("ilgeom").Param("damageBonus"), 4);
            Assert.Equal(100f, _d.Insight("salgi").Param("killsPerStack"));
            Assert.Equal(0.3f, _d.Insight("salgi").Param("mightMax"), 4);
        }

        [Fact] public void 없는_효과_값을_찾으면_어느_깨달음인지_알려준다()
        {
            var e = Assert.Throws<KeyNotFoundException>(() => _d.Insight("cheolli").Param("nothing"));
            Assert.Contains("cheolli", e.Message);
        }

        [Fact] public void 이기어검이_걸리는_무공_behavior는_무공_데이터에_있다()
        {
            var igi = _d.Insight("igi");
            Assert.NotEmpty(igi.WeaponBehaviors);
            foreach (var b in igi.WeaponBehaviors) Assert.Contains(_d.Weapons, w => w.Behavior == b);
            // 첫 무공 삼재검법(front_arc)과 나한권(melee_combo)은 꼭 들어간다(웹 프로토타입과 같음)
            Assert.Contains("front_arc", igi.WeaponBehaviors);
            Assert.Contains("melee_combo", igi.WeaponBehaviors);
            foreach (var other in _d.Insights.Where(i => i.Id != "igi")) Assert.Empty(other.WeaponBehaviors);
        }

        [Fact] public void 후보_수_특성은_실제_캐릭터_특성이다()
        {
            foreach (var trait in _d.InsightChoices.ByTrait.Keys) Assert.Contains(_d.Characters, c => c.Trait == trait);
            Assert.Contains(_d.Balance.Realms, r => r.Reward != null && r.Reward.Contains(_d.InsightChoices.RealmReward));
        }

        // ---- 추첨 ----

        [Fact] public void 후보는_기본_3개_진소운만_4개()
        {
            var ins = NewInsights();
            foreach (var c in _d.Characters)
            {
                int expected = c.Id == "jin_sowoon" ? 4 : 3;
                Assert.Equal(expected, ins.ChoiceCount(c));
                Assert.Equal(expected, ins.Roll(Array.Empty<string>(), c, new Random(1)).Count);
            }
        }

        [Fact] public void 깨달음을_고르는_돌파는_이류부터_화경까지()
        {
            var ins = NewInsights();
            var offering = _d.Balance.Realms.Where(ins.OffersAt).Select(r => r.NameKo).ToArray();
            Assert.Equal(new[] { "이류", "일류", "절정", "초절정", "화경" }, offering);
        }

        [Fact] public void 한_번의_후보에는_같은_깨달음이_두_번_나오지_않는다()
        {
            var ins = NewInsights();
            var rng = new Random(7);
            for (int k = 0; k < 500; k++)
            {
                var offer = ins.Roll(Array.Empty<string>(), 4, rng);
                Assert.Equal(offer.Count, offer.Select(o => o.Id).Distinct().Count());
            }
        }

        [Theory]
        [InlineData("jin_sowoon", 1)] [InlineData("jin_sowoon", 2)] [InlineData("jin_sowoon", 3)]
        [InlineData("namgung_hyeon", 1)] [InlineData("namgung_hyeon", 2)] [InlineData("hyegong", 3)]
        public void 이미_가진_깨달음은_다시_나오지_않고_끝까지_다_얻는다(string characterId, int seed)
        {
            var c = _d.Character(characterId);
            var ins = NewInsights();
            var picks = new InsightPicks(ins, c);
            var rng = new Random(seed);
            int choices = ins.ChoiceCount(c);
            while (true)
            {
                var offer = picks.Offer(rng);
                int remaining = 7 - picks.Taken.Count;
                Assert.Equal(Math.Min(choices, remaining), offer.Count);
                if (offer.Count == 0) break;
                Assert.DoesNotContain(offer, o => picks.Has(o.Id));
                picks.Choose(offer[rng.Next(offer.Count)].Id);
            }
            Assert.Equal(7, picks.Taken.Distinct().Count());
        }

        [Fact] public void 남은_게_적으면_남은_만큼만_다_가졌으면_빈_목록()
        {
            var ins = NewInsights();
            var jin = _d.Character("jin_sowoon");
            var taken = _d.Insights.Take(5).Select(i => i.Id).ToList();
            var offer = ins.Roll(taken, jin, new Random(3));
            Assert.Equal(new[] { "salgi", "yeokgeun" }, offer.Select(o => o.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            Assert.Empty(ins.Roll(_d.Insights.Select(i => i.Id), jin, new Random(3)));
        }

        [Fact] public void 같은_시드면_같은_후보()
        {
            var ins = NewInsights();
            var a = ins.Roll(new[] { "ungi" }, 3, new Random(42)).Select(o => o.Id).ToArray();
            var b = ins.Roll(new[] { "ungi" }, 3, new Random(42)).Select(o => o.Id).ToArray();
            Assert.Equal(a, b);
        }

        [Fact] public void 고정_시드로_많이_뽑으면_일곱_가지가_고르게_나온다()
        {
            var ins = NewInsights();
            var rng = new Random(2026);
            var count = _d.Insights.ToDictionary(i => i.Id, _ => 0);
            const int rolls = 7000;
            for (int k = 0; k < rolls; k++)
                foreach (var o in ins.Roll(Array.Empty<string>(), 3, rng)) count[o.Id]++;
            // 한 번에 3장이니 각자 3/7 확률. 기대값 3000, 표준편차 약 41
            foreach (var kv in count) Assert.InRange(kv.Value, 2800, 3200);
        }

        [Fact] public void 후보에_없는_깨달음은_고를_수_없다()
        {
            var picks = new InsightPicks(NewInsights(), _d.Character("namgung_hyeon"));
            Assert.Throws<InvalidOperationException>(() => picks.Choose("ilgeom")); // 아직 후보를 안 받음
            var offer = picks.Offer(new Random(5));
            var notOffered = _d.Insights.First(i => offer.All(o => o.Id != i.Id));
            Assert.Throws<InvalidOperationException>(() => picks.Choose(notOffered.Id));
            picks.Choose(offer[0].Id);
            Assert.Empty(picks.Offered);
            Assert.Throws<InvalidOperationException>(() => picks.Choose(offer[1].Id)); // 하나 고르면 후보는 닫힌다
        }

        [Fact] public void 녹색_구슬로_레벨60까지_가면_돌파마다_한_번씩_네_번_고른다()
        {
            var run = new RunXp(new XpCurve(_d.Balance.XpCurve), new Realms(_d.Balance.Realms));
            var ins = NewInsights();
            var picks = new InsightPicks(ins, _d.Character("namgung_hyeon"));
            var rng = new Random(11);
            int offers = 0;
            while (run.Level < 60)
            {
                foreach (var realm in run.Absorb(5).Breakthroughs)
                {
                    if (!ins.OffersAt(realm)) continue;
                    var offer = picks.Offer(rng);
                    Assert.Equal(3, offer.Count);
                    picks.Choose(offer[0].Id);
                    offers++;
                }
            }
            Assert.Equal(4, offers);
            Assert.Equal(4, picks.Taken.Distinct().Count());
        }
    }
}
