#nullable enable
// 지역 기믹 첫 묶음(기획서 7장 '지형 기믹 수치'): 범종, 탑림, 우물, 독안개, 물, 태극 바닥. 숫자는 gimmicks.json.
//
//   - 기믹은 그 지역 안에서, 생기는 순간의 경지 축척으로 놓인다. 그래서 기믹마다 scale(그 경지의 축척 배율,
//     Core/World/Scale 이 계산)을 생성자로 받아 반경을 세계 m로 바꿔 둔다. 거리 인자는 모두 세계 m.
//     작동 시간과 효과 크기는 축척과 상관없이 같다.
//   - 그 자리에 깔린 것(탑림·독안개·물)은 플레이어와 적에게 똑같이 작용한다.
//     손을 대야 움직이는 것(범종·우물·태극 바닥)은 플레이어만 쓴다. 범종은 플레이어가 울리고 적이 기절한다.
//   - 시간은 세상 시간(Time.deltaTime). 각성 연출처럼 세상이 느려지면 기믹도 느려진다.
//
// Unity 쪽에서 하는 일:
//   범종      매 프레임 Tick. 플레이어가 닿으면 TryRing(거리) → true 면 둘레 적마다 StunSeconds(종류, 거리)만큼 기절.
//             기절한 보스는 BossPatterns.Tick 을 부르지 않는다. 원각이 떨어뜨린 종은 FromBossDrop 으로 만든다.
//   탑림      PagodaForest.Layout(수치, 시드 Random, 배율) 로 무더기 가운데 기준 탑 자리를 받아 장애물을 놓는다.
//   우물      TryUse(거리, 최대 기혈) 이 0보다 크면 그만큼 회복.
//   독안개    샘마다 PoisonFogGimmick 하나. 한 생 시계로 RadiusAt·Contains. 안에 있는 대상마다 HealthLoss,
//             플레이어가 안에 있으면 진기에 QiMultiplier.
//   물        WaterWalk 를 플레이어에 하나 두고 경공 때 OnDash. 이동속도에 WaterGimmick.MoveSpeedMultiplier.
//   태극 바닥 TryStep(거리, 음양 게이지 값, 플레이어의 TaijiBlessing). 무공 피해에 blessing.DamageMultiplier(무공 음양).
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>기믹이 작용하는 대상.</summary>
    public enum GimmickTargetKind { Player, Normal, Elite, Boss }

    // ---- 범종 ----

    public sealed class BellGimmick
    {
        readonly BellGimmickDef _def;
        float _cooldown;
        float? _life;
        int? _usesLeft;

        /// <param name="scale">이 종이 생긴 경지의 축척 배율.</param>
        /// <param name="lifetimeSeconds">사라질 때까지(초). null 이면 지역에 늘 있는 종.</param>
        /// <param name="uses">울릴 수 있는 횟수. null 이면 끝없이(쿨다운마다).</param>
        public BellGimmick(BellGimmickDef def, float scale = 1f, float? lifetimeSeconds = null, int? uses = null)
        {
            _def = def;
            Scale = scale;
            _life = lifetimeSeconds;
            _usesLeft = uses;
        }

        /// <summary>원각의 범종 낙하가 남기는 종. 수명과 횟수는 패턴 데이터(leaveSeconds·leaveUses)를 따른다.</summary>
        public static BellGimmick FromBossDrop(BellGimmickDef def, BossPatternDef pattern, float scale = 1f) =>
            new BellGimmick(def, scale, pattern.LeaveSeconds, pattern.LeaveUses);

        public float Scale { get; }
        public float TouchRadius => _def.TouchRadius * Scale;
        public float StunRadius => _def.StunRadius * Scale;
        public float CooldownRemaining => _cooldown;
        public float? LifetimeRemaining => _life;
        public int? UsesLeft => _usesLeft;
        /// <summary>수명이 다했거나 횟수를 다 썼다. 치운다.</summary>
        public bool Gone => (_life is float l && l <= 0f) || _usesLeft == 0;
        public bool Ready => !Gone && _cooldown <= 0f;

        /// <summary>플레이어가 닿았나 본다. 울렸으면 true 이고 쿨다운이 걸린다.</summary>
        public bool TryRing(float playerDistance)
        {
            if (!Ready || playerDistance > TouchRadius) return false;
            _cooldown = _def.CooldownSeconds;
            if (_usesLeft != null) _usesLeft--;
            return true;
        }

        /// <summary>방금 울린 종이 이 대상을 몇 초 기절시키나. 반경 밖이거나 플레이어면 0.</summary>
        public float StunSeconds(GimmickTargetKind kind, float distance)
        {
            if (kind == GimmickTargetKind.Player || distance > StunRadius) return 0f;
            return kind == GimmickTargetKind.Boss ? _def.BossStunSeconds : _def.StunSeconds;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            _cooldown = Math.Max(0f, _cooldown - dt);
            if (_life != null) _life = Math.Max(0f, _life.Value - dt);
        }
    }

    // ---- 탑림 ----

    /// <summary>돌탑 하나. 무더기 가운데를 0,0으로 둔 세계 m.</summary>
    public readonly struct PagodaStone
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Radius;

        public PagodaStone(float x, float y, float radius)
        {
            X = x;
            Y = y;
            Radius = radius;
        }
    }

    public static class PagodaForest
    {
        /// <summary>돌탑 무더기 하나를 놓는다. 같은 시드의 Random 이면 같은 배치가 나온다.
        /// 격자 자리마다 난수를 같은 순서(흔들기 x, y, 비우기)로 꺼낸다.</summary>
        public static List<PagodaStone> Layout(PagodaForestGimmickDef def, Random rng, float scale = 1f)
        {
            var stones = new List<PagodaStone>();
            if (def.Spacing <= 0f) return stones;
            var placed = new List<(float x, float y)>(); // 화면 기준 m. 간격 판정은 축척 전에 한다.
            int n = (int)Math.Ceiling(def.PatchRadius / def.Spacing);
            float minCenter = 2f * def.PagodaRadius + def.MinGap;
            for (int j = -n; j <= n; j++)
            for (int i = -n; i <= n; i++)
            {
                float x = i * def.Spacing + (float)(rng.NextDouble() * 2 - 1) * def.Jitter;
                float y = j * def.Spacing + (float)(rng.NextDouble() * 2 - 1) * def.Jitter;
                bool skip = rng.NextDouble() < def.SkipChance;
                if (skip) continue;
                if (Math.Sqrt(x * x + y * y) + def.PagodaRadius > def.PatchRadius) continue;
                bool tooClose = false;
                foreach (var (px, py) in placed)
                {
                    float dx = px - x, dy = py - y;
                    if (dx * dx + dy * dy < minCenter * minCenter) { tooClose = true; break; }
                }
                if (tooClose) continue;
                placed.Add((x, y));
                stones.Add(new PagodaStone(x * scale, y * scale, def.PagodaRadius * scale));
            }
            return stones;
        }

        /// <summary>격자대로 놓였을 때 탑 사이 길 폭(화면 기준 m).</summary>
        public static float NominalGap(PagodaForestGimmickDef def) => def.Spacing - 2f * def.PagodaRadius;

        /// <summary>돌탑이 이 대상의 걸음을 막나. 걷는 것은 플레이어든 적이든 막는다.</summary>
        public static bool BlocksMovement(PagodaForestGimmickDef def, bool flying) => !flying || def.BlocksFlying;
    }

    // ---- 우물 ----

    public sealed class WellGimmick
    {
        readonly WellGimmickDef _def;

        public WellGimmick(WellGimmickDef def, float scale = 1f)
        {
            _def = def;
            Scale = scale;
            UsesLeft = def.Uses;
        }

        public float Scale { get; }
        public float TouchRadius => _def.TouchRadius * Scale;
        public int UsesLeft { get; private set; }
        public bool Dry => UsesLeft <= 0;

        /// <summary>플레이어가 다가왔나 본다. 쓰였으면 회복량(최대 기혈 × 0.5), 아니면 0.</summary>
        public float TryUse(float playerDistance, float maxHealth)
        {
            if (Dry || playerDistance > TouchRadius) return 0f;
            UsesLeft--;
            return maxHealth * _def.HealMaxHealthRatio;
        }
    }

    // ---- 독안개 ----

    public enum FogPhase { Clear, Spreading, Holding, Fading }

    /// <summary>독안개 샘 하나. 상태는 한 생 시계만으로 정해진다(따로 Tick 하지 않는다).</summary>
    public sealed class PoisonFogGimmick
    {
        readonly PoisonFogGimmickDef _def;

        public PoisonFogGimmick(PoisonFogGimmickDef def, float scale = 1f)
        {
            _def = def;
            Scale = scale;
        }

        public float Scale { get; }

        /// <summary>지금 퍼짐의 몇 초째인가. 퍼지는 중이 아니면 null.</summary>
        static float? EpisodeTime(PoisonFogGimmickDef def, float lifeSeconds)
        {
            if (lifeSeconds < def.FirstSpreadSeconds || def.PeriodSeconds <= 0f) return null;
            float local = (lifeSeconds - def.FirstSpreadSeconds) % def.PeriodSeconds;
            return local < def.EpisodeSeconds ? local : (float?)null;
        }

        public static FogPhase PhaseAt(PoisonFogGimmickDef def, float lifeSeconds)
        {
            if (EpisodeTime(def, lifeSeconds) is not float t) return FogPhase.Clear;
            if (t < def.SpreadSeconds) return FogPhase.Spreading;
            if (t < def.SpreadSeconds + def.HoldSeconds) return FogPhase.Holding;
            return FogPhase.Fading;
        }

        public FogPhase PhaseAt(float lifeSeconds) => PhaseAt(_def, lifeSeconds);

        /// <summary>지금 안개 반경(세계 m). 퍼질 때는 0에서 커지고, 걷힐 때는 다시 줄어든다.</summary>
        public float RadiusAt(float lifeSeconds)
        {
            if (EpisodeTime(_def, lifeSeconds) is not float t) return 0f;
            float k;
            if (t < _def.SpreadSeconds) k = _def.SpreadSeconds <= 0f ? 1f : t / _def.SpreadSeconds;
            else if (t < _def.SpreadSeconds + _def.HoldSeconds) k = 1f;
            else k = _def.FadeSeconds <= 0f ? 0f : 1f - (t - _def.SpreadSeconds - _def.HoldSeconds) / _def.FadeSeconds;
            return _def.Radius * Scale * Math.Max(0f, Math.Min(1f, k));
        }

        /// <summary>샘에서 이만큼 떨어진 자리가 지금 안개 안인가.</summary>
        public bool Contains(float distanceFromSource, float lifeSeconds)
        {
            float r = RadiusAt(lifeSeconds);
            return r > 0f && distanceFromSource <= r;
        }

        /// <summary>lifeSeconds 뒤로 처음 퍼지기 시작하는 시각.</summary>
        public float NextSpreadAt(float lifeSeconds)
        {
            if (lifeSeconds < _def.FirstSpreadSeconds) return _def.FirstSpreadSeconds;
            int k = (int)Math.Floor((lifeSeconds - _def.FirstSpreadSeconds) / _def.PeriodSeconds);
            return _def.FirstSpreadSeconds + (k + 1) * _def.PeriodSeconds;
        }

        /// <summary>안개 안에서 dt 동안 잃는 기혈. 보스와 독에 면역인 대상은 0.</summary>
        public float HealthLoss(GimmickTargetKind kind, float maxHealth, float dt, bool poisonImmune = false)
        {
            if (dt <= 0f || poisonImmune || (kind == GimmickTargetKind.Boss && _def.BossImmune)) return 0f;
            return maxHealth * _def.HealthLossPerSecond * dt;
        }

        /// <summary>플레이어가 안개 안에 있으면 얻는 진기에 곱하는 값.</summary>
        public float QiMultiplier(bool playerInside) => playerInside ? _def.QiMultiplier : 1f;
    }

    // ---- 물 ----

    public static class WaterGimmick
    {
        /// <summary>물 안의 이동속도 배율. 플레이어와 적에게 똑같이 0.7.
        /// 나는 적, 수상비 중인 플레이어, 물에 사는 적이나 수로채 성질(ignoresWater)은 1.</summary>
        public static float MoveSpeedMultiplier(WaterGimmickDef def, bool inWater, bool flying = false, bool waterWalking = false, bool ignoresWater = false)
        {
            if (!inWater || waterWalking || ignoresWater || (flying && def.FlyingIgnores)) return 1f;
            return def.MoveSpeedMultiplier;
        }
    }

    /// <summary>플레이어의 수상비. 경공을 쓰면 3초 동안 물에서 느려지지 않는다.</summary>
    public sealed class WaterWalk
    {
        readonly WaterGimmickDef _def;

        public WaterWalk(WaterGimmickDef def) { _def = def; }

        public float Remaining { get; private set; }
        public bool Active => Remaining > 0f;

        public void OnDash() => Remaining = _def.WaterWalkSeconds;

        public void Tick(float dt)
        {
            if (dt > 0f) Remaining = Math.Max(0f, Remaining - dt);
        }
    }

    // ---- 태극 바닥 ----

    public sealed class TaijiFloorGimmick
    {
        readonly TaijiFloorGimmickDef _def;
        float _cooldown;

        public TaijiFloorGimmick(TaijiFloorGimmickDef def, float scale = 1f)
        {
            _def = def;
            Scale = scale;
        }

        public float Scale { get; }
        public float TouchRadius => _def.TouchRadius * Scale;
        public float CooldownRemaining => _cooldown;
        public bool Ready => _cooldown <= 0f;

        /// <summary>플레이어가 밟았나 본다. gaugeValue 는 음양 게이지 값(YinYangGauge.Value).
        /// 받았으면 blessing 에 반대 속성 30초를 걸고 true. 게이지가 0이면 아무 일도 없고 바닥도 쓰이지 않는다.</summary>
        public bool TryStep(float playerDistance, int gaugeValue, TaijiBlessing blessing)
        {
            if (!Ready || playerDistance > TouchRadius) return false;
            var aspect = TaijiBlessing.OppositeOf(gaugeValue);
            if (aspect == YinYang.Neutral) return false;
            blessing.Grant(aspect);
            _cooldown = _def.CooldownSeconds;
            return true;
        }

        public void Tick(float dt)
        {
            if (dt > 0f) _cooldown = Math.Max(0f, _cooldown - dt);
        }
    }

    /// <summary>태극 바닥이 플레이어에게 건 기운. 플레이어에 하나 둔다.</summary>
    public sealed class TaijiBlessing
    {
        readonly TaijiFloorGimmickDef _def;

        public TaijiBlessing(TaijiFloorGimmickDef def) { _def = def; }

        /// <summary>피해가 오르는 무공 속성. 기운이 없으면 Neutral.</summary>
        public YinYang Aspect { get; private set; } = YinYang.Neutral;
        public float Remaining { get; private set; }
        public bool Active => Remaining > 0f && Aspect != YinYang.Neutral;

        /// <summary>게이지와 반대 속성. 양(+)이면 음, 음(-)이면 양, 0이면 Neutral.</summary>
        public static YinYang OppositeOf(int gaugeValue) =>
            gaugeValue > 0 ? YinYang.Yin : gaugeValue < 0 ? YinYang.Yang : YinYang.Neutral;

        /// <summary>기운을 건다. 이미 있으면 속성과 시간을 새로 건다.</summary>
        public void Grant(YinYang aspect)
        {
            Aspect = aspect;
            Remaining = aspect == YinYang.Neutral ? 0f : _def.DurationSeconds;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            Remaining = Math.Max(0f, Remaining - dt);
            if (Remaining <= 0f) Aspect = YinYang.Neutral;
        }

        /// <summary>이 무공의 피해 배율. 기운이 걸린 속성이면 1.3, 아니면 1(중립 무공은 늘 1).</summary>
        public float DamageMultiplier(YinYang weaponAspect) =>
            Active && weaponAspect == Aspect ? 1f + _def.OppositeAspectDamageBonus : 1f;
    }
}
