using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BloodRain.Sim
{
    /// <summary>적 움직임 갈래. enemies.json behavior 를 여기 다섯으로 줄인다(나머지 갈래는 C-04 뒤에 하나씩).</summary>
    public enum EnemyMove : byte
    {
        Chase = 0,   // 곧장 쫓기
        Hop = 1,     // hop_chase: 강시 콩콩. 0.6초 박자 중 72% 동안만 뛴다(평균 속도는 데이터 그대로)
        Flee = 2,    // flee: 금두꺼비
        Line = 3,    // line_charge: 처음 방향으로 곧장. EnemyAgent.MaxLife(enemies.json lifeSeconds) 뒤 사라짐
        Dash = 4,    // telegraph_dash: 혈무사. 쫓다가 DashRange 안이면 Telegraph 초 멈춰 예고 → DashSeconds 동안 DashSpeed 로 돌진 → Rest 초 쉬며 쫓기
        Keep = 5,    // kite_shoot: 혈궁수. KeepMax 보다 멀면 다가오고 KeepMin 보다 가까우면 물러난다(그 사이에선 선다)
        Curve = 6,   // fly_curve: 비강. 쫓으면서 옆으로 CurveAmp m 만큼 CurvePeriod 초 주기로 흔들린다(EnemyDef.Curve)
        Flank = 7,   // flank_pack: 설랑. 플레이어 쪽에서 FlankAngle 만큼 비껴 돌아 들어오다 CommitRange 안이면 곧장 덮친다(EnemyDef.Group)
    }

    /// <summary>telegraph_dash 단계.</summary>
    public enum DashPhase : byte { Chase = 0, Telegraph = 1, Dashing = 2, Rest = 3 }

    /// <summary>적 한 덩이의 상태. 위치·속도·반경은 세계 m(WorldScale 로 바꾼 값).</summary>
    public struct EnemyAgent
    {
        public float2 Pos;
        public float2 Knock;      // 넉백 속도. 초당 balance.json enemyStatus.knockbackKeepPerSecond(0.02)배로 줄어든다
        public float2 LineDir;
        public float Speed;       // 세계 m/s(혈월 배율까지 곱한 값)
        public float Radius;      // 세계 m
        public float Weight;      // 밀어내기에서 밀리는 정도(정예·보스 0.1)
        public float Hop;         // 콩콩 박자 0~1
        public float Health;
        public float MaxHealth;
        public float Life;        // 생긴 뒤 흐른 시간
        public float MaxLife;     // 이 시간이 지나면 사라짐(enemies.json lifeSeconds: 혈인 14·금두꺼비 25). 0 이면 끝까지 남는다
        public float ContactDamage;
        public float Contact;     // 같은 적에게 다시 맞기까지 남은 시간(balance.json contact.sameEnemyIntervalSeconds)
        public int BornRealm;     // 이 덩이가 생긴 경지. 플레이어보다 아래면 접촉 피해 없음(Scale.DealsContactDamage)
        public int Kind;          // 적 종류 순번(EnemyField 의 표)
        public int Id;            // 풀에서 꺼낼 때마다 새 번호
        // telegraph_dash(EnemyDef.Telegraph·Dash). 거리·속도는 세계 m, m/s
        public float DashRange, DashSpeed, DashSeconds, Telegraph, RestMin, RestMax;
        public float PhaseLeft;   // 지금 단계 남은 초
        // buff_aura(기수, EnemyDef.AuraRadius·AuraBonus): AuraRadius(세계 m) 안 다른 적의 Buff 를 AuraBonus 로 올린다.
        // Buff 는 EnemyField 가 매 프레임 다시 매기고, 이동 속도·접촉 피해에 (1 + Buff) 로 곱한다.
        public float AuraRadius, AuraBonus, Buff;
        // 쏘는 적(EnemyDef.Shoot: 혈궁수·매화검귀·설인). ShootInterval 0 이면 안 쏜다. 거리는 세계 m.
        // ShotLeft 가 0 이 되고 플레이어가 ShootRange 안이면 Fire = 1 — 탄은 EnemyField 가 만들고 Fire 를 지운다.
        public float KeepMin, KeepMax, ShootRange, ShootInterval, ShotLeft;
        public byte Fire;
        // 감전(뇌부 Lv5 '감전', weapons.json shockSeconds) 같은 멈춤. 남은 초 동안 걷지도 쏘지도 않는다(넉백은 받는다).
        public float Stun;
        // fly_curve(비강, EnemyDef.Curve): 옆 흔들림 폭(세계 m)과 주기(초). 위상은 Hop 자리(0~1)를 같이 쓴다
        public float CurveAmp, CurvePeriod;
        // flank_pack(설랑, EnemyDef.Group): 비껴 드는 각(라디안, 부호가 왼쪽·오른쪽)과 곧장 덮치는 거리(세계 m)
        public float FlankAngle, CommitRange;
        public float2 DashDir;
        public byte Phase;        // DashPhase
        public byte Move;
        public byte Dead;         // 1 이면 Step 끝에 걷어 낸다(Line 시간 초과 등)
        public byte NoDrop;       // 1 이면 쓰러져도 구슬을 떨구지 않는다(쌍강시 되살아난 몸)
    }

    // 적 시뮬레이션(C-02). 구조체 배열(NativeArray) 하나에 살아 있는 적만 앞쪽에 빽빽이 두고, 죽으면 맨 뒤 것을 그 자리로 옮긴다(풀).
    // 한 프레임: 움직임 잡(추적·콩콩·넉백·멀어진 적 되돌리기) → 격자 해시 잡 → 밀어내기 잡. 모두 Burst, 병렬.
    // 숫자(속도·기혈)는 넣는 쪽(EnemyField)이 데이터·Core 에서 읽어 세계 단위로 바꿔 넣는다.
    public sealed class EnemySim : IDisposable
    {
        public const float HopPeriod = 0.6f;
        public const float HopDuty = 0.72f;
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
        /// recycleBeyond 를 넘게 멀어진 적은 플레이어 둘레 recycleTo 거리, 플레이어가 가는 쪽으로 옮긴다.
        /// knockKeepPerSecond 는 넉백 속도가 1초 뒤 남는 비율(balance.json enemyStatus.knockbackKeepPerSecond).</summary>
        public void Step(float dt, float2 player, float2 playerHeading, float cell, float recycleBeyond, float recycleTo,
                         float speedScale = 1f, float knockKeepPerSecond = 0.02f)
        {
            if (_count == 0 || dt <= 0f) return; // Removed 는 그리는 쪽이 읽고 비운다

            var move = new MoveJob
            {
                Agents = _a, Dt = dt, Player = player, Heading = playerHeading,
                RecycleBeyond = recycleBeyond, RecycleTo = recycleTo, SpeedScale = speedScale,
                KnockKeep = math.pow(knockKeepPerSecond, dt),
            }.Schedule(_count, 64);

            _grid.Clear();
            if (_grid.Capacity < _count) _grid.Capacity = _count;
            var hash = new HashJob { Agents = _a, InvCell = 1f / cell, Grid = _grid.AsParallelWriter() }.Schedule(_count, 128, move);
            var sep = new SeparateJob { Src = _a, Dst = _b, InvCell = 1f / cell, Grid = _grid }.Schedule(_count, 64, hash);
            sep.Complete();

            (_a, _b) = (_b, _a);

            for (int i = _count - 1; i >= 0; i--)
                if (_a[i].Dead != 0) RemoveAt(i);

            RebuildGrid(cell);
        }

        // ── 충돌(C-03) ─────────────────────────────────────────────
        // Step 이 끝나면 격자를 최종 위치로 다시 짓는다. 무공·구슬·접촉은 모두 이 격자로 원 대 원을 찾는다.
        // 칸 크기는 Step 에 넘긴 cell. 칸보다 큰 적(정예·보스)은 MaxRadius 만큼 둘레 칸을 더 본다.

        float _cell = 1f;
        float _maxRadius;

        /// <summary>지금 격자에 든 적 가운데 가장 큰 반경(세계 m).</summary>
        public float MaxRadius => _maxRadius;

        /// <summary>위치를 바꾼 뒤(넉백·순간이동 등) 격자를 다시 지을 때.</summary>
        public void RebuildGrid(float cell)
        {
            _cell = math.max(1e-4f, cell);
            _grid.Clear();
            _maxRadius = 0f;
            if (_count == 0) return;
            if (_grid.Capacity < _count) _grid.Capacity = _count;
            new HashJob { Agents = _a, InvCell = 1f / _cell, Grid = _grid.AsParallelWriter() }.Schedule(_count, 128).Complete();
            for (int i = 0; i < _count; i++) _maxRadius = math.max(_maxRadius, _a[i].Radius);
        }

        /// <summary>center 둘레 radius 원에 겹치는 적의 자리(index)를 results 에 담는다(원 대 원: 거리 &lt; radius + 적 반경).</summary>
        public int QueryCircle(float2 center, float radius, System.Collections.Generic.List<int> results)
        {
            results.Clear();
            if (_count == 0) return 0;
            float reach = radius + _maxRadius;
            int2 lo = (int2)math.floor((center - reach) / _cell);
            int2 hi = (int2)math.floor((center + reach) / _cell);
            for (int y = lo.y; y <= hi.y; y++)
            for (int x = lo.x; x <= hi.x; x++)
            {
                if (!_grid.TryGetFirstValue(Key(new int2(x, y)), out int j, out var it)) continue;
                do
                {
                    var e = _a[j];
                    float rr = radius + e.Radius;
                    if (math.lengthsq(e.Pos - center) < rr * rr) results.Add(j);
                } while (_grid.TryGetNextValue(out j, ref it));
            }
            return results.Count;
        }

        /// <summary>한 번 맞은 적.</summary>
        public struct ContactHit
        {
            public int Index;
            public int Id;
            public float Damage;
        }

        readonly System.Collections.Generic.List<int> _near = new System.Collections.Generic.List<int>();

        /// <summary>플레이어 원(center, radius)에 닿은 적 가운데 쿨다운이 끝난 적을 hits 에 담고 쿨다운을 interval 로 되돌린다.
        /// playerRealm 보다 아래 경지에서 생긴 적은 밟히는 쪽이라 피해를 주지 않는다(TU-03 CrushField 가 따로 처리).</summary>
        public int Contacts(float2 center, float radius, float interval, int playerRealm,
                            Func<int, int, bool> dealsDamage, System.Collections.Generic.List<ContactHit> hits)
        {
            hits.Clear();
            QueryCircle(center, radius, _near);
            foreach (int i in _near)
            {
                var e = _a[i];
                if (e.Contact > 0f || e.ContactDamage <= 0f) continue;
                if (dealsDamage != null && !dealsDamage(e.BornRealm, playerRealm)) continue;
                e.Contact = interval;
                _a[i] = e;
                hits.Add(new ContactHit { Index = i, Id = e.Id, Damage = e.ContactDamage * (1f + e.Buff) });
            }
            return hits.Count;
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
            public float KnockKeep; // 이번 프레임 넉백이 남는 비율 = knockbackKeepPerSecond ^ Dt

            public void Execute(int i)
            {
                var e = Agents[i];
                e.Life += Dt;
                e.Contact = math.max(0f, e.Contact - Dt);
                float2 to = Player - e.Pos;
                float d = math.length(to);
                float2 dir = d > 1e-5f ? to / d : new float2(1, 0);
                float speed = e.Speed * SpeedScale * (1f + e.Buff);
                float2 v = float2.zero;
                if (e.Stun > 0f)
                {
                    e.Stun = math.max(0f, e.Stun - Dt);
                }
                else
                {
                if (e.ShootInterval > 0f)
                {
                    e.ShotLeft -= Dt;
                    if (e.ShotLeft <= 0f && d <= e.ShootRange) { e.Fire = 1; e.ShotLeft = e.ShootInterval; }
                }
                switch ((EnemyMove)e.Move)
                {
                    case EnemyMove.Keep:
                        if (d > e.KeepMax) v = dir * speed;
                        else if (d < e.KeepMin) v = -dir * speed;
                        break;
                    case EnemyMove.Hop:
                        e.Hop = math.frac(e.Hop + Dt / HopPeriod);
                        if (e.Hop < HopDuty) v = dir * (speed / HopDuty);
                        break;
                    case EnemyMove.Flee:
                        v = -dir * speed;
                        break;
                    case EnemyMove.Curve:
                    {
                        // 옆 자리 = Amp·sin(2π(t/P + 위상)). 그 미분을 옆 속도로 더한다(앞으로 가는 빠르기는 데이터 그대로)
                        float w = 2f * math.PI / math.max(0.05f, e.CurvePeriod);
                        float side = e.CurveAmp * w * math.cos(w * e.Life + e.Hop * 2f * math.PI);
                        v = dir * speed + new float2(-dir.y, dir.x) * side;
                        break;
                    }
                    case EnemyMove.Flank:
                    {
                        // 멀면 플레이어 쪽 방향을 FlankAngle 만큼 돌려 옆으로 감아 들고(앞으로 가는 몫은 cos), 가까우면 곧장
                        if (d > e.CommitRange)
                        {
                            float s = math.sin(e.FlankAngle), c = math.cos(e.FlankAngle);
                            v = new float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c) * speed;
                        }
                        else v = dir * speed;
                        break;
                    }
                    case EnemyMove.Line:
                        v = e.LineDir * speed;
                        break;
                    case EnemyMove.Dash:
                        e.PhaseLeft -= Dt;
                        switch ((DashPhase)e.Phase)
                        {
                            case DashPhase.Telegraph:
                                e.DashDir = dir; // 예고 동안 겨누다가 돌진하는 순간 굳힌다
                                if (e.PhaseLeft <= 0f) { e.Phase = (byte)DashPhase.Dashing; e.PhaseLeft = e.DashSeconds; }
                                break;
                            case DashPhase.Dashing:
                                v = e.DashDir * e.DashSpeed * SpeedScale * (1f + e.Buff);
                                if (e.PhaseLeft <= 0f)
                                {
                                    // 쉬는 초는 [RestMin, RestMax] 사이. Id·나이로 섞은 값이라 잡 안에서도 같은 결과
                                    float r = (math.hash(new float2(e.Id, e.Life)) & 0xFFFF) / 65535f;
                                    e.Phase = (byte)DashPhase.Rest;
                                    e.PhaseLeft = math.lerp(e.RestMin, e.RestMax, r);
                                }
                                break;
                            case DashPhase.Rest:
                                v = dir * speed;
                                if (e.PhaseLeft <= 0f) e.Phase = (byte)DashPhase.Chase;
                                break;
                            default:
                                v = dir * speed;
                                if (d < e.DashRange) { e.Phase = (byte)DashPhase.Telegraph; e.PhaseLeft = e.Telegraph; e.DashDir = dir; }
                                break;
                        }
                        break;
                    default:
                        v = dir * speed;
                        break;
                }
                } // 감전 아님
                e.Pos += (v + e.Knock) * Dt;
                e.Knock *= KnockKeep;
                if (e.MaxLife > 0f && e.Life > e.MaxLife) e.Dead = 1;

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
