#nullable enable
// 쓴 오의 하나가 흘러가는 것(기획서 5장 '오의 상세', characters.json ultimates[].params, backlog R-15).
//   피해 한 방은 Ultimates.HitDamage, 천설부·천마해체 효과는 UltimateEffects·Life.Dissolution(R4-10)이 한다.
//   여기서는 모든 오의에 대해 '언제 몇 번 때리나'와 나머지 다섯(천뢰낙·사자후·만천화우·태극무량·팔방풍우)의 데이터 숫자를 준다.
//   - 때리는 박자(Pulse): 쓰는 순간 한 번. 만천화우처럼 durationSeconds·mightPerSecond·pulseSeconds 가 있으면 0초·1초·2초 … 지속 시간 안에서 pulseSeconds(1초)마다 한 번(3초면 세 번).
//     공력 배율 0 인 태극무량·천마해체는 때리지 않는다.
//   - 몸에 붙는 것: 태극무량 5초 동안 받는 피해 0(params.blocksDamage)·몸 주위 적 탄 되돌림(params.reflectsProjectiles).
//   - 맞은 적에 붙는 것: 천뢰낙 감전 1초, 사자후 기절 3초, 만천화우 중독.
//   - 모양: 천뢰낙 화면 안 무작위 12곳(StrikePoints), 사자후 반경 6m(InRadius, 경지 축척을 곱함), 팔방풍우 8방향(Directions)·3연타(Volleys).
//   현경 '오의 2연속'(Life.UltimateCasts)이면 박자마다 두 번 때리고, 지속형(태극무량)은 R4-10 천마해체처럼 새로 시작할 뿐 늘지 않는다(Life.UseUltimate 가 새로 만든다).
// 모양 숫자(R-15b·R-05b, 2026-11-05 사람이 추천대로 — 같은 몸의 시작 무공·마천패 역류 파도에서 가져옴): 태극무량 원 반경 3m, 팔방풍우 연타 0.3초·넉백 15·장풍 탄,
// 천뢰낙 한 곳 반경 1.2m, 만천화우 중독(3초 동안 0.5초마다 6 x 공력), 사자후 음파 0.3초에 퍼짐, 역류 폭 3m 물살(속도 300)이 앞쪽 화면 끝까지 한 번.
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
            // 지속 시간 동안 pulseSeconds 마다 때리는 오의(만천화우 '초당 공력 xN', 1초마다). 박자가 안 적혔으면 쓰는 순간 한 번.
            _pulseInterval = DurationSeconds > 0f && def.Param("mightPerSecond", 0f) > 0f ? def.Param("pulseSeconds", 0f) : 0f;
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
        public float DamageTakenMultiplier => Buffing && Def.Param("blocksDamage", 0f) > 0f ? 0f : 1f;

        /// <summary>태극무량이 붙어 있으면 몸 주위 원(GuardRadius) 안의 적 탄을 되돌려 보낸다.</summary>
        public bool ReflectsProjectiles => Buffing && Def.Param("reflectsProjectiles", 0f) > 0f;

        // ---- 맞은 적에 붙는 것(천설부 얼림은 UltimateEffects) ----

        /// <summary>맞은 적 기절 시간(사자후 3). 없으면 0.</summary>
        public float StunSeconds => Def.Param("stunSeconds", 0f);
        /// <summary>맞은 적 감전 시간(천뢰낙 1). 없으면 0.</summary>
        public float ShockSeconds => Def.Param("shockSeconds", 0f);
        /// <summary>맞은 적을 중독시키나(만천화우). 세기·시간은 PoisonTickDamage·PoisonSeconds.</summary>
        public bool Poisons => Def.Param("poisons", 0f) > 0f;

        // ---- 모양 ----

        /// <summary>팔방풍우 연타 수(3). 없으면 1.</summary>
        public int Volleys => (int)Def.Param("volleys", 1f);

        /// <summary>사자후 반경 안인가(맞는 것은 쓰는 순간, 퍼지는 모양은 WaveRadiusAt). distance·반경은 세계 m, scale 은 지금 경지 축척(Scale.Factor).</summary>
        public bool InRadius(float distance, float scale = 1f) =>
            Def.Params != null && Def.Params.ContainsKey("radius") && distance <= Def.Param("radius") * scale;

        /// <summary>팔방풍우 연타 사이 초(0.3). k 번째(0부터) 연타는 쓰고 k × 이 값 뒤에 나간다.</summary>
        public float VolleyGapSeconds => Def.Param("volleyGapSeconds", 0f);
        /// <summary>팔방풍우 장풍 한 발 넉백(15). 없으면 0.</summary>
        public float Knockback => Def.Param("knockback", 0f);
        /// <summary>팔방풍우 장풍 탄(초속 m·반경 m·수명 초, 장풍 shot 과 같음). 반경·속도에는 경지 축척을 곱해 쓴다.</summary>
        public (float Speed, float Radius, float LifeSeconds) Shot =>
            (Def.Param("shotSpeed", 0f), Def.Param("shotRadius", 0f), Def.Param("shotLifeSeconds", 0f));

        /// <summary>천뢰낙 한 곳 반경(1.2m, 뇌부 낙뢰와 같음) × 경지 축척.</summary>
        public float StrikeRadius(float scale = 1f) => Def.Param("strikeRadius", 0f) * scale;
        /// <summary>천뢰낙 낙뢰 한 곳(point)에 맞나. 거리는 세계 m.</summary>
        public bool InStrike(Vector2 point, Vector2 enemy, float scale = 1f) =>
            Vector2.Distance(point, enemy) <= StrikeRadius(scale);

        /// <summary>태극무량 탄을 되돌리는 원 반경(3m) × 경지 축척. 사자후는 InRadius 를 쓴다.</summary>
        public float GuardRadius(float scale = 1f) => Def.Id == TaijiMuryang ? Def.Param("radius", 0f) * scale : 0f;

        /// <summary>사자후 음파가 쓰고 elapsed 초 뒤 닿은 반경. spreadSeconds(0.3) 동안 0 에서 radius 까지 고르게 커진다. 퍼지는 시간이 없으면 바로 radius.</summary>
        public float WaveRadiusAt(float elapsed, float scale = 1f)
        {
            float r = Def.Param("radius", 0f) * scale, t = Def.Param("spreadSeconds", 0f);
            return t <= 0f ? r : r * Math.Clamp(elapsed / t, 0f, 1f);
        }

        /// <summary>만천화우 중독: 시간(3초), 틱 간격(0.5초), 한 틱 피해(6 x 공력 배율, 독무 Lv1 장판 한 틱과 같음).</summary>
        public float PoisonSeconds => Def.Param("poisonSeconds", 0f);
        public float PoisonTickSeconds => Def.Param("poisonTickSeconds", 0f);
        public float PoisonTickDamage(float might) => Def.Param("poisonDamage", 0f) * might;
        /// <summary>중독 한 번이 다 도는 동안 들어가는 틱 수(3초 / 0.5초 = 6).</summary>
        public int PoisonTicks => PoisonTickSeconds > 0f ? (int)Math.Floor(PoisonSeconds / PoisonTickSeconds + 1e-4f) : 0;

        /// <summary>역류 물살 폭(3m) × 경지 축척.</summary>
        public float WaveWidth(float scale = 1f) => Def.Param("width", 0f) * scale;
        /// <summary>역류 물살 속도(300, 플레이어 기본 이동 100 기준 — bosspatterns crimson_reverse_wave 와 같은 단위).</summary>
        public float WaveSpeed => Def.Param("waveSpeed", 0f);
        /// <summary>역류 물살이 쓰는 띠 안인가: 플레이어(origin)에서 바라보는 쪽(facing)으로 0 ~ reach(화면 끝까지 거리) 사이, 옆으로 폭의 절반 안.
        /// 물살은 한 번 지나가며 띠 안 적을 reach 쪽 끝으로 민다.</summary>
        public bool InSweep(Vector2 origin, Vector2 facing, Vector2 enemy, float reach, float scale = 1f)
        {
            if (facing.LengthSquared() <= 0f) facing = Vector2.UnitX;
            var f = Vector2.Normalize(facing);
            var d = enemy - origin;
            float along = Vector2.Dot(d, f), side = MathF.Abs(d.X * -f.Y + d.Y * f.X);
            return along >= 0f && along <= reach && side <= WaveWidth(scale) / 2f;
        }

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
