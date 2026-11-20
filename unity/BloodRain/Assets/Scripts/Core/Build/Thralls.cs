#nullable enable
// 어강술 내 편 강시(SC-06a, 2026-11-20 사람이 추천대로 — decisions #50, 기획서 20장 비전 8, synergies.json secrets eogang).
//   정신부(뇌부·부적)에 멈춘 강시(family jiangshi)를 쇄겸술 사슬로 끌어오면 seconds(30)초 동안 내 편, 최대 maxAllies(10).
//   내 편 강시는 가장 가까운 적을 제 걸음으로 쫓고, 닿은(두 몸 반경 합 안) 적에게 제 접촉 피해를 hitIntervalSeconds(0.5)마다 준다.
//   플레이어·적과는 서로 통과하고 적은 내 편을 노리지 않는다. 시간이 다 되면 구슬 없이 사라진다.
//   여기는 셈만. 그리기와 피해 넣기는 Presentation(ThrallRunner)이 한다. 길이는 세계 m, 걸음은 세계 m/s.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BloodRain.Core.Build
{
    public sealed class Thrall
    {
        public int Id;
        public Vector2 Pos;
        public float Speed;
        public float Damage;
        public float Radius;
        public float Left;
        public float HitCooldown;
        /// <summary>적 종류(그림을 고를 때).</summary>
        public string EnemyId = "";
    }

    /// <summary>가장 가까운 적: 찾았나, 그 적 번호, 자리, 반경.</summary>
    public readonly struct ThrallTarget
    {
        public readonly bool Found;
        public readonly int TargetId;
        public readonly Vector2 Pos;
        public readonly float Radius;
        public ThrallTarget(int targetId, Vector2 pos, float radius) { Found = true; TargetId = targetId; Pos = pos; Radius = radius; }
    }

    public readonly struct ThrallHit
    {
        public readonly int ThrallId;
        public readonly int TargetId;
        public readonly float Damage;
        public ThrallHit(int thrallId, int targetId, float damage) { ThrallId = thrallId; TargetId = targetId; Damage = damage; }
    }

    public sealed class ThrallBand
    {
        readonly List<Thrall> _list = new List<Thrall>();
        int _nextId = 1;

        public IReadOnlyList<Thrall> All => _list;
        public int Count => _list.Count;
        /// <summary>이번 생에 부린 수(모두).</summary>
        public int Taken { get; private set; }

        /// <summary>어강술이 켜졌고 칸이 남았으면 내 편으로 들인다. 들였으면 그 강시, 아니면 null.</summary>
        public Thrall? TryTake(SecretEffects fx, string enemyId, Vector2 pos, float speed, float damage, float radius, float hitInterval)
        {
            if (!(fx.Thrall is (float seconds, int max)) || _list.Count >= max) return null;
            var t = new Thrall { Id = _nextId++, Pos = pos, Speed = speed, Damage = damage, Radius = radius, Left = seconds, HitCooldown = 0f, EnemyId = enemyId };
            _list.Add(t);
            Taken++;
            return t;
        }

        /// <summary>한 프레임. nearest(자리)가 그 자리에서 가장 가까운 적을 돌려준다. 닿아서 때린 것들을 돌려주고, 시간이 다 된 강시는 빠진다(gone 에 번호).</summary>
        public List<ThrallHit> Tick(float dt, Func<Vector2, ThrallTarget> nearest, float hitInterval, List<int>? gone = null)
        {
            var hits = new List<ThrallHit>();
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var t = _list[i];
                t.Left -= dt;
                if (t.Left <= 0f) { gone?.Add(t.Id); _list.RemoveAt(i); continue; }
                t.HitCooldown = Math.Max(0f, t.HitCooldown - dt);
                var target = nearest(t.Pos);
                if (!target.Found) continue;
                var to = target.Pos - t.Pos;
                float d = to.Length(), reach = t.Radius + target.Radius;
                if (d > reach)
                {
                    float step = Math.Min(d - reach * 0.5f, t.Speed * dt);
                    if (d > 1e-6f) t.Pos += to / d * step;
                    d = Vector2.Distance(target.Pos, t.Pos);
                }
                if (d <= reach && t.HitCooldown <= 0f)
                {
                    hits.Add(new ThrallHit(t.Id, target.TargetId, t.Damage));
                    t.HitCooldown = hitInterval;
                }
            }
            return hits;
        }

        public void Clear() => _list.Clear();
    }
}
