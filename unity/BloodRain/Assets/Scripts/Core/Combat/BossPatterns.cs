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

        public float ResolveAt => TelegraphAt + Pattern.TelegraphSeconds;
    }

    public sealed class BossPatterns
    {
        readonly BossPatternSetDef _def;
        readonly BossPatternDef[] _order;
        readonly bool[] _phase2Resolved;
        readonly List<BossPatternCast> _pending = new List<BossPatternCast>();
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
        /// <summary>보스가 멈춰 서 있어야 하는가(금강장 예고처럼 BossHolds 인 패턴을 예고하는 중).</summary>
        public bool Holding => _pending.Any(c => c.Pattern.BossHolds);

        /// <summary>남은 기혈 비율로 2막인지. 문턱과 같으면 2막이다.</summary>
        public static bool IsPhase2(BossPatternSetDef def, float healthRatio) => healthRatio <= def.Phase2At;

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
        public static bool InBeam(BossPatternDef p, float dx, float dy, float beamDegrees, float targetRadius = 0f)
        {
            double a = beamDegrees * Math.PI / 180.0;
            float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            float along = dx * ca + dy * sa;
            float perp = Math.Abs(-dx * sa + dy * ca);
            return along >= 0f && along <= p.Length + targetRadius && perp <= p.Width * 0.5f + targetRadius;
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
        public static (float x, float y)[] RingOffsets(BossPatternDef p)
        {
            var list = new (float x, float y)[p.Count];
            for (int i = 0; i < p.Count; i++)
            {
                double a = 2.0 * Math.PI * i / p.Count;
                list[i] = ((float)(Math.Cos(a) * p.Radius), (float)(Math.Sin(a) * p.Radius));
            }
            return list;
        }

        /// <summary>플레이어 둘레 가로·세로 ±Scatter 안에서 Count 곳을 고른다(범종 낙하).</summary>
        public static (float x, float y)[] ScatterOffsets(BossPatternDef p, Random rng)
        {
            var list = new (float x, float y)[p.Count];
            for (int i = 0; i < p.Count; i++)
                list[i] = ((float)(rng.NextDouble() * 2 - 1) * p.Scatter, (float)(rng.NextDouble() * 2 - 1) * p.Scatter);
            return list;
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
}
