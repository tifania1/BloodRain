using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using BloodRain.Core.Progression;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>환혼 후보 추첨과 몸 만들기(기획서 21장 '환혼: 다음 몸 고르기'·'타고난 성질', 5장 문파 표).</summary>
    public class RebirthTests
    {
        readonly GameData _d = TestData.Load();
        CheonhaDef C => _d.Cheonha;
        IEnumerable<string> AllSects => C.Sects.Select(s => s.Id);
        IEnumerable<string> StartSects => C.Sects.Where(s => s.Unlock == "start").Select(s => s.Id);

        RebirthCandidate Candidate(string sect, params string[] traits) =>
            new RebirthCandidate(C.Sect(sect), C.Region(C.Sect(sect).Region), traits.Select(C.Trait).ToList());

        // ---- 후보 추첨 ----

        [Fact] public void 첫_환혼은_처음_세_문파_고정이고_성질도_다시_고르기도_없다()
        {
            Assert.False(C.Rebirth.FirstRebirthTraits);
            for (int seed = 0; seed < 50; seed++)
            {
                var draw = new RebirthDraw(C, AllSects, 4, 3, first: true, new Random(seed));
                Assert.Equal(C.Rebirth.FirstRebirth, draw.Candidates.Select(x => x.Sect.Id));
                Assert.All(draw.Candidates, x => Assert.Empty(x.Traits));
                Assert.Equal(0, draw.RerollsLeft);
                Assert.Throws<InvalidOperationException>(() => draw.Reroll());
            }
            Assert.Equal(new[] { "shaolin", "gaebang", "namgung" }, C.Rebirth.FirstRebirth);
        }

        [Fact] public void 후보는_해금한_문파에서_겹치지_않게_나온다()
        {
            var unlocked = new[] { "shaolin", "gaebang", "namgung", "tang", "wudang" };
            for (int seed = 0; seed < 500; seed++)
            {
                var list = Rebirth.Roll(C, unlocked, 3, new Random(seed));
                Assert.Equal(3, list.Count);
                Assert.Equal(3, list.Select(x => x.Sect.Id).Distinct().Count());
                Assert.All(list, x => Assert.Contains(x.Sect.Id, unlocked));
                Assert.All(list, x => Assert.Equal(x.Sect.Region, x.Region.Id));
            }
        }

        [Fact] public void 해금한_문파가_모자라면_같은_문파가_성질만_달리해_나온다()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                // 넷째 몸을 샀는데 셋만 열렸을 때: 셋은 모두 나오고 하나가 겹친다
                var four = Rebirth.Roll(C, StartSects, 4, new Random(seed));
                Assert.Equal(4, four.Count);
                Assert.Equal(StartSects.OrderBy(x => x), four.Select(x => x.Sect.Id).Distinct().OrderBy(x => x));
                var twins = four.GroupBy(x => x.Sect.Id).Single(g => g.Count() == 2).ToList();
                Assert.False(twins[0].SameTraits(twins[1]), $"seed {seed}: 같은 문파 두 후보의 성질이 같다");

                // 문파가 하나뿐이면 넷 모두 그 문파, 성질은 서로 다르다
                var one = Rebirth.Roll(C, new[] { "huashan" }, 4, new Random(seed));
                Assert.All(one, x => Assert.Equal("huashan", x.Sect.Id));
                for (int i = 0; i < one.Count; i++)
                    for (int j = i + 1; j < one.Count; j++) Assert.False(one[i].SameTraits(one[j]));
            }
        }

        [Fact] public void 겹친_문파는_끝_카드에만_오지_않는다()
        {
            var places = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++)
            {
                var four = Rebirth.Roll(C, StartSects, 4, new Random(seed));
                var dup = four.GroupBy(x => x.Sect.Id).Single(g => g.Count() == 2).Key;
                places.Add(four.FindIndex(x => x.Sect.Id == dup));
            }
            Assert.True(places.Count >= 3, "겹친 문파의 첫 자리가 거의 한곳에 몰린다");
        }

        [Fact] public void 해금하지_않은_문파나_없는_문파는_안_된다()
        {
            Assert.Throws<InvalidOperationException>(() => Rebirth.Roll(C, Array.Empty<string>(), 3, new Random(1)));
            Assert.Throws<ArgumentException>(() => Rebirth.Roll(C, new[] { "shaolin", "emei" }, 3, new Random(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Rebirth.Roll(C, StartSects, 0, new Random(1)));
        }

        [Fact] public void 문파마다_후보에_들_확률이_같다()
        {
            const int draws = 20000;
            var seen = AllSects.ToDictionary(x => x, _ => 0);
            var rng = new Random(2026);
            for (int i = 0; i < draws; i++)
                foreach (var x in Rebirth.Roll(C, AllSects, 3, rng)) seen[x.Sect.Id]++;
            double expected = 3.0 / C.Sects.Count;
            foreach (var kv in seen) Assert.InRange(kv.Value / (double)draws, expected - 0.02, expected + 0.02);
        }

        [Fact] public void 성질_수는_countWeights_비율이고_같은_성질과_안_맞는_쌍은_같이_안_붙는다()
        {
            const int n = 40000;
            var w = C.BirthTraits.CountWeights;
            Assert.Equal(new[] { 0.2f, 0.6f, 0.2f }, w);
            var byCount = new int[w.Count];
            var perTrait = C.BirthTraits.List.ToDictionary(t => t.Id, _ => 0);
            var rng = new Random(7);
            for (int i = 0; i < n; i++)
            {
                var traits = Rebirth.RollTraits(C.BirthTraits, rng);
                byCount[traits.Count]++;
                Assert.Equal(traits.Count, traits.Select(t => t.Id).Distinct().Count());
                foreach (var pair in C.BirthTraits.Excludes)
                    Assert.False(pair.All(id => traits.Any(t => t.Id == id)), $"{string.Join("·", pair)} 이 같이 붙었다");
                // 데이터 순서로 돌려준다
                var idx = traits.Select(t => C.BirthTraits.List.IndexOf(t)).ToList();
                Assert.Equal(idx.OrderBy(x => x), idx);
                foreach (var t in traits) perTrait[t.Id]++;
            }
            for (int k = 0; k < w.Count; k++) Assert.InRange(byCount[k] / (double)n, w[k] - 0.015, w[k] + 0.015);
            // 성질 하나하나는 고르게 붙는다(안 맞는 쌍이 있는 넷은 조금 덜 붙는다)
            double mean = perTrait.Values.Average();
            Assert.All(perTrait, kv => Assert.InRange(kv.Value, mean * 0.85, mean * 1.15));
        }

        [Fact] public void 같은_시드면_같은_후보가_나온다()
        {
            string Show(IEnumerable<RebirthCandidate> list) =>
                string.Join(" / ", list.Select(x => x.Sect.Id + ":" + string.Join(",", x.Traits.Select(t => t.Id))));
            for (int seed = 0; seed < 20; seed++)
                Assert.Equal(Show(Rebirth.Roll(C, AllSects, 4, new Random(seed))), Show(Rebirth.Roll(C, AllSects, 4, new Random(seed))));
        }

        [Fact] public void 다시_고르기는_남은_횟수만큼만_된다()
        {
            var draw = new RebirthDraw(C, AllSects, 4, 2, first: false, new Random(3));
            Assert.Equal(4, draw.Count);
            Assert.Equal(4, draw.Candidates.Count);
            var first = draw.Candidates;
            draw.Reroll();
            Assert.Equal(1, draw.RerollsLeft);
            Assert.Equal(4, draw.Candidates.Count);
            draw.Reroll();
            Assert.Equal(0, draw.RerollsLeft);
            Assert.Throws<InvalidOperationException>(() => draw.Reroll());
            Assert.NotSame(first, draw.Candidates);

            // 다시 뽑으면 대개 다른 후보가 나온다
            int changed = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                var d = new RebirthDraw(C, AllSects, 3, 1, first: false, new Random(seed));
                var before = d.Candidates.Select(x => x.Sect.Id).ToList();
                if (!before.SequenceEqual(d.Reroll().Select(x => x.Sect.Id))) changed++;
            }
            Assert.True(changed > 40);
        }

        // ---- 몸 만들기 ----

        [Fact] public void 몸은_문파_바탕에_성질을_얹는다()
        {
            var body = Rebirth.Body(C, Candidate("shaolin", "cheonsaeng", "eumche"));
            var ch = body.Character;
            Assert.Equal("shaolin", ch.Id);
            Assert.Equal("nahan_fist", ch.StartWeapon);
            Assert.Equal("saja_hu", ch.Ultimate);
            Assert.Equal("flat_damage_reduction2", ch.Trait);
            Assert.Equal(120f, ch.MaxHealth);
            Assert.Equal(0.9f, ch.MoveSpeed, 3);
            Assert.Equal(10 - 30, body.YinyangStart);
            Assert.Equal("songshan", body.Region.Id);
            Assert.Equal(0f, body.RebirthQiBonus);

            var sheet = body.ComputeStats(_d, new Dictionary<string, int>(), new Dictionary<string, int>());
            Assert.Equal(1.15f, sheet.Might, 3);
            Assert.Equal(-0.05f, sheet.CooldownReduction, 3);
            Assert.Equal(0.3f, sheet.Recovery, 3);
            Assert.Equal(120f, sheet.MaxHealth, 3);

            var loadout = body.NewLoadout(_d);
            Assert.Equal(1, Assert.Single(loadout.Weapons).Value);
            Assert.Equal(1, loadout.Weapons["nahan_fist"]);
            Assert.Equal(-20, loadout.Gauge.Value);
        }

        [Fact] public void 성질_능력치는_연무장과_심법_위에_더해지고_몸은_바뀌지_않는다()
        {
            var body = Rebirth.Body(C, Candidate("namgung", "danmyeong", "ganggol"));
            var extra = new StatBonus();
            extra.Add(StatKind.Might, 0.1f);
            var sheet = body.ComputeStats(_d, new Dictionary<string, int>(), new Dictionary<string, int> { ["Might"] = 2, ["MaxHealth"] = 1 }, extra);
            // 공력: 단명 0.25 + 연무장 0.10 + extra 0.10 / 기혈: 100 × (1 - 0.40 + 0.25 + 0.10)
            Assert.Equal(1.45f, sheet.Might, 3);
            Assert.Equal(95f, sheet.MaxHealth, 3);
            Assert.Equal(0.9f, sheet.MoveSpeed, 3);
            // 꺼낸 사본을 고쳐도 몸의 성질 값은 그대로다
            body.TraitStats.Add(StatKind.Might, 5);
            Assert.Equal(0.25f, body.TraitStats[StatKind.Might], 3);
        }

        [Fact] public void 윤회의_기_성질_보너스는_더한다()
        {
            Assert.Equal(0.5f, Rebirth.Body(C, Candidate("tang", "dunjae", "danmyeong")).RebirthQiBonus, 3);
            Assert.Equal(0.3f, Rebirth.Body(C, Candidate("tang", "dunjae")).RebirthQiBonus, 3);
            Assert.Equal(+30 - 10, Rebirth.Body(C, Candidate("tang", "yangche")).YinyangStart);
        }

        [Fact] public void 모든_성질이_능력치로_읽힌다()
        {
            foreach (var t in C.BirthTraits.List)
            {
                var b = Rebirth.TraitStats(new[] { t });
                foreach (var kv in t.Stats) Assert.Equal(kv.Value, b[Stats.Parse(kv.Key)], 4);
            }
        }

        [Fact] public void 문파마다_몸과_로드아웃이_문파_값대로다()
        {
            foreach (var s in C.Sects)
            {
                var body = Rebirth.Body(C, Candidate(s.Id));
                var l = body.NewLoadout(_d);
                Assert.Equal(new[] { s.StartWeapon }, l.Weapons.Keys);
                Assert.Equal(s.YinyangStart, l.Gauge.Value);
                Assert.Equal(s.MaxHealth, body.Character.MaxHealth);
                Assert.Equal(s.Unlock, body.Character.Unlock);
                Assert.Equal(s.Region, body.Region.Id);
                // 대표 인물이 있는 문파는 그 인물과 태극 구간까지 같다
                if (s.Leader != null) Assert.Equal(_d.Character(s.Leader).HarmonyRange, s.HarmonyRange);
            }
        }

        [Fact] public void 무당_몸은_태극_구간이_40이다()
        {
            var sec = TestData.GddSection("한눈에 보기");
            Assert.Contains("음양조화: 태극 구간 -40~+40", sec.Replace("\\~", "~"));
            Assert.Equal(40, C.Sect("wudang").HarmonyRange);
            var l = Rebirth.Body(C, Candidate("wudang", "yangche")).NewLoadout(_d);
            Assert.Equal(30, l.Gauge.Value);
            Assert.Equal(YinYangBand.Harmony, l.Gauge.Band);
            var other = Rebirth.Body(C, Candidate("namgung", "yangche")).NewLoadout(_d);
            Assert.Equal(40, other.Gauge.Value);
            Assert.Equal(YinYangBand.Bias, other.Gauge.Band);
        }

        [Fact] public void 태어난_문파의_진전은_하나_더_센다()
        {
            Assert.Equal(1, C.Rebirth.HomeSchoolTiers);
            foreach (var s in C.Sects)
            {
                var body = Rebirth.Body(C, Candidate(s.Id));
                var l = body.NewLoadout(_d);
                var school = _d.Synergies.Schools.Single(x => x.Id == s.Id);
                Assert.Contains(s.StartWeapon, school.Members);
                var without = Synergy.Compute(_d, l).School(s.Id);
                var with = body.ComputeSynergy(_d, l).School(s.Id);
                Assert.Equal(1, without.Count);
                Assert.Equal(2, with.Count);
                Assert.Equal(1, with.Bonus);
                // 시작 무공 하나만으로 2개 단계가 켜진다
                Assert.Equal(2, with.TierCount);
                // 다른 문파는 그대로
                Assert.All(body.ComputeSynergy(_d, l).Schools.Where(x => x.School.Id != s.Id), x => Assert.Equal(0, x.Bonus));
            }
            // 고수의 유해 같은 바깥 보너스와 더해진다
            var shaolin = Rebirth.Body(C, Candidate("shaolin"));
            var extra = shaolin.SchoolExtra(new Dictionary<string, int> { ["shaolin"] = 1, ["wudang"] = 1 });
            Assert.Equal(2, extra["shaolin"]);
            Assert.Equal(1, extra["wudang"]);
            Assert.Equal(3, shaolin.ComputeSynergy(_d, shaolin.NewLoadout(_d), new Dictionary<string, int> { ["shaolin"] = 1 }).TierOf("shaolin"));
        }

        [Fact] public void 서장_몸은_진소운_제_몸이다()
        {
            var body = Rebirth.Prologue(_d);
            Assert.True(body.IsPrologue);
            Assert.Null(body.Sect);
            Assert.Same(_d.Character("jin_sowoon"), body.Character);
            Assert.Equal("cheongun", body.Region.Id);
            Assert.Empty(body.Traits);
            Assert.Empty(body.SchoolBonus);
            Assert.Equal(0f, body.RebirthQiBonus);
            var l = body.NewLoadout(_d);
            Assert.Equal(new[] { "samjae_sword" }, l.Weapons.Keys);
            // 진소운 특성: 돌파 깨달음 4개
            Assert.Equal(4, Insights.From(_d).ChoiceCount(body.Character));
            Assert.Equal(3, Insights.From(_d).ChoiceCount(Rebirth.Body(C, Candidate("shaolin")).Character));
        }

        [Fact] public void 후보를_고르면_그_후보의_몸이_된다()
        {
            var draw = new RebirthDraw(C, AllSects, 3, 0, first: false, new Random(11));
            for (int i = 0; i < draw.Candidates.Count; i++)
            {
                var body = draw.Choose(i);
                Assert.Same(draw.Candidates[i].Sect, body.Sect);
                Assert.Equal(draw.Candidates[i].Traits, body.Traits);
                Assert.Equal(draw.Candidates[i].Density, body.Region.Density);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => draw.Choose(3));
        }
    }
}
