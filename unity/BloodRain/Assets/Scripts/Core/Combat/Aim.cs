#nullable enable
// 조준(기획서 12장 '화면 편의 규칙' 조준, 8장 조작). 웹 프로토타입 aimAngle(WP-52)과 같은 규칙.
//   방향이 있는 무공(장풍·혈조·삼재검법 절기 검기 …)이 어느 쪽으로 나갈지 고른다.
//   1. 패드 오른쪽 스틱(모바일 두 번째 손가락)을 기울이는 동안은 그쪽. 데드존은 입력 쪽이 거르고 여기엔 0 아닌 벡터만 온다.
//   2. 조준 방식이 '가까운 적'(기본)이면 닿는 거리 안 가장 가까운 적 쪽.
//   3. 적이 없거나 '이동 방향'이면 바라보는 쪽(마지막으로 움직인 쪽).
//   닿는 거리: 근접은 사거리 × 1.25, 탄은 탄속 × 수명(웹과 같음). 거리는 경지 m, 축척은 화면 쪽이 곱한다.
using System;
using System.Collections.Generic;

namespace BloodRain.Core.Combat
{
    public enum AimMode
    {
        /// <summary>닿는 거리 안 가장 가까운 적(기본).</summary>
        Nearest,
        /// <summary>늘 마지막으로 움직인 쪽.</summary>
        Move,
    }

    public static class Aim
    {
        /// <summary>근접 무공이 노릴 적을 찾는 거리 = 사거리 × 이 값.</summary>
        public const float MeleeReachScale = 1.25f;

        public static float MeleeReach(float range) => Math.Max(0f, range) * MeleeReachScale;

        public static float ProjectileReach(float speed, float lifeSeconds) => Math.Max(0f, speed) * Math.Max(0f, lifeSeconds);

        /// <summary>설정 값('near'·'move', 웹 store aimMode 와 같은 낱말)을 읽는다. 모르는 값이면 기본(가까운 적).</summary>
        public static AimMode Parse(string? value) => value == "move" ? AimMode.Move : AimMode.Nearest;

        public static string ToKey(AimMode mode) => mode == AimMode.Move ? "move" : "near";

        /// <summary>
        /// (px, py) 에서 reach 안 가장 가까운 점의 번호. 없으면 -1. 같은 거리면 앞 번호(웹 nearestEnemies 와 같음).
        /// xs·ys 는 같은 길이, 죽은 적은 넘기기 전에 빼 둔다.
        /// </summary>
        public static int Nearest(float px, float py, IReadOnlyList<float> xs, IReadOnlyList<float> ys, float reach)
        {
            if (reach <= 0f) return -1;
            int n = Math.Min(xs.Count, ys.Count), best = -1;
            float bestD = reach * reach;
            for (int i = 0; i < n; i++)
            {
                float dx = xs[i] - px, dy = ys[i] - py, d = dx * dx + dy * dy;
                if (d > bestD || (best >= 0 && d >= bestD)) continue;
                best = i; bestD = d;
            }
            return best;
        }

        /// <summary>
        /// 나갈 각도(라디안, atan2). 스틱 &gt; 가까운 적(hasTarget, 방식이 Nearest 일 때) &gt; 바라보는 쪽.
        /// 바라보는 쪽이 (0, 0)이면 오른쪽(0).
        /// </summary>
        public static float Angle(AimMode mode, float px, float py, float facingX, float facingY,
            bool hasTarget, float targetX, float targetY, float stickX = 0f, float stickY = 0f)
        {
            if (stickX != 0f || stickY != 0f) return (float)Math.Atan2(stickY, stickX);
            if (mode == AimMode.Nearest && hasTarget && (targetX != px || targetY != py))
                return (float)Math.Atan2(targetY - py, targetX - px);
            if (facingX == 0f && facingY == 0f) return 0f;
            return (float)Math.Atan2(facingY, facingX);
        }

        /// <summary>적 위치 목록에서 바로 고른다(Nearest + Angle).</summary>
        public static float Angle(AimMode mode, float px, float py, float facingX, float facingY,
            IReadOnlyList<float> enemyXs, IReadOnlyList<float> enemyYs, float reach, float stickX = 0f, float stickY = 0f)
        {
            int i = mode == AimMode.Nearest && stickX == 0f && stickY == 0f ? Nearest(px, py, enemyXs, enemyYs, reach) : -1;
            return i < 0
                ? Angle(mode, px, py, facingX, facingY, false, 0f, 0f, stickX, stickY)
                : Angle(mode, px, py, facingX, facingY, true, enemyXs[i], enemyYs[i], stickX, stickY);
        }
    }
}
