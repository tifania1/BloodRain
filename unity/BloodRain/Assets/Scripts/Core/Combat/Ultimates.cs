#nullable enable
// 오의 한 방 피해(기획서 8장 '오의 상세'의 "공력 xN").
//   공력 xN = 그 몸의 시작 무공 한 방 피해 × N × 오의 위력(절정 1.25).
//   시작 무공 한 방 = 지금 레벨의 무공 피해(Lv1→Lv8 보간) × 공력. 시작 무공을 아직 안 들었거나 잃었으면 Lv1 피해 × 공력.
//   절기·음양 극·각성 여운처럼 무공에 붙는 배율은 무공 실행기가 아는 것이라 weaponMultiplier 로 받는다(웹 프로토타입 weaponDamage 와 같다).
// 웹 프로토타입 useUlt 와 시뮬레이터 RunSimulator.FireUltimate 가 쓰던 셈을 Core 로 옮긴 것이다.
// 현경 '오의 2연속'(Life.UltimateCasts)은 한 번 눌러 이 피해가 두 번 나간다. 게이지는 Life.UseUltimate 가 한 번만 비운다.
using System;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public static class Ultimates
    {
        /// <summary>characters.json ultimates 에서 찾는다. 없으면 예외.</summary>
        public static UltimateDef Find(GameData data, string id) =>
            data.Ultimates.FirstOrDefault(u => u.Id == id)
            ?? throw new InvalidOperationException($"모르는 오의: {id}");

        /// <summary>그 몸의 오의.</summary>
        public static UltimateDef Of(GameData data, CharacterDef character) => Find(data, character.Ultimate);

        /// <summary>시작 무공 한 방 피해(공력까지). 오의 배율의 바탕.</summary>
        public static float StartWeaponHit(GameData data, CharacterDef character, Loadout loadout, float might,
            float weaponMultiplier = 1f, int maxLevel = 8)
        {
            var w = data.Weapon(character.StartWeapon);
            float raw = loadout.Weapons.TryGetValue(w.Id, out var lv) ? WeaponMath.DamageAt(w, lv, maxLevel) : w.DamageLv1;
            return raw * might * weaponMultiplier;
        }

        /// <summary>오의 한 방(한 곳·한 타) 피해. 배율 0인 오의(태극무량·천마해체)는 0.</summary>
        public static float HitDamage(UltimateDef ult, float startWeaponHit, float powerMultiplier = 1f) =>
            ult.MightMultiplier <= 0 ? 0f : startWeaponHit * ult.MightMultiplier * powerMultiplier;

        /// <summary>몸·무공 상태에서 곧장 오의 한 방 피해. powerMultiplier 는 Life.UltimatePowerMultiplier.</summary>
        public static float HitDamage(GameData data, CharacterDef character, Loadout loadout, float might,
            float powerMultiplier = 1f, float weaponMultiplier = 1f) =>
            HitDamage(Of(data, character), StartWeaponHit(data, character, loadout, might, weaponMultiplier), powerMultiplier);
    }
}
