#nullable enable
// 경지 돌파 연출(기획서 7장 '돌파 연출' 표). 숫자는 cheonha.json breakthrough·crush, 축척 계산은 World.Scale.
// 언제 돌파하는지는 Progression.RunXp(AbsorbResult.Breakthroughs)가 정한다. 여기서는 그 뒤 1.4초를 맡는다.
//
//   0초       Stamp      세상 시간 0.05배, 흑백 심상. 새 경지 글자가 낙관처럼 찍힌다.
//   0.2초     ZoomStart  화면이 새 축척으로 물러나기 시작한다(천천히 출발해 천천히 멈춤). 혈우 장막이 밖으로 밀려난다.
//   0.8초     Sweep      화면 대각선 0.6배 안의 아래 경지 적이 한꺼번에 쓸려 나간다(밟힘, 진기 0.3).
//   1.4초     End        세상 시간 1로. 깨달음 3택1, 절기 그릇이 늘어나는 돌파면 각성 판정(Build.Loadout.AwakenOnBreakthrough).
//   0~1.4초   적 생성 멈춤(SpawnHeld).
//
// 세상 시간 0.05배는 표의 첫 줄에 적힌 대로 처음부터 걸고, 물러나는 동안에도 그대로 둔다(줌 도중 적이 움직이면 쓸림 반경이 흐트러진다).
//
// Unity 쪽 흐름:
//   1. RunXp.Absorb 결과에 돌파가 있으면 Start(흡수 전 레벨, 흡수 뒤 레벨). 레벨업 창은 End 뒤로 미룬다.
//   2. 매 프레임 Tick(Time.unscaledDeltaTime) → Time.timeScale = WorldTimeScale, 카메라는 ViewDiagonal(세계 m).
//   3. Sweep 신호가 오면 화면 안 적을 ScaledFoe 목록으로 만들어 SelectSweep(목록) 결과를 밟힘으로 처리한다.
//   4. 생성 디렉터는 SpawnHeld 동안 적을 만들지 않는다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>돌파 연출 장면 신호. Tick 한 번에 여럿이 올 수 있어서 묶음으로 돌려준다.</summary>
    [Flags]
    public enum BreakthroughCue
    {
        None = 0,
        /// <summary>새 경지 글자 낙관, 흑백 심상.</summary>
        Stamp = 1,
        /// <summary>새 축척으로 물러나기 시작.</summary>
        ZoomStart = 2,
        /// <summary>쓸림. 이 신호에 SelectSweep 을 적용한다.</summary>
        Sweep = 4,
        /// <summary>물러나기가 끝났다. 깨달음 3택1과 각성 판정을 연다.</summary>
        End = 8,
    }

    public sealed class Breakthrough
    {
        readonly Scale _scale;
        readonly BreakthroughDef _def;
        readonly CrushDef _crush;
        float _t;
        float _speed = 1f;
        bool _started;
        BreakthroughCue _fired;
        float _fromExponent;
        float _toExponent;

        /// <summary>장면 신호를 시간 순서대로 하나씩 알린다(Tick 의 반환값과 같은 내용).</summary>
        public event Action<BreakthroughCue>? Cue;

        public Breakthrough(GameData data) : this(new Scale(data), data.Cheonha.Breakthrough, data.Cheonha.Crush) { }

        public Breakthrough(Scale scale, BreakthroughDef def, CrushDef crush)
        {
            _scale = scale;
            _def = def;
            _crush = crush;
        }

        /// <summary>연출이 진행 중인가(0초부터 End 까지).</summary>
        public bool IsActive { get; private set; }
        /// <summary>연출 시작부터 흐른 실제 시간.</summary>
        public float Elapsed => _t;
        public float FromLevel { get; private set; }
        public float ToLevel { get; private set; }
        /// <summary>돌파 전 경지 순번.</summary>
        public int FromRealm { get; private set; }
        /// <summary>새 경지 순번. 쓸림은 이보다 아래에서 생긴 적이 대상이다.</summary>
        public int ToRealm { get; private set; }
        public bool SweepDone => (_fired & BreakthroughCue.Sweep) != 0;

        /// <summary>연출이 끝나는 시각(물러나기가 끝나는 때).</summary>
        public float DurationSeconds => _def.ZoomToSeconds / _speed;
        /// <summary>다시 오른 경지라 줄인 연출인가.</summary>
        public bool IsRevisit { get; private set; }

        /// <summary>돌파 연출을 시작한다. 한 번에 여러 경지를 넘었으면 마지막 경지까지 한 번에 물러난다.</summary>
        /// <param name="fromLevel">흡수 전 레벨(소수면 진기 비율까지).</param>
        /// <param name="toLevel">흡수 뒤 레벨.</param>
        /// <param name="revisit">이미 올라 본 경지면 true: 같은 순서를 revisitSeconds 로 줄여 보여 준다(연출 시계가 그만큼 빨리 간다).</param>
        public void Start(float fromLevel, float toLevel, bool revisit = false)
        {
            _speed = revisit && _def.RevisitSeconds > 0f ? _def.ZoomToSeconds / _def.RevisitSeconds : 1f;
            IsRevisit = revisit && _speed != 1f;
            if (IsActive) throw new InvalidOperationException("돌파 연출이 이미 진행 중입니다");
            FromLevel = fromLevel;
            ToLevel = toLevel;
            FromRealm = _scale.RealmIndex((int)Math.Floor(fromLevel));
            ToRealm = _scale.RealmIndex((int)Math.Floor(toLevel));
            _fromExponent = _scale.ViewExponent(fromLevel);
            _toExponent = _scale.ViewExponent(toLevel);
            _t = 0f;
            _fired = BreakthroughCue.None;
            _started = true;
            IsActive = true;
        }

        /// <summary>연출 시계를 실제 시간으로 민다. 이번에 지나간 장면 신호를 돌려준다.</summary>
        public BreakthroughCue Tick(float unscaledDt)
        {
            if (!_started) return BreakthroughCue.None;
            if (unscaledDt > 0f) _t += unscaledDt * _speed;
            if (!IsActive) return BreakthroughCue.None;

            var cues = Pass(BreakthroughCue.Stamp, 0f)
                     | Pass(BreakthroughCue.ZoomStart, _def.ZoomFromSeconds)
                     | Pass(BreakthroughCue.Sweep, _crush.SweepAtSeconds)
                     | Pass(BreakthroughCue.End, _def.ZoomToSeconds);
            if ((cues & BreakthroughCue.End) != 0) IsActive = false;

            if (Cue != null && cues != BreakthroughCue.None)
                foreach (var c in CueOrder)
                    if ((cues & c) != 0) Cue(c);
            return cues;
        }

        static readonly BreakthroughCue[] CueOrder =
        {
            BreakthroughCue.Stamp, BreakthroughCue.ZoomStart, BreakthroughCue.Sweep, BreakthroughCue.End,
        };

        BreakthroughCue Pass(BreakthroughCue cue, float at)
        {
            if ((_fired & cue) != 0 || _t < at) return BreakthroughCue.None;
            _fired |= cue;
            return cue;
        }

        /// <summary>세상 시간 배율. 연출 동안 0.05, 끝나면 1.</summary>
        public float WorldTimeScale => IsActive ? _def.TimeScale : 1f;

        /// <summary>적 생성을 멈추는가. 연출 시작부터 1.4초(실제 시간).</summary>
        public bool SpawnHeld => _started && _t < _def.SpawnHoldSeconds;

        /// <summary>물러나기 진행 0~1(천천히 출발해 천천히 멈춤). t는 연출 시작부터 실제 시간.</summary>
        public float ZoomProgressAt(float t)
        {
            float len = _def.ZoomToSeconds - _def.ZoomFromSeconds;
            float k = len <= 0f ? (t >= _def.ZoomToSeconds ? 1f : 0f) : (t - _def.ZoomFromSeconds) / len;
            k = k < 0f ? 0f : k > 1f ? 1f : k;
            return k * k * (3f - 2f * k);
        }

        public float ZoomProgress => ZoomProgressAt(_t);

        /// <summary>t초의 화면 대각선(세계 m). 축척 지수를 섞으므로 배율로는 고르게 물러난다.</summary>
        public float ViewDiagonalAt(float t) =>
            _scale.DiagonalAtExponent(_fromExponent + (_toExponent - _fromExponent) * ZoomProgressAt(t));

        /// <summary>지금 화면 대각선(세계 m). 연출이 끝나면 새 경지의 대각선.</summary>
        public float ViewDiagonal => ViewDiagonalAt(_t);

        /// <summary>돌파 전·뒤 혈우 장막 반경(세계 m). 장막이 없는 경지면 null. 그 사이를 그리는 일은 ZoomProgress 로.</summary>
        public float? FromBoundaryRadius => _scale.BoundaryRadius(FromRealm);
        public float? ToBoundaryRadius => _scale.BoundaryRadius(ToRealm);

        /// <summary>쓸림 반경(세계 m) = 쓸리는 순간(0.8초)의 화면 대각선 × 0.6.</summary>
        public float SweepRadius => ViewDiagonalAt(_crush.SweepAtSeconds) * _crush.SweepViewMultiple;

        /// <summary>쓸림 대상: 반경 안, 새 경지보다 아래에서 생긴 적(땅의 주인 빼고). 넘긴 목록의 순번.</summary>
        public List<int> SelectSweep(IReadOnlyList<ScaledFoe> foes) => _scale.SelectSweep(foes, ToRealm, SweepRadius);
    }
}
