using System;
using System.Collections.Generic;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BloodRain.Presentation.Combat
{
    // 무공 실행기(C-04). weapons.json behavior 마다 웹 프로토타입 fireWeapon 과 같은 규칙으로 판정한다.
    //   피해 = Core WeaponMath.DamageAt(무공, 레벨). 사거리·속도·넉백은 경지 m → WorldScale 세계 m.
    //   쿨다운은 weapons.json cooldown(aura 는 웹처럼 0.5초마다 한 번, 초당 피해의 절반, random_rapid 는 초당 4→9발).
    //   능력치(범위·쿨감·투사체 수 등)는 Loadout/Stats 가 붙을 때(C-06) 곱한다. 지금은 모두 1.
    // 처음 8갈래: front_arc(삼재검법), melee_combo(나한권), orbit_slash(창천검기), aura(독무),
    //   homing_dagger(비도술), slow_projectile(장풍), random_rapid(섬광지), random_bomb(벽력탄).
    // 아직 없는 것: 절기(evolved) 모양, 중독·화상·느려짐 같은 적 상태, 넉백 저항.
    public sealed class WeaponRunner : MonoBehaviour
    {
        [Serializable]
        public sealed class Slot
        {
            public string id;
            [Range(1, 8)] public int level = 1;
            [NonSerialized] public WeaponDef Def;
            [NonSerialized] public float Cooldown;
            [NonSerialized] public int Fired;
        }

        public EnemyField enemies;
        public PlayerController player;
        public Material material;
        public List<Slot> slots = new List<Slot>();
        public bool showStats = true;

        static readonly string[] TestOrder =
        {
            "samjae_sword", "nahan_fist", "changcheon_qi", "poison_mist", "flying_dagger", "palm_wind", "flash_finger", "thunder_bomb",
        };

        struct Pending { public float Delay; public Action Apply; }
        struct Fx
        {
            public float Angle, Span, Radius, Life, Age;
            public Color Color;
            public bool Sweep;      // orbit_slash: 한 바퀴씩 도는 칼날
            public int Sweeps;
            public Slot Weapon;
            public float Damage;
            public HashSet<int> Hit;
            public float Prev;      // 지난 프레임까지 돈 각
            public bool Fixed;      // 땅에 박힌 이펙트(벽력탄). 아니면 플레이어를 따라간다
            public Vector2 Center;
        }

        readonly List<Pending> _pending = new List<Pending>();
        readonly List<Fx> _fx = new List<Fx>();
        readonly List<int> _near = new List<int>();
        readonly List<int> _filtered = new List<int>();
        readonly Dictionary<string, float> _dealt = new Dictionary<string, float>();
        Material _mat;
        MaterialPropertyBlock _mpb;
        readonly Dictionary<int, Mesh> _sectors = new Dictionary<int, Mesh>();
        static readonly int IdTint = Shader.PropertyToID("_Tint");
        static readonly int IdDissolve = Shader.PropertyToID("_Dissolve");
        readonly Matrix4x4[] _one = new Matrix4x4[1];
        readonly Vector4[] _tint = new Vector4[1];
        readonly float[] _dis = new float[1];

        WorldScale World => WorldScale.Current;
        Vector2 P => player.transform.position;

        /// <summary>무공별 준 피해 합(정산 화면 막대용).</summary>
        public IReadOnlyDictionary<string, float> Dealt => _dealt;

        void Awake()
        {
            var data = DataBoot.Data;
            if (slots.Count == 0)
            {
                var body = Meta.RebirthHandoff.Body ?? Core.Meta.Rebirth.Prologue(data);
                slots.Add(new Slot { id = body.Character.StartWeapon });
            }
            foreach (var s in slots) s.Def = data.Weapon(s.id);
            if (material != null)
            {
                var white = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                white.SetPixels32(px);
                white.Apply();
                _mat = new Material(material) { name = "무공 이펙트", enableInstancing = true, mainTexture = white };
            }
            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>시험용: 무공을 하나 더 익힌다(있으면 레벨 +1).</summary>
        public void Learn(string id)
        {
            var s = slots.Find(x => x.id == id);
            if (s != null) { s.level = Mathf.Min(8, s.level + 1); return; }
            slots.Add(new Slot { id = id, Def = DataBoot.Data.Weapon(id) });
        }

        void Update()
        {
            if (World == null || enemies == null || player == null || enemies.Sim == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.leftCtrlKey.isPressed)
            {
                if (kb.digit1Key.wasPressedThisFrame) Learn(TestOrder[0]);
                if (kb.digit2Key.wasPressedThisFrame) Learn(TestOrder[1]);
                if (kb.digit3Key.wasPressedThisFrame) Learn(TestOrder[2]);
                if (kb.digit4Key.wasPressedThisFrame) Learn(TestOrder[3]);
                if (kb.digit5Key.wasPressedThisFrame) Learn(TestOrder[4]);
                if (kb.digit6Key.wasPressedThisFrame) Learn(TestOrder[5]);
                if (kb.digit7Key.wasPressedThisFrame) Learn(TestOrder[6]);
                if (kb.digit8Key.wasPressedThisFrame) Learn(TestOrder[7]);
            }

            float dt = Time.deltaTime;
            foreach (var s in slots)
            {
                s.Cooldown -= dt;
                if (s.Cooldown > 0f) continue;
                Fire(s);
                float cd = s.Def.Behavior == "aura" ? 0.5f
                         : s.Def.Behavior == "random_rapid" ? 1f / Mathf.Lerp(4f, 9f, T(s)) // weapons.json shotsPerSecond [4, 9]
                         : Mathf.Max(0.05f, s.Def.Cooldown);
                s.Cooldown += cd;
                if (s.Cooldown < -1f) s.Cooldown = 0f;
            }

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                p.Delay -= dt;
                if (p.Delay <= 0f) { _pending.RemoveAt(i); p.Apply(); }
                else _pending[i] = p;
            }

            StepProjectiles(dt);

            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                f.Age += dt;
                if (f.Sweep) SweepHits(ref f);
                if (f.Age >= f.Life) _fx.RemoveAt(i);
                else _fx[i] = f;
            }
        }

        float Damage(Slot s) => WeaponMath.DamageAt(s.Def, s.level);
        static float T(Slot s) => (s.level - 1) / 7f;

        void Deal(Slot s, float dmg, int hit)
        {
            if (hit <= 0) return;
            _dealt.TryGetValue(s.id, out float d);
            _dealt[s.id] = d + dmg * hit;
        }

        void Fire(Slot s)
        {
            var ws = World;
            float dmg = Damage(s);
            s.Fired++;
            switch (s.Def.Behavior)
            {
                case "front_arc":
                {
                    float R = ws.Length(s.Def.Range);
                    // 웹 결정: 사거리 1.25배 안에 적이 있으면 가장 가까운 적 쪽, 없으면 바라보는 쪽
                    float baseA = Nearest(R * 1.25f, out var dir) ? Mathf.Atan2(dir.y, dir.x) : Mathf.Atan2(player.Facing.y, player.Facing.x);
                    var dirs = s.level >= 4 ? new[] { baseA, baseA + Mathf.PI } : new[] { baseA };
                    int strikes = s.level >= 7 ? 3 : 1;
                    for (int k = 0; k < strikes; k++)
                        Later(k * 0.12f, () =>
                        {
                            foreach (var a in dirs)
                            {
                                AddFx(a, Mathf.PI, R, 0.22f, new Color(0.95f, 0.93f, 0.85f, 0.75f));
                                Deal(s, dmg, HitArc(P, R, a, dmg, ws.Speed(3f)));
                            }
                        });
                    break;
                }
                case "melee_combo":
                {
                    float R = ws.Length(s.Def.Range);
                    int hits = Mathf.RoundToInt(Mathf.Lerp(3, 5, T(s))); // weapons.json hits [3, 5]
                    for (int k = 0; k < hits; k++)
                        Later(k * 0.08f, () =>
                        {
                            AddFx(Mathf.Atan2(player.Facing.y, player.Facing.x), Mathf.PI * 2f, R * 0.6f, 0.12f, new Color(0.85f, 0.55f, 0.25f, 0.6f));
                            enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                            Deal(s, dmg, enemies.Damage(_near, dmg, P, ws.Speed(4f)));
                        });
                    break;
                }
                case "orbit_slash":
                {
                    float R = ws.Length(s.Def.Range);
                    int sweeps = s.level >= 5 ? 2 : 1;
                    _fx.Add(new Fx
                    {
                        Angle = UnityEngine.Random.value * Mathf.PI * 2f, Span = 0.6f, Radius = R, Life = 0.36f * sweeps,
                        Color = new Color(0.55f, 0.75f, 0.95f, 0.8f), Sweep = true, Sweeps = sweeps, Weapon = s, Damage = dmg, Hit = new HashSet<int>(),
                    });
                    break;
                }
                case "aura":
                {
                    float R = ws.Length(Mathf.Lerp(s.Def.Range, 3.5f, T(s))); // 3.5 = weapons.json rangeLv8(WeaponDef 에 아직 없음)
                    float hitDmg = dmg * 0.5f;                                  // 0.5초마다 초당 피해의 절반
                    AddFx(0f, Mathf.PI * 2f, R, 0.5f, new Color(0.37f, 0.50f, 0.29f, 0.22f));
                    enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                    Deal(s, hitDmg, enemies.Damage(_near, hitDmg, P, 0f));
                    break;
                }
                case "homing_dagger":
                {
                    // 비도: 가까운 적 n 명에게, 초속 12m, 관통 1→3, 1.2초, 따라가기 6(웹 값)
                    int n = Mathf.RoundToInt(Mathf.Lerp(1, 4, T(s)));
                    int pierce = Mathf.RoundToInt(Mathf.Lerp(1, 3, T(s)));
                    NearestN(ws.Length(s.Def.Range * 1.4f), n, _targets);
                    for (int k = 0; k < n; k++)
                    {
                        int tg = _targets.Count > 0 ? _targets[k % _targets.Count] : -1;
                        var agents = enemies.Sim.Agents;
                        float a = tg >= 0 ? Mathf.Atan2(agents[tg].Pos.y - P.y, agents[tg].Pos.x - P.x)
                                          : Mathf.Atan2(player.Facing.y, player.Facing.x) + (k - n / 2f) * 0.25f;
                        a += UnityEngine.Random.Range(-0.08f, 0.08f);
                        Shoot(s, a, ws.Speed(12f), ws.Length(0.25f), dmg, pierce, 1.2f, tg >= 0 ? agents[tg].Id : 0, 6f, 0f,
                              new Color(0.80f, 0.82f, 0.88f, 1f));
                    }
                    break;
                }
                case "slow_projectile":
                {
                    // 장풍: 바라보는 쪽으로 초속 5m, 반경 0.55→0.88m, 다 뚫고, 2.6초, 넉백 5. Lv6 2연발
                    int shots = s.level >= 6 ? 2 : 1;
                    for (int k = 0; k < shots; k++)
                        Later(k * 0.15f, () =>
                        {
                            float r = ws.Length(0.55f * Mathf.Lerp(1f, 1.6f, T(s)));
                            Shoot(s, Mathf.Atan2(player.Facing.y, player.Facing.x), ws.Speed(5f), r, dmg, 999, 2.6f, 0, 0f, ws.Speed(5f),
                                  new Color(0.95f, 0.85f, 0.55f, 0.55f));
                        });
                    break;
                }
                case "random_rapid":
                {
                    // 섬광지: 사거리 안 가까운 12명 가운데 하나에게 초속 18m, 안 뚫음, 0.6초. 쿨다운은 초당 4→9발
                    NearestN(ws.Length(s.Def.Range), 12, _targets);
                    if (_targets.Count == 0) break;
                    var e = enemies.Sim.Agents[_targets[UnityEngine.Random.Range(0, _targets.Count)]];
                    Shoot(s, Mathf.Atan2(e.Pos.y - P.y, e.Pos.x - P.x), ws.Speed(18f), ws.Length(0.16f), dmg, 0, 0.6f, 0, 0f, 0f,
                          new Color(1f, 0.95f, 0.6f, 1f));
                    break;
                }
                case "random_bomb":
                {
                    // 벽력탄: 7m 안 가까운 20명 가운데 무작위 자리(없으면 둘레 4m)에 0.6초 뒤 터짐, 반경 2→3m, 넉백 3.
                    // 화상(burnPerSecond)은 적 상태 칸이 붙을 때(C-04 남은 것)
                    int n = Mathf.RoundToInt(Mathf.Lerp(1, 3, T(s)));
                    NearestN(ws.Length(7f), 20, _targets);
                    float R = ws.Length(s.Def.Range * Mathf.Lerp(1f, 1.5f, T(s)));
                    for (int k = 0; k < n; k++)
                    {
                        Vector2 at;
                        if (_targets.Count > 0) { var e = enemies.Sim.Agents[_targets[UnityEngine.Random.Range(0, _targets.Count)]]; at = new Vector2(e.Pos.x, e.Pos.y); }
                        else at = P + new Vector2(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f)) * ws.Length(1f);
                        AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.6f, new Color(0.70f, 0.15f, 0.12f, 0.18f)); // 떨어질 자리 표시
                        Later(0.6f, () =>
                        {
                            AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.35f, new Color(0.95f, 0.55f, 0.20f, 0.7f));
                            enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
                            Deal(s, dmg, enemies.Damage(_near, dmg, at, ws.Speed(3f)));
                        });
                    }
                    break;
                }
                default:
                    break; // 아직 없는 갈래
            }
        }

        // ── 투사체 ──────────────────────────────────────────────
        struct Proj
        {
            public Vector2 Pos, Vel;
            public float Radius, Damage, Life, Homing, Knock;
            public int Pierce, Target;
            public Slot Weapon;
            public Color Color;
            public HashSet<int> Hit;
        }

        readonly List<Proj> _projs = new List<Proj>();
        readonly List<int> _targets = new List<int>();
        readonly List<int> _one1 = new List<int>(1);

        void Shoot(Slot s, float angle, float speed, float radius, float dmg, int pierce, float life, int targetId, float homing, float knock, Color c)
        {
            if (_projs.Count >= DataBoot.Data.Balance.Limits.Projectiles) return;
            _projs.Add(new Proj
            {
                Pos = P, Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed, Radius = radius, Damage = dmg, Life = life,
                Homing = homing, Knock = knock, Pierce = pierce, Target = targetId, Weapon = s, Color = c, Hit = new HashSet<int>(),
            });
        }

        void StepProjectiles(float dt)
        {
            var sim = enemies.Sim;
            for (int i = _projs.Count - 1; i >= 0; i--)
            {
                var p = _projs[i];
                p.Life -= dt;
                if (p.Target != 0 && p.Homing > 0f)
                {
                    int ti = IndexOf(p.Target);
                    if (ti >= 0)
                    {
                        var e = sim.Agents[ti];
                        var want = (new Vector2(e.Pos.x, e.Pos.y) - p.Pos).normalized * p.Vel.magnitude;
                        p.Vel = Vector2.Lerp(p.Vel, want, Mathf.Clamp01(p.Homing * dt));
                    }
                    else p.Target = 0;
                }
                p.Pos += p.Vel * dt;
                sim.QueryCircle(new float2(p.Pos.x, p.Pos.y), p.Radius, _near);
                bool dead = p.Life <= 0f;
                foreach (int j in _near)
                {
                    if (dead) break;
                    int id = sim.Agents[j].Id;
                    if (p.Hit.Contains(id)) continue;
                    p.Hit.Add(id);
                    _one1.Clear();
                    _one1.Add(j);
                    Deal(p.Weapon, p.Damage, enemies.Damage(_one1, p.Damage, p.Pos, p.Knock));
                    if (p.Pierce-- <= 0) dead = true;
                    break; // 한 프레임에 하나(쓰러짐으로 자리 번호가 바뀌니 다음 프레임에 다시 찾는다)
                }
                if (dead) _projs.RemoveAt(i);
                else _projs[i] = p;
            }
        }

        int IndexOf(int id)
        {
            var a = enemies.Sim.Agents;
            for (int i = 0; i < enemies.Sim.Count; i++) if (a[i].Id == id) return i;
            return -1;
        }

        void NearestN(float within, int n, List<int> outIdx)
        {
            outIdx.Clear();
            enemies.Sim.QueryCircle(new float2(P.x, P.y), within, _near);
            var a = enemies.Sim.Agents;
            var p = P;
            _near.Sort((x, y) => math.distancesq(a[x].Pos, new float2(p.x, p.y)).CompareTo(math.distancesq(a[y].Pos, new float2(p.x, p.y))));
            int last = -1;
            foreach (int i in _near) { if (i == last || outIdx.Contains(i)) continue; last = i; outIdx.Add(i); if (outIdx.Count >= n) break; }
        }

        // orbit_slash: 칼날이 한 바퀴 도는 동안 지나간 부채꼴 안의 적을 한 바퀴에 한 번씩
        void SweepHits(ref Fx f)
        {
            float perSweep = f.Life / f.Sweeps;
            float turn = Mathf.Min(f.Age, f.Life) / perSweep * Mathf.PI * 2f;
            int prevSweep = (int)(f.Prev / (Mathf.PI * 2f));
            int sweep = Mathf.Min(f.Sweeps - 1, (int)(turn / (Mathf.PI * 2f)));
            if (sweep > prevSweep) f.Hit.Clear();
            float from = f.Angle + f.Prev, span = turn - f.Prev;
            f.Prev = turn;
            enemies.Sim.QueryCircle(new float2(P.x, P.y), f.Radius, _near);
            var agents = enemies.Sim.Agents;
            _filtered.Clear();
            foreach (int i in _near)
            {
                var a = agents[i];
                if (f.Hit.Contains(a.Id)) continue;
                float ang = Mathf.Atan2(a.Pos.y - P.y, a.Pos.x - P.x);
                float rel = Mathf.Repeat(ang - from, Mathf.PI * 2f);
                float d = Vector2.Distance(new Vector2(a.Pos.x, a.Pos.y), P);
                float slack = d > 1e-4f ? a.Radius / d : Mathf.PI; // 덩이 반경만큼 각을 넓힌다
                if (rel <= span + slack || rel >= Mathf.PI * 2f - slack) { _filtered.Add(i); f.Hit.Add(a.Id); }
            }
            Deal(f.Weapon, f.Damage, enemies.Damage(_filtered, f.Damage, P, World.Speed(2f)));
        }

        int HitArc(Vector2 c, float R, float a, float dmg, float knock)
        {
            enemies.Sim.QueryCircle(new float2(c.x, c.y), R, _near);
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var agents = enemies.Sim.Agents;
            _filtered.Clear();
            foreach (int i in _near)
            {
                var e = agents[i];
                float dx = e.Pos.x - c.x, dy = e.Pos.y - c.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (dx * ca + dy * sa >= -0.1f * d) _filtered.Add(i); // 웹과 같은 반원 판정
            }
            return enemies.Damage(_filtered, dmg, c, knock);
        }

        bool Nearest(float within, out Vector2 dir)
        {
            dir = Vector2.zero;
            enemies.Sim.QueryCircle(new float2(P.x, P.y), within, _near);
            float best = float.MaxValue;
            var agents = enemies.Sim.Agents;
            foreach (int i in _near)
            {
                var d = new Vector2(agents[i].Pos.x - P.x, agents[i].Pos.y - P.y);
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; dir = d; }
            }
            return best < float.MaxValue && dir.sqrMagnitude > 1e-8f;
        }

        void Later(float delay, Action a)
        {
            if (delay <= 0f) a();
            else _pending.Add(new Pending { Delay = delay, Apply = a });
        }

        void AddFx(float angle, float span, float radius, float life, Color c) =>
            _fx.Add(new Fx { Angle = angle, Span = span, Radius = radius, Life = life, Color = c });

        void AddFxAt(Vector2 at, float angle, float span, float radius, float life, Color c) =>
            _fx.Add(new Fx { Angle = angle, Span = span, Radius = radius, Life = life, Color = c, Fixed = true, Center = at });

        void LateUpdate()
        {
            if (_mat == null || player == null || (_fx.Count == 0 && _projs.Count == 0)) return;
            var rp = new RenderParams(_mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            foreach (var f in _fx)
            {
                float k = f.Age / f.Life;
                float angle = f.Sweep ? f.Angle + f.Prev : f.Angle;
                var c = f.Fixed ? f.Center : P;
                var col = f.Color;
                if (!f.Sweep) col.a *= 1f - k;
                Draw(rp, Sector(f.Span), new Vector3(c.x, c.y, -0.25f), angle, f.Radius, col);
            }
            var disc = Sector(Mathf.PI * 2f);
            foreach (var p in _projs)
                Draw(rp, disc, new Vector3(p.Pos.x, p.Pos.y, -0.26f), 0f, p.Radius, p.Color);
        }

        void Draw(RenderParams rp, Mesh mesh, Vector3 pos, float angle, float radius, Color col)
        {
            _one[0] = Matrix4x4.TRS(pos, Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg), new Vector3(radius, radius, 1f));
            _tint[0] = col;
            _dis[0] = 0f;
            _mpb.Clear();
            _mpb.SetVectorArray(IdTint, _tint);
            _mpb.SetFloatArray(IdDissolve, _dis);
            Graphics.RenderMeshInstanced(rp, mesh, 0, _one, 1);
        }

        /// <summary>반지름 1, +x 축을 가운데로 span 만큼 벌어진 부채꼴.</summary>
        Mesh Sector(float span)
        {
            int key = Mathf.RoundToInt(span * 1000f);
            if (_sectors.TryGetValue(key, out var m)) return m;
            int seg = Mathf.Max(6, Mathf.CeilToInt(span / (Mathf.PI * 2f) * 48f));
            var v = new Vector3[seg + 2];
            var uv = new Vector2[seg + 2];
            var tri = new int[seg * 3];
            v[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i <= seg; i++)
            {
                float a = -span / 2f + span * i / seg;
                v[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                uv[i + 1] = new Vector2(0.5f + v[i + 1].x * 0.5f, 0.5f + v[i + 1].y * 0.5f);
            }
            for (int i = 0; i < seg; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = i + 2; tri[i * 3 + 2] = i + 1; }
            m = new Mesh { name = "부채꼴", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            _sectors[key] = m;
            return m;
        }

        void OnGUI()
        {
            if (!showStats) return;
            var sb = new System.Text.StringBuilder("무공 ");
            foreach (var s in slots)
            {
                _dealt.TryGetValue(s.id, out float d);
                sb.Append($"{s.Def?.NameKo} Lv{s.level} 피해 {d:0}  ");
            }
            sb.Append(" (Ctrl+1~4 익히기)");
            GUI.Label(new Rect(12, 72, 900, 22), sb.ToString());
        }
    }
}
