#nullable enable
// 보스 연전(기획서 7장 '모드', WP-38 — 2026-11-20 추천대로, decisions #39, cheonha.json bossRush). 봉인 엔딩 뒤 열린다(Meta/Modes).
//   고른 몸으로 현경·시작 무공 Lv8 에서 시작해 땅의 주인 일곱을 데이터 차례(원각 → 혁련휘)로 연달아.
//   하나 쓰러뜨릴 때마다 레벨업 세 번·기혈 가득, 기혈은 기준 체력 × healthCurve(20분) = 기준 체력 그대로.
//   혼엔 보상을 안 남기고 다 깬 시간만(Soul.RecordBossRush — 가장 빠른 것). 판은 일일 도전처럼 혼 복사본(DailyChallenge.Sandbox)으로 돌린다.
//   싸움 자체(패턴·장판)는 천하 한 생과 같은 BossAwakening·PatternRunner 를 쓴다. 이 클래스는 차례·시계·처치 보상만 센다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.World;

namespace BloodRain.Core.Meta
{
    public sealed class BossRush
    {
        public const string ModeId = "boss_rush";

        readonly GameData _data;
        readonly IReadOnlyList<RegionDef> _order;

        /// <summary>싸울 차례: 땅의 주인이 있는 지역(봉마곡 빼고) 데이터 차례.</summary>
        public static IReadOnlyList<RegionDef> Order(CheonhaDef c) => c.BossRegions.ToList();

        /// <summary>그 땅 주인의 보스 연전 기혈: 기준 체력 × healthCurve(healthMinute 분).</summary>
        public static float Health(GameData data, string region)
        {
            var c = data.Cheonha;
            var boss = data.Bosses.First(b => b.Id == c.Region(region).Boss);
            return boss.Health * BossAwakening.HealthMultiplier(c.Bosses, c.BossRush.HealthMinute);
        }

        public BossRush(GameData data)
        {
            _data = data;
            _order = Order(data.Cheonha);
            if (_order.Count == 0) throw new InvalidOperationException("땅의 주인이 있는 지역이 없습니다");
        }

        /// <summary>지금 몇 번째(0부터). 다 깨면 Count.</summary>
        public int Index { get; private set; }
        public int Count => _order.Count;
        public bool Finished => Index >= _order.Count;
        /// <summary>지금 싸울 땅(다 깼으면 null).</summary>
        public RegionDef? Current => Finished ? null : _order[Index];
        /// <summary>지금 주인의 기혈(다 깼으면 0).</summary>
        public float CurrentHealth => Current == null ? 0f : Health(_data, Current.Id);
        /// <summary>시작부터 걸린 시간(초). 레벨업 화면처럼 멈춘 동안은 부른 쪽이 Tick 을 안 부른다.</summary>
        public float Seconds { get; private set; }

        public void Tick(float dt)
        {
            if (Finished || dt <= 0) return;
            Seconds += dt;
        }

        /// <summary>지금 주인을 쓰러뜨렸다. 줄 레벨업 수(마지막이면 0 — 바로 끝). 기혈을 가득 채우는 것은 부른 쪽.</summary>
        public int Defeat()
        {
            if (Finished) throw new InvalidOperationException("보스 연전이 이미 끝났습니다");
            Index++;
            return Finished ? 0 : _data.Cheonha.BossRush.LevelUpsPerKill;
        }

        /// <summary>다 깬 시간(초, 버림). 끝나지 않았으면 null.</summary>
        public int? ClearSeconds => Finished ? (int)Math.Floor(Seconds) : (int?)null;
    }
}
