using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BloodRain.Sim
{
    /// <summary>적 움직임 갈래. enemies.json behavior 를 여기 넷으로 줄인다(나머지 갈래는 C-04 뒤에 하나씩).</summary>
    public enum EnemyMove : byte
    {
        Chase = 0,   // 곧장 쫓기
        Hop = 1,     // hop_chase: 강시 콩콩. 0.6초 박자 중 72% 동안만 뛴다(평균 속도는 데이터 그대로)
        Flee = 2,    // flee: 금두꺼비
        Line = 3,    // line_charge: 처음 방향으로 곧장. 14초 뒤 사라짐
    }

    /// <summary>적 한 덩이의 상태. 위치·속도·반경은 세계 m(WorldScale 로 바꾼 값).</summary>
    public struct EnemyAgent
    {
        public float2 Pos;
        public float2 Knock;      // 넉백 속도. 초당 0.02배로 줄어든다(웹과 같다)
        public float2 LineDir;
        public float Speed;       // 세계 m/s(혈월 배율까지 곱한 값)
        public float Radius;      // 세계 m
        public float Weight;      // 밀어내기에서 밀리는 정도(정예·보스 0.1)
        public float Hop;         // 콩콩 박자 0~1
        public float Health;
        public float MaxHealth;
        public float Life;        // 생긴 뒤 흐른 시간
        public int Kind;          // 적 종류 순번(EnemyField 의 표)
        public int Id;            // 풀에서 꺼낼 때마다 새 번호
        public byte Move;
        public byte Dead;         // 1 이면 Step 끝에 걷어 낸다(Line 시간 초과 등)
    }

    // 적 시뮬레이션(C-02). 구조체 배열(NativeArray) 하나에 살아 있는 적만 앞쪽에 빽빽이 두고, 죽으면 맨 뒤 것을 그 자리로 옮긴다(풀).
    // 한 프레임: 움직임 잡(추적·콩콩·넉백·멀어진 적 되돌리기) → 격자 해시 잡 → 밀어내기 잡. 모두 Burst, 병렬.
    // 숫자(속도·기혈)는 넣는 쪽(EnemyField)이 데이터·Core 에서 읽어 세계 단위로 바꿔 넣는다.
    public sealed class EnemySim : IDisposable
    {
        public const float HopPeriod = 0.6f;
        public const float HopDuty = 0.72f;
        public const float LineLifeSeconds = 14f;
        const int MaxNeighbors = 12;

        NativeArray<EnemyAgent> _a;
        NativeArray<EnemyAgent> _b;
        NativeParallelMultiHashMap<int, int> _grid;
        int _count;
        int _nextId = 1;

        public int Count => _count;
        public int Capacity => _a.Length;
        public NativeArray<EnemyAgent> Agents => _a;

        /// <summary>Step 에서 걷어 낸 적의 Id(그리는 쪽이 정리하라고). 다음 Step 전까지 유효.</summary>
        public readonly System.Collections.Generic.List<int> Removed = new System.Collections.Generic.List<int>();

        public EnemySim(int capacity)
        {
            _a = new NativeArray<EnemyAgent>(capacity, Allocator.Persistent);
            _b = new NativeArray<EnemyAgent>(capacity, Allocator.Persistent);
            _grid = new NativeParallelMultiHashMap<int, int>(capacity, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (_a.IsCreated) _a.Dispose();
            if (_b.IsCreated) _b.Dispose();
            if (_grid.IsCreated) _grid.Dispose();
        }

        /// <summary>풀에서 한 자리를 꺼내 넣는다. 꽉 찼으면 -1. 돌려주는 값은 Id.</summary>
        public int Spawn(EnemyAgent a)
        {
            if (_count >= _a.Length) return -1;
            a.Id = _nextId++;
            a.Dead = 0;
            a.Hop = math.frac(a.Hop);
            _a[_count++] = a;
            return a.Id;
        }

        public EnemyAgent Get(int index) => _a[index];
        public void Set(int index, EnemyAgent a) => _a[index] = a;

        /// <summary>index 자리 적을 걷어 낸다(맨 뒤 적이 그 자리로 온다).</summary>
        public void RemoveAt(int index)
        {
            Removed.Add(_a[index].Id);
            _count--;
            if (index != _count) _a[index] = _a[_count];
        }

        public void Clear()
        {
            for (int i = 0; i < _count; i++) Removed.Add(_a[i].Id);
            _count = 0;
        }

        /// <summary>한 프레임 진행. player 는 세계 m. cell 은 격자 칸 크기(세계 m, 보통 적 지름의 2배 남짓).
        /// recycleBeyond 를 넘게 멀어진 적은 플레이어 둘레 recycleTo 거리, 플레이어가 가는 쪽으로 옮긴다.</summary>
        public void Step(float dt, float2 player, float2 playerHeading, float cell, float recycleBeyond, float recycleTo, float speedScale = 1f)
        {
            Removed.Clear();
            if (_count == 0 || dt <= 0f) return;

            var move = new MoveJob
            {
                Agents = _a, Dt = dt, Player = player, Heading = playerHeading,
                RecycleBeyond = recycleBeyond, RecycleTo = recycleTo, SpeedScale = speedScale,
            }.Schedule(_count, 64);

            _grid.Clear();
            if (_grid.Capacity < _count) _grid.Capacity = _count;
            var hash = new HashJob { Agents = _a, InvCell = 1f / cell, Grid = _grid.AsParallelWriter() }.Schedule(_count, 128, move);
            var sep = new SeparateJob { Src = _a, Dst = _b, InvCell = 1f / cell, Grid = _grid }.Schedule(_count, 64, hash);
            sep.Complete();

            (_a, _b) = (_b, _a);

            for (int i = _count - 1; i >= 0; i--)
                if (_a[i].Dead != 0) RemoveAt(i);
        }

        static int Key(int2 c) => (c.x * 73856093) ^ (c.y * 19349663);

        [BurstCompile]
        struct MoveJob : IJobParallelFor
        {
            public NativeArray<EnemyAgent> Agents;
            public float Dt;
            public float2 Player;
            public float2 Heading;
            public float RecycleBeyond;
            public float RecycleTo;
            public float SpeedScale;

            public void Execute(int i)
            {
                var e = Agents[i];
                e.Life += Dt;
                float2 to = Player - e.Pos;
                float d = math.length(to);
                float2 dir = d > 1e-5f ? to / d : new float2(1, 0);
                float speed = e.Speed * SpeedScale;
                float2 v = float2.zero;
                switch ((EnemyMove)e.Move)
                {
                    case EnemyMove.Hop:
                        e.Hop = math.frac(e.Hop + Dt / HopPeriod);
                        if (e.Hop < HopDuty) v = dir * (speed / HopDuty);
                        break;
                    case EnemyMove.Flee:
                        v = -dir * speed;
                        break;
                    case EnemyMove.Line:
                        v = e.LineDir * speed;
                        if (e.Life > LineLifeSeconds) e.Dead = 1;
                        break;
                    default:
                        v = dir * speed;
                        break;
                }
                e.Pos += (v + e.Knock) * Dt;
                e.Knock *= math.pow(0.02f, Dt);

                // 멀리 떨어진 적은 플레이어가 가는 쪽(서 있으면 원래 쪽 반대편)으로 옮겨 다시 쓴다
                if (d > RecycleBeyond && e.Move != (byte)EnemyMove.Line && e.Move != (byte)EnemyMove.Flee)
                {
                    float2 side = math.lengthsq(Heading) > 1e-6f ? math.normalize(Heading) : dir;
                    // 같은 자리에 겹치지 않게 Id 로 조금 비튼다
                    float a = (e.Id * 0.618034f % 1f - 0.5f) * 1.6f;
                    float s = math.sin(a), c = math.cos(a);
                    side = new float2(side.x * c - side.y * s, side.x * s + side.y * c);
                    e.Pos = Player + side * RecycleTo;
                }
                Agents[i] = e;
            }
        }

        [BurstCompile]
        struct HashJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EnemyAgent> Agents;
            public float InvCell;
            public NativeParallelMultiHashMap<int, int>.ParallelWriter Grid;

            public void Execute(int i)
            {
                int2 c = (int2)math.floor(Agents[i].Pos * InvCell);
                Grid.Add(Key(c), i);
            }
        }

        // 겹친 적끼리 밀어내기. 둘레 3x3 칸에서 가까운 이웃을 최대 12까지 보고, 겹친 만큼의 절반을 내 쪽으로 민다.
        // 이전 위치(Src)만 읽고 새 위치(Dst)에 쓰니 병렬로 돌려도 순서에 따라 결과가 바뀌지 않는다.
        [BurstCompile]
        struct SeparateJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EnemyAgent> Src;
            [WriteOnly] public NativeArray<EnemyAgent> Dst;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> Grid;
            public float InvCell;

            public void Execute(int i)
            {
                var e = Src[i];
                int2 c = (int2)math.floor(e.Pos * InvCell);
                float2 push = float2.zero;
                int seen = 0;
                for (int y = -1; y <= 1 && seen < MaxNeighbors; y++)
                for (int x = -1; x <= 1 && seen < MaxNeighbors; x++)
                {
                    if (!Grid.TryGetFirstValue(Key(c + new int2(x, y)), out int j, out var it)) continue;
                    do
                    {
                        if (j == i) continue;
                        var o = Src[j];
                        float2 dd = e.Pos - o.Pos;
                        float rr = e.Radius + o.Radius;
                        float d2 = math.lengthsq(dd);
                        if (d2 >= rr * rr) continue;
                        seen++;
                        if (d2 < 1e-8f)
                        {
                            // 완전히 겹치면 Id 로 방향을 정한다
                            float a = (e.Id - o.Id) * 2.39996f;
                            dd = new float2(math.cos(a), math.sin(a)) * 1e-3f;
                            d2 = 1e-6f;
                        }
                        float dist = math.sqrt(d2);
                        push += dd / dist * ((rr - dist) * 0.5f);
                        if (seen >= MaxNeighbors) break;
                    } while (Grid.TryGetNextValue(out j, ref it));
                }
                e.Pos += push * e.Weight;
                Dst[i] = e;
            }
        }
    }
}
