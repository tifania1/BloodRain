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
    // 무공 실행기(C-04, 진행 중). weapons.json behavior 마다 웹 프로토타입 fireWeapon 과 같은 규칙으로 판정한다.
    //   피해 = Core WeaponMath.DamageAt(무공, 레벨). 사거리·넉백은 경지 m → WorldScale 세계 m.
    //   쿨다운은 weapons.json cooldown(aura 는 웹처럼 0.5초마다 한 번, 초당 피해의 절반).
    //   능력치(범위·쿨감·투사체 수 등)는 Loadout/Stats 가 붙을 때(C-06) 곱한다. 지금은 모두 1.
    // 지금 되는 갈래: front_arc(삼재검법), melee_combo(나한권), orbit_slash(창천검기), aura(독무).
    // 남은 갈래(homing_dagger·slow_projectile·random_rapid·random_bomb)는 투사체 풀과 함께 다음 세션.
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

        static readonly string[] TestOrder = { "samjae_sword", "nahan_fist", "changcheon_qi", "poison_mist" };

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
            }

            float dt = Time.deltaTime;
            foreach (var s in slots)
            {
                s.Cooldown -= dt;
                if (s.Cooldown > 0f) continue;
                Fire(s);
                float cd = s.Def.Behavior == "aura" ? 0.5f : Mathf.Max(0.05f, s.Def.Cooldown);
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
                default:
                    break; // 아직 없는 갈래
            }
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

        void LateUpdate()
        {
            if (_mat == null || player == null || _fx.Count == 0) return;
            var rp = new RenderParams(_mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            foreach (var f in _fx)
            {
                float k = f.Age / f.Life;
                float angle = f.Sweep ? f.Angle + f.Prev : f.Angle;
                var mesh = Sector(f.Span);
                _one[0] = Matrix4x4.TRS(new Vector3(P.x, P.y, -0.25f), Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg), new Vector3(f.Radius, f.Radius, 1f));
                var col = f.Color;
                if (!f.Sweep) col.a *= 1f - k;
                _tint[0] = col;
                _dis[0] = 0f;
                _mpb.Clear();
                _mpb.SetVectorArray(IdTint, _tint);
                _mpb.SetFloatArray(IdDissolve, _dis);
                Graphics.RenderMeshInstanced(rp, mesh, 0, _one, 1);
            }
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
