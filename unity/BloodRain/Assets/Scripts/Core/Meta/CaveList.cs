#nullable enable
// 수련동 도전 과제 목록(기획서 10장 '객잔 시설' 수련동 '도전 과제 목록과 보상', '해금과 도전 과제' 표, backlog EL-07).
//   혼 기록으로 이룬 것과 남은 것을 보여 주기만 한다. 줄은 데이터에서 만든다(웹 template.html caveRows 와 같은 차례·같은 줄).
//   1 서장 끝 → 연무장·첫 환혼
//   2 땅의 주인 첫 처치(봉마곡 빼고 데이터 차례) → 그 땅으로 여는 문파·그 땅 문파의 대표 인물·그 땅으로 여는 무공(없으면 봉인석만)
//   3 도전 과제 무공(weapons.json unlock 이 kills_/dash_/seal_talisman_ 토큰)
//   4 칭호(cheonha.json titles)
//   5 극성으로 땅의 주인 처치 → 깨달음(insights.json unlock boss_at_extreme)
//   6 의상(cheonha.json costumes boss_in_harmony)
//   7 금두꺼비 누적 → 연무장 항목 최대 레벨(balance.json training bonusMax)
//   8 의서 → 진엔딩 조건 하나(cheonha.json cycle.trueEndingManuscripts)
// 글은 Unity 가 ui.cave.* 키(strings)와 이름 키로 얹는다. 여기서는 무엇·누구·몇인지만 낸다.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public enum CaveRowKind { Prologue, BossFirstKill, Challenge, Title, ExtremeBoss, Costume, GoldenToads, Manuscripts }

    public enum CaveRewardKind { TrainingHallAndRebirth, Sect, Leader, Weapon, SealStone, Title, Insight, Costume, TrainingMax, TrueEndingCondition }

    /// <summary>보상 하나. Id 는 그 종류의 데이터 id(문파·인물·무공·칭호·깨달음·의상·연무장 능력치), Amount 는 TrainingMax 의 더하는 레벨.</summary>
    public readonly struct CaveReward
    {
        public readonly CaveRewardKind Kind;
        public readonly string Id;
        public readonly int Amount;
        public CaveReward(CaveRewardKind kind, string id = "", int amount = 0) { Kind = kind; Id = id; Amount = amount; }
        public override string ToString() => Amount != 0 ? $"{Kind}:{Id}+{Amount}" : Id.Length > 0 ? $"{Kind}:{Id}" : Kind.ToString();
    }

    /// <summary>수련동 한 줄. Id: BossFirstKill 은 지역 id, Challenge 는 토큰, Title·Costume 은 그 id, GoldenToads 는 연무장 능력치, 나머지는 빈 문자열.
    /// Subject: 조건 글에 들어갈 다른 id(BossFirstKill·Costume 은 땅의 주인 id). Target/Progress: 숫자로 세는 조건만(누적은 지금까지, 한 생은 Progress null).</summary>
    public sealed class CaveRow
    {
        public CaveRowKind Kind { get; }
        public string Id { get; }
        public string Subject { get; }
        public bool Done { get; }
        public int? Target { get; }
        public int? Progress { get; }
        public IReadOnlyList<CaveReward> Rewards { get; }

        public CaveRow(CaveRowKind kind, string id, string subject, bool done, int? target, int? progress, IReadOnlyList<CaveReward> rewards)
        {
            Kind = kind; Id = id; Subject = subject; Done = done; Target = target; Progress = progress; Rewards = rewards;
        }

        /// <summary>조건 글 키(strings ko.json ui.cave.*). Challenge 는 카운터마다, Title 은 조건마다 다르다.</summary>
        public string ConditionKey => Kind switch
        {
            CaveRowKind.Prologue => "ui.cave.prologue",
            CaveRowKind.BossFirstKill => "ui.cave.boss_first_kill",
            CaveRowKind.Challenge => UnlockToken.ParseChallenge(Id)!.Value.Counter switch
            {
                ChallengeCounter.Kills => "ui.cave.kills_one_run",
                ChallengeCounter.Dashes => "ui.cave.dashes_one_run",
                _ => "ui.cave.talismans_total",
            },
            CaveRowKind.Title => "ui.cave.title_" + Subject,
            CaveRowKind.ExtremeBoss => "ui.cave.extreme_boss",
            CaveRowKind.Costume => "ui.cave.costume",
            CaveRowKind.GoldenToads => "ui.cave.golden_toads",
            _ => "ui.cave.manuscripts",
        };
    }

    public static class CaveList
    {

        public static List<CaveRow> Rows(Soul soul)
        {
            var data = soul.Data;
            var C = data.Cheonha;
            var rows = new List<CaveRow>
            {
                new CaveRow(CaveRowKind.Prologue, "", "", soul.PrologueDone, null, null, new[] { new CaveReward(CaveRewardKind.TrainingHallAndRebirth) }),
            };

            foreach (var r in C.BossRegions)
            {
                var token = UnlockToken.BossPrefix + r.Id;
                var got = new List<CaveReward>();
                got.AddRange(C.Sects.Where(s => s.Unlock == token).Select(s => new CaveReward(CaveRewardKind.Sect, s.Id)));
                var leader = r.Sect == null ? null : C.Sects.FirstOrDefault(s => s.Id == r.Sect)?.Leader;
                if (leader != null) got.Add(new CaveReward(CaveRewardKind.Leader, leader));
                got.AddRange(data.Weapons.Where(w => w.Unlock == token).Select(w => new CaveReward(CaveRewardKind.Weapon, w.Id)));
                if (got.Count == 0) got.Add(new CaveReward(CaveRewardKind.SealStone, r.SealStone ?? ""));
                rows.Add(new CaveRow(CaveRowKind.BossFirstKill, r.Id, r.Boss!, soul.BossesKilled.Contains(r.Id), null, null, got));
            }

            foreach (var w in data.Weapons)
            {
                var c = UnlockToken.ParseChallenge(w.Unlock);
                if (c == null) continue;
                int? progress = c.Value.OneRun ? (int?)null : c.Value.Counter == ChallengeCounter.Talismans ? soul.TalismansUsedTotal : (int?)null;
                rows.Add(new CaveRow(CaveRowKind.Challenge, w.Unlock, "", soul.IsUnlocked(w.Unlock), c.Value.Count, progress, new[] { new CaveReward(CaveRewardKind.Weapon, w.Id) }));
            }

            foreach (var t in C.Titles)
            {
                int? target = t.When == TitleDef.FoodOneRun ? t.Count : t.When == TitleDef.AllSectsLived ? C.Sects.Count : (int?)null;
                int? progress = t.When == TitleDef.AllSectsLived ? C.Sects.Count(s => soul.LivesBySect.TryGetValue(s.Id, out var n) && n > 0) : (int?)null;
                rows.Add(new CaveRow(CaveRowKind.Title, t.Id, t.When, soul.Titles.Contains(t.Id), target, progress, new[] { new CaveReward(CaveRewardKind.Title, t.Id) }));
            }

            var extremeInsights = data.Insights.Where(i => i.Unlock == UnlockToken.BossAtExtreme).Select(i => new CaveReward(CaveRewardKind.Insight, i.Id)).ToArray();
            if (extremeInsights.Length > 0)
                rows.Add(new CaveRow(CaveRowKind.ExtremeBoss, "", "", soul.IsUnlocked(UnlockToken.BossAtExtreme), null, null, extremeInsights));

            foreach (var c in C.Costumes.Where(c => c.When == CostumeDef.BossInHarmony))
                rows.Add(new CaveRow(CaveRowKind.Costume, c.Id, c.Boss, soul.IsUnlocked(c.Token), null, null, new[] { new CaveReward(CaveRewardKind.Costume, c.Id) }));

            var greed = data.Balance.Training.Items.FirstOrDefault(i => i.BonusMax != null);
            if (greed != null)
            {
                var b = greed.BonusMax!;
                rows.Add(new CaveRow(CaveRowKind.GoldenToads, greed.Stat, "", soul.GoldenToadsTotal >= b.GoldenToadsTotal, b.GoldenToadsTotal, soul.GoldenToadsTotal,
                    new[] { new CaveReward(CaveRewardKind.TrainingMax, greed.Stat, b.Levels) }));
            }

            int need = C.Cycle.TrueEndingManuscripts;
            if (need > 0)
                rows.Add(new CaveRow(CaveRowKind.Manuscripts, "", "", soul.Manuscripts >= need, need, System.Math.Min(soul.Manuscripts, need),
                    new[] { new CaveReward(CaveRewardKind.TrueEndingCondition) }));
            return rows;
        }
    }
}
