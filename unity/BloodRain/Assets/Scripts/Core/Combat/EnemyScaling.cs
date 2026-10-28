// 적 기혈 = 기준 기혈 × (1 + 0.08 × 경과 분) × 장 배율
// 적 피해 = 기준 피해 × (1 + 0.03 × 경과 분) × √장 배율
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
            e.Health * (1f + _def.HealthPerMinute * minutes) * ChapterMultiplier(chapter);

        public float Damage(float baseDamage, float minutes, string chapter) =>
            baseDamage * (1f + _def.DamagePerMinute * minutes) * (float)Math.Sqrt(ChapterMultiplier(chapter));
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
