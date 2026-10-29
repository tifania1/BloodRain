#nullable enable
// 돌파 깨달음 고르기. 보상에 insight_choice 가 붙은 경지를 돌파하면 아직 안 가진 깨달음 중에서
// 몇 장을 뽑아 보여 주고 하나를 고른다. 보여 주는 수는 insights.json choices(기본 3, 진소운 특성은 4).
// 남은 깨달음이 그보다 적으면 남은 만큼만, 다 가졌으면 빈 목록이다(화면은 돌파 연출만 하고 닫는다).
// 난수는 밖에서 받는다. 같은 시드면 같은 후보가 나와야 일일 도전과 테스트가 된다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public sealed class Insights
    {
        readonly IReadOnlyList<InsightDef> _all;
        readonly InsightChoicesDef _choices;

        public Insights(IReadOnlyList<InsightDef> all, InsightChoicesDef choices)
        {
            if (all.Select(i => i.Id).Distinct().Count() != all.Count)
                throw new ArgumentException("insights.json 에 같은 id 가 두 번 있습니다");
            _all = all;
            _choices = choices;
        }

        public static Insights From(GameData data) => new Insights(data.Insights, data.InsightChoices);

        public IReadOnlyList<InsightDef> All => _all;

        public InsightDef Get(string id) => _all.First(i => i.Id == id);

        /// <summary>이 캐릭터가 돌파 때 보는 후보 수.</summary>
        public int ChoiceCount(CharacterDef character) =>
            _choices.ByTrait.TryGetValue(character.Trait, out var n) ? n : _choices.Base;

        /// <summary>이 경지를 돌파하면 깨달음을 고르는지.</summary>
        public bool OffersAt(RealmDef realm) =>
            realm.Reward != null && realm.Reward.Split(',').Any(r => r.Trim() == _choices.RealmReward);

        /// <summary>taken 을 뺀 깨달음 중에서 count 개를 겹치지 않게 뽑는다. 데이터 순서 위에서 부분 셔플.</summary>
        public IReadOnlyList<InsightDef> Roll(IEnumerable<string> taken, int count, Random rng)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var skip = new HashSet<string>(taken);
            var pool = _all.Where(i => !skip.Contains(i.Id)).ToList();
            int n = Math.Min(count, pool.Count);
            for (int i = 0; i < n; i++)
            {
                int j = rng.Next(i, pool.Count);
                var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
            }
            return pool.GetRange(0, n);
        }

        public IReadOnlyList<InsightDef> Roll(IEnumerable<string> taken, CharacterDef character, Random rng) =>
            Roll(taken, ChoiceCount(character), rng);
    }

    /// <summary>한 판에서 얻은 깨달음. 돌파 때 Offer 로 후보를 받고, 그중 하나를 Choose 한다.</summary>
    public sealed class InsightPicks
    {
        readonly Insights _insights;
        readonly CharacterDef _character;
        readonly List<string> _taken = new List<string>();
        IReadOnlyList<InsightDef> _offered = Array.Empty<InsightDef>();

        public InsightPicks(Insights insights, CharacterDef character)
        {
            _insights = insights;
            _character = character;
        }

        /// <summary>고른 순서대로.</summary>
        public IReadOnlyList<string> Taken => _taken;
        /// <summary>지금 화면에 떠 있는 후보. 고르고 나면 빈다.</summary>
        public IReadOnlyList<InsightDef> Offered => _offered;

        public bool Has(string id) => _taken.Contains(id);

        public IReadOnlyList<InsightDef> Offer(Random rng)
        {
            _offered = _insights.Roll(_taken, _character, rng);
            return _offered;
        }

        public InsightDef Choose(string id)
        {
            var pick = _offered.FirstOrDefault(i => i.Id == id);
            if (pick == null) throw new InvalidOperationException($"지금 후보에 없는 깨달음입니다: {id}");
            _taken.Add(id);
            _offered = Array.Empty<InsightDef>();
            return pick;
        }
    }
}
