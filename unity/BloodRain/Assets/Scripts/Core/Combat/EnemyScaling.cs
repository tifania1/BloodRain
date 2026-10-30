// 적 기혈 = 기준 기혈 × (1 + 0.08 × 경과 분) × 혈운 농도
// 적 피해 = 기준 피해 × (1 + 0.03 × 경과 분) × √혈운 농도
// 천하 구조에서는 서 있는 자리의 혈운 농도(Regions.Density, Life.Density)를 넘기는 쪽이 맞다.
// 장 이름으로 찾는 쪽(chapterMultiplier)은 그 장 지역의 농도와 같은 값이고 시뮬레이터가 쓴다.
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public sealed class EnemyScaling
    {
        readonly EnemyScalingDef _def;

        public EnemyScaling(EnemyScalingDef def) { _def = def; }

        public float ChapterMultiplier(string chapter) =>
            _def.ChapterMultiplier.TryGetValue(chapter, out var m) ? m : 1f;

        public float Health(EnemyDef e, float minutes, string chapter) =>
            HealthAt(e.Health, minutes, ChapterMultiplier(chapter));

        public float Damage(float baseDamage, float minutes, string chapter) =>
            DamageAt(baseDamage, minutes, ChapterMultiplier(chapter));

        /// <summary>혈운 농도로 바로 셈한 기혈. 농도가 0 아래면 0으로 본다.</summary>
        public float HealthAt(float baseHealth, float minutes, float density) =>
            baseHealth * (1f + _def.HealthPerMinute * minutes) * Math.Max(0f, density);

        /// <summary>혈운 농도로 바로 셈한 피해(√농도).</summary>
        public float DamageAt(float baseDamage, float minutes, float density) =>
            baseDamage * (1f + _def.DamagePerMinute * minutes) * (float)Math.Sqrt(Math.Max(0f, density));
    }

    public static class Damage
    {
        /// <summary>받는 피해 = max(최소 피해, 적 피해 - 호신).</summary>
        public static float Taken(float incoming, float armor, float minDamage = 1f) =>
            Math.Max(minDamage, incoming - armor);
    }

    public static class WeaponMath
    {
        /// <summary>Lv1 → Lv8 선형 보간한 피해.</summary>
        public static float DamageAt(WeaponDef w, int level, int maxLevel = 8)
        {
            if (level <= 1) return w.DamageLv1;
            if (level >= maxLevel) return w.DamageLv8;
            float t = (level - 1) / (float)(maxLevel - 1);
            return w.DamageLv1 + (w.DamageLv8 - w.DamageLv1) * t;
        }
    }
}
