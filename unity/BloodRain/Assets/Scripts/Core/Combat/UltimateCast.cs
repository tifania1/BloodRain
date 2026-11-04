#nullable enable
// 쓴 오의 하나가 흘러가는 것(기획서 5장 '오의 상세', characters.json ultimates[].params, backlog R-15).
//   피해 한 방은 Ultimates.HitDamage, 천설부·천마해체 효과는 UltimateEffects·Life.Dissolution(R4-10)이 한다.
//   여기서는 모든 오의에 대해 '언제 몇 번 때리나'와 나머지 다섯(천뢰낙·사자후·만천화우·태극무량·팔방풍우)의 데이터 숫자를 준다.
//   - 때리는 박자(Pulse): 쓰는 순간 한 번. 만천화우처럼 durationSeconds 와 mightPerSecond 가 있으면 0초·1초·2초 … 지속 시간 안에서 1초마다 한 번(3초면 세 번).
//     공력 배율 0 인 태극무량·천마해체는 때리지 않는다.
//   - 몸에 붙는 것: 태극무량 5초 동안 받는 피해 0·몸 주위 적 탄 되돌림.
//   - 맞은 적에 붙는 것: 천뢰낙 감전 1초, 사자후 기절 3초, 만천화우 중독.
//   - 모양: 천뢰낙 화면 안 무작위 12곳(StrikePoints), 사자후 반경 6m(InRadius, 경지 축척을 곱함), 팔방풍우 8방향(Directions)·3연타(Volleys).
//   현경 '오의 2연속'(Life.UltimateCasts)이면 박자마다 두 번 때리고, 지속형(태극무량)은 R4-10 천마해체처럼 새로 시작할 뿐 늘지 않는다(Life.UseUltimate 가 새로 만든다).
// 숫자가 기획서에 없는 칸(태극무량 원 반경, 팔방풍우 연타 간격·넉백, 천뢰낙 한 곳 반경, 중독 세기, 사자후 음파 퍼지는 시간)은
// backlog R-15b 사람 결정 대기라 여기 없다. 일검·역류는 params 가 없어 '쓰는 순간 한 번'만(역류 물살은 R-05b).
using System;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public sealed class UltimateCast
    {
        public const string TaijiMuryang = "taiji_muryang";

        readonly float _pulseInterval;

        public UltimateCast(UltimateDef def)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            DurationSeconds = def.Param("durationSeconds", 0f);
            // 지속 시간 동안 1초마다 때리는 오의(만천화우 '초당 공력 xN')
            _pulseInterval = DurationSeconds > 0f && def.Param("mightPerSecond", 0f) > 0f ? 1f : 0f;
            TotalPulses = def.MightMultiplier <= 0f ? 0
                : _pulseInterval > 0f ? Math.Max(1, (int)Math.Ceiling(DurationSeconds / _pulseInterval - 1e-4f)) : 1;
        }

        public UltimateDef Def { get; }
        public string Id => Def.Id;
        /// <summary>쓰고 난 뒤 흐른 세상 시간(초).</summary>
        public float Elapsed { get; private set; }
        /// <summary>지속 시간(만천화우 3·태극무량 5·천마해체 8). 한 번에 끝나는 오의는 0.</summary>
        public float DurationSeconds { get; }
        /// <summary>한 번 쓸 때 때리는 박자 수(만천화우 3초면 3, 때리는 오의는 1, 태극무량·천마해체는 0).</summary>
        public int TotalPulses { get; }
        /// <summary>지금까지 때린 박자 수.</summary>
        public int PulsesDone { get; private set; }
        /// <summary>아직 몸에 붙어 있거나 때릴 박자가 남았나.</summary>
        public bool IsActive => Elapsed < DurationSeconds || PulsesDone < TotalPulses;

        /// <summary>세상 시간 dt 를 흘리고, 이번에 때려야 할 박자 수를 돌려준다. 쓰고 처음 부르면(dt 0 이어도) 첫 박자 1.
        /// 박자는 0초·1초·2초 …(만천화우). 한 박자마다 Ultimates.HitDamage 만큼(현경이면 Life.UltimateCasts 번) 때린다.</summary>
        public int Advance(float dt)
        {
            if (dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            Elapsed += dt;
            int pulses = 0;
            while (PulsesDone < TotalPulses && PulsesDone * _pulseInterval <= Elapsed + 1e-4f)
            {
                PulsesDone++;
                pulses++;
            }
            return pulses;
        }

        bool Buffing => Elapsed < DurationSeconds;

        // ---- 몸에 붙는 것(천마해체는 Life.Dissolution) ----

        /// <summary>받는 피해에 곱한다. 태극무량이 붙어 있으면 0('받는 피해 무효'), 아니면 1.</summary>
        public float DamageTakenMultiplier => ReflectsProjectiles ? 0f : 1f;

        /// <summary>태극무량이 붙어 있으면 몸 주위 원 안의 적 탄을 되돌려 보낸다. 원 반경은 R-15b.</summary>
        public bool ReflectsProjectiles => Buffing && Id == TaijiMuryang;

        // ---- 맞은 적에 붙는 것(천설부 얼림은 UltimateEffects) ----

        /// <summary>맞은 적 기절 시간(사자후 3). 없으면 0.</summary>
        public float StunSeconds => Def.Param("stunSeconds", 0f);
        /// <summary>맞은 적 감전 시간(천뢰낙 1). 없으면 0.</summary>
        public float ShockSeconds => Def.Param("shockSeconds", 0f);
        /// <summary>맞은 적을 중독시키나(만천화우). 중독 세기·시간은 R-15b.</summary>
        public bool Poisons => Def.Param("poisons", 0f) > 0f;

        // ---- 모양 ----

        /// <summary>팔방풍우 연타 수(3). 없으면 1.</summary>
        public int Volleys => (int)Def.Param("volleys", 1f);

        /// <summary>사자후 반경 안인가. distance·반경은 세계 m, scale 은 지금 경지 축척(Scale.Factor).</summary>
        public bool InRadius(float distance, float scale = 1f) =>
            Def.Params != null && Def.Params.ContainsKey("radius") && distance <= Def.Param("radius") * scale;

        /// <summary>팔방풍우 방향들: 바라보는 쪽부터 같은 간격으로 directions 개(8이면 45도마다). 단위 벡터.</summary>
        public Vector2[] Directions(Vector2 facing)
        {
            int n = (int)Def.Param("directions");
            float baseAngle = facing.LengthSquared() > 0f ? MathF.Atan2(facing.Y, facing.X) : 0f;
            var dirs = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                float a = baseAngle + 2f * MathF.PI * i / n;
                dirs[i] = new Vector2(MathF.Cos(a), MathF.Sin(a));
            }
            return dirs;
        }

        /// <summary>천뢰낙 낙뢰 자리: 화면 사각형(viewMin~viewMax, 세계 좌표) 안 무작위 strikes 곳.</summary>
        public Vector2[] StrikePoints(Vector2 viewMin, Vector2 viewMax, Random rng)
        {
            int n = (int)Def.Param("strikes");
            var pts = new Vector2[n];
            for (int i = 0; i < n; i++)
                pts[i] = new Vector2(
                    viewMin.X + (float)rng.NextDouble() * (viewMax.X - viewMin.X),
                    viewMin.Y + (float)rng.NextDouble() * (viewMax.Y - viewMin.Y));
            return pts;
        }
    }
}
