#nullable enable
// 문파 병기 칸과 떨어뜨리기(기획서 8장 '문파 병기와 6단', gear.json, docs/design/sinbyeong.md).
// 칸은 한 생에 하나. 같은 병기를 다시 주우면 한 등급 오르고, 다른 병기를 주우면 그것으로 바꿔 범병부터.
// 그 문파 땅에서 droppers 적은 drop.chance(+ 기연 × luckBonus)로, guaranteed 정예는 확정으로 떨어뜨린다.
// 그 땅의 주인을 쓰러뜨리면 그 문파 병기를 쥐고 있을 때 한 등급 오른다.
// 등급 피해·범위는 그 문파 무공 전부에, 6단(신병합일)은 신병 + 대표 무공이 절기로 깨어 있을 때.
using System;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    /// <summary>한 생의 병기 칸. 생이 끝나면 버린다(저장하지 않음).</summary>
    public sealed class GearSlot
    {
        /// <summary>쥔 병기 id. 빈손이면 null.</summary>
        public string? GearId { get; private set; }
        /// <summary>등급 자리(0 = 범병). 빈손이면 -1.</summary>
        public int Grade { get; private set; } = -1;

        public bool Empty => GearId == null;

        /// <summary>줍는다. 같은 병기면 한 등급(가장 높은 등급이면 그대로), 다르면 바꿔 범병. 돌려주는 값은 주운 뒤 등급 자리.</summary>
        public int Pick(GameData data, string gearId)
        {
            Gear.Def(data, gearId);
            if (GearId == gearId) Grade = Math.Min(Grade + 1, data.Gear.Grades.Count - 1);
            else { GearId = gearId; Grade = 0; }
            return Grade;
        }

        /// <summary>쥔 병기가 그 문파 것이면 한 등급 올린다(땅의 주인 처치). 올렸으면 true.</summary>
        public bool GradeUpIfSect(GameData data, string? sect)
        {
            if (GearId == null || sect == null) return false;
            if (Gear.Def(data, GearId).Sect != sect) return false;
            if (Grade >= data.Gear.Grades.Count - 1) return false;
            Grade++;
            return true;
        }

        public GearGradeDef? GradeDef(GameData data) => GearId == null ? null : data.Gear.Grades[Grade];
    }

    public static class Gear
    {
        public static GearDef Def(GameData data, string id) =>
            data.Gear.Gear.FirstOrDefault(g => g.Id == id) ?? throw new ArgumentException($"없는 병기입니다: {id}");

        /// <summary>그 문파 병기. 없으면 null.</summary>
        public static GearDef? OfSect(GameData data, string? sect) =>
            sect == null ? null : data.Gear.Gear.FirstOrDefault(g => g.Sect == sect);

        /// <summary>그 지역에서 그 적을 쓰러뜨렸을 때 떨어지는 병기 id. 안 떨어지면 null.
        /// luck 은 기연(StatSheet). 확정 정예는 rng 를 쓰지 않는다.</summary>
        public static string? RollDrop(GameData data, string regionId, string enemyId, double luck, Random rng)
        {
            var region = data.Cheonha.Regions.FirstOrDefault(r => r.Id == regionId);
            var gear = OfSect(data, region?.Sect);
            if (gear == null) return null;
            if (gear.Guaranteed.Contains(enemyId)) return gear.Id;
            if (!gear.Droppers.Contains(enemyId)) return null;
            return rng.NextDouble() < DropChance(data, luck) ? gear.Id : null;
        }

        /// <summary>droppers 적 한 마리의 확률(0~1로 자름).</summary>
        public static double DropChance(GameData data, double luck) =>
            Math.Max(0, Math.Min(1, data.Gear.Drop.Chance + Math.Max(0, luck) * data.Gear.Drop.LuckBonus));

        /// <summary>그 무공이 쥔 병기의 등급 보너스를 받는지. 문파 병기면 그 문파 무공 전부(synergies schools members), 문파 없는 병기면 대표 무공 하나.</summary>
        public static bool AppliesTo(GameData data, GearSlot slot, string weaponId)
        {
            if (slot.GearId == null) return false;
            var g = Def(data, slot.GearId);
            if (g.Weapon == weaponId) return true;
            if (g.Sect == null) return false;
            var school = data.Synergies.Schools.FirstOrDefault(s => s.Id == g.Sect);
            return school != null && school.Members.Contains(weaponId) && data.Weapons.Any(w => w.Id == weaponId);
        }

        /// <summary>그 무공 피해에 더하는 몫(0.1 = +10%). 받지 않으면 0.</summary>
        public static double DamageBonus(GameData data, GearSlot slot, string weaponId) =>
            AppliesTo(data, slot, weaponId) ? slot.GradeDef(data)!.Damage : 0;

        /// <summary>그 무공 범위에 더하는 몫. 받지 않으면 0.</summary>
        public static double AreaBonus(GameData data, GearSlot slot, string weaponId) =>
            AppliesTo(data, slot, weaponId) ? slot.GradeDef(data)!.Area : 0;

        /// <summary>그 카드(무공·심법 id)의 레벨업 뽑기 무게: 쥔 병기 문파의 시너지 구성원이면 등급 offerWeight, 아니면 1.
        /// 문파 없는 병기는 대표 무공 하나에만.</summary>
        public static double OfferWeight(GameData data, GearSlot? slot, string offerId)
        {
            if (slot?.GearId == null) return 1.0;
            double w = slot.GradeDef(data)!.OfferWeight;
            if (w <= 0 || w == 1.0) return 1.0;
            var g = Def(data, slot.GearId);
            if (g.Weapon == offerId) return w;
            if (g.Sect == null) return 1.0;
            var school = data.Synergies.Schools.FirstOrDefault(s => s.Id == g.Sect);
            return school != null && school.Members.Contains(offerId) ? w : 1.0;
        }

        /// <summary>6단(신병합일)인지: 쥔 병기가 6단 등급이고, 그 무공이 병기의 대표 무공이고, 절기로 깨어 있다.</summary>
        public static bool IsSixth(GameData data, GearSlot slot, string weaponId, bool awakened)
        {
            if (!awakened || slot.GearId == null) return false;
            return slot.GradeDef(data)!.Sixth && Def(data, slot.GearId).Weapon == weaponId;
        }

        /// <summary>이펙트 단계 1~6. 6단 조건이면 6, 아니면 WeaponGrowth.VisualStage(1~5).</summary>
        public static int VisualStage(GameData data, GearSlot slot, WeaponDef weapon, int level, bool awakened) =>
            IsSixth(data, slot, weapon.Id, awakened) ? 6 : Combat.WeaponGrowth.VisualStage(weapon, level);
    }
}
