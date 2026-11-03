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
    //   쿨다운은 Core WeaponGrowth.Cooldown(쿨다운 0 인 독무는 tickSeconds 마다 초당 피해 x tickSeconds, 섬광지는 초당 4→9발).
    //   레벨 성장(연타·개수·관통·범위·새 동작)은 WeaponGrowth, 탄속·반경·수명·넉백·간격은 WeaponDef(weapons.json) 에서 읽는다(C-04b).
    //   능력치(범위·쿨감·투사체 수 등)는 Loadout/Stats 가 붙을 때(C-06) 곱한다. 지금은 모두 1.
    // 처음 8갈래: front_arc(삼재검법), melee_combo(나한권), orbit_slash(창천검기), aura(독무),
    //   homing_dagger(비도술), slow_projectile(장풍), random_rapid(섬광지), random_bomb(벽력탄).
    // 아직 없는 것: 절기(evolved) 모양, 중독·화상·느려짐 같은 적 상태, 넉백 저항.
    // 그림(V-04): 삼재검법은 구운 붓 획, 나머지는 ai.json AI 재료(나한권 fx_fist_ink, 창천검기 fx_sword_qi_glow, 독무 fx_mist_ink_3,
    //   비도 fx_dagger_ink, 장풍 fx_palm_ink, 섬광지 fx_finger_glow, 벽력탄 fx_bomb_ink_1·2 + fx_bomb_glow, 뇌부 fx_lightning_glow).
    //   재료가 없으면 옛 부채꼴·원으로. 모두 renderQueue 3002(먹)·3003(빛)이라 적 덩이(3000) 위에 그린다.
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

        [Header("붓 획(V-04). VfxBootstrap 이 채운다. 비면 옛 부채꼴로 그린다")]
        public Material brushMaterial;
        public Texture2D[] samjaeTier1;
        [Tooltip("텍스처 한 변(경지 m), samjae.json sizeMeters")]
        public float samjaeTier1Meters = 5.74f;
        [Tooltip("호가 펼친 각(라디안), samjae.json span")]
        public float samjaeTier1Span = 2.3f;

        [Header("뇌부 낙뢰(W-01). AI 재료 fx_lightning_glow: 먹 받침(보통 섞기) → 금빛·흰빛(더하기), 위에서 아래로 그어짐")]
        public Material brushAddMaterial;
        public Texture2D lightningShade, lightningGold, lightningCore;
        [Tooltip("텍스처 한 변(경지 m), ai.json sizeMeters")]
        public float lightningMeters = 4f;

        // AI 재료 한 장(ai.json 한 줄). 층: stroke·shade(보통 섞기, 먹) → gold(더하기, 금빛) → core(더하기, 흰빛)
        [Serializable]
        public sealed class AiFx
        {
            public string name;
            [Tooltip("텍스처 한 변(경지 m), ai.json sizeMeters")] public float meters = 2f;
            [Tooltip("셰이더 _Kind(radial 2·down 3·up 4·right 1·left 5), ai.json progress")] public float kind = 1f;
            public Texture2D stroke, shade, gold, core;
        }

        [Header("무공 AI 재료(V-04). VfxBootstrap 이 ai.json 전부를 채운다. 없는 무공은 옛 도형으로 그린다")]
        public List<AiFx> aiFx = new List<AiFx>();
        [Tooltip("이펙트를 적 덩이(renderQueue 3000) 위에 그린다")]
        public int fxRenderQueue = 3002;
        static readonly Color MistWash = new Color32(0x4F, 0x7F, 0x7A, 0xFF); // vfx.md wash 청회

        // 낙뢰 시간: 0.08초에 위에서 내리꽂히고, 0.06초 머문 뒤 0.36초까지 흩어진다(기획서 9장 '0.4초 안쪽')
        const float BoltDraw = 0.08f, BoltHold = 0.06f, BoltLife = 0.36f;

        // 획 시간(vfx.md: 0.4초 안에 흩어진다). 그어지기 → 잠깐 멈춤 → 흩어짐
        const float BrushDraw = 0.10f, BrushHold = 0.06f, BrushLife = 0.40f;
        static readonly Color StrokeInk = new Color(0.078f, 0.078f, 0.078f, 1f); // #141414

        static readonly string[] TestOrder =
        {
            "samjae_sword", "nahan_fist", "changcheon_qi", "poison_mist", "flying_dagger", "palm_wind", "flash_finger", "thunder_bomb", "thunder_talisman",
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
                _mat = new Material(material) { name = "무공 이펙트", enableInstancing = true, mainTexture = white, renderQueue = fxRenderQueue };
            }
            _mpb = new MaterialPropertyBlock();
            // 붓 획·AI 재료도 적 덩이 위로(같은 3000 이면 덩이 뒤에 깔리기도 했다). 공유 에셋은 건드리지 않게 복사본을 쓴다
            if (brushMaterial != null) _ink = new Material(brushMaterial) { name = "무공 획(먹)", renderQueue = fxRenderQueue };
            if (brushAddMaterial != null) _add = new Material(brushAddMaterial) { name = "무공 획(빛)", renderQueue = fxRenderQueue + 1 };
            foreach (var f in aiFx) if (f != null && !string.IsNullOrEmpty(f.name)) _ai[f.name] = f;
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
                if (kb.digit9Key.wasPressedThisFrame) Learn(TestOrder[8]);
            }

            float dt = Time.deltaTime;
            foreach (var s in slots)
            {
                s.Cooldown -= dt;
                if (s.Cooldown > 0f) continue;
                // 겨눌 적이 없으면 쿨다운을 쓰지 않고 다음 프레임에 다시 본다(뇌부)
                if (!Fire(s)) { s.Cooldown = 0f; continue; }
                // 쿨다운 0(독무처럼 상시)이면 tickSeconds 마다 한 번, 섬광지는 1 / shotsPerSecond(WeaponGrowth.Cooldown)
                float cd = s.Def.Cooldown <= 0f && s.Def.ShotsPerSecond == null ? Tick(s)
                         : Mathf.Max(0.05f, WeaponGrowth.Cooldown(s.Def, s.level));
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
            StepBrushes(dt);

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
        // 숫자는 모두 weapons.json(기획서 9장 'Lv8까지 성장'·'날아가는 것과 넉백'). 빠진 값의 기본은 옛 웹 값.
        static float Knock(Slot s) => s.Def.Knockback ?? 0f;
        static float Gap(Slot s, float fallback) => s.Def.GapSeconds ?? fallback;
        static float Tick(Slot s) => s.Def.TickSeconds ?? 0.5f;
        static bool Has(Slot s, string milestone) => WeaponGrowth.HasMilestone(s.Def, s.level, milestone);
        static WeaponShot ShotOf(Slot s) => s.Def.Shot ?? throw new InvalidOperationException($"{s.id} weapons.json 에 shot 이 없습니다");

        void Deal(Slot s, float dmg, int hit)
        {
            if (hit <= 0) return;
            _dealt.TryGetValue(s.id, out float d);
            _dealt[s.id] = d + dmg * hit;
        }

        /// <summary>한 번 쓴다. 겨눌 적이 없어 쓰지 않았으면 false(쿨다운을 쓰지 않는다).</summary>
        bool Fire(Slot s)
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
                    var dirs = Has(s, "앞뒤 베기") ? new[] { baseA, baseA + Mathf.PI } : new[] { baseA };
                    int strikes = Has(s, "3연 베기") ? 3 : 1;
                    float gap = Gap(s, 0.12f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < strikes; k++)
                        Later(k * gap, () =>
                        {
                            foreach (var a in dirs)
                            {
                                if (!AddBrush(s, a)) AddFx(a, Mathf.PI, R, 0.22f, new Color(0.95f, 0.93f, 0.85f, 0.75f));
                                Deal(s, dmg, HitArc(P, R, a, dmg, kb));
                            }
                        });
                    break;
                }
                case "melee_combo":
                {
                    float R = ws.Length(s.Def.Range);
                    int hits = WeaponGrowth.Hits(s.Def, s.level); // weapons.json hits [3, 5]
                    float gap = Gap(s, 0.08f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < hits; k++)
                        Later(k * gap, () =>
                        {
                            // 나한권: 바라보는 쪽 사거리 절반 자리에 주먹 먹 자국(fx_fist_ink), 연타마다 조금씩 흔들어 찍는다
                            float fa = Mathf.Atan2(player.Facing.y, player.Facing.x) + UnityEngine.Random.Range(-0.35f, 0.35f);
                            var fat = P + new Vector2(Mathf.Cos(fa), Mathf.Sin(fa)) * R * 0.55f;
                            if (!AddSpark("fx_fist_ink", fat, fa, 0f, 0.28f, 0.05f))
                                AddFx(Mathf.Atan2(player.Facing.y, player.Facing.x), Mathf.PI * 2f, R * 0.6f, 0.12f, new Color(0.85f, 0.55f, 0.25f, 0.6f));
                            enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                            Deal(s, dmg, enemies.Damage(_near, dmg, P, kb));
                        });
                    break;
                }
                case "orbit_slash":
                {
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level)); // Lv8 범위 +40%(areaLv8)
                    int sweeps = Has(s, "2회 회전") ? 2 : 1;
                    _fx.Add(new Fx
                    {
                        Angle = UnityEngine.Random.value * Mathf.PI * 2f, Span = 0.6f, Radius = R, Life = (s.Def.SweepSeconds ?? 0.36f) * sweeps,
                        Color = new Color(0.55f, 0.75f, 0.95f, 0.8f), Sweep = true, Sweeps = sweeps, Weapon = s, Damage = dmg, Hit = new HashSet<int>(),
                    });
                    break;
                }
                case "aura":
                {
                    float R = ws.Length(WeaponGrowth.Range(s.Def, s.level)); // range → rangeLv8(2 → 3.5m)
                    float tick = Tick(s);
                    float hitDmg = dmg * tick;                                 // tickSeconds 마다 초당 피해 x tickSeconds
                    // 독무: 몸 둘레 안개(fx_mist_ink_3, 가운데서 번짐)를 틱마다 새로 피워 두 장씩 겹치게, 청회 담채색으로 옅게
                    if (!AddSpark("fx_mist_ink_3", Vector2.zero, UnityEngine.Random.value * Mathf.PI * 2f, R * 2.3f, tick * 2.2f, tick * 0.8f, true, 0.55f, MistWash))
                        AddFx(0f, Mathf.PI * 2f, R, tick, new Color(0.37f, 0.50f, 0.29f, 0.22f));
                    enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                    Deal(s, hitDmg, enemies.Damage(_near, hitDmg, P, ws.Speed(Knock(s))));
                    break;
                }
                case "homing_dagger":
                {
                    // 비도: 가까운 적 n 명에게(projectiles 1→4), 관통 1→3, 탄속·반경·수명·따라가기는 shot(12·0.25·1.2·6)
                    int n = WeaponGrowth.Projectiles(s.Def, s.level);
                    int pierce = WeaponGrowth.Pierce(s.Def, s.level);
                    var shot = ShotOf(s);
                    float kb = ws.Speed(Knock(s));
                    NearestN(ws.Length(s.Def.Range * 1.4f), n, _targets);
                    for (int k = 0; k < n; k++)
                    {
                        int tg = _targets.Count > 0 ? _targets[k % _targets.Count] : -1;
                        var agents = enemies.Sim.Agents;
                        float a = tg >= 0 ? Mathf.Atan2(agents[tg].Pos.y - P.y, agents[tg].Pos.x - P.x)
                                          : Mathf.Atan2(player.Facing.y, player.Facing.x) + (k - n / 2f) * 0.25f;
                        a += UnityEngine.Random.Range(-0.08f, 0.08f);
                        Shoot(s, a, ws.Speed(shot.Speed), ws.Length(shot.Radius), dmg, pierce, shot.LifeSeconds, tg >= 0 ? agents[tg].Id : 0, shot.Homing, kb,
                              new Color(0.80f, 0.82f, 0.88f, 1f));
                    }
                    break;
                }
                case "slow_projectile":
                {
                    // 장풍: 바라보는 쪽으로 shot(초속 5m, 반경 0.55m x areaLv8 1.6, 2.6초), 다 뚫고(pierce 999), 넉백 5. Lv6 2연발
                    int shots = Has(s, "2연발") ? 2 : 1;
                    var shot = ShotOf(s);
                    int pierce = WeaponGrowth.Pierce(s.Def, s.level);
                    float gap = Gap(s, 0.15f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < shots; k++)
                        Later(k * gap, () =>
                        {
                            float r = ws.Length(shot.Radius * WeaponGrowth.Area(s.Def, s.level));
                            Shoot(s, Mathf.Atan2(player.Facing.y, player.Facing.x), ws.Speed(shot.Speed), r, dmg, pierce, shot.LifeSeconds, 0, shot.Homing, kb,
                                  new Color(0.95f, 0.85f, 0.55f, 0.55f));
                        });
                    break;
                }
                case "random_rapid":
                {
                    // 섬광지: 사거리 안 가까운 12명 가운데 하나에게 shot(초속 18m, 0.16m, 0.6초), 안 뚫음, 넉백 1.2. 쿨다운은 초당 4→9발
                    NearestN(ws.Length(s.Def.Range), 12, _targets);
                    if (_targets.Count == 0) break;
                    var shot = ShotOf(s);
                    var e = enemies.Sim.Agents[_targets[UnityEngine.Random.Range(0, _targets.Count)]];
                    Shoot(s, Mathf.Atan2(e.Pos.y - P.y, e.Pos.x - P.x), ws.Speed(shot.Speed), ws.Length(shot.Radius), dmg,
                          WeaponGrowth.Pierce(s.Def, s.level), shot.LifeSeconds, 0, shot.Homing, ws.Speed(Knock(s)),
                          new Color(1f, 0.95f, 0.6f, 1f));
                    break;
                }
                case "random_bomb":
                {
                    // 벽력탄: 7m 안 가까운 20명 가운데 무작위 자리(없으면 둘레 4m)에 0.6초 뒤 터짐, 반경 2→3m, 넉백 3.
                    // 개수 projectiles, 거리 seekRange, 퓨즈 fuseSeconds, 반경 range x areaLv8, 넉백 knockback(weapons.json).
                    // 화상(burnPerSecond·burnSeconds)은 적 상태 칸이 붙을 때(C-04 남은 것)
                    int n = WeaponGrowth.Projectiles(s.Def, s.level);
                    NearestN(ws.Length(s.Def.SeekRange ?? 7f), 20, _targets);
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level));
                    float fuse = s.Def.FuseSeconds ?? 0.6f, kb = ws.Speed(Knock(s));
                    for (int k = 0; k < n; k++)
                    {
                        Vector2 at;
                        if (_targets.Count > 0) { var e = enemies.Sim.Agents[_targets[UnityEngine.Random.Range(0, _targets.Count)]]; at = new Vector2(e.Pos.x, e.Pos.y); }
                        else at = P + new Vector2(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f)) * ws.Length(1f);
                        AddFxAt(at, 0f, Mathf.PI * 2f, R, fuse, new Color(0.70f, 0.15f, 0.12f, 0.18f)); // 떨어질 자리 표시
                        Later(fuse, () =>
                        {
                            // 벽력탄: 터진 자리에 먹 번짐(fx_bomb_ink_1·2 번갈아) 위로 불꽃 빛(fx_bomb_glow)
                            float rot = UnityEngine.Random.value * Mathf.PI * 2f;
                            bool ink = AddSpark(s.Fired % 2 == 0 ? "fx_bomb_ink_1" : "fx_bomb_ink_2", at, rot, R * 2.4f, 0.55f, 0.08f);
                            bool glow = AddSpark("fx_bomb_glow", at, rot, R * 2.2f, 0.35f, 0.06f);
                            if (!ink && !glow) AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.35f, new Color(0.95f, 0.55f, 0.20f, 0.7f));
                            enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
                            Deal(s, dmg, enemies.Damage(_near, dmg, at, kb));
                        });
                    }
                    break;
                }
                case "random_lightning":
                {
                    // 뇌부(W-01, 기획서 9장 '날아가는 것과 넉백'): range(9m) 안 서로 다른 적 projectiles(2→5) 곳에 바닥 표식,
                    // fuseSeconds(0.4초) 뒤 반경 strikeRadius(1.2m) 안을 친다. 넉백 0. 적이 없으면 쓰지 않는다(쿨다운도 안 씀).
                    // Lv5 '감전'부터 맞은 적이 shockSeconds(0.5초) 멈춘다. 보스(땅의 주인)는 EnemyField 무리에 없으니 따로 막을 것이 없다 —
                    // 보스가 무리에 들어오면 여기서 거른다. 연무장 개수·범위는 Life 가 붙을 때(C-06 남은 것)
                    int n = WeaponGrowth.Projectiles(s.Def, s.level);
                    NearestN(ws.Length(s.Def.Range), 40, _targets);
                    if (_targets.Count == 0) { s.Fired--; return false; }
                    // 가까운 40 가운데 서로 다른 n 을 무작위로(부분 섞기)
                    for (int k = 0; k < Mathf.Min(n, _targets.Count); k++)
                    {
                        int j = UnityEngine.Random.Range(k, _targets.Count);
                        (_targets[k], _targets[j]) = (_targets[j], _targets[k]);
                    }
                    float R = ws.Length(s.Def.StrikeRadius ?? 1.2f);
                    float fuse = s.Def.FuseSeconds ?? 0.4f, kb = ws.Speed(Knock(s));
                    float shock = Has(s, "감전") ? s.Def.ShockSeconds ?? 0.5f : 0f;
                    for (int k = 0; k < Mathf.Min(n, _targets.Count); k++)
                    {
                        var e = enemies.Sim.Agents[_targets[k]];
                        var at = new Vector2(e.Pos.x, e.Pos.y);
                        AddFxAt(at, 0f, Mathf.PI * 2f, R, fuse, new Color(0.79f, 0.64f, 0.15f, 0.22f)); // 떨어질 자리(금빛 표식)
                        Later(fuse, () =>
                        {
                            if (!AddBolt(at)) AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.25f, new Color(1f, 0.92f, 0.55f, 0.8f));
                            enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
                            if (shock > 0f)
                                foreach (int i in _near)
                                {
                                    var a = enemies.Sim.Get(i);
                                    a.Stun = Mathf.Max(a.Stun, shock);
                                    enemies.Sim.Set(i, a);
                                }
                            Deal(s, dmg, enemies.Damage(_near, dmg, at, kb));
                        });
                    }
                    break;
                }
                default:
                    break; // 아직 없는 갈래
            }
            return true;
        }

        // ── 투사체 ──────────────────────────────────────────────
        struct Proj
        {
            public Vector2 Pos, Vel;
            public float Radius, Damage, Life, Homing, Knock, Age;
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
                p.Age += dt;
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
            Deal(f.Weapon, f.Damage, enemies.Damage(_filtered, f.Damage, P, World.Speed(Knock(f.Weapon))));
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

        // ── 붓 획(V-04) ─────────────────────────────────────────
        struct Brush
        {
            public Texture2D Tex;
            public float Angle, Size, Span, Age;
            public Color Color;
        }

        readonly List<Brush> _brushes = new List<Brush>();
        Mesh _quad;
        MaterialPropertyBlock _brushProps;
        static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdProgress = Shader.PropertyToID("_Progress");
        static readonly int IdSpan = Shader.PropertyToID("_Span");
        static readonly int IdKind = Shader.PropertyToID("_Kind");
        static readonly int IdAlpha = Shader.PropertyToID("_Alpha");

        /// <summary>그 무공의 붓 획이 있으면 띄우고 true. 지금은 삼재검법 1단만(2·3단 텍스처와 담채·금빛 층은 V-05).</summary>
        bool AddBrush(Slot s, float angle)
        {
            if (brushMaterial == null || s.id != "samjae_sword" || samjaeTier1 == null || samjaeTier1.Length == 0) return false;
            var tex = samjaeTier1[s.Fired % samjaeTier1.Length];
            if (tex == null) return false;
            _brushes.Add(new Brush { Tex = tex, Angle = angle, Size = World.Length(samjaeTier1Meters), Span = samjaeTier1Span, Color = StrokeInk });
            return true;
        }

        void StepBrushes(float dt)
        {
            for (int i = _brushes.Count - 1; i >= 0; i--)
            {
                var b = _brushes[i];
                b.Age += dt;
                if (b.Age >= BrushLife) _brushes.RemoveAt(i);
                else _brushes[i] = b;
            }
        }

        void DrawBrushes()
        {
            if (_brushes.Count == 0 || _ink == null) return;
            if (_quad == null) _quad = MakeQuad();
            if (_brushProps == null) _brushProps = new MaterialPropertyBlock();
            var rp = new RenderParams(_ink) { matProps = _brushProps, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            var c = P;
            foreach (var b in _brushes)
            {
                float progress = Mathf.Clamp01(b.Age / BrushDraw);
                float dissolve = Mathf.Clamp01((b.Age - BrushDraw - BrushHold) / (BrushLife - BrushDraw - BrushHold));
                _brushProps.Clear();
                _brushProps.SetTexture(IdMainTex, b.Tex);
                _brushProps.SetColor(IdColor, b.Color);
                _brushProps.SetFloat(IdProgress, progress);
                _brushProps.SetFloat(IdDissolve, dissolve);
                _brushProps.SetFloat(IdSpan, b.Span);
                _brushProps.SetFloat(IdKind, 0f);
                _brushProps.SetFloat(IdAlpha, 1f);
                var m = Matrix4x4.TRS(new Vector3(c.x, c.y, -0.27f), Quaternion.Euler(0, 0, b.Angle * Mathf.Rad2Deg), new Vector3(b.Size, b.Size, 1f));
                Graphics.RenderMesh(rp, _quad, 0, m);
            }
        }

        // ── 낙뢰(W-01) ──────────────────────────────────────────
        // AiFxLayers 와 같은 층 순서(shade 보통 섞기 → gold·core 더하기)를 RenderMesh 로 바로 그린다(낙뢰마다 GameObject 를 만들지 않으려고).
        struct Bolt { public Vector2 At; public float Age; }
        readonly List<Bolt> _bolts = new List<Bolt>();
        static readonly Color BoltGold = new Color32(0xC9, 0xA2, 0x27, 0xFF);

        bool AddBolt(Vector2 at)
        {
            if (_ink == null || _add == null || lightningGold == null) return false;
            _bolts.Add(new Bolt { At = at });
            return true;
        }

        void DrawBolts(float dt)
        {
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                b.Age += dt;
                if (b.Age >= BoltLife) _bolts.RemoveAt(i); else _bolts[i] = b;
            }
            if (_bolts.Count == 0) return;
            if (_quad == null) _quad = MakeQuad();
            if (_brushProps == null) _brushProps = new MaterialPropertyBlock();
            float size = World.Length(lightningMeters);
            foreach (var b in _bolts)
            {
                float progress = Mathf.Clamp01(b.Age / BoltDraw);
                float dissolve = Mathf.Clamp01((b.Age - BoltDraw - BoltHold) / (BoltLife - BoltDraw - BoltHold));
                // 번개 아래 끝이 친 자리에 오게 텍스처 가운데를 조금 위로 올린다
                var m = Matrix4x4.TRS(new Vector3(b.At.x, b.At.y + size * 0.4f, -0.28f), Quaternion.identity, new Vector3(size, size, 1f));
                Layer(_ink, lightningShade, StrokeInk, progress, dissolve, m);
                Layer(_add, lightningGold, BoltGold, progress, dissolve, m);
                Layer(_add, lightningCore, Color.white, progress, dissolve, m);
            }
        }

        void Layer(Material mat, Texture2D tex, Color col, float progress, float dissolve, Matrix4x4 m, float kind = 3f, float alpha = 1f)
        {
            if (tex == null || mat == null) return;
            _brushProps.Clear();
            _brushProps.SetTexture(IdMainTex, tex);
            _brushProps.SetColor(IdColor, col);
            _brushProps.SetFloat(IdProgress, progress);
            _brushProps.SetFloat(IdDissolve, dissolve);
            _brushProps.SetFloat(IdKind, kind); // 뇌부는 ai.json progress "down"(3)
            _brushProps.SetFloat(IdAlpha, alpha);
            var rp = new RenderParams(mat) { matProps = _brushProps, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            Graphics.RenderMesh(rp, _quad, 0, m);
        }

        // ── 무공 AI 재료(V-04) ──────────────────────────────────
        // 무공마다 ai.json 재료 한 장을 붙인다(무공 → 재료 이름은 아래 표, ai.json use 줄과 같음).
        // 한 번 뜨는 것(Spark): 그어지기(draw) → 머묾 → 흩어짐(뒤 45%). 투사체는 날아가는 쪽으로 돌려 몸에 붙여 그린다.
        Material _ink, _add;
        readonly Dictionary<string, AiFx> _ai = new Dictionary<string, AiFx>();

        struct Spark
        {
            public AiFx Fx;
            public Vector2 Pos;      // Follow 면 플레이어 기준 자리
            public bool Follow;
            public float Angle, Size, Age, Life, Draw, Alpha;
            public Color Ink;
        }
        readonly List<Spark> _sparks = new List<Spark>();

        AiFx Ai(string name) => name != null && _ai.TryGetValue(name, out var f) && _ink != null ? f : null;

        /// <summary>재료가 있으면 한 번 띄우고 true. size 는 세계 m(0 이면 재료 sizeMeters 를 경지 m 로).</summary>
        bool AddSpark(string name, Vector2 pos, float angle, float size, float life, float draw, bool follow = false, float alpha = 1f, Color? ink = null)
        {
            var f = Ai(name);
            if (f == null) return false;
            _sparks.Add(new Spark
            {
                Fx = f, Pos = pos, Follow = follow, Angle = angle, Size = size > 0f ? size : World.Length(f.meters),
                Life = life, Draw = draw, Alpha = alpha, Ink = ink ?? StrokeInk,
            });
            return true;
        }

        void DrawSparks(float dt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Age += dt;
                if (s.Age >= s.Life) _sparks.RemoveAt(i); else _sparks[i] = s;
            }
            if (_sparks.Count == 0) return;
            var c = P;
            foreach (var s in _sparks)
            {
                float progress = s.Draw > 0f ? Mathf.Clamp01(s.Age / s.Draw) : 1f;
                float fadeFrom = Mathf.Max(s.Draw, s.Life * 0.55f);
                float dissolve = Mathf.Clamp01((s.Age - fadeFrom) / Mathf.Max(0.01f, s.Life - fadeFrom));
                var at = s.Follow ? c + s.Pos : s.Pos;
                DrawAi(s.Fx, at, s.Angle, s.Size, progress, dissolve, s.Alpha, s.Ink);
            }
        }

        void DrawAi(AiFx f, Vector2 at, float angle, float size, float progress, float dissolve, float alpha, Color ink)
        {
            if (_quad == null) _quad = MakeQuad();
            if (_brushProps == null) _brushProps = new MaterialPropertyBlock();
            var m = Matrix4x4.TRS(new Vector3(at.x, at.y, -0.28f), Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg), new Vector3(size, size, 1f));
            Layer(_ink, f.stroke, ink, progress, dissolve, m, f.kind, alpha);
            Layer(_ink, f.shade, StrokeInk, progress, dissolve, m, f.kind, alpha);
            Layer(_add, f.gold, BoltGold, progress, dissolve, m, f.kind, alpha);
            Layer(_add, f.core, Color.white, progress, dissolve, m, f.kind, alpha);
        }

        // 투사체 그림: 무공 → (재료, 길이 = 경지 m 또는 투사체 반경의 몇 배, 그어지는 시간)
        bool ProjFx(Slot s, out AiFx f, out float realmMeters, out float radiusTimes)
        {
            realmMeters = 0f; radiusTimes = 0f; f = null;
            switch (s.Def.Behavior)
            {
                case "homing_dagger": f = Ai("fx_dagger_ink"); realmMeters = 1.4f; break;   // 비도 궤적
                case "slow_projectile": f = Ai("fx_palm_ink"); radiusTimes = 3.6f; break;   // 장풍 손바람
                case "random_rapid": f = Ai("fx_finger_glow"); realmMeters = 1.8f; break;   // 섬광지 지풍
            }
            return f != null;
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "붓 획 사각형" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        void AddFx(float angle, float span, float radius, float life, Color c) =>
            _fx.Add(new Fx { Angle = angle, Span = span, Radius = radius, Life = life, Color = c });

        void AddFxAt(Vector2 at, float angle, float span, float radius, float life, Color c) =>
            _fx.Add(new Fx { Angle = angle, Span = span, Radius = radius, Life = life, Color = c, Fixed = true, Center = at });

        void LateUpdate()
        {
            if (player == null) return;
            DrawBrushes();
            DrawBolts(Time.deltaTime);
            DrawSparks(Time.deltaTime);
            var qi = Ai("fx_sword_qi_glow");
            // 창천검기: 도는 칼날 끝에 검기 한 장. 텍스처 위(+y)가 바깥을 보게 돌리고, 마지막 바퀴 끝 30% 에 흩어진다
            if (qi != null)
                foreach (var f in _fx)
                {
                    if (!f.Sweep) continue;
                    float a = f.Angle + f.Prev;
                    float dissolve = Mathf.Clamp01((f.Age / f.Life - 0.7f) / 0.3f);
                    var at = P + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * f.Radius * 0.62f;
                    DrawAi(qi, at, a - Mathf.PI / 2f, f.Radius * 0.95f, 1f, dissolve, 1f, StrokeInk);
                }
            // 투사체: 날아가는 쪽으로 돌린 재료. 머리가 투사체 자리에 오게 길이 0.3 만큼 뒤로 물린다
            foreach (var p in _projs)
            {
                if (!ProjFx(p.Weapon, out var pf, out float rm, out float rt)) continue;
                float size = rm > 0f ? World.Length(rm) : p.Radius * rt;
                var dir = p.Vel.sqrMagnitude > 1e-8f ? p.Vel.normalized : Vector2.right;
                float dissolve = Mathf.Clamp01(1f - p.Life / 0.15f);
                DrawAi(pf, p.Pos - dir * size * 0.3f, Mathf.Atan2(dir.y, dir.x), size, Mathf.Clamp01(p.Age / 0.06f), dissolve, 1f, StrokeInk);
            }
            if (_mat == null || (_fx.Count == 0 && _projs.Count == 0)) return;
            var rp = new RenderParams(_mat) { matProps = _mpb, layer = gameObject.layer, worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f) };
            foreach (var f in _fx)
            {
                if (f.Sweep && qi != null) continue;
                float k = f.Age / f.Life;
                float angle = f.Sweep ? f.Angle + f.Prev : f.Angle;
                var c = f.Fixed ? f.Center : P;
                var col = f.Color;
                if (!f.Sweep) col.a *= 1f - k;
                Draw(rp, Sector(f.Span), new Vector3(c.x, c.y, -0.25f), angle, f.Radius, col);
            }
            var disc = Sector(Mathf.PI * 2f);
            foreach (var p in _projs)
                if (!ProjFx(p.Weapon, out _, out _, out _))
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
            sb.Append(" (Ctrl+1~9 익히기, 9 뇌부)");
            GUI.Label(new Rect(12, 72, 900, 22), sb.ToString());
        }
    }
}
