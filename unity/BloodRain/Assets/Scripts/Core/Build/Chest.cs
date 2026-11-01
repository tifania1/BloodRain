#nullable enable
// 정예가 떨군 보갑(寶匣)을 열 때(기획서 8장 '절기 그릇과 각성', 보상 표).
// 1) 그릇이 남았고 문턱에 선 무공이 있으면 보갑이 곧 절기 각성이다(강화는 주지 않는다).
// 2) 아니면 가진 무공·심법 강화 n개. n 은 balance.json chest.upgradeCountWeights(1개·2개·3개… 가중치)로 뽑고,
//    장강수로채 진전 4단계 같은 덧셈은 extraUpgrades 로 더한다(ExtraUpgrades(synergy) 가 synergies.json 에서 센다). 강화할 것이 없으면 그 몫은 은자·음식(Fallback).
// 각성 연출은 Combat.Awakening, 그릇 수는 Loadout.Vessels 가 맡는다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public sealed class ChestResult
    {
        /// <summary>보갑이 곧 각성이면 그 절기(아직 Evolve 하지 않음, 각성 연출이 끝날 때 Loadout.Evolve).</summary>
        public EvolutionDef? Awakened;
        /// <summary>적용까지 끝난 강화 목록. 각성이면 비어 있다.</summary>
        public readonly List<Offer> Upgrades = new List<Offer>();
    }

    public static class Chest
    {
        /// <summary>가중치대로 강화 개수(1부터)를 뽑는다.</summary>
        public static int RollUpgradeCount(ChestDef rule, Random rng)
        {
            var w = rule.UpgradeCountWeights;
            if (w.Count == 0) return 1;
            double total = 0;
            foreach (var x in w) { if (x < 0) throw new InvalidOperationException("보갑 가중치는 0 이상입니다"); total += x; }
            if (total <= 0) return 1;
            // 많은 쪽부터 깎는다(웹 프로토타입·시뮬레이터가 처음부터 이 순서로 뽑아 와서 같은 시드면 같은 결과).
            double r = rng.NextDouble() * total;
            for (int i = w.Count - 1; i >= 0; i--)
            {
                r -= w[i];
                if (r < 0) return i + 1;
            }
            return 1;
        }

        /// <summary>보갑을 연다. 강화는 loadout 에 바로 적용한다.</summary>
        public static ChestResult Open(GameData data, Loadout loadout, int playerLevel, Random rng, int extraUpgrades = 0, string mode = "normal")
        {
            if (extraUpgrades < 0) throw new ArgumentOutOfRangeException(nameof(extraUpgrades));
            var result = new ChestResult();
            var evo = loadout.AwakenOnChest(playerLevel, mode);
            if (evo != null) { result.Awakened = evo; return result; }

            int n = RollUpgradeCount(data.Balance.Chest, rng) + extraUpgrades;
            for (int i = 0; i < n; i++)
            {
                var pool = new List<Offer>();
                foreach (var o in loadout.Candidates(EmptySet))
                    if (o.Kind == OfferKind.UpgradeWeapon || o.Kind == OfferKind.UpgradePassive) pool.Add(o);
                var pick = pool.Count > 0 ? pool[rng.Next(pool.Count)] : new Offer(OfferKind.Fallback, "silver_or_food");
                loadout.Apply(pick);
                result.Upgrades.Add(pick);
            }
            return result;
        }

        /// <summary>진전 단계가 주는 보갑 강화 덧셈(synergies.json 단계의 chestUpgrades, 닿은 단계까지 모두).</summary>
        public static int ExtraUpgrades(SynergyState synergy)
        {
            int n = 0;
            foreach (var s in synergy.Schools)
                foreach (var t in s.ReachedTiers) n += t.ChestUpgrades;
            return n;
        }

        static readonly HashSet<string> EmptySet = new HashSet<string>();
    }
}
