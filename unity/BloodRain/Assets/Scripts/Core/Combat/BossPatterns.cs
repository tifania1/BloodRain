#nullable enable
// 땅의 주인 패턴 시간표(기획서 6장 '땅의 주인 패턴 수치'). 숫자는 bosspatterns.json.
// 웹 프로토타입 template.html 의 bossAI 와 같은 규칙이다(예고 시간 등 프로토타입에 없던 값은 데이터 _note 참고).
//
//   - 싸움이 시작되고 FirstCastSeconds 뒤 첫 패턴. 그 뒤 Order 차례로 IntervalSeconds 마다.
//     간격은 패턴이 시작된 시각에서 다음 패턴이 시작되는 시각까지다(예고·판정 길이와 상관없다).
//   - 남은 기혈 비율이 Phase2At 이하가 되면 2막. 그 순간 2막 패턴(Phase2)이 예고를 시작하고, 예고가 끝나면 켜져서 끝까지 간다.
//     이미 잡혀 있던 다음 패턴 시각은 그대로 두고, 2막이 된 뒤 시작하는 패턴부터 간격이 Phase2IntervalSeconds 다.
//   - 보스가 기절한 동안은 Tick 을 부르지 않는다. 패턴 시계가 같이 멈춘다(프로토타입과 같다).
//   - 길이·반경은 화면 기준 m(축척 배율 1). 세계에 놓을 때는 지금 경지 축척 배율을 곱한다.
//
// Unity 쪽 흐름:
//   1. 보스전이 시작되면 new BossPatterns(data.BossPatternSet(보스 id)).
//   2. 매 프레임(보스가 기절하지 않았을 때) Tick(Time.deltaTime, 남은 기혈 / 최대 기혈) 이 돌려준 일을 순서대로 처리한다.
//      Telegraph: 예고를 띄운다(Aim 이 player_at_telegraph 면 이때 방향·자리를 잡는다). Resolve: 판정하거나 소환한다.
//      Phase2: 2막 연출. 바로 뒤에 2막 패턴의 Telegraph 가 온다.
//   3. Holding 이면 보스를 멈춰 세운다. 2막 패턴은 ActiveSeconds(패턴)가 있으면 BeamDegrees 로 줄마다 각도를 구해 InBeam 으로 맞힌다.
//   4. 피해는 ScaleDamage(BaseDamage, 싸움을 건 분, 혈운 농도, balance.json enemyScaling.damagePerMinute) 후 호신을 뺀다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    /// <summary>Tick 이 알리는 일의 종류. 같은 시각이면 이 순서로 온다.</summary>
    public enum BossPatternEventKind
    {
        /// <summary>남은 기혈이 문턱 이하가 됐다. 2막 패턴의 Telegraph 가 같은 시각에 따라온다.</summary>
        Phase2,
        /// <summary>예고를 시작한다.</summary>
        Telegraph,
        /// <summary>판정한다(피해·소환). 2막 패턴이면 이때부터 켜진다.</summary>
        Resolve,
    }

    public readonly struct BossPatternEvent
    {
        public readonly BossPatternEventKind Kind;
        /// <summary>Phase2 이면 null.</summary>
        public readonly BossPatternDef? Pattern;
        /// <summary>차례 패턴의 순번(0부터). 2막 패턴과 Phase2 는 -1.</summary>
        public readonly int Cast;
        /// <summary>싸움이 시작된 뒤 이 일이 정해진 시각(초). 프레임 사이에 끼면 지금 시각보다 앞설 수 있다.</summary>
        public readonly float At;

        public BossPatternEvent(BossPatternEventKind kind, BossPatternDef? pattern, int cast, float at)
        {
            Kind = kind;
            Pattern = pattern;
            Cast = cast;
            At = at;
        }
    }

    /// <summary>시간표의 한 칸: 몇 번째 패턴이 무엇이고 언제 예고·판정하는지.</summary>
    public readonly struct BossPatternCast
    {
        public readonly int Index;
        public readonly BossPatternDef Pattern;
        public readonly float TelegraphAt;
        /// <summary>이 패턴이 2막에 시작됐는가(그렇다면 다음 패턴까지 2막 간격).</summary>
        public readonly bool InPhase2;

        public BossPatternCast(int index, BossPatternDef pattern, float telegraphAt, bool inPhase2)
        {
            Index = index;
            Pattern = pattern;
            TelegraphAt = telegraphAt;
            InPhase2 = inPhase2;
        }

        /// <summary>판정 시각. 순간이동(blink_slam)은 마지막 자리에서 내려찍는 때(BlinkSlamSeconds), 나머지는 예고가 끝날 때.</summary>
        public float ResolveAt => TelegraphAt + (Pattern.Shape == "blink_slam" ? BossPatterns.BlinkSlamSeconds(Pattern) : Pattern.TelegraphSeconds);
        /// <summary>BossHolds 인 패턴에서 보스가 멈춰 서는 끝(예고부터 시전이 끝날 때까지, CastSeconds). 태극권은 빨아들이는 1.5초까지 선다(6장).</summary>
        public float HoldUntil => TelegraphAt + Math.Max(Pattern.TelegraphSeconds, BossPatterns.CastSeconds(Pattern));
    }

    public sealed class BossPatterns
    {
        readonly BossPatternSetDef _def;
        readonly BossPatternDef[] _order;
        readonly bool[] _phase2Resolved;
        readonly List<BossPatternCast> _pending = new List<BossPatternCast>();
        float _holdUntil = float.NegativeInfinity;
        float _t;
        int _nextIndex;
        float _nextAt;

        public BossPatterns(BossPatternSetDef def)
        {
            _def = def;
            _order = OrderOf(def);
            _phase2Resolved = new bool[def.Phase2.Count];
            _nextAt = def.FirstCastSeconds;
        }

        public BossPatternSetDef Def => _def;
        /// <summary>싸움이 시작되고 흐른 시간(기절한 동안은 빼고).</summary>
        public float Elapsed => _t;
        /// <summary>2막이 시작된 시각. 아직 1막이면 null.</summary>
        public float? Phase2StartedAt { get; private set; }
        public bool InPhase2 => Phase2StartedAt != null;
        /// <summary>다음에 시작할 차례 패턴.</summary>
        public BossPatternCast NextCast => new BossPatternCast(_nextIndex, _order[_nextIndex % _order.Length], _nextAt, InPhase2);
        /// <summary>예고는 시작했고 아직 판정 전인 차례 패턴들.</summary>
        public IReadOnlyList<BossPatternCast> Pending => _pending;
        /// <summary>보스가 멈춰 서 있어야 하는가(BossHolds 인 패턴을 예고하거나 시전하는 중, 판정 뒤 CastSeconds 까지 포함).</summary>
        public bool Holding => _pending.Any(c => c.Pattern.BossHolds) || _t < _holdUntil;

        /// <summary>남은 기혈 비율로 2막인지. 문턱과 같으면 2막이다. 2막 패턴이 없는 보스(서장 장삼)는 2막이 오지 않는다.</summary>
        public static bool IsPhase2(BossPatternSetDef def, float healthRatio) => def.Phase2.Count > 0 && healthRatio <= def.Phase2At;

        /// <summary>시계를 민다. healthRatio 는 이번 Tick 을 시작할 때의 남은 기혈 / 최대 기혈.
        /// 이번에 지나간 일을 시각 순서대로 돌려준다.</summary>
        public IReadOnlyList<BossPatternEvent> Tick(float dt, float healthRatio)
        {
            var events = new List<BossPatternEvent>();
            float from = _t;
            if (dt > 0f) _t += dt;

            if (Phase2StartedAt == null && IsPhase2(_def, healthRatio))
            {
                Phase2StartedAt = from;
                events.Add(new BossPatternEvent(BossPatternEventKind.Phase2, null, -1, from));
                foreach (var p in _def.Phase2)
                    events.Add(new BossPatternEvent(BossPatternEventKind.Telegraph, p, -1, from));
            }

            while (_nextAt <= _t)
            {
                var cast = NextCast;
                events.Add(new BossPatternEvent(BossPatternEventKind.Telegraph, cast.Pattern, cast.Index, cast.TelegraphAt));
                _pending.Add(cast);
                if (cast.Pattern.BossHolds) _holdUntil = Math.Max(_holdUntil, cast.HoldUntil);
                _nextAt += IntervalAfter(_def, cast.TelegraphAt, Phase2StartedAt);
                _nextIndex++;
            }

            for (int i = 0; i < _pending.Count;)
            {
                var c = _pending[i];
                if (c.ResolveAt <= _t)
                {
                    events.Add(new BossPatternEvent(BossPatternEventKind.Resolve, c.Pattern, c.Index, c.ResolveAt));
                    _pending.RemoveAt(i);
                }
                else i++;
            }

            if (Phase2StartedAt is float p2)
            {
                for (int i = 0; i < _def.Phase2.Count; i++)
                {
                    var p = _def.Phase2[i];
                    float on = p2 + p.TelegraphSeconds;
                    if (_phase2Resolved[i] || on > _t) continue;
                    _phase2Resolved[i] = true;
                    events.Add(new BossPatternEvent(BossPatternEventKind.Resolve, p, -1, on));
                }
            }

            if (events.Count < 2) return events;
            return events.OrderBy(e => e.At).ThenBy(e => (int)e.Kind).ThenBy(e => e.Cast).ToList();
        }

        /// <summary>2막 패턴이 켜진 뒤 흐른 시간. 아직 안 켜졌으면 null.</summary>
        public float? ActiveSeconds(BossPatternDef phase2Pattern)
        {
            if (Phase2StartedAt is not float p2) return null;
            float on = p2 + phase2Pattern.TelegraphSeconds;
            return _t >= on ? _t - on : (float?)null;
        }

        /// <summary>패턴 시간표를 미리 펼친다. phase2StartedAt 은 2막이 시작된 시각(없으면 1막만).
        /// Tick 을 같은 시각에 2막으로 넘기면 같은 표가 나온다.</summary>
        public static List<BossPatternCast> Plan(BossPatternSetDef def, float untilSeconds, float? phase2StartedAt = null)
        {
            var order = OrderOf(def);
            var plan = new List<BossPatternCast>();
            float t = def.FirstCastSeconds;
            for (int i = 0; t <= untilSeconds; i++)
            {
                bool p2 = phase2StartedAt != null && phase2StartedAt.Value <= t;
                plan.Add(new BossPatternCast(i, order[i % order.Length], t, p2));
                t += IntervalAfter(def, t, phase2StartedAt);
            }
            return plan;
        }

        static float IntervalAfter(BossPatternSetDef def, float castAt, float? phase2StartedAt) =>
            phase2StartedAt != null && phase2StartedAt.Value <= castAt ? def.Phase2IntervalSeconds : def.IntervalSeconds;

        static BossPatternDef[] OrderOf(BossPatternSetDef def)
        {
            if (def.Order.Count == 0) throw new InvalidOperationException("패턴 순서(order)가 비어 있습니다");
            if (def.IntervalSeconds <= 0f || def.Phase2IntervalSeconds <= 0f)
                throw new InvalidOperationException("패턴 간격은 0보다 커야 합니다");
            return def.Order.Select(def.Pattern).ToArray();
        }

        // ---- 피해 ----

        /// <summary>11장 적 피해 식. 기준 피해 × (1 + damagePerMinute × 싸움을 건 분) × √혈운 농도. 호신은 받는 쪽에서 뺀다.</summary>
        public static float ScaleDamage(float baseDamage, float fightMinute, float density, float damagePerMinute) =>
            baseDamage * (1f + damagePerMinute * Math.Max(0f, fightMinute)) * (float)Math.Sqrt(Math.Max(0f, density));

        // ---- 모양(보스나 패턴 가운데를 0,0으로 둔 화면 기준 m, 각도는 도. 경계는 맞는 것으로 친다) ----

        /// <summary>부채꼴. facingDegrees 는 예고를 시작할 때 잡은 방향.</summary>
        public static bool InCone(BossPatternDef p, float dx, float dy, float facingDegrees, float targetRadius = 0f)
        {
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d > p.Radius + targetRadius) return false;
            if (d <= 1e-4f) return true;
            float diff = AngleDiff((float)(Math.Atan2(dy, dx) * 180.0 / Math.PI), facingDegrees);
            return diff <= p.AngleDegrees * 0.5f;
        }

        /// <summary>동그라미 하나(범종 낙하 한 곳 등).</summary>
        public static bool InCircle(float radius, float dx, float dy, float targetRadius = 0f) =>
            dx * dx + dy * dy <= (radius + targetRadius) * (radius + targetRadius);

        /// <summary>보스 몸에서 beamDegrees 쪽으로 뻗은 빛줄기 하나.</summary>
        public static bool InBeam(BossPatternDef p, float dx, float dy, float beamDegrees, float targetRadius = 0f) =>
            InLine(p.Length, p.Width, dx, dy, beamDegrees, targetRadius);

        /// <summary>몸에서 degrees 쪽으로 length 만큼 뻗은 폭 width 의 줄.</summary>
        public static bool InLine(float length, float width, float dx, float dy, float degrees, float targetRadius = 0f)
        {
            double a = degrees * Math.PI / 180.0;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            float along = dx * ca + dy * sa;
            float perp = Math.Abs(-dx * sa + dy * ca);
            return along >= 0f && along <= length + targetRadius && perp <= width * 0.5f + targetRadius;
        }

        /// <summary>빛줄기 beam 번째(0부터)의 각도. towardPlayerDegrees 는 2막이 된 순간 보스에서 플레이어 쪽.
        /// 반시계(각도가 커지는 쪽)로 돈다.</summary>
        public static float BeamDegrees(BossPatternDef p, float towardPlayerDegrees, float activeSeconds, int beam)
        {
            int n = Math.Max(1, p.Count);
            float a = towardPlayerDegrees + p.StartOffsetDegrees + p.TurnDegreesPerSecond * Math.Max(0f, activeSeconds) + beam * 360f / n;
            return Normalize(a);
        }

        /// <summary>원 위에 고르게 놓은 소환 자리(가운데 기준). 첫 자리는 0도.</summary>
        public static (float x, float y)[] RingOffsets(BossPatternDef p) => RingOffsets(p, p.Radius);

        /// <summary>한 시전이 예고 시작부터 마지막 판정까지 걸리는 시간(초).
        /// jump_slam 은 Count 번 뛰는데 뛸 때마다 예고가 있어 Count × 예고 + (Count - 1) × 사이,
        /// 여러 파(Waves)를 쏘는 것은 첫 예고 + (Waves - 1) × 사이, 태극권(inhale_exhale)은 예고 + 빨아들이는 시간.
        /// 꽃잎 나선은 예고 + 쏘는 시간(LeaveSeconds), 얼음 가시는 예고 + 마지막 가시까지, 순간이동은 BlinkSlamSeconds. 나머지는 예고만.</summary>
        public static float CastSeconds(BossPatternDef p)
        {
            if (p.Shape == "jump_slam")
            {
                int n = Math.Max(1, p.Count);
                return n * p.TelegraphSeconds + (n - 1) * p.WaveIntervalSeconds;
            }
            if (p.Shape == "inhale_exhale") return p.TelegraphSeconds + p.PullSeconds;
            if (p.Shape == "spiral_bullets") return p.TelegraphSeconds + p.LeaveSeconds;
            if (p.Shape == "chain_circles") return p.TelegraphSeconds + (Math.Max(1, p.Count) - 1) * p.WaveIntervalSeconds;
            if (p.Shape == "blink_slam") return BlinkSlamSeconds(p);
            return p.TelegraphSeconds + (Math.Max(1, p.Waves) - 1) * p.WaveIntervalSeconds;
        }

        /// <summary>조여 드는 고리(closing_ring)의 지금 반경. activeSeconds 는 판정에서 흐른 시간.
        /// Radius 에서 ShrinkPerSecond 로 줄어 MinRadius 에서 멈춘다.</summary>
        public static float ClosingRadius(BossPatternDef p, float activeSeconds) =>
            Math.Max(p.MinRadius, p.Radius - p.ShrinkPerSecond * Math.Max(0f, activeSeconds));

        /// <summary>조여 드는 고리가 MinRadius 에 닿을 때까지(초). 줄지 않으면 0.</summary>
        public static float ClosingSeconds(BossPatternDef p) =>
            p.ShrinkPerSecond > 0f ? Math.Max(0f, p.Radius - p.MinRadius) / p.ShrinkPerSecond : 0f;

        /// <summary>반경 radius 원 위에 고르게 놓은 Count 자리(검진의 검). 첫 자리는 firstDegrees 쪽.</summary>
        public static (float x, float y)[] RingOffsets(BossPatternDef p, float radius, float firstDegrees = 0f)
        {
            var list = new (float x, float y)[Math.Max(0, p.Count)];
            for (int i = 0; i < list.Length; i++)
            {
                double a = (firstDegrees + 360.0 * i / list.Length) * Math.PI / 180.0;
                list[i] = ((float)(Math.Cos(a) * radius), (float)(Math.Sin(a) * radius));
            }
            return list;
        }

        /// <summary>장판(PoolRadius) 안인가. 장판 가운데 기준.</summary>
        public static bool InPool(BossPatternDef p, float dx, float dy, float targetRadius = 0f) =>
            p.PoolRadius > 0f && InCircle(p.PoolRadius, dx, dy, targetRadius);

        /// <summary>플레이어 둘레 가로·세로 ±Scatter 안에서 Count 곳을 고른다(범종 낙하).</summary>
        public static (float x, float y)[] ScatterOffsets(BossPatternDef p, Random rng)
        {
            var list = new (float x, float y)[p.Count];
            for (int i = 0; i < p.Count; i++)
                list[i] = ((float)(rng.NextDouble() * 2 - 1) * p.Scatter, (float)(rng.NextDouble() * 2 - 1) * p.Scatter);
            return list;
        }

        // ---- 세 땅의 주인 판정(R-11: 당가타·장강·무당산). Unity·웹이 같은 셈을 쓴다 ----

        /// <summary>패턴 속도(플레이어 기본 이동속도 100 기준)를 화면 기준 m/s 로. moveBase 는 balance.json stats.moveBase.</summary>
        public static float MetersPerSecond(float patternSpeed, float moveBase) => patternSpeed / 100f * moveBase;

        // 흑백(split_field). 경계는 보스를 지나고, 예고를 시작할 때 보스 → 플레이어 쪽(aimDegrees)과 수직이다.
        // 그때 플레이어가 선 쪽이 검은(음) 구역, 반대쪽이 흰(양) 구역. 2막 경계는 TurnDegreesPerSecond 로 반시계로 돈다.
        // 내 색은 음양 게이지가 기운 쪽(YinYangGauge.Leaning): 양이면 흰 구역, 음이면 검은 구역, 태극 구간이면 양쪽 다 내 색.

        /// <summary>흑백 경계의 법선(검은 구역 쪽) 각도. activeSeconds 는 켜진 뒤 흐른 시간(1막 흑백은 돌지 않아 상관없다).</summary>
        public static float SplitFieldBlackDegrees(BossPatternDef p, float aimDegrees, float activeSeconds) =>
            Normalize(aimDegrees + p.TurnDegreesPerSecond * Math.Max(0f, activeSeconds));

        /// <summary>보스 기준 (dx, dy) 가 어느 색 구역인가. 경계 위는 검은 구역으로 친다.</summary>
        public static YinYang SplitFieldColor(BossPatternDef p, float dx, float dy, float aimDegrees, float activeSeconds)
        {
            double a = SplitFieldBlackDegrees(p, aimDegrees, activeSeconds) * Math.PI / 180.0;
            return dx * Math.Cos(a) + dy * Math.Sin(a) >= -1e-4 ? YinYang.Yin : YinYang.Yang;
        }

        /// <summary>그 구역에서 받는 피해 배율. myLeaning 이 Neutral(태극 구간)이면 1, 구역 색과 다르면 DamageTakenMultiplier.</summary>
        public static float SplitFieldDamageTaken(BossPatternDef p, YinYang zoneColor, YinYang myLeaning) =>
            myLeaning != YinYang.Neutral && myLeaning != zoneColor && p.DamageTakenMultiplier > 0f ? p.DamageTakenMultiplier : 1f;

        /// <summary>한 번에: 보스 기준 (dx, dy) 에 서 있을 때 받는 피해 배율.</summary>
        public static float SplitFieldDamageTaken(BossPatternDef p, float dx, float dy, float aimDegrees, float activeSeconds, YinYang myLeaning) =>
            SplitFieldDamageTaken(p, SplitFieldColor(p, dx, dy, aimDegrees, activeSeconds), myLeaning);

        // 칠성검진(arrangement big_dipper). 손잡이 끝(요광)에서 국자 끝(천추)까지, 손잡이 → 국자를 +x 로 둔 북두칠성 꼴.
        // x 는 0~1(전체 길이 Length 에 곱한다), y 는 국자가 휜 쪽(+). 일곱째(국자 끝)에 플레이어를 둔다.
        static readonly (float x, float y)[] BigDipper =
        {
            (0f, 0f), (0.18f, 0.05f), (0.36f, 0.03f), (0.54f, 0.11f), (0.67f, 0.30f), (0.93f, 0.33f), (1f, 0.09f),
        };

        /// <summary>북두칠성 꼴(손잡이 끝 → 국자 끝이 +x, x 0~1). 웹 천하 시안 BIG_DIPPER 가 같은 값이다.</summary>
        public static IReadOnlyList<(float x, float y)> BigDipperShape => BigDipper;

        /// <summary>칠성검진 일곱 자리(플레이어 기준). handleDegrees 는 국자 끝 → 손잡이 쪽 방향
        /// (보스 쪽을 주면 손잡이가 보스를 향한다). 일곱째가 (0, 0), 곧 예고를 시작할 때 플레이어 자리다.</summary>
        public static (float x, float y)[] BigDipperOffsets(BossPatternDef p, float handleDegrees)
        {
            if (p.Count != BigDipper.Length) throw new InvalidOperationException($"{p.Id}: 북두칠성은 {BigDipper.Length}곳입니다(count {p.Count})");
            // 모양 좌표는 손잡이 → 국자가 +x. handleDegrees 는 그 반대 방향이라 180도 돌린다.
            double a = (handleDegrees + 180.0) * Math.PI / 180.0;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            var end = BigDipper[BigDipper.Length - 1];
            var list = new (float x, float y)[BigDipper.Length];
            for (int i = 0; i < list.Length; i++)
            {
                float u = (BigDipper[i].x - end.x) * p.Length, v = (BigDipper[i].y - end.y) * p.Length;
                list[i] = (u * ca - v * sa, u * sa + v * ca);
            }
            return list;
        }

        /// <summary>태극권: 보스 기준 (dx, dy) 에 선 플레이어가 이번 프레임 끌려가는 거리(보스 쪽 벡터).
        /// 반경 Radius 안이면 기본 이동속도 × PullSpeedRatio 로 끌리고, 보스와 stopDistance(몸 반경 합) 안으로는 들어가지 않는다.</summary>
        public static (float x, float y) InhalePull(BossPatternDef p, float dx, float dy, float dt, float moveBase, float stopDistance = 0f, float targetRadius = 0f)
        {
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dt <= 0f || d <= stopDistance || d > p.Radius + targetRadius || d <= 1e-4f) return (0f, 0f);
            float step = Math.Min(d - stopDistance, moveBase * p.PullSpeedRatio * dt);
            return (-dx / d * step, -dy / d * step);
        }

        /// <summary>태극권 끝에 내뿜는 원(BurstRadius) 안인가.</summary>
        public static bool InBurst(BossPatternDef p, float dx, float dy, float targetRadius = 0f) => InCircle(p.BurstRadius, dx, dy, targetRadius);

        /// <summary>갈고리 사슬 끝이 판정 뒤 sinceResolve 초에 보스에서 얼마나 나갔나(Length 에서 멈춘다).</summary>
        public static float ChainReach(BossPatternDef p, float sinceResolve, float moveBase) =>
            Math.Min(p.Length, MetersPerSecond(p.ProjectileSpeed, moveBase) * Math.Max(0f, sinceResolve));

        /// <summary>사슬에 걸렸나: 보스에서 aimDegrees 쪽으로 reach 만큼 나간 폭 Width 의 줄 안.</summary>
        public static bool InChain(BossPatternDef p, float dx, float dy, float aimDegrees, float reach, float targetRadius = 0f) =>
            InLine(reach, p.Width, dx, dy, aimDegrees, targetRadius);

        /// <summary>걸린 뒤 elapsed 초에 플레이어가 있어야 할 자리(보스 기준). 걸린 자리(hitDx, hitDy)에서 보스 앞
        /// (보스와 stopDistance 떨어진 곳)까지 PullSeconds 동안 곧게 끌려간다. 경공으로 끊는 것은 부르는 쪽에서(끊기면 그만 부른다).</summary>
        public static (float x, float y) ChainPullOffset(BossPatternDef p, float hitDx, float hitDy, float elapsed, float stopDistance)
        {
            float d = (float)Math.Sqrt(hitDx * hitDx + hitDy * hitDy);
            if (d <= stopDistance || d <= 1e-4f) return (hitDx, hitDy);
            float t = p.PullSeconds > 0f ? Math.Min(1f, Math.Max(0f, elapsed) / p.PullSeconds) : 1f;
            float r = d + (stopDistance - d) * t;
            return (hitDx / d * r, hitDy / d * r);
        }

        /// <summary>끌려가는 게 끝났나.</summary>
        public static bool ChainPullDone(BossPatternDef p, float elapsed) => elapsed >= p.PullSeconds;

        // ---- 세 땅의 주인 판정(R4-02: 화산·곤륜 설원·십만대산). Unity·웹이 같은 셈을 쓴다 ----
        // 각도는 도, 0도는 +x, 반시계. 자리는 모두 가운데(보스나 플레이어) 기준 (dx, dy).

        /// <summary>꽃잎 나선(spiral_bullets): 판정 뒤 LeaveSeconds 동안 HitIntervalSeconds 마다 갈래마다 한 발. 갈래 하나가 쏘는 발 수.
        /// 판정 순간(0초)에 첫 발이 나가고, LeaveSeconds 에 닿기 전까지 쏜다.</summary>
        public static int SpiralShotsPerArm(BossPatternDef p) =>
            p.HitIntervalSeconds > 0f ? Math.Max(1, (int)Math.Ceiling(p.LeaveSeconds / p.HitIntervalSeconds - 1e-4)) : 1;

        /// <summary>꽃잎 나선 shot 번째 발(0부터)이 판정 뒤 몇 초에 나가나.</summary>
        public static float SpiralShotSeconds(BossPatternDef p, int shot) => shot * p.HitIntervalSeconds;

        /// <summary>꽃잎 나선 arm 갈래 shot 번째 발의 방향. startDegrees 는 첫 갈래의 처음 방향(부르는 쪽이 정한다, 보통 플레이어 쪽).
        /// 갈래는 360 ÷ Count 도씩 고르게 벌어지고, 나선 전체가 초당 TurnDegreesPerSecond 로 돈다.</summary>
        public static float SpiralShotDegrees(BossPatternDef p, float startDegrees, int arm, int shot) =>
            Normalize(startDegrees + 360f * arm / Math.Max(1, p.Count) + p.TurnDegreesPerSecond * SpiralShotSeconds(p, shot));

        /// <summary>매화검기 방사(radial_burst) wave 번째 파(0부터)의 갈래 방향 Count 개. 고르게 벌어지고 파마다 WaveOffsetDegrees 씩 엇갈린다.</summary>
        public static float[] RadialBurstDegrees(BossPatternDef p, float startDegrees, int wave)
        {
            var list = new float[Math.Max(0, p.Count)];
            for (int i = 0; i < list.Length; i++)
                list[i] = Normalize(startDegrees + 360f * i / list.Length + p.WaveOffsetDegrees * wave);
            return list;
        }

        /// <summary>눈보라(view_shrink): 판정 뒤 sinceResolve 초의 보이는 범위 배율. LeaveSeconds 동안 ViewScale, 그 밖은 1.</summary>
        public static float ViewScaleAt(BossPatternDef p, float sinceResolve) =>
            sinceResolve >= 0f && sinceResolve < p.LeaveSeconds && p.ViewScale > 0f ? p.ViewScale : 1f;

        /// <summary>얼음 가시(chain_circles) i 번째(0부터)의 자리(보스 기준). 보스 발밑(0m)에서 aimDegrees 쪽 Length 까지 Count 개를 고르게.</summary>
        public static (float x, float y) ChainCircleOffset(BossPatternDef p, float aimDegrees, int i)
        {
            float d = p.Count > 1 ? p.Length * i / (p.Count - 1) : 0f;
            double a = aimDegrees * Math.PI / 180.0;
            return ((float)(Math.Cos(a) * d), (float)(Math.Sin(a) * d));
        }

        /// <summary>얼음 가시 i 번째가 판정 뒤 몇 초에 솟나.</summary>
        public static float ChainCircleSeconds(BossPatternDef p, int i) => i * p.WaveIntervalSeconds;

        /// <summary>얼음 가시 i 번째에 맞나(그 가시 가운데 반경 Radius). 플레이어는 보스 기준 (dx, dy).</summary>
        public static bool InChainCircle(BossPatternDef p, float aimDegrees, int i, float dx, float dy, float targetRadius = 0f)
        {
            var c = ChainCircleOffset(p, aimDegrees, i);
            return InCircle(p.Radius, dx - c.x, dy - c.y, targetRadius);
        }

        /// <summary>줄어드는 싸움터(shrinking_arena) 반경을 dt 만큼 줄인다. nearSlowGimmick 이면(모닥불 곁에서 싸우면) SlowedNearMultiplier 배로 천천히.
        /// 처음 반경은 Radius, MinRadius 에서 멈춘다. 모닥불 곁인지는 부르는 쪽이 gimmicks campfire(CampfireGimmick.Inside)로 정한다.</summary>
        public static float ArenaRadiusStep(BossPatternDef p, float radius, float dt, bool nearSlowGimmick)
        {
            float speed = p.ShrinkPerSecond * (nearSlowGimmick && p.SlowedNearMultiplier > 0f ? p.SlowedNearMultiplier : 1f);
            return Math.Max(p.MinRadius, radius - speed * Math.Max(0f, dt));
        }

        /// <summary>줄어드는 싸움터가 MinRadius 에 닿을 때까지(초). 모닥불 곁에서만 싸우면 nearSlowGimmick.</summary>
        public static float ArenaSeconds(BossPatternDef p, bool nearSlowGimmick)
        {
            float speed = p.ShrinkPerSecond * (nearSlowGimmick && p.SlowedNearMultiplier > 0f ? p.SlowedNearMultiplier : 1f);
            return speed > 0f ? Math.Max(0f, p.Radius - p.MinRadius) / speed : 0f;
        }

        /// <summary>얼음 위인가: 싸움터 가운데 기준 (dx, dy) 가 반경 radius 원 바깥. 몸이 원 안에 조금이라도 걸치면 아직 안이다.</summary>
        public static bool OutsideArena(float radius, float dx, float dy, float targetRadius = 0f) =>
            dx * dx + dy * dy > (radius + targetRadius) * (radius + targetRadius);

        /// <summary>순간이동(blink_slam) 네 자리(플레이어 기준, 예고를 시작할 때 플레이어 자리). 반경 Scatter 원 안에서 고르게 뽑는다.
        /// 마지막 자리에서 내려찍는다.</summary>
        public static (float x, float y)[] BlinkOffsets(BossPatternDef p, Random rng)
        {
            var list = new (float x, float y)[Math.Max(0, p.Count)];
            for (int i = 0; i < list.Length; i++) list[i] = DiscOffset(rng, p.Scatter);
            return list;
        }

        /// <summary>순간이동 i 번째(0부터)가 시전 시작 뒤 몇 초에 옮겨 가나. 첫 번째는 시작하자마자, 그 뒤 WaveIntervalSeconds 마다.</summary>
        public static float BlinkSeconds(BossPatternDef p, int i) => i * p.WaveIntervalSeconds;

        /// <summary>내려찍기 판정까지(시전 시작 뒤 초): 마지막 순간이동 뒤 예고 TelegraphSeconds.</summary>
        public static float BlinkSlamSeconds(BossPatternDef p) => BlinkSeconds(p, Math.Max(1, p.Count) - 1) + p.TelegraphSeconds;

        /// <summary>혈월(empower)이 켜져 있으면 모든 적(보스 포함)의 피해 배율, 아니면 1.</summary>
        public static float EmpowerDamage(BossPatternDef p, bool active) => active && p.EnemyDamageMultiplier > 0f ? p.EnemyDamageMultiplier : 1f;

        /// <summary>혈월이 켜져 있으면 모든 적(보스 포함)의 이동속도 배율, 아니면 1.</summary>
        public static float EmpowerSpeed(BossPatternDef p, bool active) => active && p.EnemySpeedMultiplier > 0f ? p.EnemySpeedMultiplier : 1f;

        // ---- 종장 단계 판정(R4-03: 혈의·혈해·진혈마). 자리는 가운데(플레이어나 싸움터 섬) 기준 (dx, dy) ----

        /// <summary>맥 짚는 손(closing_ring) 고리 띠(폭 Width)에 닿나. activeSeconds 는 판정 뒤 흐른 시간, 가운데는 예고 때 플레이어 자리.
        /// 고리가 MinRadius 에 닿은 뒤(ClosingSeconds)에는 고리 안쪽 전체가 맞는다(안에 남으면 끝에 맞는다).</summary>
        public static bool InClosingRing(BossPatternDef p, float activeSeconds, float dx, float dy, float targetRadius = 0f)
            => activeSeconds <= ClosingSeconds(p) && InClosingRingShape(p, activeSeconds, dx, dy, targetRadius);

        /// <summary>조여 드는 고리가 아직 살아 있나. 고리는 MinRadius 에 닿는 순간(ClosingSeconds) 마지막으로 맞히고 사라진다
        /// (기획서 맥 짚는 손 '고리가 1.5m에 닿을 때까지 안에 남으면 맞는다'). 줄지 않는 고리(ShrinkPerSecond 0)는 DataCheck 가 막는다.</summary>
        public static bool ClosingRingAlive(BossPatternDef p, float activeSeconds) => activeSeconds >= 0f && activeSeconds <= ClosingSeconds(p);

        static bool InClosingRingShape(BossPatternDef p, float activeSeconds, float dx, float dy, float targetRadius)
        {
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            float half = p.Width * 0.5f;
            if (activeSeconds >= ClosingSeconds(p)) return d <= p.MinRadius + half + targetRadius;
            float r = ClosingRadius(p, activeSeconds);
            return Math.Abs(d - r) <= half + targetRadius;
        }

        /// <summary>역병 구름(drifting_clouds) Count 곳이 피는 자리(플레이어 기준, 반경 Scatter 원 안).</summary>
        public static (float x, float y)[] CloudOffsets(BossPatternDef p, Random rng)
        {
            var list = new (float x, float y)[Math.Max(0, p.Count)];
            for (int i = 0; i < list.Length; i++) list[i] = DiscOffset(rng, p.Scatter);
            return list;
        }

        /// <summary>구름 하나를 dt 동안 플레이어 쪽으로 옮긴다. (dx, dy) 는 구름 → 플레이어. 돌려주는 값은 구름이 움직인 거리(벡터).
        /// 빠르기는 ProjectileSpeed(기본 이동속도 100 기준)를 m/s 로 바꾼 값이고, 플레이어 자리를 지나치지 않는다.</summary>
        public static (float x, float y) CloudDrift(BossPatternDef p, float dx, float dy, float dt, float moveBase)
        {
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dt <= 0f || d <= 1e-4f) return (0f, 0f);
            float step = Math.Min(d, MetersPerSecond(p.ProjectileSpeed, moveBase) * dt);
            return (dx / d * step, dy / d * step);
        }

        /// <summary>구름이 판정 뒤 sinceResolve 초에 아직 떠 있나(LeaveSeconds 뒤 흩어진다).</summary>
        public static bool CloudAlive(BossPatternDef p, float sinceResolve) => sinceResolve >= 0f && sinceResolve < p.LeaveSeconds;

        /// <summary>차오르는 피바다(shrinking_islands) 섬의 지금 반경. Radius 에서 ShrinkPerSecond 로 MinRadius 까지.</summary>
        public static float IslandRadius(BossPatternDef p, float activeSeconds) => ClosingRadius(p, activeSeconds);

        /// <summary>섬이 MinRadius 에 닿을 때까지(초). 약 90초.</summary>
        public static float IslandSeconds(BossPatternDef p) => ClosingSeconds(p);

        /// <summary>섬 위인가: 섬 가운데들(싸움터 기준 자리, 부르는 쪽이 정한다) 가운데 하나라도 반경 안이면 섬 위.
        /// (x, y) 는 같은 기준의 서 있는 자리. 몸 가운데가 섬 안이어야 한다(걸치기만 하면 피 위).</summary>
        public static bool OnIsland(BossPatternDef p, float activeSeconds, IReadOnlyList<(float x, float y)> islands, float x, float y)
        {
            float r = IslandRadius(p, activeSeconds);
            foreach (var c in islands)
            {
                float dx = x - c.x, dy = y - c.y;
                if (dx * dx + dy * dy <= r * r) return true;
            }
            return false;
        }

        /// <summary>혈우 탄막(rain_drops) 한 파의 핏방울 Count 곳(그 파를 잡을 때 플레이어 기준, 반경 Scatter 원 안).</summary>
        public static (float x, float y)[] RainDropOffsets(BossPatternDef p, Random rng)
        {
            var list = new (float x, float y)[Math.Max(0, p.Count)];
            for (int i = 0; i < list.Length; i++) list[i] = DiscOffset(rng, p.Scatter);
            return list;
        }

        /// <summary>혈우 탄막 wave 번째 파(0부터)가 시전 시작 뒤 몇 초에 그림자를 드리우나. 떨어지는 것은 그 뒤 TelegraphSeconds.</summary>
        public static float RainWaveSeconds(BossPatternDef p, int wave) => wave * p.WaveIntervalSeconds;

        /// <summary>반경 r 원 안에서 고르게 한 점.</summary>
        static (float x, float y) DiscOffset(Random rng, float r)
        {
            double d = r * Math.Sqrt(rng.NextDouble()), a = rng.NextDouble() * 2 * Math.PI;
            return ((float)(Math.Cos(a) * d), (float)(Math.Sin(a) * d));
        }

        static float Normalize(float deg)
        {
            float a = deg % 360f;
            return a < 0f ? a + 360f : a;
        }

        static float AngleDiff(float a, float b)
        {
            float d = Math.Abs(Normalize(a) - Normalize(b));
            return d > 180f ? 360f - d : d;
        }
    }

    /// <summary>빙분신(clones, 설무향). 설무향이 Count 몸으로 갈라져 보스 둘레 반경 Scatter 안에 선다.
    /// 그 가운데 진짜 하나만 바닥 그림자가 있다. 분신은 한 대 맞으면 깨지고 LeaveSeconds 가 지나면 남은 분신이 사라진다.
    /// 진짜는 맞아도 깨지지 않고(보스 기혈이 깎인다) 시간이 지나도 남는다.</summary>
    public sealed class BossClones
    {
        readonly BossPatternDef _def;
        readonly bool[] _broken;

        /// <summary>판정 때 만든다. 자리(보스가 갈라진 자리 기준)와 진짜를 rng 로 고른다.</summary>
        public BossClones(BossPatternDef def, Random rng)
        {
            if (def.Count < 1) throw new InvalidOperationException($"{def.Id}: 분신 수(count)가 없습니다");
            _def = def;
            var offs = new (float x, float y)[def.Count];
            for (int i = 0; i < offs.Length; i++)
            {
                double d = def.Scatter * Math.Sqrt(rng.NextDouble()), a = rng.NextDouble() * 2 * Math.PI;
                offs[i] = ((float)(Math.Cos(a) * d), (float)(Math.Sin(a) * d));
            }
            Offsets = offs;
            RealIndex = rng.Next(def.Count);
            _broken = new bool[def.Count];
        }

        public IReadOnlyList<(float x, float y)> Offsets { get; }
        /// <summary>진짜(그림자가 있는 몸)의 번호.</summary>
        public int RealIndex { get; }

        /// <summary>i 번째 몸이 바닥 그림자를 드리우나(진짜만).</summary>
        public bool HasShadow(int i) => i == RealIndex;

        /// <summary>i 번째 몸이 맞았다. 분신이면 깨지고 true, 진짜면 false(보스 기혈은 부르는 쪽이 깎는다).</summary>
        public bool Hit(int i)
        {
            if (i == RealIndex || _broken[i]) return false;
            _broken[i] = true;
            return true;
        }

        /// <summary>판정 뒤 sinceResolve 초에 i 번째 몸이 서 있나.</summary>
        public bool Standing(int i, float sinceResolve) =>
            i == RealIndex || (!_broken[i] && sinceResolve < _def.LeaveSeconds);
    }

    /// <summary>반쪽 독안개(half_field). 경계는 보스를 지나는 세로줄(화면 x 가 보스와 같은 줄)이고,
    /// 덮는 쪽은 +1(오른쪽, x 가 큰 쪽) 아니면 -1(왼쪽). 첫 시전은 플레이어가 선 쪽, Alternate 면 그다음부터 번갈아,
    /// 아니면 시전마다 플레이어가 선 쪽. 플레이어가 보스와 같은 x 에 서 있으면 오른쪽으로 친다.</summary>
    public sealed class HalfFieldFog
    {
        readonly BossPatternDef _def;
        public HalfFieldFog(BossPatternDef def) { _def = def; }

        /// <summary>마지막으로 덮은 쪽. 아직 안 깔았으면 0.</summary>
        public int LastSide { get; private set; }

        /// <summary>예고를 시작할 때 부른다. playerDx 는 플레이어 x - 보스 x. 덮을 쪽을 돌려준다.</summary>
        public int Choose(float playerDx)
        {
            int side = LastSide != 0 && _def.Alternate ? -LastSide : (playerDx >= 0f ? 1 : -1);
            LastSide = side;
            return side;
        }

        /// <summary>그 쪽 안개 안인가. dx 는 보스 x 기준. 경계 위(그리고 몸 반경만큼 걸친 것)는 맞는다.</summary>
        public static bool Covers(int side, float dx, float targetRadius = 0f) => side != 0 && dx * side >= -targetRadius;
    }

    /// <summary>분열(split, 당소혜 2막). 남은 기혈을 Count 몸이 HealthShare 씩 나눠 갖는다.
    /// 첫 몸이 쓰러지면 RejoinSeconds 를 잰다. 그 안에 다 쓰러지면 끝(Defeated). 못 다 쓰러뜨리면 쓰러진 몸이 모두
    /// 남은 몸 가운데 가장 많은 기혈로 일어나고, 시계는 다음에 처음 쓰러지는 몸부터 다시 잰다.
    /// 자리는 BossPatterns.RingOffsets(p, p.Scatter, 각도). 분열한 동안 패턴 시계(BossPatterns.Tick)는 멈춘다.</summary>
    public sealed class BossSplit
    {
        readonly BossPatternDef _def;
        readonly float[] _health;

        public BossSplit(BossPatternDef def, float remainingHealth)
        {
            if (def.Count <= 0) throw new InvalidOperationException($"{def.Id}: 분열 몸 수(count)가 0입니다");
            _def = def;
            _health = new float[def.Count];
            float each = Math.Max(0f, remainingHealth) * (def.HealthShare > 0f ? def.HealthShare : 1f / def.Count);
            for (int i = 0; i < _health.Length; i++) _health[i] = each;
        }

        public int Count => _health.Length;
        public float Health(int i) => _health[i];
        public bool Alive(int i) => _health[i] > 0f;
        public int AliveCount => _health.Count(h => h > 0f);
        /// <summary>다 쓰러졌다(땅의 주인 처치).</summary>
        public bool Defeated => AliveCount == 0;
        /// <summary>되살아나기까지 남은 시간. 시계가 안 돌면 null.</summary>
        public float? RejoinLeft { get; private set; }

        /// <summary>i 번 몸에 피해. 이번에 쓰러뜨렸으면 true. 쓰러진 몸·0 이하 피해는 무시한다.</summary>
        public bool Damage(int i, float amount)
        {
            if (amount <= 0f || _health[i] <= 0f || Defeated) return false;
            _health[i] = Math.Max(0f, _health[i] - amount);
            if (_health[i] > 0f) return false;
            if (Defeated) RejoinLeft = null;
            else if (RejoinLeft == null) RejoinLeft = _def.RejoinSeconds;
            return true;
        }

        /// <summary>시계를 민다. 이번에 되살아난 몸 번호들(없으면 빈 목록).</summary>
        public IReadOnlyList<int> Tick(float dt)
        {
            if (RejoinLeft is not float left || dt <= 0f || Defeated) return Array.Empty<int>();
            left -= dt;
            if (left > 0f) { RejoinLeft = left; return Array.Empty<int>(); }
            RejoinLeft = null;
            float best = _health.Max();
            var revived = new List<int>();
            for (int i = 0; i < _health.Length; i++)
                if (_health[i] <= 0f) { _health[i] = best; revived.Add(i); }
            return revived;
        }
    }

    /// <summary>역류 파도(sweep_wave, 마천패 2막). 웹 프로토타입 lordContinuous 'sweep_wave' 와 같은 규칙이다.
    ///   - 2막 패턴이 켜진 순간(Resolve)부터 WaveIntervalSeconds 마다 한 파(첫 파는 켜진 순간). WaveSeconds(p, k).
    ///   - 한 파는 그 순간 플레이어 자리에서 진행 방향 반대쪽으로 화면 대각선 × StartViewMultiple 떨어진 곳에서 생겨,
    ///     WarnSeconds 동안 서 있다가(붉은 물결 예고) 초당 MetersPerSecond(ProjectileSpeed, 이동 기준) 로 대각선 × 2 × StartViewMultiple 만큼 간다.
    ///   - 폭 Width 띠(진행 방향에 수직인 무한 줄)가 플레이어를 지나면 한 번 맞는다. 경공 무적이면 넘는다(부르는 쪽이 무적을 본다).
    ///   - 방향: 기획서 6장은 '하류에서 상류로'. 장강 물길 방향을 아는 쪽(Unity 지도)이 degrees 를 정하고,
    ///     모르면 RandomDegrees(웹처럼 네 방향 가운데 하나). 배 위에 있으면 안 맞는 것도 부르는 쪽이 본다(배는 장강 지형 기믹).
    /// 길이는 화면 기준 m. 세계에 놓을 때 지금 경지 축척 배율을 곱한다(viewDiagonal 도 같은 기준).</summary>
    public sealed class SweepWave
    {
        /// <summary>파가 생겨 움직이기 전 예고 시간(웹 값 0.6초, 데이터에 없음).</summary>
        public const float WarnSeconds = 0.6f;
        /// <summary>출발점이 플레이어에서 떨어진 거리 = 화면 대각선 × 이 값(웹 값 0.55). 가는 거리는 그 두 배.</summary>
        public const float StartViewMultiple = 0.55f;

        readonly BossPatternDef _def;
        readonly float _cos, _sin, _speed, _length;
        float _warn, _moved;

        /// <summary>wave 번째 파(0부터)가 패턴이 켜진 뒤 몇 초에 생기나.</summary>
        public static float WaveSeconds(BossPatternDef p, int wave) => wave * (p.WaveIntervalSeconds > 0f ? p.WaveIntervalSeconds : 6f);

        /// <summary>물길 방향을 모를 때: 0·90·180·270도 가운데 하나(웹과 같다).</summary>
        public static float RandomDegrees(Random rng) => rng.Next(4) * 90f;

        /// <summary>파 하나를 띄운다. (playerX, playerY) 는 이 순간 플레이어 자리, degrees 는 파가 나아가는 쪽, moveBase 는 이동 기준(m/초).</summary>
        public SweepWave(BossPatternDef def, float playerX, float playerY, float degrees, float viewDiagonal, float moveBase)
        {
            _def = def;
            double a = degrees * Math.PI / 180.0;
            _cos = (float)Math.Cos(a);
            _sin = (float)Math.Sin(a);
            Degrees = degrees;
            float back = viewDiagonal * StartViewMultiple;
            X = playerX - _cos * back;
            Y = playerY - _sin * back;
            _length = back * 2f;
            _speed = BossPatterns.MetersPerSecond(def.ProjectileSpeed, moveBase);
            _warn = WarnSeconds;
        }

        public float Degrees { get; }
        /// <summary>띠 가운데 줄이 지나는 지금 자리.</summary>
        public float X { get; private set; }
        public float Y { get; private set; }
        /// <summary>아직 예고 중인가(움직이지 않고 맞히지 않는다).</summary>
        public bool Warning => _warn > 0f;
        /// <summary>끝까지 갔다(지워도 된다).</summary>
        public bool Done => _moved >= _length;
        /// <summary>이 파가 이미 플레이어를 맞혔다.</summary>
        public bool HasHit { get; private set; }

        /// <summary>시간을 민다. 아직 남아 있으면 true.</summary>
        public bool Step(float dt)
        {
            if (Done || dt <= 0f) return !Done;
            if (_warn > 0f)
            {
                float w = Math.Min(_warn, dt);
                _warn -= w;
                dt -= w;
                if (dt <= 0f) return true;
            }
            float v = Math.Min(_speed * dt, _length - _moved);
            X += _cos * v;
            Y += _sin * v;
            _moved += v;
            return !Done;
        }

        /// <summary>(px, py) 가 띠 안인가. 예고 중이거나 끝났으면 false.</summary>
        public bool Covers(float px, float py, float targetRadius = 0f)
        {
            if (Warning || Done) return false;
            float along = (px - X) * _cos + (py - Y) * _sin;
            return Math.Abs(along) < _def.Width / 2f + targetRadius;
        }

        /// <summary>이번에 처음 맞혔으면 true(한 파는 한 번만 맞는다). 경공 무적·배 위는 부르는 쪽이 거르고 부른다.</summary>
        public bool TryHit(float px, float py, float targetRadius = 0f)
        {
            if (HasHit || !Covers(px, py, targetRadius)) return false;
            HasHit = true;
            return true;
        }
    }
}
