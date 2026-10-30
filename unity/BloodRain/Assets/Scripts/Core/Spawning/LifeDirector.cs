// 한 생의 적 생성(기획서 7장 '한 생의 흐름', 11장 '화면 위 적 수 목표'·'생성 규칙').
// 매초 한 번 Second 를 부르면 이번 1초에 만들 적(종류·태어난 경지·덩이 사람 수)과 그 사이 일어난 사건을 돌려준다.
// 종류는 한 생 가중치표(chapters.json life)와 서 있는 지역의 고유 적, 덩이 크기는 그 순간 경지(cheonha.json realms clusterSize).
// 땅의 주인과 싸우는 동안 목표는 0.4배(bosses.spawnTargetScale), 돌파 연출 1.4초(breakthrough.spawnHoldSeconds)와
// 각성 파동 뒤 2.5초(balance.json awakening.spawnPauseSeconds)는 만들지 않는다.
// 현경 뒤 봉인석이 모자라면 30분부터 혈마의 그림자가 나오고 5분마다 한 단계씩 강해진다(lifeEnd). 단계 하나가 얼마나 센지는 T-14(혈마)에서 정한다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;
using BloodRain.Core.World;

namespace BloodRain.Core.Spawning
{
    /// <summary>만들 적 하나(덩이 하나).</summary>
    public readonly struct LifeSpawn
    {
        public readonly string Enemy;
        /// <summary>태어난 경지 순번(밟힘·덩이 판정에 쓴다).</summary>
        public readonly int BornRealm;
        /// <summary>덩이 속 사람 수.</summary>
        public readonly int Members;

        public LifeSpawn(string enemy, int bornRealm, int members) { Enemy = enemy; BornRealm = bornRealm; Members = members; }
        public override string ToString() => $"{Enemy}@{BornRealm}x{Members}";
    }

    /// <summary>한 생 사건(정예·무리·보부상 등)을 서 있는 지역으로 풀어 놓은 것.</summary>
    public readonly struct LifeEvent
    {
        public readonly WaveEvent Wave;
        /// <summary>그 지역에서 실제로 나올 적(지역 정예·고유 적으로 바뀐 id).</summary>
        public readonly string Enemy;
        public readonly int BornRealm;
        public readonly int Members;

        public LifeEvent(WaveEvent wave, string enemy, int bornRealm, int members) { Wave = wave; Enemy = enemy; BornRealm = bornRealm; Members = members; }
    }

    public sealed class LifeSecond
    {
        public List<LifeSpawn> Spawns { get; } = new List<LifeSpawn>();
        public List<LifeEvent> Events { get; } = new List<LifeEvent>();
        /// <summary>이번 초의 화면 위 적 수 목표(보스전 배율 적용 뒤).</summary>
        public float Target { get; internal set; }
        /// <summary>생성이 멈춰 있었나(돌파·각성).</summary>
        public bool Held { get; internal set; }
    }

    public sealed class LifeDirector
    {
        readonly WaveTable _table;
        readonly RegionSpawns _regions;
        readonly Scale _scale;
        readonly SpawnCurve _curve;
        readonly WaveSchedule _schedule;
        readonly CheonhaDef _c;
        readonly float _fraction;
        readonly Random _rng;
        float _hold;
        float _lastMinute = -1f;

        public LifeDirector(GameData data, Random rng)
        {
            _rng = rng;
            _c = data.Cheonha;
            _table = WaveTable.From(data);
            _regions = new RegionSpawns(data, WaveTable.Life);
            _scale = new Scale(data);
            _curve = _table.Curve(WaveTable.Life, data.Balance.SpawnTarget);
            _schedule = _table.Schedule(WaveTable.Life, rng);
            _fraction = data.Balance.SpawnTarget.SpawnFractionPerSecond;
        }

        public WaveSchedule Schedule => _schedule;
        public Scale Scale => _scale;
        /// <summary>땅의 주인과 싸우는 중(목표 0.4배).</summary>
        public bool BossFight { get; set; }
        /// <summary>남은 생성 멈춤(초, 세상 시간).</summary>
        public float HoldRemaining => _hold;

        /// <summary>돌파 연출이 시작됐다. spawnHoldSeconds 동안 만들지 않는다.</summary>
        public void OnBreakthrough() => Hold(_c.Breakthrough.SpawnHoldSeconds);

        /// <summary>생성을 이만큼 멈춘다(각성 파동 등). 이미 더 길게 멈춰 있으면 그대로.</summary>
        public void Hold(float seconds) => _hold = Math.Max(_hold, seconds);

        /// <summary>세상 시간이 흐른다(멈춤만 줄어든다).</summary>
        public void Tick(float dt) => _hold = Math.Max(0f, _hold - dt);

        public float TargetAt(float minute) => _curve.TargetAt(minute) * (BossFight ? _c.Bosses.SpawnTargetScale : 1f);

        /// <summary>이번 1초. level 은 플레이어 레벨(덩이 크기를 정하는 경지), regionId 는 서 있는 지역.</summary>
        public LifeSecond Second(float minute, int level, string regionId, int aliveNow)
        {
            var r = new LifeSecond();
            int realm = _scale.RealmIndex(level);
            int members = _scale.ClusterSize(realm);

            // 사건은 멈춤과 상관없이 시각이 지나면 한 번씩 나온다(멈춤이 끝난 뒤에 몰아서 나오지 않게 지금 낸다).
            foreach (var e in _schedule.Between(_lastMinute, minute))
                r.Events.Add(new LifeEvent(e, _regions.EnemyFor(e, regionId, _rng), realm, members));
            _lastMinute = minute;

            float target = TargetAt(minute);
            r.Target = target;
            if (_hold > 0f) { r.Held = true; return r; }

            float deficit = target - aliveNow;
            if (deficit <= 0) return r;
            int n = (int)Math.Ceiling(deficit * _fraction - 1e-4f);
            var local = _regions.LocalEnemies(regionId);
            for (int i = 0; i < n; i++)
                r.Spawns.Add(new LifeSpawn(_table.PickEnemy(WaveTable.Life, minute, local, _rng), realm, members));
            return r;
        }

        /// <summary>혈마의 그림자 단계. 0이면 없음. 현경이고 봉인석이 모자랄 때 shadowFromMinute 에 1, 그 뒤 shadowStepMinutes 마다 +1.</summary>
        public int ShadowStep(float minute, int level, int sealStones) => ShadowStep(_c, _scale, minute, level, sealStones);

        public static int ShadowStep(CheonhaDef c, Scale scale, float minute, int level, int sealStones)
        {
            var end = c.LifeEnd;
            int top = scale.RealmIndex(c.Bosses.FinalRequires.Realm);
            if (scale.RealmIndex(level) < top) return 0;
            if (sealStones >= c.Bosses.FinalRequires.SealStones) return 0;
            if (minute < end.ShadowFromMinute) return 0;
            if (end.ShadowStepMinutes <= 0) return 1;
            return 1 + (int)Math.Floor((minute - end.ShadowFromMinute) / end.ShadowStepMinutes + 1e-4f);
        }
    }
}
