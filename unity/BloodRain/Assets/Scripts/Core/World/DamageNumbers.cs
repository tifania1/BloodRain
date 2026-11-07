#nullable enable
// 피해 숫자(기획서 12장 설정 '피해 숫자 표시', 11장 '동시 피해 숫자 표시 80', 15장 풀링, backlog DN-01).
//   어느 맞음에 숫자를 띄울지(ShouldShow), 떠 있는 숫자 목록(상한을 넘으면 가장 오래된 것부터 버림, 수명이 다하면 지움).
//   그리는 일(글꼴·색·떠오르는 높이)과 설정에서 끈 경우 안 그리는 일은 Unity 가 한다. 끄더라도 목록은 그대로 돈다(켜면 바로 보이게).
//   값은 balance.json damageNumber·limits.damageNumbers. 웹 프로토타입 addNum 과 같은 셈 — 다만 웹은 상한 + 1 개까지 두던 것을 상한까지로.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public struct DamageNumber
    {
        /// <summary>적 자리(가로는 흔든 뒤).</summary>
        public float X, Y;
        /// <summary>적 자리에서 '위로' 띄울 거리(m). 위가 어느 부호인지는 그리는 쪽 좌표계(웹은 y 가 아래로, Unity 는 위로).</summary>
        public float Lift;
        public int Value;
        public bool Crit;
        public float Age;
    }

    public sealed class DamageNumbers
    {
        readonly DamageNumberDef _d;
        readonly int _limit;
        readonly List<DamageNumber> _items = new List<DamageNumber>();

        public DamageNumbers(BalanceDef b) : this(b.DamageNumber, b.Limits.DamageNumbers) { }
        public DamageNumbers(DamageNumberDef d, int limit)
        {
            _d = d;
            _limit = Math.Max(1, limit);
        }

        /// <summary>떠 있는 숫자(오래된 것부터).</summary>
        public IReadOnlyList<DamageNumber> Items => _items;
        public float LifeSeconds => _d.LifeSeconds;

        /// <summary>이 맞음에 숫자를 띄울지. roll 은 [0,1) 난수 하나(치명타면 안 씀).</summary>
        public static bool ShouldShow(DamageNumberDef d, float damage, bool crit, bool eliteOrBoss, double roll)
        {
            if (crit) return true;
            if (damage < d.MinDamage) return false;
            return roll < (eliteOrBoss ? d.ChanceEliteBoss : d.Chance);
        }

        public bool ShouldShow(float damage, bool crit, bool eliteOrBoss, double roll) => ShouldShow(_d, damage, crit, eliteOrBoss, roll);

        /// <summary>적 자리(x, y)·몸 반지름으로 숫자를 띄운다. jitterRoll 은 [0,1) 난수(가로 흔듦). 상한이면 가장 오래된 것을 버린다.</summary>
        public DamageNumber Add(float enemyX, float enemyY, float enemyRadius, float damage, bool crit, double jitterRoll)
        {
            var n = new DamageNumber
            {
                X = enemyX + (float)((jitterRoll * 2 - 1) * _d.JitterMeters),
                Y = enemyY,
                Lift = enemyRadius * _d.HeadOffsetRadii,
                Value = (int)Math.Round(damage, MidpointRounding.AwayFromZero),
                Crit = crit,
            };
            while (_items.Count >= _limit) _items.RemoveAt(0);
            _items.Add(n);
            return n;
        }

        /// <summary>시간을 흘리고 수명이 다한 숫자를 지운다.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < _items.Count; i++) { var n = _items[i]; n.Age += dt; _items[i] = n; }
            _items.RemoveAll(n => n.Age >= _d.LifeSeconds);
        }

        /// <summary>0~1, 갓 뜬 숫자가 1 이고 수명 끝에 0.</summary>
        public float Alpha(in DamageNumber n) => Math.Max(0f, 1f - n.Age / _d.LifeSeconds);

        public void Clear() => _items.Clear();
    }
}
