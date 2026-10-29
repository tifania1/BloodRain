// 레벨업 카드를 고르는 방식. 사람이 고르는 걸 흉내 내는 단순한 규칙 셋.
//   average : 가진 무공을 키우고, 짝 심법을 Lv3까지 맞추고, 무공 칸은 6개까지 채운다(보통 플레이어)
//   greedy  : 지금 무리에 쏟는 화력이 가장 많이 오르는 카드만 고른다(절기 짝을 따지지 않는다)
//   slow    : 아무거나 고른다(빌드를 계획하지 않는 처음 하는 사람)
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Sim
{
    public sealed class PolicyContext
    {
        public GameData Data = null!;
        public Loadout Loadout = null!;
        public Random Rng = null!;
        /// <summary>이 카드를 골랐을 때 무리에 쏟는 초당 피해 기대값.</summary>
        public Func<Offer, double> CrowdDpsAfter = _ => 0;
    }

    public interface IPolicy
    {
        string Id { get; }
        string NameKo { get; }
        Offer Choose(IReadOnlyList<Offer> offers, PolicyContext ctx);
        /// <summary>초절정 돌파 7칸째를 무공 쪽에 쓸지.</summary>
        bool ExtraSlotIsWeapon(PolicyContext ctx);
        /// <summary>돌파 순간 문턱 무공이 여럿일 때 깨울 절기.</summary>
        EvolutionDef PickAwaken(IReadOnlyList<EvolutionDef> candidates, PolicyContext ctx);
    }

    public static class Policies
    {
        public static readonly string[] All = { "average", "greedy", "slow" };

        public static IPolicy Create(string id) => id switch
        {
            "average" => new AveragePolicy(),
            "greedy" => new GreedyPolicy(),
            "slow" => new SlowPolicy(),
            _ => throw new ArgumentException($"모르는 정책입니다: {id} (average, greedy, slow 중 하나)"),
        };
    }

    public sealed class AveragePolicy : IPolicy
    {
        public string Id => "average";
        public string NameKo => "평균";

        // 점수가 높은 카드를 고른다. 비슷하면 약간의 흔들림(Noise)으로 갈린다.
        const double Noise = 0.6;
        /// <summary>무공이 이 수보다 적으면 새 무공을 먼저 받는다.</summary>
        const int EagerWeapons = 4;
        static readonly HashSet<StatKind> DamageStats = new HashSet<StatKind>
        {
            StatKind.Might, StatKind.CooldownReduction, StatKind.Area, StatKind.Amount, StatKind.CritChance,
        };

        public Offer Choose(IReadOnlyList<Offer> offers, PolicyContext ctx)
        {
            Offer best = offers[0];
            double bestScore = double.MinValue;
            foreach (var o in offers)
            {
                double s = Score(o, ctx) + ctx.Rng.NextDouble() * Noise;
                if (s > bestScore) { bestScore = s; best = o; }
            }
            return best;
        }

        static double Score(Offer o, PolicyContext ctx)
        {
            var l = ctx.Loadout;
            int pairMin = ctx.Data.Balance.Evolution.PairPassiveMinLevel;
            switch (o.Kind)
            {
                case OfferKind.UpgradeWeapon: return 9 + 0.1 * l.Weapons[o.Id];               // 높은 것부터 Lv8로
                case OfferKind.NewWeapon:
                    // 짝 심법을 이미 가진 무공이면 조금 더 반긴다.
                    return (l.Weapons.Count < EagerWeapons ? 9.8 : 8.0) + (HasPairPassive(o.Id, ctx) ? 0.5 : 0);
                case OfferKind.NewPassive:
                    if (IsPairOfOwned(o.Id, ctx)) return 9.0;
                    // 짝 없는 심법은 짝 심법 자리를 남겨 둘 수 있을 때만.
                    if (l.PassiveSlots - l.Passives.Count <= MissingPairs(ctx)) return 2.0;
                    return IsDamage(o.Id, ctx) ? 7.0 : 5.0;
                case OfferKind.UpgradePassive:
                    if (IsPairOfOwned(o.Id, ctx) && l.Passives[o.Id] < pairMin) return 9.3;     // 짝 심법은 Lv3까지 서둘러
                    return IsDamage(o.Id, ctx) ? 6.0 : 4.0;
                default: return 0;
            }
        }

        static bool IsPairOfOwned(string passiveId, PolicyContext ctx) =>
            ctx.Data.Evolutions.Any(e => e.Passive == passiveId && ctx.Loadout.Weapons.ContainsKey(e.Weapon));

        static bool HasPairPassive(string weaponId, PolicyContext ctx) =>
            ctx.Data.Evolutions.Any(e => e.Weapon == weaponId && ctx.Loadout.Passives.ContainsKey(e.Passive));

        /// <summary>가진 무공 중 짝 심법이 아직 없는 수(심법 칸을 그만큼 남겨 둔다).</summary>
        static int MissingPairs(PolicyContext ctx) =>
            ctx.Loadout.Weapons.Keys.Count(w => ctx.Data.Evolutions.Any(e => e.Weapon == w && !ctx.Loadout.Passives.ContainsKey(e.Passive)));

        static bool IsDamage(string passiveId, PolicyContext ctx) =>
            ctx.Data.Passive(passiveId).Effects.Any(e => DamageStats.Contains(Stats.Parse(e.Stat)));

        public bool ExtraSlotIsWeapon(PolicyContext ctx) => true;
        public EvolutionDef PickAwaken(IReadOnlyList<EvolutionDef> candidates, PolicyContext ctx) => candidates[0];
    }

    public sealed class GreedyPolicy : IPolicy
    {
        public string Id => "greedy";
        public string NameKo => "화력 욕심";

        public Offer Choose(IReadOnlyList<Offer> offers, PolicyContext ctx)
        {
            Offer best = offers[0];
            double bestScore = double.MinValue;
            foreach (var o in offers)
            {
                double s = ctx.CrowdDpsAfter(o) + ctx.Rng.NextDouble() * 1e-6;
                if (s > bestScore) { bestScore = s; best = o; }
            }
            return best;
        }

        public bool ExtraSlotIsWeapon(PolicyContext ctx) => true;
        public EvolutionDef PickAwaken(IReadOnlyList<EvolutionDef> candidates, PolicyContext ctx) => candidates[0];
    }

    public sealed class SlowPolicy : IPolicy
    {
        public string Id => "slow";
        public string NameKo => "아무거나";

        public Offer Choose(IReadOnlyList<Offer> offers, PolicyContext ctx) => offers[ctx.Rng.Next(offers.Count)];
        public bool ExtraSlotIsWeapon(PolicyContext ctx) => ctx.Rng.Next(2) == 0;
        public EvolutionDef PickAwaken(IReadOnlyList<EvolutionDef> candidates, PolicyContext ctx) =>
            candidates[ctx.Rng.Next(candidates.Count)];
    }
}
