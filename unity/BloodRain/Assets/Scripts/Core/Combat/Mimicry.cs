#nullable enable
// 혈영 분신이 플레이어 무공을 흉내 낼 때 쓰는 값(기획서 6장 '지역별 고유 적', 10장 혈영회주 패턴).
// 잡몹 혈영 분신(enemies.json mimic): 첫 무공(시작 무공) 하나를 같은 레벨로, 피해 DamageScale 배·쿨다운 CooldownScale 배.
// 혈영회주가 부르는 분신(bosspatterns.json mirror_clone 의 mimic): 가진 무공 모두를 같은 레벨로, 피해 절반·쿨다운 그대로.
// 피해는 무공 기본 피해(레벨 보간) × 배율이고 플레이어 심법·문파 보너스는 받지 않는다(2026-11-03 예약 세션이 정함).
// 절기로 각성한 무공도 각성 전 바탕 무공으로 흉내 낸다(절기 모양·피해는 안 따라 함, 2026-11-03 02:3x 예약 세션이 정함).
// 이렇게 해야 분신이 '빌드의 약점'(빈 방향·느린 쿨)을 그대로 갖되, 공력을 몰아 찍은 빌드가 제 공력에 녹는 일이 없다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    /// <summary>
    /// 분신이 쓰는 무공 하나. Damage 는 한 번 맞을 때 피해(받는 쪽 호신 빼기 전), Cooldown 은 무공 쿨다운 × 배율(상시 무공은 0).
    /// Interval 은 실제로 다음 피해까지 초 — 쿨다운이 있으면 Cooldown, 독무 같은 상시(aura) 무공이면 닿는 간격(tickSeconds, 없으면 0.5) × 쿨 배율.
    /// Aura 면 분신 몸 둘레(무공 range, 경지 m) 안에 있는 동안 Interval 마다 한 번 맞는다(2026-11-03 21시 예약 세션, mimic.md 시뮬레이션 가정과 맞춤).
    /// </summary>
    public readonly struct MimicWeapon
    {
        public readonly string WeaponId;
        public readonly int Level;
        public readonly float Damage;
        public readonly float Cooldown;
        public readonly float Interval;
        public readonly bool Aura;

        public MimicWeapon(string weaponId, int level, float damage, float cooldown)
            : this(weaponId, level, damage, cooldown, cooldown, false) { }

        public MimicWeapon(string weaponId, int level, float damage, float cooldown, float interval, bool aura)
        {
            WeaponId = weaponId; Level = level; Damage = damage; Cooldown = cooldown; Interval = interval; Aura = aura;
        }

        public override string ToString() => $"{WeaponId} Lv{Level} {Damage:0.##}/{Cooldown:0.##}s";
    }

    public static class Mimicry
    {
        /// <summary>상시 무공에 tickSeconds 가 없을 때 닿는 간격(시뮬레이터 WeaponModel.Interval 과 같음).</summary>
        public const float DefaultTickSeconds = 0.5f;

        /// <summary>
        /// 분신이 쓸 무공 목록. 첫 무공은 늘 시작 무공이다(Loadout 은 무공을 빼지 않는다).
        /// allWeapons 가 아니면 그 하나만, 맞으면 시작 무공을 맨 앞에 두고 나머지를 이름순으로(순서가 판마다 같게).
        /// 독무처럼 쿨다운 0(상시)인 무공은 Cooldown 이 배율을 곱해도 0 이고, 대신 Interval·Aura 로 닿는 간격을 준다.
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
                float cooldown = WeaponGrowth.Cooldown(w, level) * mimic.CooldownScale;
                bool aura = w.Behavior == "aura";
                float interval = aura ? (w.TickSeconds ?? DefaultTickSeconds) * mimic.CooldownScale : cooldown;
                list.Add(new MimicWeapon(id, level,
                    WeaponMath.DamageAt(w, level, WeaponGrowth.MaxLevel) * mimic.DamageScale,
                    cooldown, interval, aura));
            }
            return list;
        }

        /// <summary>소환 패턴이 mimic 을 따로 주면 그것, 아니면 소환한 적의 mimic. 둘 다 없으면 null(흉내 내지 않는 적).</summary>
        public static EnemyMimic? For(EnemyDef enemy, BossPatternDef? summonedBy = null)
            => summonedBy?.Mimic ?? enemy.Mimic;
    }
}
