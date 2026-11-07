#nullable enable
// 기연 사건을 판 위에 놓는 셈(기획서 20장 기연, synergies.json encounters[].spot). 웹 한 생 판 newEnc·updateSynergy 기연 줄과 같은 규칙이다.
//   1. 판 시작에 사건마다 AppearMinute(spot 창 안에서 고르게)를 뽑아 둔다.
//   2. 게임 분이 그때가 되면 EncounterRoller.Roll(id, 그때 서 있는 땅 ctx). 형편이 안 맞아 못 굴리면(CanAppear 거짓) RetryMinutes 뒤 다시.
//      나오기로 됐으면 플레이어 둘레 RingRadius 고리(PlaceOffset)에 놓는다. 밀서·영물은 고리가 없어(0) 부른 쪽 길로(다음 등롱·금두꺼비 고리).
//   3. 닿기는 Reached(ReachRadius, 경계는 안 닿음), 머무는 초는 Progress(닿으면 +dt, 벗어나면 DecayPerSecond × dt 만큼 줄고 0 아래로 안 감).
//      속삭임·검총은 한 번 닿은 뒤 Left(LeaveRadius 밖, 경계는 안 떠남)면 거절·다시 뜰 수 있음.
// 길이는 경지 m(Unity 는 WorldScale 을 곱한다), 분은 게임 분.
using System;
using BloodRain.Core.Data;

namespace BloodRain.Core.Spawning
{
    public static class EncounterSpots
    {
        /// <summary>나올 때가 됐는데 형편이 안 맞으면(그 땅에 문파가 없음 등) 이만큼 뒤 다시 본다. 웹 값(ev.at = G.gm + 1).</summary>
        public const float RetryMinutes = 1f;

        /// <summary>나올 게임 분. spot 이 없으면 0(판 시작).</summary>
        public static float AppearMinute(EncounterDef e, Random rng)
        {
            var s = e.Spot;
            if (s == null) return 0f;
            return s.AppearFromMinute + (float)rng.NextDouble() * Math.Max(0f, s.AppearToMinute - s.AppearFromMinute);
        }

        /// <summary>놓을 자리(플레이어 기준, m): 무작위 쪽, RingRadius 거리. 땅에 놓지 않는 사건이면 (0, 0).</summary>
        public static (float x, float y) PlaceOffset(EncounterDef e, Random rng)
        {
            float r = e.Spot?.RingRadius ?? 0f;
            double a = rng.NextDouble() * 2 * Math.PI;
            return r > 0f ? ((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r)) : (0f, 0f);
        }

        /// <summary>땅에 놓는 사건인가(RingRadius 가 있음).</summary>
        public static bool OnGround(EncounterDef e) => (e.Spot?.RingRadius ?? 0f) > 0f;

        /// <summary>distance 떨어져 있으면 닿았나.</summary>
        public static bool Reached(EncounterDef e, float distance) => e.Spot != null && distance < e.Spot.ReachRadius;

        /// <summary>한 번 닿은 뒤 distance 면 떠났나(LeaveRadius 가 없으면 늘 거짓).</summary>
        public static bool Left(EncounterDef e, float distance) => e.Spot != null && e.Spot.LeaveRadius > 0f && distance > e.Spot.LeaveRadius;

        /// <summary>머문(예를 갖춘) 초를 한 프레임 쌓는다. inReach 가 거짓이면 DecayPerSecond 로 준다.</summary>
        public static float Progress(EncounterDef e, float progress, float dt, bool inReach)
        {
            if (inReach) return progress + dt;
            float decay = e.Spot?.DecayPerSecond ?? 0f;
            return Math.Max(0f, progress - decay * dt);
        }
    }
}
