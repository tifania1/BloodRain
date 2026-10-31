using System;
using System.Collections.Generic;
using UnityEngine;

namespace BloodRain.Sim
{
    /// <summary>바닥에 떨어진 진기 구슬 하나. 위치는 세계 m.</summary>
    public struct Orb
    {
        public Vector2 Pos;
        public double Value;
        public float Age;
        public bool Pulled;   // 한 번 끌리기 시작하면 끝까지 따라온다
        public bool Big;      // 합쳐진 큰 구슬
    }

    // 진기 구슬(C-05). 웹 프로토타입 dropOrb·구슬 흡수와 같은 규칙:
    //   자석 반경(stats.magnetBase, 경지 m) 안에 들면 끌리기 시작하고, 끌리는 속도는 9 + 4 × 나이(경지 m/s).
    //   플레이어 0.5m(경지 m) 안이면 흡수. 바닥 구슬이 limits.orbs(400)를 넘으면 오래된 100개를 하나로 합쳐 플레이어 둘레 5m 에 둔다.
    // 길이는 넣을 때 WorldScale 로 바꾼 세계 m 를 받는다(Step 인자). 400개라 잡 없이 메인 스레드로 충분하다.
    public sealed class OrbField
    {
        public const int MergeBatch = 100;

        readonly List<Orb> _orbs = new List<Orb>();
        readonly System.Random _rng;

        public int Limit;
        public IReadOnlyList<Orb> Orbs => _orbs;
        public int Count => _orbs.Count;
        /// <summary>지금까지 합친 횟수(시험·디버그용).</summary>
        public int Merges { get; private set; }

        public OrbField(int limit = 400, int seed = 1)
        {
            Limit = limit;
            _rng = new System.Random(seed);
        }

        /// <summary>구슬 하나를 떨어뜨린다. 넘치면 오래된 MergeBatch 개를 합쳐 player 둘레 mergeDistance(세계 m)에 큰 구슬 하나로.</summary>
        public void Drop(Vector2 pos, double value, Vector2 player, float mergeDistance)
        {
            _orbs.Add(new Orb { Pos = pos, Value = value });
            if (_orbs.Count <= Limit) return;
            int n = Math.Min(MergeBatch, _orbs.Count);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += _orbs[i].Value;
            _orbs.RemoveRange(0, n);
            float a = (float)(_rng.NextDouble() * Math.PI * 2);
            _orbs.Add(new Orb { Pos = player + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * mergeDistance, Value = sum, Big = true });
            Merges++;
        }

        /// <summary>한 프레임. 길이·속도는 모두 세계 단위. 흡수한 진기 합과 구슬 수를 돌려준다.</summary>
        public (double value, int count) Step(float dt, Vector2 player, float magnet, float absorb, float pullBase, float pullPerSecond)
        {
            double gained = 0;
            int taken = 0;
            float m2 = magnet * magnet, a2 = absorb * absorb;
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                var o = _orbs[i];
                o.Age += dt;
                var to = player - o.Pos;
                float d2 = to.sqrMagnitude;
                if (o.Pulled || d2 < m2)
                {
                    o.Pulled = true;
                    float d = Mathf.Sqrt(d2);
                    float sp = pullBase + o.Age * pullPerSecond;
                    if (d > 1e-5f) o.Pos += to / d * Mathf.Min(d, sp * dt);
                    d2 = (player - o.Pos).sqrMagnitude;
                }
                if (d2 < a2)
                {
                    gained += o.Value;
                    taken++;
                    _orbs.RemoveAt(i);
                    continue;
                }
                _orbs[i] = o;
            }
            return (gained, taken);
        }

        /// <summary>바닥 구슬을 모두 끌어온다(취기부).</summary>
        public void PullAll()
        {
            for (int i = 0; i < _orbs.Count; i++) { var o = _orbs[i]; o.Pulled = true; _orbs[i] = o; }
        }

        public void Clear() => _orbs.Clear();
    }
}
