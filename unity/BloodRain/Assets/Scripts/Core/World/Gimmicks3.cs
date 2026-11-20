#nullable enable
// 지역 기믹 다섯의 판정(R-10 남은 것, 2026-11-20 — 값은 gimmicks.json, 근거는 docs/design/gimmick_start_values.md 1절 '표대로').
//   대나무 덤불(당가타)·떠내려오는 배·역류 파도(장강 수로)·운해(무당산)·잔도(화산). 자리는 World.GimmickLayout 이 정하고,
//   여기는 '그 자리에서 무엇이 되나'만 계산한다. 거리는 지금 경지 축척의 m(scale 을 곱해 넘긴다), 속도 100 = 플레이어 기본 걸음(balance stats.moveBase).
using System;
using System.Collections.Generic;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>대나무 덤불: 탑림과 같은 무더기에 대나무 포기. 걷는 것만 막고, 무공에 맞으면 베이고(가끔 음식), 얼마 뒤 다시 자란다.</summary>
    public static class BambooThicketGimmick
    {
        /// <summary>무더기 안 포기 자리(탑림 배치 함수 그대로, 반경은 포기 반경).</summary>
        public static List<PagodaStone> Layout(BambooThicketGimmickDef def, Random rng, float scale = 1f) => PagodaForest.Layout(def.AsPatch(), rng, scale);

        /// <summary>이 몸을 막나. 나는 적·투사체는 지나간다(데이터 그대로).</summary>
        public static bool Blocks(BambooThicketGimmickDef def, bool flying, bool projectile) =>
            projectile ? def.BlocksProjectiles : flying ? def.BlocksFlying : true;

        /// <summary>무공에 맞은 횟수로 베이나.</summary>
        public static bool Breaks(BambooThicketGimmickDef def, int hits) => hits >= Math.Max(1, def.HitsToBreak);

        /// <summary>베였을 때 음식을 떨구나(dropChance).</summary>
        public static bool DropsFood(BambooThicketGimmickDef def, Random rng) => def.DropChance > 0 && rng.NextDouble() < def.DropChance;

        /// <summary>떨군 음식의 회복(최대 기혈 비율).</summary>
        public static float FoodHeal(BambooThicketGimmickDef def, float maxHealth) => maxHealth * def.HealMaxHealthRatio;

        /// <summary>베인 포기가 다시 서는 때(생 시작부터 초).</summary>
        public static float RegrowAt(BambooThicketGimmickDef def, float cutAtSeconds) => cutAtSeconds + def.RegrowSeconds;
    }

    /// <summary>떠내려오는 배: 물길 하나를 따라 spacing 간격으로 하류로 흐른다. 배 위면 물 감속·역류 파도가 없고 배와 함께 떠간다.</summary>
    public static class DriftingBoatsGimmick
    {
        public static Vector2 Direction(DriftingBoatsGimmickDef def)
        {
            double r = def.DirectionDegrees * Math.PI / 180.0;
            return new Vector2((float)Math.Cos(r), (float)Math.Sin(r));
        }

        /// <summary>초당 m(속도 그대로 m/s — 데이터가 '초속 1.5m').</summary>
        public static Vector2 Velocity(DriftingBoatsGimmickDef def, float scale = 1f) => Direction(def) * (def.Speed * scale);

        /// <summary>물길 위 배들의 한가운데. 물길은 start 에서 Direction 쪽으로 laneLength m. 생 시작부터 lifeSeconds 흐른 자리,
        /// 하류 끝을 넘은 배는 상류 끝에서 다시 나온다. 배 수 = floor(laneLength / spacing).</summary>
        public static List<Vector2> Boats(DriftingBoatsGimmickDef def, Vector2 laneStart, float laneLength, float lifeSeconds, float scale = 1f)
        {
            var list = new List<Vector2>();
            float spacing = def.Spacing * scale;
            if (spacing <= 0f || laneLength <= 0f) return list;
            int n = Math.Max(1, (int)Math.Floor(laneLength / spacing));
            float run = n * spacing; // 한 바퀴 길이(배 간격이 고르게)
            float shift = def.Speed * scale * lifeSeconds;
            var dir = Direction(def);
            for (int i = 0; i < n; i++)
            {
                float along = (i * spacing + shift) % run;
                if (along < 0) along += run;
                if (along > laneLength) continue;
                list.Add(laneStart + dir * along);
            }
            return list;
        }

        /// <summary>p 가 그 배 위인가(배는 Direction 쪽으로 length, 가로 width 인 판).</summary>
        public static bool OnBoat(DriftingBoatsGimmickDef def, Vector2 boatCenter, Vector2 p, float scale = 1f)
        {
            var dir = Direction(def);
            var d = p - boatCenter;
            float along = Vector2.Dot(d, dir), across = Vector2.Dot(d, new Vector2(-dir.Y, dir.X));
            return Math.Abs(along) <= def.Length * scale * 0.5f && Math.Abs(across) <= def.Width * scale * 0.5f;
        }
    }

    /// <summary>역류 파도: firstSeconds 에 처음, 그 뒤 periodSeconds 마다. telegraphSeconds 예고 뒤 폭 width 파도가 하류에서 상류로 화면을 가로지른다.
    /// 그 순간 장강 수로에 서 있을 때만. 배 위·경공 무적이면 안 맞는다.</summary>
    public static class ReverseWaveGimmick
    {
        /// <summary>n 번째(0부터) 파도의 예고 시작 시각.</summary>
        public static float StartAt(ReverseWaveGimmickDef def, int n) => def.FirstSeconds + Math.Max(0, n) * def.PeriodSeconds;

        /// <summary>(after, until] 사이에 예고가 시작되는 파도 수.</summary>
        public static int StartsBetween(ReverseWaveGimmickDef def, float afterSeconds, float untilSeconds)
        {
            if (def.PeriodSeconds <= 0f || untilSeconds < def.FirstSeconds) return 0;
            int upto = (int)Math.Floor((untilSeconds - def.FirstSeconds) / def.PeriodSeconds) + 1;
            int before = afterSeconds < def.FirstSeconds ? 0 : (int)Math.Floor((afterSeconds - def.FirstSeconds) / def.PeriodSeconds) + 1;
            return Math.Max(0, upto - before);
        }

        /// <summary>지금 예고 중인가(강물이 붉다).</summary>
        public static bool Telegraphing(ReverseWaveGimmickDef def, float lifeSeconds)
        {
            if (lifeSeconds < def.FirstSeconds || def.PeriodSeconds <= 0f) return false;
            float into = (lifeSeconds - def.FirstSeconds) % def.PeriodSeconds;
            return into < def.TelegraphSeconds;
        }

        /// <summary>파도가 나아가는 m/s(속도 100 = 플레이어 기본 걸음).</summary>
        public static float SpeedMetersPerSecond(ReverseWaveGimmickDef def, float moveBase) => def.ProjectileSpeed / 100f * moveBase;

        public static Vector2 Direction(ReverseWaveGimmickDef def)
        {
            double r = def.DirectionDegrees * Math.PI / 180.0;
            return new Vector2((float)Math.Cos(r), (float)Math.Sin(r));
        }

        /// <summary>파도 앞머리(직선, Direction 에 수직)가 front 에 있을 때 p 가 그 폭 안인가.</summary>
        public static bool Hits(ReverseWaveGimmickDef def, Vector2 front, Vector2 p, float scale = 1f) =>
            Math.Abs(Vector2.Dot(p - front, Direction(def))) <= def.Width * scale * 0.5f;

        /// <summary>안 맞는가: 배 위거나 경공 무적.</summary>
        public static bool Immune(bool onBoat, bool dashInvulnerable) => onBoat || dashInvulnerable;

        /// <summary>맞은 대상의 피해. 잡몹은 쓰러진다(float.PositiveInfinity).</summary>
        public static float Damage(ReverseWaveGimmickDef def, GimmickTargetKind kind, float maxHealth) => kind switch
        {
            GimmickTargetKind.Player => maxHealth * def.PlayerDamageMaxHealthRatio,
            GimmickTargetKind.Normal => float.PositiveInfinity,
            _ => maxHealth * def.EliteDamageMaxHealthRatio,
        };
    }

    /// <summary>운해: firstSeconds 에 처음, 그 뒤 periodSeconds 마다 플레이어 둘레 ±scatter 안 count 곳에 반경 radius 구름이 telegraphSeconds 에 피어
    /// leaveSeconds 머물며 흐른다. 안에 든 플레이어를 보스 뺀 적이 놓친다.</summary>
    public static class CloudSeaGimmick
    {
        /// <summary>(after, until] 사이에 피기 시작하는 무리 수.</summary>
        public static int BloomsBetween(CloudSeaGimmickDef def, float afterSeconds, float untilSeconds)
        {
            if (def.PeriodSeconds <= 0f || untilSeconds < def.FirstSeconds) return 0;
            int upto = (int)Math.Floor((untilSeconds - def.FirstSeconds) / def.PeriodSeconds) + 1;
            int before = afterSeconds < def.FirstSeconds ? 0 : (int)Math.Floor((afterSeconds - def.FirstSeconds) / def.PeriodSeconds) + 1;
            return Math.Max(0, upto - before);
        }

        /// <summary>구름 자리(플레이어 둘레 ±scatter 정사각 안 고르게).</summary>
        public static List<Vector2> Places(CloudSeaGimmickDef def, Vector2 player, Random rng, float scale = 1f)
        {
            var list = new List<Vector2>(def.Count);
            float s = def.Scatter * scale;
            for (int i = 0; i < def.Count; i++)
                list.Add(player + new Vector2((float)(rng.NextDouble() * 2 - 1) * s, (float)(rng.NextDouble() * 2 - 1) * s));
            return list;
        }

        /// <summary>핀 지 since 초 뒤 구름 한가운데(driftDegrees 쪽으로 driftSpeed — 100 = 플레이어 기본 걸음).</summary>
        public static Vector2 Drift(CloudSeaGimmickDef def, Vector2 bloomedAt, float sinceSeconds, float moveBase, float scale = 1f)
        {
            double r = def.DriftDegrees * Math.PI / 180.0;
            float v = def.DriftSpeed / 100f * moveBase * scale;
            return bloomedAt + new Vector2((float)Math.Cos(r), (float)Math.Sin(r)) * (v * Math.Max(0f, sinceSeconds));
        }

        /// <summary>핀 지 since 초: 아직 피는 중(예고), 머무는 중, 사라짐.</summary>
        public static bool Covering(CloudSeaGimmickDef def, float sinceSeconds) =>
            sinceSeconds >= def.TelegraphSeconds && sinceSeconds < def.TelegraphSeconds + def.LeaveSeconds;

        /// <summary>이 적이 구름 안 플레이어를 놓치나(보스는 bossImmune 이면 안 놓침).</summary>
        public static bool Hides(CloudSeaGimmickDef def, Vector2 cloud, Vector2 player, bool isBoss, float scale = 1f) =>
            !(isBoss && def.BossImmune) && Vector2.Distance(cloud, player) <= def.Radius * scale;
    }

    /// <summary>잔도: 반경 patchRadius 벼랑 무더기를 폭 pathWidth 잔도가 가로·세로로 지른다. 벼랑은 걸을 수 없고 경공으로도 못 건넌다(blocksDash).</summary>
    public static class CliffPathGimmick
    {
        /// <summary>p 가 무더기 안 벼랑(잔도 밖)인가.</summary>
        public static bool OnCliff(CliffPathGimmickDef def, Vector2 patchCenter, Vector2 p, float scale = 1f)
        {
            var d = p - patchCenter;
            if (d.Length() > def.PatchRadius * scale) return false;
            float half = def.PathWidth * scale * 0.5f;
            bool horizontal = def.Paths >= 1 && Math.Abs(d.Y) <= half;
            bool vertical = def.Paths >= 2 && Math.Abs(d.X) <= half;
            return !(horizontal || vertical);
        }

        /// <summary>이 몸이 벼랑에 막히나. 나는 적·투사체는 데이터대로, 경공 중이면 blocksDash.</summary>
        public static bool Blocks(CliffPathGimmickDef def, bool flying, bool projectile, bool dashing) =>
            projectile ? def.BlocksProjectiles : flying ? def.BlocksFlying : dashing ? def.BlocksDash : true;
    }
}
