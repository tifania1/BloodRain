#nullable enable
// 서장(청운진 객잔 마당 10분)의 적 생성. 한 생의 LifeDirector 와 같은 모양으로 매초 Second 를 부른다(기획서 4장 '서장 상세 흐름', 21장).
//   종류: chapters.json prologue 가중치표. 서장은 지역 고유 적이 없다.
//   수: balance.json 화면 위 적 수 곡선, 8:30 ~ 9:00(장삼 경고) 사이는 만들지 않는다(prologue stopSpawningAtMinute ~ boss).
//   사건: 3:30 혈인 돌진, 5:00 정예 강시 도부수, 9:00 장삼. 시각이 지나면 한 번씩 낸다(멈춤 중에도).
//   덩이: '천하 규칙을 그대로 쓴다'(21장)에 따라 한 생과 같이 그 순간 경지(cheonha.json realms clusterSize)로 정한다.
//   멈춤: 돌파 연출(breakthrough.spawnHoldSeconds), 각성 파동 뒤(balance.json awakening.spawnPauseSeconds)는 Hold 로.
// 장삼전 동안 잡몹을 얼마나 줄일지는 아직 정하지 않았다(P-02 '보스전 잡몹 수'). 정해지기 전엔 TargetScale 기본 1(곡선 그대로).
using System;
using BloodRain.Core.Data;
using BloodRain.Core.World;

namespace BloodRain.Core.Spawning
{
    public sealed class PrologueDirector
    {
        readonly WaveTable _table;
        readonly Scale _scale;
        readonly SpawnCurve _curve;
        readonly WaveSchedule _schedule;
        readonly CheonhaDef _c;
        readonly float _fraction;
        readonly Random _rng;
        float _hold;
        float _lastMinute = -1f;

        public PrologueDirector(GameData data, Random rng)
        {
            _rng = rng;
            _c = data.Cheonha;
            _table = WaveTable.From(data);
            _scale = new Scale(data);
            _curve = _table.Curve(WaveTable.Prologue, data.Balance.SpawnTarget);
            _schedule = _table.Schedule(WaveTable.Prologue, rng);
            _fraction = data.Balance.SpawnTarget.SpawnFractionPerSecond;
        }

        public WaveSchedule Schedule => _schedule;
        public Scale Scale => _scale;
        /// <summary>목표 적 수에 곱하는 배율. 장삼전 잡몹 수(P-02)가 정해지면 Unity 쪽이 장삼전 동안 바꾼다.</summary>
        public float TargetScale { get; set; } = 1f;
        public float HoldRemaining => _hold;

        /// <summary>돌파 연출이 시작됐다(서장은 다시 오른 경지가 없다).</summary>
        public void OnBreakthrough() => Hold(_c.Breakthrough.SpawnHoldSeconds);

        /// <summary>생성을 이만큼 멈춘다(각성 파동 등). 이미 더 길게 멈춰 있으면 그대로.</summary>
        public void Hold(float seconds) => _hold = Math.Max(_hold, seconds);

        public void Tick(float dt) => _hold = Math.Max(0f, _hold - dt);

        /// <summary>8:30~9:00 생성 중지 구간이면 0.</summary>
        public float TargetAt(float minute) => _curve.SpawningStopped(minute) ? 0f : _curve.TargetAt(minute) * TargetScale;

        /// <summary>이번 1초. level 은 진소운의 레벨(덩이 크기를 정하는 경지).</summary>
        public LifeSecond Second(float minute, int level, int aliveNow)
        {
            var r = new LifeSecond();
            int realm = _scale.RealmIndex(level);
            int members = _scale.ClusterSize(realm);

            foreach (var e in _schedule.Between(_lastMinute, minute))
                r.Events.Add(new LifeEvent(e, e.Enemy, realm, members));
            _lastMinute = minute;

            float target = TargetAt(minute);
            r.Target = target;
            if (_hold > 0f) { r.Held = true; return r; }

            float deficit = target - aliveNow;
            if (deficit <= 0) return r;
            int n = (int)Math.Ceiling(deficit * _fraction - 1e-4f);
            for (int i = 0; i < n; i++)
                r.Spawns.Add(new LifeSpawn(_table.PickEnemy(WaveTable.Prologue, minute, _rng), realm, members));
            return r;
        }
    }
}
