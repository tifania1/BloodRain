#nullable enable
// 혈영 분신이 플레이어 무공을 흉내 낼 때 쓰는 값(기획서 6장 '지역별 고유 적', 10장 혈영회주 패턴).
// 잡몹 혈영 분신(enemies.json mimic): 첫 무공(시작 무공) 하나를 같은 레벨로, 피해 DamageScale 배·쿨다운 CooldownScale 배.
// 혈영회주가 부르는 분신(bosspatterns.json mirror_clone 의 mimic): 가진 무공 모두를 같은 레벨로, 피해 절반·쿨다운 그대로.
// 피해는 무공 기본 피해(레벨 보간) × 배율이고 플레이어 심법·문파 보너스는 받지 않는다(2026-11-03 예약 세션이 정함).
// 이렇게 해야 분신이 '빌드의 약점'(빈 방향·느린 쿨)을 그대로 갖되, 공력을 몰아 찍은 빌드가 제 공력에 녹는 일이 없다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    /// <summary>분신이 쓰는 무공 하나. Damage 는 한 번 맞을 때 피해(받는 쪽 호신 빼기 전), Cooldown 은 다음 발까지 초.</summary>
    public readonly struct MimicWeapon
    {
        public readonly string WeaponId;
        public readonly int Level;
        public readonly float Damage;
        public readonly float Cooldown;

        public MimicWeapon(string weaponId, int level, float damage, float cooldown)
        {
            WeaponId = weaponId; Level = level; Damage = damage; Cooldown = cooldown;
        }

        public override string ToString() => $"{WeaponId} Lv{Level} {Damage:0.##}/{Cooldown:0.##}s";
    }

    public static class Mimicry
    {
        /// <summary>
        /// 분신이 쓸 무공 목록. 첫 무공은 늘 시작 무공이다(Loadout 은 무공을 빼지 않는다).
        /// allWeapons 가 아니면 그 하나만, 맞으면 시작 무공을 맨 앞에 두고 나머지를 이름순으로(순서가 판마다 같게).
        /// 독무처럼 쿨다운 0(상시)인 무공은 배율을 곱해도 0 이다.
        /// </summary>
        public static List<MimicWeapon> Weapons(GameData data, CharacterDef character, Loadout loadout, EnemyMimic mimic)
        {
            if (mimic == null) throw new ArgumentNullException(nameof(mimic));
            var ids = new List<string>();
            if (loadout.Weapons.ContainsKey(character.StartWeapon)) ids.Add(character.StartWeapon);
            if (mimic.AllWeapons || ids.Count == 0)
            {
                var rest = new List<string>();
                foreach (var id in loadout.Weapons.Keys)
                    if (id != character.StartWeapon) rest.Add(id);
                rest.Sort(StringComparer.Ordinal);
                if (mimic.AllWeapons) ids.AddRange(rest);
                else if (rest.Count > 0) ids.Add(rest[0]);
            }

            var list = new List<MimicWeapon>(ids.Count);
            foreach (var id in ids)
            {
                var w = data.Weapon(id);
                int level = loadout.Weapons[id];
                list.Add(new MimicWeapon(id, level,
                    WeaponMath.DamageAt(w, level, WeaponGrowth.MaxLevel) * mimic.DamageScale,
                    WeaponGrowth.Cooldown(w, level) * mimic.CooldownScale));
            }
            return list;
        }

        /// <summary>소환 패턴이 mimic 을 따로 주면 그것, 아니면 소환한 적의 mimic. 둘 다 없으면 null(흉내 내지 않는 적).</summary>
        public static EnemyMimic? For(EnemyDef enemy, BossPatternDef? summonedBy = null)
            => summonedBy?.Mimic ?? enemy.Mimic;
    }
}
