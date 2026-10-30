#nullable enable
// 지역 기믹 둘째 묶음(T-16, 기획서 7장 '지형 기믹 수치'). 수치는 gimmicks.json.
// 첫 묶음(Gimmicks.cs)과 같은 약속: 길이는 화면 기준 m에 scale(그 기믹이 생긴 경지 축척)을 곱하고, 시간은 세상 시간 초.
using System;
using System.Collections.Generic;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>주기 날씨(눈보라·붉은 달). 한 생 시계로 켜져 있는지만 본다.</summary>
    public static class WeatherGimmick
    {
        public static bool ActiveAt(WeatherGimmickDef def, float lifeSeconds)
        {
            if (lifeSeconds < def.FirstSeconds) return false;
            if (def.PeriodSeconds <= 0) return lifeSeconds < def.FirstSeconds + def.DurationSeconds;
            float into = (lifeSeconds - def.FirstSeconds) % def.PeriodSeconds;
            return into < def.DurationSeconds;
        }

        /// <summary>다음에 켜지는 시각(지금 켜져 있으면 그다음 번).</summary>
        public static float NextStartAt(WeatherGimmickDef def, float lifeSeconds)
        {
            if (lifeSeconds < def.FirstSeconds) return def.FirstSeconds;
            int n = (int)Math.Floor((lifeSeconds - def.FirstSeconds) / def.PeriodSeconds) + 1;
            return def.FirstSeconds + n * def.PeriodSeconds;
        }

        static float Or1(float v) => v > 0 ? v : 1f;

        public static float PlayerMoveSpeed(WeatherGimmickDef def, float lifeSeconds) => ActiveAt(def, lifeSeconds) ? Or1(def.PlayerMoveSpeedMultiplier) : 1f;
        public static float EnemyMoveSpeed(WeatherGimmickDef def, float lifeSeconds) => ActiveAt(def, lifeSeconds) ? Or1(def.EnemyMoveSpeedMultiplier) : 1f;
        public static float ViewScale(WeatherGimmickDef def, float lifeSeconds) => ActiveAt(def, lifeSeconds) ? Or1(def.ViewScale) : 1f;
    }

    /// <summary>부술 수 있는 것(술독·검각 시렁).</summary>
    public sealed class BreakableGimmick
    {
        readonly BreakableGimmickDef _def;
        public BreakableGimmick(BreakableGimmickDef def) { _def = def; HitsLeft = Math.Max(1, def.HitsToBreak); }

        public int HitsLeft { get; private set; }
        public bool Broken => HitsLeft <= 0;

        /// <summary>무공에 맞았다. 이번에 깨졌으면 true.</summary>
        public bool Hit()
        {
            if (Broken) return false;
            HitsLeft--;
            return Broken;
        }

        /// <summary>깨질 때 떨구는 것. 음식이면 회복 비율, 은자면 양. 안 떨구면 둘 다 0.</summary>
        public (float heal, int silver) RollDrop(Random rng)
        {
            if (rng.NextDouble() >= _def.DropChance) return (0f, 0);
            return _def.Drop switch
            {
                "food" => (_def.HealMaxHealthRatio, 0),
                "silver" => (0f, rng.Next(_def.SilverMin, _def.SilverMax + 1)),
                _ => (0f, 0),
            };
        }
    }

    /// <summary>낙석.</summary>
    public static class FallingRocksGimmick
    {
        /// <summary>(after, until] 사이에 그림자가 지기 시작하는 횟수. 한 생 시계로 periodSeconds, 2×periodSeconds … 에 진다.</summary>
        public static int FallsBetween(FallingRocksGimmickDef def, float afterSeconds, float untilSeconds)
        {
            if (def.PeriodSeconds <= 0 || untilSeconds <= afterSeconds) return 0;
            int a = (int)Math.Floor(Math.Max(0f, afterSeconds) / def.PeriodSeconds);
            int b = (int)Math.Floor(untilSeconds / def.PeriodSeconds);
            return b - a;
        }

        /// <summary>떨어질 자리 count 곳(플레이어 둘레 가로·세로 ±scatter).</summary>
        public static List<Vector2> Places(FallingRocksGimmickDef def, Vector2 player, Random rng, float scale = 1f)
        {
            var list = new List<Vector2>(def.Count);
            for (int i = 0; i < def.Count; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * def.Scatter * scale;
                float y = (float)(rng.NextDouble() * 2 - 1) * def.Scatter * scale;
                list.Add(player + new Vector2(x, y));
            }
            return list;
        }

        public static bool Hits(FallingRocksGimmickDef def, Vector2 rock, Vector2 target, float scale = 1f) =>
            Vector2.Distance(rock, target) <= def.Radius * scale;

        /// <summary>맞은 대상의 피해. 잡몹은 쓰러진다(float.PositiveInfinity).</summary>
        public static float Damage(FallingRocksGimmickDef def, GimmickTargetKind kind, float maxHealth) => kind switch
        {
            GimmickTargetKind.Player => maxHealth * def.PlayerDamageMaxHealthRatio,
            GimmickTargetKind.Normal => float.PositiveInfinity,
            _ => maxHealth * def.EliteDamageMaxHealthRatio,
        };
    }

    /// <summary>매화 더미. 닿으면 회복하고 다시 필 때까지 비어 있다.</summary>
    public sealed class PlumHeapGimmick
    {
        readonly PlumHeapGimmickDef _def;
        public PlumHeapGimmick(PlumHeapGimmickDef def, float scale = 1f) { _def = def; Scale = scale; }

        public float Scale { get; }
        public float RegrowRemaining { get; private set; }
        public bool Blooming => RegrowRemaining <= 0f;

        /// <summary>플레이어가 이 거리에 있다. 닿았고 피어 있으면 회복량을 돌려주고 진다.</summary>
        public float TryUse(float playerDistance, float maxHealth)
        {
            if (!Blooming || playerDistance > _def.TouchRadius * Scale) return 0f;
            RegrowRemaining = _def.RegrowSeconds;
            return maxHealth * _def.HealMaxHealthRatio;
        }

        public void Tick(float dt) => RegrowRemaining = Math.Max(0f, RegrowRemaining - dt);
    }

    /// <summary>빙판: 속도가 바라는 속도로 천천히 바뀐다.</summary>
    public static class IceFloorGimmick
    {
        /// <summary>이번 프레임의 속도. 빙판이 아니거나 나는 적이면 바라는 속도 그대로.</summary>
        public static Vector2 Step(IceFloorGimmickDef def, Vector2 current, Vector2 desired, float dt, bool onIce, bool flying = false)
        {
            if (!onIce || (flying && def.FlyingIgnores) || def.InertiaSeconds <= 0) return desired;
            float k = Math.Min(1f, dt / def.InertiaSeconds);
            return current + (desired - current) * k;
        }
    }

    /// <summary>모닥불.</summary>
    public static class CampfireGimmick
    {
        public static bool Inside(CampfireGimmickDef def, float distance, float scale = 1f) => distance <= def.Radius * scale;

        /// <summary>이번 프레임 플레이어 회복량.</summary>
        public static float Heal(CampfireGimmickDef def, float distance, float maxHealth, float dt, float scale = 1f) =>
            Inside(def, distance, scale) ? maxHealth * def.HealPerSecondMaxHealthRatio * dt : 0f;

        /// <summary>적 이동속도 배율. 느려지는 계열(빙강시)이 반경 안이면 줄어든다.</summary>
        public static float EnemyMoveSpeed(CampfireGimmickDef def, string family, float distance, float scale = 1f) =>
            family == def.SlowedFamily && Inside(def, distance, scale) ? def.SlowedMoveSpeedMultiplier : 1f;
    }

    /// <summary>혈제단(한 지역에 Count 개). 켜진 수와 버프.</summary>
    public static class BloodAltarGimmick
    {
        /// <summary>한 생 시계로 켜진 제단 수(부서진 것 포함 상한 Count).</summary>
        public static int LitCount(BloodAltarGimmickDef def, float lifeSeconds)
        {
            if (lifeSeconds < def.FirstLightSeconds) return 0;
            int n = 1 + (def.LightEverySeconds > 0 ? (int)Math.Floor((lifeSeconds - def.FirstLightSeconds) / def.LightEverySeconds) : def.Count);
            return Math.Min(def.Count, n);
        }

        /// <summary>켜졌고 안 부서진 제단 가까이(buffRadius) 있는 적의 피해 배율.</summary>
        public static float EnemyDamage(BloodAltarGimmickDef def, bool nearLitAltar) => nearLitAltar ? def.EnemyDamageMultiplier : 1f;
        public static float EnemyMoveSpeed(BloodAltarGimmickDef def, bool nearLitAltar) => nearLitAltar ? def.EnemyMoveSpeedMultiplier : 1f;
        public static bool Near(BloodAltarGimmickDef def, float distance, float scale = 1f) => distance <= def.BuffRadius * scale;
    }
}
