#nullable enable
// 정예·장삼·땅의 주인이 같이 쓰는 기본 모양 다섯의 셈(기획서 6장 '정예 패턴 수치'·'땅의 주인 패턴 수치', bosspatterns.json).
// 웹 프로토타입 castPattern 의 같은 shape 칸과 같은 규칙이다(unity_tasks BR-01·C-10a·E-01·R-01Ua·R4-06U 의 PatternRunner 가 쓴다).
//   projectile_fan 부채 탄 · jump_slam 뛰어 내려찍기 · circles 플레이어 둘레 원 · summon 부르기 · line 돌진·일섬
// 시간표(언제 예고·판정하나)는 BossPatterns, 여기는 '어느 쪽, 어디, 몇 초 서 있나'만. 길이는 화면 기준 m, 각도는 도.
// 데이터에 값이 없을 때 쓰는 기본값 넷은 웹 프로토타입 값이다(DefaultShotRadius 등). 데이터로 옮기는 것은 사람이 해 본 뒤(P-02 와 같이).
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public static class PatternShapes
    {
        /// <summary>부채 탄 한 발 반경. 패턴에 Width(지름)가 있으면 그 절반(설인 왕 얼음덩이 1.2m), 없으면 이 값. 웹 값.</summary>
        public const float DefaultShotRadius = 0.5f;
        /// <summary>Length 가 없는 부채 탄(혈침 탄막)이 날아가는 거리 = 화면 대각선 × 이 값. 웹 값.</summary>
        public const float DefaultFlightViewRatio = 0.6f;
        /// <summary>Scatter 가 없는 circles(여럿)의 흩는 폭. 웹 값.</summary>
        public const float DefaultScatter = 2.5f;
        /// <summary>summon 이 플레이어 둘레에 세우는 가장 가까운 거리. 웹 값.</summary>
        public const float SummonMinDistance = 1.5f;

        // ── projectile_fan ──
        /// <summary>한 파의 탄 각도들: Count 가 둘 이상이면 aim 을 가운데로 AngleDegrees 를 고르게(양 끝 포함), 하나면 aim.</summary>
        public static float[] FanDegrees(BossPatternDef p, float aimDegrees)
        {
            int n = Math.Max(1, p.Count);
            var a = new float[n];
            float half = p.AngleDegrees * 0.5f;
            for (int i = 0; i < n; i++) a[i] = n > 1 ? aimDegrees - half + 2f * half * i / (n - 1) : aimDegrees;
            return a;
        }

        /// <summary>탄 한 발 반경(m).</summary>
        public static float FanShotRadius(BossPatternDef p) => p.Width > 0f ? p.Width * 0.5f : DefaultShotRadius;

        /// <summary>탄이 날아가는 거리(m). viewDiagonal 은 지금 화면 대각선(화면 기준 m).</summary>
        public static float FanFlightLength(BossPatternDef p, float viewDiagonal) =>
            p.Length > 0f ? p.Length : viewDiagonal * DefaultFlightViewRatio;

        /// <summary>탄이 사는 초 = 거리 / 탄속. moveBase 는 balance.json stats.moveBase.</summary>
        public static float FanFlightSeconds(BossPatternDef p, float viewDiagonal, float moveBase)
        {
            float sp = BossPatterns.MetersPerSecond(p.ProjectileSpeed, moveBase);
            return sp > 0f ? FanFlightLength(p, viewDiagonal) / sp : 0f;
        }

        /// <summary>wave 번째 파(0부터)가 나가는 초(예고 시작부터). 방향은 예고 때 잡은 것 그대로.</summary>
        public static float FanWaveSeconds(BossPatternDef p, int wave) => p.TelegraphSeconds + wave * p.WaveIntervalSeconds;

        // ── jump_slam ──
        /// <summary>k 번째 뜀(0부터)이 시작되는 초(예고 시작부터). 이때 플레이어 자리에 그림자를 잡는다.</summary>
        public static float JumpTakeoffSeconds(BossPatternDef p, int k) => k * (p.TelegraphSeconds + p.WaveIntervalSeconds);

        /// <summary>k 번째 뜀이 그림자 자리에 내려앉아 Radius 안을 치는 초. 몸도 그 자리로 옮긴다. 마지막 착지 = BossPatterns.CastSeconds.</summary>
        public static float JumpLandSeconds(BossPatternDef p, int k) => JumpTakeoffSeconds(p, k) + p.TelegraphSeconds;

        // ── circles(aim player_at_telegraph) ──
        /// <summary>
        /// 예고를 시작할 때 원 가운데들(그때 플레이어 자리 기준). arrangement big_dipper 면 북두칠성(bossDegrees = 플레이어에서 보스 쪽),
        /// 하나면 플레이어 자리 그대로, 여럿이면 가로·세로 ±Scatter(없으면 DefaultScatter) 안 무작위. aim self 는 보스 몸 하나라 여기 안 온다.
        /// </summary>
        public static (float x, float y)[] CircleCenters(BossPatternDef p, Random rng, float bossDegrees)
        {
            if (p.Arrangement == "big_dipper") return BossPatterns.BigDipperOffsets(p, bossDegrees);
            int n = Math.Max(1, p.Count);
            if (n == 1) return new[] { (0f, 0f) };
            float s = p.Scatter > 0f ? p.Scatter : DefaultScatter;
            var list = new (float x, float y)[n];
            for (int i = 0; i < n; i++) list[i] = ((float)(rng.NextDouble() * 2 - 1) * s, (float)(rng.NextDouble() * 2 - 1) * s);
            return list;
        }

        // ── summon ──
        /// <summary>판정 때 그 순간 플레이어 둘레에 Count 마리 자리: 무작위 쪽, 거리 SummonMinDistance~Scatter(Scatter 가 더 작거나 없으면 SummonMinDistance).</summary>
        public static (float x, float y)[] SummonOffsets(BossPatternDef p, Random rng)
        {
            float far = Math.Max(SummonMinDistance, p.Scatter);
            var list = new (float x, float y)[Math.Max(0, p.Count)];
            for (int i = 0; i < list.Length; i++)
            {
                double a = rng.NextDouble() * 2 * Math.PI, r = SummonMinDistance + (far - SummonMinDistance) * rng.NextDouble();
                list[i] = ((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
            }
            return list;
        }

        // ── line ──
        /// <summary>돌진(ProjectileSpeed 있음)이 달리는 초 = Length / 걸음. 일섬(속도 없음)은 0.</summary>
        public static float ChargeRunSeconds(BossPatternDef p, float moveBase)
        {
            float sp = BossPatterns.MetersPerSecond(p.ProjectileSpeed, moveBase);
            return sp > 0f ? p.Length / sp : 0f;
        }

        /// <summary>line 한 번 동안 몸이 다른 걸음을 못 하는 초 = 예고 + 달리기 + RecoverySeconds(굳음).</summary>
        public static float LineHoldSeconds(BossPatternDef p, float moveBase) =>
            p.TelegraphSeconds + ChargeRunSeconds(p, moveBase) + p.RecoverySeconds;
    }
}
