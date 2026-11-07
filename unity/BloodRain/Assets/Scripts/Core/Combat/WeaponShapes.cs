#nullable enable
// M3 무공 일곱의 모양 셈(기획서 9장 무공 표, unity_tasks R-12Ua·R-12Ub). 웹 프로토타입 fireWeapon 의 같은 behavior 칸과 같은 규칙.
//   취선봉 random_arc · 태극검 orbiting_blades · 쇄겸술 pull_chain · 혈조 forward_claws ·
//   음공 ring_wave · 이화창 pierce_thrust · 낙화검우 petal_scatter.
// 성장 숫자(개수·사거리·지속)는 Combat/WeaponGrowth 와 weapons.json, 여기는 '어느 쪽으로 몇 갈래, 누가 맞나'만.
// weapons.json 에 없는 모양 값은 웹 TRIAL_W 와 같은 Trial 상수로 한곳에 둔다(R-17 프로토타입 값, 사람이 해 본 뒤 데이터로).
// 거리는 경지 m, 각도는 라디안. er 은 적 몸 반지름.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Combat
{
    public static class WeaponShapes
    {
        /// <summary>R-17 프로토타입 값(웹 template.html TRIAL_W 와 같음 — WeaponShapesTests 가 확인). 데이터로 옮기는 것은 사람이 해 본 뒤.</summary>
        public static class Trial
        {
            public const float BladeTurnPerSecond = 1.2f, BladeRadius = 0.35f, BladeRehitSeconds = 0.5f;
            public const float PullTo = 1.5f;
            public const float ClawSpreadRad = 0.35f, ClawWidth = 0.35f;
            public const float SonicStunSeconds = 0.6f, SonicSecondRingDelay = 0.3f;
            public const float SpearWidth = 0.6f;
            public const float PetalSpreadRad = 1.2f;
        }

        /// <summary>취선봉 원호가 몸 뒤로 조금 넘어가도 맞는 여유(앞쪽 성분 ≥ -0.1 × 거리). 웹 값.</summary>
        public const float ArcBackTolerance = 0.1f;
        /// <summary>이화창 찌르기 사이 초. 웹 값.</summary>
        public const float ThrustGapSeconds = 0.15f;
        /// <summary>이화창이 몸 뒤 이만큼까지 맞힌다. 웹 값.</summary>
        public const float ThrustBackReach = 0.2f;

        // ── 취선봉 ──
        /// <summary>휘두르기 한 번의 방향들: 절기(팔선과해)면 directions 갈래 고르게, 아니면 무작위 하나(angle01 0~1).</summary>
        public static float[] ArcDirections(bool evolved, int directions, Func<double> angle01) =>
            evolved ? Enumerable.Range(0, Math.Max(1, directions)).Select(i => (float)(i * 2 * Math.PI / Math.Max(1, directions))).ToArray()
                    : new[] { (float)(angle01() * 2 * Math.PI) };

        /// <summary>몸에서 (dx, dy) 인 적이 angle 쪽 반경 R 원호에 맞나.</summary>
        public static bool ArcHits(float dx, float dy, float er, float angle, float radius)
        {
            float d = (float)Math.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
            return d <= radius + er && dx * (float)Math.Cos(angle) + dy * (float)Math.Sin(angle) >= -ArcBackTolerance * d;
        }

        // ── 태극검 ──
        /// <summary>now 초에 검 i 의 각도(n 자루가 고르게, a0 은 쏠 때 무작위 시작각).</summary>
        public static float BladeAngle(float a0, float now, int i, int n) =>
            a0 + now * (float)(2 * Math.PI) * Trial.BladeTurnPerSecond + i * (float)(2 * Math.PI) / Math.Max(1, n);

        /// <summary>같은 적은 BladeRehitSeconds 마다 한 번(lastHit 은 그 적을 마지막으로 벤 시각, 없으면 null).</summary>
        public static bool BladeCanRehit(float? lastHit, float now) => lastHit == null || lastHit.Value + Trial.BladeRehitSeconds <= now;

        /// <summary>절기(태극양의검) 흑백 검: 짝수 번은 흰 검(민다), 홀수 번은 검은 검(안 민다).</summary>
        public static bool BladePushes(bool evolved, int i) => !evolved || i % 2 == 0;

        // ── 쇄겸술 ──
        /// <summary>사거리 안 가장 먼 적 n (먼 차례, 같으면 앞 번호).</summary>
        public static List<int> FarthestWithin(float px, float py, IReadOnlyList<float> xs, IReadOnlyList<float> ys, float range, int n)
        {
            var list = new List<(int i, float d)>();
            int c = Math.Min(xs.Count, ys.Count);
            for (int i = 0; i < c; i++)
            {
                float dx = xs[i] - px, dy = ys[i] - py, d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d <= range) list.Add((i, d));
            }
            return list.OrderByDescending(x => x.d).ThenBy(x => x.i).Take(Math.Max(0, n)).Select(x => x.i).ToList();
        }

        /// <summary>사슬에 걸린 적이 끌려가는 자리: 몸에서 PullTo 거리(이미 더 가까우면 그대로). 정예·보스는 안 끌린다(호출자가 거름).</summary>
        public static (float x, float y) PulledTo(float px, float py, float ex, float ey)
        {
            float dx = ex - px, dy = ey - py, d = (float)Math.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
            float k = Math.Min(1f, Trial.PullTo / d);
            return (px + dx * k, py + dy * k);
        }

        /// <summary>절기 흑룡탄: 모으는 자리 = 몸 앞(바라보는 쪽 단위 벡터) PullTo.</summary>
        public static (float x, float y) GatherCenter(float px, float py, float facingX, float facingY) =>
            (px + facingX * Trial.PullTo, py + facingY * Trial.PullTo);

        /// <summary>흑룡탄 모으기 칸 수: gatherSeconds 를 0.1초씩(웹과 같음), 적어도 1.</summary>
        public static int GatherSteps(float gatherSeconds) => Math.Max(1, (int)Math.Round(gatherSeconds / 0.1f, MidpointRounding.AwayFromZero));

        /// <summary>흑룡탄 k 번째 칸(1부터)에 적이 모으는 자리 쪽으로 남은 거리의 몇 할을 가나: 0.9 / (남은 칸 수). 마지막 칸 뒤 폭발.</summary>
        public static float GatherFraction(int k, int steps) => 0.9f / (steps - k + 1);

        // ── 혈조 ──
        /// <summary>n 갈래 할퀴기 각도: 조준 각 base 를 가운데로 ClawSpreadRad 간격.</summary>
        public static float[] ClawAngles(float baseAngle, int n) =>
            Enumerable.Range(0, Math.Max(0, n)).Select(i => baseAngle + (i - (n - 1) / 2f) * Trial.ClawSpreadRad).ToArray();

        /// <summary>한 갈래(angle, 길이 R)에 맞나. 한 번 쏠 때 같은 적은 한 갈래에만(호출자가 집합으로).</summary>
        public static bool ClawHits(float dx, float dy, float er, float angle, float reach)
        {
            float ca = (float)Math.Cos(angle), sa = (float)Math.Sin(angle), along = dx * ca + dy * sa, perp = Math.Abs(-dx * sa + dy * ca);
            return along > 0f && along < reach + er && perp < Trial.ClawWidth + er;
        }

        // ── 음공 ──
        /// <summary>음파 고리가 나가는 시각들: Lv6 '음파 2겹'이면 0 과 SonicSecondRingDelay.</summary>
        public static float[] RingDelays(bool doubleRing) => doubleRing ? new[] { 0f, Trial.SonicSecondRingDelay } : new[] { 0f };

        /// <summary>음파에 기절하나(보스는 안 걸림). roll 은 0~1 난수.</summary>
        public static bool RingStuns(bool boss, float chance, double roll) => !boss && roll < chance;

        // ── 이화창 ──
        /// <summary>찌르는 방향들: 절기(백화난무창) directions 갈래, Lv7 '회전 찌르기'면 네 방향, 아니면 조준 각 하나.</summary>
        public static float[] ThrustDirections(float baseAngle, bool evolved, int directions, bool spin)
        {
            int n = evolved ? Math.Max(1, directions) : spin ? 4 : 1;
            return Enumerable.Range(0, n).Select(i => baseAngle + i * (float)(2 * Math.PI) / n).ToArray();
        }

        /// <summary>찌르기 한 줄(길이 R, 폭 SpearWidth)에 맞나. 관통이라 줄 안 적은 다 맞는다.</summary>
        public static bool ThrustHits(float dx, float dy, float er, float angle, float reach)
        {
            float ca = (float)Math.Cos(angle), sa = (float)Math.Sin(angle), along = dx * ca + dy * sa, perp = Math.Abs(-dx * sa + dy * ca);
            return along > -ThrustBackReach && along < reach + er && perp < Trial.SpearWidth / 2f + er;
        }

        // ── 낙화검우 ──
        /// <summary>꽃잎 n 발 각도: 절기(매화만개)면 한 바퀴 고르게, 아니면 조준 각 둘레 PetalSpreadRad 안 무작위(spread01 0~1).</summary>
        public static float[] PetalAngles(float baseAngle, int n, bool evolved, Func<double> spread01) =>
            Enumerable.Range(0, Math.Max(0, n)).Select(i => evolved
                ? baseAngle + i * (float)(2 * Math.PI) / n
                : baseAngle + (float)((spread01() - 0.5) * Trial.PetalSpreadRad)).ToArray();
    }
}
