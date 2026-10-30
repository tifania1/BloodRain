#nullable enable
// 혈마의 그림자(기획서 7장 '생의 끝'). 현경인데 봉인석이 모자라면 30분부터 쫓아오는 죽지 않는 그림자.
// 단계는 LifeDirector.ShadowStep 이 낸다. 여기서는 단계 하나의 세기만 계산한다(cheonha.json lifeEnd.shadow, T-39).
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public readonly struct BloodShadowStrength
    {
        /// <summary>0이면 그림자가 없다.</summary>
        public readonly int Step;
        public readonly float ContactDamage;
        /// <summary>플레이어 기본 이동속도 100 기준.</summary>
        public readonly float MoveSpeed;
        public readonly float HitIntervalSeconds;
        /// <summary>화면 위 모든 적의 피해 배율(그림자 자신은 빼고).</summary>
        public readonly float EnemyDamageMultiplier;

        public BloodShadowStrength(int step, float contactDamage, float moveSpeed, float hitInterval, float enemyDamageMultiplier)
        {
            Step = step;
            ContactDamage = contactDamage;
            MoveSpeed = moveSpeed;
            HitIntervalSeconds = hitInterval;
            EnemyDamageMultiplier = enemyDamageMultiplier;
        }

        public bool Active => Step > 0;
    }

    public static class BloodShadow
    {
        public static BloodShadowStrength Strength(BloodShadowDef d, int step)
        {
            if (step <= 0) return new BloodShadowStrength(0, 0f, 0f, 0f, 1f);
            int k = step - 1;
            float dmg = d.ContactDamage * (float)Math.Pow(d.DamageGrowth, k);
            float speed = Math.Min(d.MaxSpeed, d.MoveSpeed + d.SpeedPerStep * k);
            return new BloodShadowStrength(step, dmg, speed, d.HitIntervalSeconds, 1f + d.EnemyDamagePerStep * k);
        }

        /// <summary>그림자가 플레이어 기본 이동속도(100)보다 빨라지는 첫 단계. 끝까지 안 빨라지면 0.</summary>
        public static int FirstStepFasterThanPlayer(BloodShadowDef d)
        {
            for (int s = 1; s <= 100; s++)
            {
                if (Strength(d, s).MoveSpeed > 100f) return s;
                if (d.MoveSpeed + d.SpeedPerStep * (s - 1) >= d.MaxSpeed) break;
            }
            return 0;
        }
    }
}
