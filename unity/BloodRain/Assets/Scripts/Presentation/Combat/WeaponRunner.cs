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
    //   능력치는 LifeRunner 가 넣는다(C-06a): 쾌속 cooldownReduction, 범위 areaMultiplier, 개수 extraAmount(비도·벽력탄·뇌부 개수에 더함),
    //   지속 durationMultiplier(장풍 수명, 기획서 9장 '장풍 수명은 지속').
    // 처음 8갈래: front_arc(삼재검법), melee_combo(나한권), orbit_slash(창천검기), aura(독무),
    //   homing_dagger(비도술), slow_projectile(장풍), random_rapid(섬광지), random_bomb(벽력탄).
    // 적 상태: 중독·화상은 EnemyField.AddDot·Damage(poisonSeconds), 느려짐은 EnemyAgent.Slow, 넉백 저항은 EnemyField.Damage(ST-02U).
    // 절기 모양은 C-09e 가 하나씩 붙이는 중.
    // 그림(V-04): 삼재검법은 구운 붓 획, 나머지는 ai.json AI 재료(나한권 fx_fist_ink, 창천검기 fx_sword_qi_glow, 독무 fx_mist_ink_3,
    //   비도 fx_dagger_ink, 장풍 fx_palm_ink, 섬광지 fx_finger_glow, 벽력탄 fx_bomb_ink_1·2 + fx_bomb_glow, 뇌부 fx_lightning_glow).
    //   재료가 없으면 옛 부채꼴·원으로. 모두 renderQueue 3002(먹)·3003(빛)이라 적 덩이(3000) 위에 그린다.
    public sealed class WeaponRunner : MonoBehaviour
    {
        // C-06a 몸 능력치(StatSheet). LifeRunner 가 넣는다. 쾌속은 쿨다운에, 범위는 휘두르는 반경·폭발 반경에 곱한다.
        // 개수는 여러 개를 쏘는 무공(비도술·벽력탄·뇌부)의 개수에 더하고, 지속은 장풍 수명에 곱한다(연무장 몫은 StatSheet 에 이미 들어 있음).
        [NonSerialized] public float cooldownReduction;
        [NonSerialized] public float areaMultiplier = 1f;
        [NonSerialized] public int extraAmount;
        [NonSerialized] public float durationMultiplier = 1f;
        // ST-02U 탄속(StatSheet.ProjectileSpeed, 연무장)은 무공 탄 속도에, 폭발 범위(1 + ExplosionArea, 화룡심법)는 벽력탄 폭발 반경에 곱한다
        [NonSerialized] public float projectileSpeedMultiplier = 1f;
        [NonSerialized] public float explosionAreaMultiplier = 1f;
        /// <summary>ST-02U 화상 배율(1 + BurnDamage, 화룡심법). 벽력탄 화상 초당 피해에 지속 피해 배율과 같이 곱한다.</summary>
        [NonSerialized] public float burnMultiplier = 1f;
        // C-06b 곱하는 진전 효과(SynergyEffects). 남궁 2 검 계열·개방 3 낮은 기혈은 한 방 피해에, 곤륜 3 강시는 맞는 적에 따라 곱한다.
        [NonSerialized] public BloodRain.Core.Build.SynergyEffects effects = BloodRain.Core.Build.SynergyEffects.None;
        [NonSerialized] public float healthRatio = 1f;

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
        [Tooltip("2단(첫 마일스톤부터): 본 획 + 담채(청회, 커지며 옅어짐). 순서는 stroke 와 같은 번호")]
        public Texture2D[] samjaeTier2, samjaeTier2Wash;
        public float samjaeTier2Meters = 6.456f, samjaeTier2Span = 2.4f;
        [Tooltip("3단(둘째 마일스톤부터): 본 획 + 담채 + 금빛 비백")]
        public Texture2D[] samjaeTier3, samjaeTier3Wash, samjaeTier3Gold;
        public float samjaeTier3Meters = 7.1f, samjaeTier3Span = 2.5f;

        // 붓 획 텍스처는 원래 512 칸에서 획 있는 쪽만 잘라 구웠다(V-10). 자리는 Brush/*.json crop → 셰이더 _Rect 꼴.
        [Serializable]
        public struct BrushCrop
        {
            [Tooltip("텍스처 이름(층 stroke·wash·gold 뺀 것), 예: samjae_t1_1")] public string name;
            [Tooltip("원래 칸 uv 에서 잘린 자리: 왼쪽 아래 x·y, 너비·높이(0~1)")] public Vector4 rect;
        }
        [Tooltip("VfxBootstrap 이 Brush/*.json crop 에서 채운다. 없는 이름은 자르지 않은 512 칸으로 본다")]
        public List<BrushCrop> brushCrops = new List<BrushCrop>();

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
        readonly List<float> _xs = new List<float>(), _ys = new List<float>();
        readonly Dictionary<int, float> _mistSeconds = new Dictionary<int, float>(); // C-09e 칠보단혼무: 적 Id → 안개 안에 머문 초
        readonly List<int> _executeIds = new List<int>();
        PlayerVitals _vitals; // 혈조 흡혈
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

            Hook();
            float dt = Time.deltaTime;
            foreach (var s in slots)
            {
                s.Cooldown -= dt;
                if (s.Cooldown > 0f) continue;
                // 겨눌 적이 없으면 쿨다운을 쓰지 않고 다음 프레임에 다시 본다(뇌부)
                if (!Fire(s)) { s.Cooldown = 0f; continue; }
                SkySword(s); // C-06c 남궁 3
                // 쿨다운 0(독무처럼 상시)이면 tickSeconds 마다 한 번, 섬광지는 1 / shotsPerSecond(WeaponGrowth.Cooldown)
                float cd = s.Def.Cooldown <= 0f && s.Def.ShotsPerSecond == null ? Tick(s)
                         : Mathf.Max(0.05f, WeaponGrowth.Cooldown(s.Def, s.level) * (1f - cooldownReduction)); // C-06a 쾌속
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
            StepNeedles(dt); // C-06c 당가 5

            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                var f = _fx[i];
                f.Age += dt;
                if (f.Sweep) SweepHits(ref f);
                if (f.Age >= f.Life) _fx.RemoveAt(i);
                else _fx[i] = f;
            }
        }

        float Damage(Slot s) => WeaponMath.DamageAt(s.Def, s.level) * effects.WeaponDamageMultiplier(s.Def, healthRatio)
            * (insightDamage != null ? insightDamage(s.id) : 1f) * EvoDamage(s) * AspectDamage(s);

        // YY-01U 음양 편향 쪽 속성 무공 +15%·극성 +40%(기획서 3장), 비전 무극이면 음·양 모두 그 값(Core YinYangGauge.AspectDamageMultiplier)
        static float AspectDamage(Slot s)
        {
            var lr = LifeRunner.Instance;
            if (lr == null || lr.Life == null) return 1f;
            var sw = Hud.SecretWatch.Instance;
            float? secret = sw != null ? sw.Effects.AspectDamageBonus(s.Def.Aspect) : null;
            return lr.Life.Loadout.Gauge.AspectDamageMultiplier(s.Def.Aspect, secret);
        }

        // C-09e 각성한 무공이면 그 절기(한 생의 Loadout.Evolved, C-09d 보갑이 깨움). 한 생이 없거나 아직이면 null
        static BloodRain.Core.Data.EvolutionDef EvoOf(Slot s)
        {
            var lr = LifeRunner.Instance;
            if (lr == null || lr.Life == null) return null;
            bool on = false;
            foreach (var id in lr.Life.Loadout.Evolved) if (id == s.id) { on = true; break; }
            if (!on) return null;
            foreach (var e in DataBoot.Data.Evolutions) if (e.Weapon == s.id) return e;
            return null;
        }

        // 절기 피해 배율(balance.json evolution.damageMultiplier) × 각성 여운(Core Awakening.DamageMultiplier, 깨어난 뒤 그 절기 1.5배)
        static float EvoDamage(Slot s)
        {
            var lr = LifeRunner.Instance;
            if (lr == null || lr.Life == null) return 1f;
            float m = lr.Life.Awakening.DamageMultiplier(s.id);
            if (EvoOf(s) != null) m *= DataBoot.Data.Balance.Evolution.DamageMultiplier;
            return m;
        }
        float RangeMul(Slot s) => insightRange != null ? insightRange(s.Def) : 1f;

        // C-07b 깨달음(InsightEffects): 일검파만 무공 피해, 이기어검 근접 범위. LifeRunner 가 넣는다
        [NonSerialized] public Func<string, float> insightDamage;
        [NonSerialized] public Func<BloodRain.Core.Data.WeaponDef, float> insightRange;
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
                    // C-09e 천지인 삼검(절기): 사거리 × rangeMultiplier, 조준 각과 ±120° 세 방향, waveEvery 번째마다 조준 쪽 관통 검기(웹 front_arc)
                    var evo = EvoOf(s);
                    float R = ws.Length(s.Def.Range * (evo != null ? evo.Param("rangeMultiplier", 1f) : 1f));
                    // 웹 결정: 사거리 1.25배 안에 적이 있으면 가장 가까운 적 쪽, 없으면 바라보는 쪽
                    float baseA = Nearest(R * 1.25f, out var dir) ? Mathf.Atan2(dir.y, dir.x) : Mathf.Atan2(player.Facing.y, player.Facing.x);
                    var dirs = EvolvedHits.FrontArcDirections(baseA, evo != null, Has(s, "앞뒤 베기"));
                    int strikes = Has(s, "3연 베기") ? 3 : 1;
                    float gap = Gap(s, 0.12f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < strikes; k++)
                        Later(k * gap, () =>
                        {
                            foreach (var a in dirs)
                            {
                                // 절기면 금빛 부채꼴을 붓 획 위에 한 겹 더(웹 gold)
                                if (!AddBrush(s, a) || evo != null) AddFx(a, Mathf.PI, R, 0.22f, evo != null ? new Color(1f, 0.82f, 0.38f, 0.55f) : new Color(0.95f, 0.93f, 0.85f, 0.75f));
                                Deal(s, dmg, HitArc(P, R, a, dmg, kb));
                            }
                        });
                    if (evo != null && EvolvedHits.SendsWave(evo, s.Fired))
                        Shoot(s, baseA, ws.Speed(evo.Param("waveSpeed")), ws.Length(evo.Param("waveRadius")), dmg * evo.Param("waveDamage"), 999,
                              evo.Param("waveLifeSeconds"), 0, 0f, kb, new Color(1f, 0.82f, 0.38f, 0.85f));
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
                            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(P.x, P.y), R); // C-09b 친 자리 안 등롱
                            Deal(s, dmg, enemies.Damage(_near, dmg, P, kb));
                        });
                    // C-09e 백보신권(절기): 조준 각 가운데로 projectiles 발 갈래 권경, 다 뚫음. 조준은 탄속 × 수명 안 가장 가까운 적(웹 melee_combo)
                    var fistEvo = EvoOf(s);
                    if (fistEvo != null)
                    {
                        float spd = fistEvo.Param("projectileSpeed"), life = fistEvo.Param("projectileLifeSeconds");
                        float a0 = Nearest(ws.Length(spd * life), out var fd) ? Mathf.Atan2(fd.y, fd.x) : Mathf.Atan2(player.Facing.y, player.Facing.x);
                        foreach (var a in EvolvedHits.FanAngles(fistEvo, a0))
                            Shoot(s, a, ws.Speed(spd), ws.Length(fistEvo.Param("projectileRadius")), dmg * fistEvo.Param("projectileDamage"), 999, life, 0, 0f, kb,
                                  new Color(1f, 0.82f, 0.38f, 0.8f));
                    }
                    break;
                }
                case "orbit_slash":
                {
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier * RangeMul(s)); // Lv8 범위 +40%(areaLv8)
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
                    var mistEvo = EvoOf(s); // C-09e 절기 칠보단혼무: 반경 × rangeMultiplier, 안에 executeSeconds 머문 일반 적 즉사
                    float R = ws.Length(WeaponGrowth.Range(s.Def, s.level) * (mistEvo != null ? mistEvo.Param("rangeMultiplier", 1f) : 1f)); // range → rangeLv8(2 → 3.5m)
                    float tick = Tick(s);
                    float hitDmg = dmg * tick;                                 // tickSeconds 마다 초당 피해 x tickSeconds
                    // 독무: 몸 둘레 안개(fx_mist_ink_3, 가운데서 번짐)를 틱마다 새로 피워 두 장씩 겹치게, 청회 담채색으로 옅게
                    if (!AddSpark("fx_mist_ink_3", Vector2.zero, UnityEngine.Random.value * Mathf.PI * 2f, R * 2.3f, tick * 2.2f, tick * 0.8f, true, 0.55f, MistWash))
                        AddFx(0f, Mathf.PI * 2f, R, tick, new Color(0.37f, 0.50f, 0.29f, 0.22f));
                    enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                    BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(P.x, P.y), R); // C-09b 친 자리 안 등롱
                    // ST-02U 독무 틱마다 중독 표시(poisonSeconds) — 당가 몸 '중독된 적 +20%'·독 번짐이 본다
                    // 칠보단혼무: 틱마다 안에 있는 적의 머문 초를 더해 두고(웹 e.poisonT), 맞힌 뒤 살아 있으면 처형. 정예·보스(Weight 0.1)는 안 됨
                    _executeIds.Clear();
                    if (mistEvo != null)
                    {
                        if (_mistSeconds.Count > 4096) _mistSeconds.Clear();
                        var ag = enemies.Sim.Agents;
                        foreach (int i in _near)
                        {
                            int id = ag[i].Id;
                            _mistSeconds.TryGetValue(id, out float t0);
                            float t1 = t0 + tick;
                            _mistSeconds[id] = t1;
                            if (EvolvedHits.PoisonExecutes(mistEvo, t1, ag[i].Weight < 0.5f)) _executeIds.Add(id);
                        }
                    }
                    Deal(s, hitDmg, enemies.Damage(_near, hitDmg, P, ws.Speed(Knock(s)), null, s.Def.PoisonSeconds ?? 0f));
                    foreach (int id in _executeIds)
                    {
                        int i = IndexOf(id);
                        if (i < 0) continue;
                        var at = enemies.Sim.Agents[i].Pos;
                        AddFxAt(new Vector2(at.x, at.y), 0f, Mathf.PI * 2f, ws.Length(0.6f), 0.4f, new Color(0.25f, 0.38f, 0.18f, 0.7f));
                        _mistSeconds.Remove(id);
                        enemies.Kill(i);
                    }
                    break;
                }
                case "homing_dagger":
                {
                    // 비도: 가까운 적 n 명에게(projectiles 1→4), 관통 1→3, 탄속·반경·수명·따라가기는 shot(12·0.25·1.2·6)
                    // C-09e 절기 무영비도: + extraProjectiles 자루, 자루마다 bounces 번 튕기고 늘 중독(웹 homing_dagger 절기)
                    var daggerEvo = EvoOf(s);
                    int n = WeaponGrowth.Projectiles(s.Def, s.level) + extraAmount + (daggerEvo != null ? (int)daggerEvo.Param("extraProjectiles", 0f) : 0); // C-06a 개수
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
                        int before = _projs.Count;
                        Shoot(s, a, ws.Speed(shot.Speed), ws.Length(shot.Radius), dmg, pierce, shot.LifeSeconds, tg >= 0 ? agents[tg].Id : 0, shot.Homing, kb,
                              daggerEvo != null ? new Color(0.62f, 0.80f, 0.95f, 0.75f) : new Color(0.80f, 0.82f, 0.88f, 1f));
                        if (daggerEvo != null && _projs.Count > before)
                        {
                            var pr = _projs[_projs.Count - 1];
                            pr.Bounce = (int)daggerEvo.Param("bounces"); pr.BounceEvo = daggerEvo; pr.Poison = true;
                            _projs[_projs.Count - 1] = pr;
                        }
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
                            float r = ws.Length(shot.Radius * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier);
                            Shoot(s, Mathf.Atan2(player.Facing.y, player.Facing.x), ws.Speed(shot.Speed), r, dmg, pierce, shot.LifeSeconds * durationMultiplier, 0, shot.Homing, kb,
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
                    // 화상(burnPerSecond·burnSeconds)은 터질 때 EnemyField.AddDot 으로(ST-02U)
                    int n = WeaponGrowth.Projectiles(s.Def, s.level) + extraAmount; // C-06a 개수
                    NearestN(ws.Length(s.Def.SeekRange ?? 7f), 20, _targets);
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier * explosionAreaMultiplier * RangeMul(s));
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
                            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(at.x, at.y), R); // C-09b 친 자리 안 등롱
                            // ST-02U 화상: 폭발 원 안 적에 burnPerSecond(Lv1→Lv8) × 화상 배율 × 지속 피해 배율, burnSeconds × 지속.
                            // 맞히기 전에 건다(맞혀 쓰러뜨리면 자리 번호가 바뀐다)
                            float burn = WeaponGrowth.At(s.Def.BurnPerSecond, s.level) * burnMultiplier * enemies.dotMultiplier;
                            enemies.AddDot(_near, (s.Def.BurnSeconds ?? 0f) * durationMultiplier, burn, false);
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
                    // 보스가 무리에 들어오면 여기서 거른다. 연무장 개수는 extraAmount 로 더함(C-06a)
                    // C-09e 절기 오뢰정법: 사거리 × rangeMultiplier 안 strikes(+개수) 곳, 반경 × radiusMultiplier, 늘 감전,
                    //   맞은 적마다 lightningRange 안 곁의 적으로 lightningChains 번(옮을 때 lightningFalloff 배) — Core EvolvedHits.FiveThunders
                    var thEvo = EvoOf(s);
                    int n = (thEvo != null ? (int)thEvo.Param("strikes") : WeaponGrowth.Projectiles(s.Def, s.level)) + extraAmount; // C-06a 개수
                    NearestN(ws.Length(s.Def.Range * (thEvo != null ? thEvo.Param("rangeMultiplier", 1f) : 1f)), Mathf.Max(40, n * 4), _targets);
                    if (_targets.Count == 0) { s.Fired--; return false; }
                    // 가까운 40 가운데 서로 다른 n 을 무작위로(부분 섞기)
                    for (int k = 0; k < Mathf.Min(n, _targets.Count); k++)
                    {
                        int j = UnityEngine.Random.Range(k, _targets.Count);
                        (_targets[k], _targets[j]) = (_targets[j], _targets[k]);
                    }
                    float R = ws.Length((s.Def.StrikeRadius ?? 1.2f) * (thEvo != null ? thEvo.Param("radiusMultiplier", 1f) : 1f));
                    float fuse = s.Def.FuseSeconds ?? 0.4f, kb = ws.Speed(Knock(s));
                    float shock = (thEvo != null || Has(s, "감전")) ? s.Def.ShockSeconds ?? 0.5f : 0f;
                    for (int k = 0; k < Mathf.Min(n, _targets.Count); k++)
                    {
                        var e = enemies.Sim.Agents[_targets[k]];
                        var at = new Vector2(e.Pos.x, e.Pos.y);
                        AddFxAt(at, 0f, Mathf.PI * 2f, R, fuse, new Color(0.79f, 0.64f, 0.15f, 0.22f)); // 떨어질 자리(금빛 표식)
                        Later(fuse, () =>
                        {
                            if (!AddBolt(at)) AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.25f, new Color(1f, 0.92f, 0.55f, 0.8f));
                            enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
                            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(at.x, at.y), R); // C-09b 친 자리 안 등롱
                            if (shock > 0f)
                                foreach (int i in _near)
                                {
                                    var a = enemies.Sim.Get(i);
                                    a.Stun = Mathf.Max(a.Stun, shock);
                                    enemies.Sim.Set(i, a);
                                }
                            if (thEvo != null) PlanThunderChains(thEvo, at, R);
                            Deal(s, dmg, enemies.Damage(_near, dmg, at, kb));
                            if (thEvo != null) StrikeThunderChains(s, dmg, shock);
                        });
                    }
                    break;
                }
                case "random_arc":
                {
                    // 취선봉(R-12Ua, 9장): 무작위 방향 큰 원호, 큰 넉백. 휘두르기 hits(1→3)회를 gapSeconds 간격, 범위 range × areaLv8.
                    // 절기 팔선과해: 한 번에 directions 갈래 사방, 맞은 적 stunSeconds 기절(웹 random_arc, Core WeaponShapes.ArcDirections)
                    var evo = EvoOf(s);
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier * RangeMul(s));
                    int swings = evo != null ? 1 : WeaponGrowth.Hits(s.Def, s.level);
                    int dirN = evo != null ? Mathf.RoundToInt(evo.Param("directions", 8f)) : 1;
                    float stun = evo != null ? evo.Param("stunSeconds") : 0f;
                    float gap = Gap(s, 0.18f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < swings; k++)
                        Later(k * gap, () =>
                        {
                            foreach (var a in WeaponShapes.ArcDirections(evo != null, dirN, () => UnityEngine.Random.value))
                            {
                                AddFx(a, Mathf.PI, R, 0.26f, evo != null ? new Color(1f, 0.82f, 0.38f, 0.55f) : new Color(0.80f, 0.62f, 0.38f, 0.7f));
                                Deal(s, dmg, HitArc(P, R, a, dmg, kb, stun));
                            }
                        });
                    break;
                }
                case "orbiting_blades":
                {
                    // 태극검(R-12Ua, 9장): 검 projectiles(2→5)+개수 자루가 duration(3→5초) × 지속 동안 몸 둘레 range 를 돈다.
                    // 같은 적은 Trial.BladeRehitSeconds 마다 한 번(Core WeaponShapes.BladeAngle·BladeCanRehit, 웹 orbiting_blades).
                    // 절기 태극양의검: 흑백 검 swords 자루가 쿨마다 끊김 없이, 흰 검(짝수)만 밀고, 태극 구간이면 피해 × (1 + taijiDamageBonus)
                    var evo = EvoOf(s);
                    int n = (evo != null ? Mathf.RoundToInt(evo.Param("swords", 8f)) : WeaponGrowth.Projectiles(s.Def, s.level)) + extraAmount;
                    float R = ws.Length(s.Def.Range * areaMultiplier * RangeMul(s)), bladeR = ws.Length(WeaponShapes.Trial.BladeRadius);
                    const float step = 0.1f;
                    float life = evo != null ? Mathf.Max(WeaponGrowth.At(s.Def.Duration, 1, 3f) * durationMultiplier, WeaponGrowth.Cooldown(s.Def, s.level) + step)
                                             : WeaponGrowth.At(s.Def.Duration, s.level, 3f) * durationMultiplier;
                    var lr = LifeRunner.Instance;
                    bool harmony = lr != null && lr.Life != null && lr.Life.Loadout.Gauge.Band == BloodRain.Core.Progression.YinYangBand.Harmony;
                    float bdmg = evo != null && harmony ? dmg * (1f + evo.Param("taijiDamageBonus")) : dmg;
                    float a0 = UnityEngine.Random.value * Mathf.PI * 2f, kb = ws.Speed(Knock(s));
                    var last = new Dictionary<int, float>();
                    var push = new List<int>(); var still = new List<int>();
                    for (int k = 0; k * step < life; k++)
                    {
                        float now = k * step;
                        Later(now, () =>
                        {
                            push.Clear(); still.Clear();
                            var agents = enemies.Sim.Agents;
                            for (int i = 0; i < n; i++)
                            {
                                float a = WeaponShapes.BladeAngle(a0, now, i, n);
                                var at = P + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R;
                                AddFxAt(at, 0f, Mathf.PI * 2f, bladeR, step * 1.2f,
                                    evo == null ? new Color(0.36f, 0.46f, 0.60f, 0.85f) : i % 2 == 0 ? new Color(0.96f, 0.94f, 0.88f, 0.9f) : new Color(0.12f, 0.12f, 0.14f, 0.9f));
                                enemies.Sim.QueryCircle(new float2(at.x, at.y), bladeR, _near);
                                BloodRain.Presentation.Fx.LanternLayer.Strike(at, bladeR); // C-09b 친 자리 안 등롱
                                foreach (int j in _near)
                                {
                                    int id = agents[j].Id;
                                    float? prev = last.TryGetValue(id, out var t0) ? t0 : (float?)null;
                                    if (!WeaponShapes.BladeCanRehit(prev, now) || push.Contains(j) || still.Contains(j)) continue;
                                    last[id] = now;
                                    (WeaponShapes.BladePushes(evo != null, i) ? push : still).Add(j);
                                }
                            }
                            // 검은 검(안 미는 쪽)은 id 로 들고 있다가, 미는 쪽 피해(쓰러짐으로 자리 번호가 바뀜) 뒤 번호를 다시 찾는다
                            for (int q = 0; q < still.Count; q++) still[q] = agents[still[q]].Id;
                            if (push.Count > 0) Deal(s, bdmg, enemies.Damage(push, bdmg, P, kb));
                            if (still.Count > 0)
                            {
                                push.Clear();
                                foreach (int id in still) { int j = IndexOf(id); if (j >= 0) push.Add(j); }
                                if (push.Count > 0) Deal(s, bdmg, enemies.Damage(push, bdmg, P, 0f));
                            }
                        });
                    }
                    break;
                }
                case "pull_chain":
                {
                    // 쇄겸술(R-12Ua, 9장): 사거리 안 가장 먼 적 projectiles(1→3)+개수에게 사슬낫. 맞은 적은 몸에서 Trial.PullTo 까지 끌려오고
                    // slow(0.3→0.5) 둔화(EnemyAgent.Slow, enemyStatus.slowFadePerSecond 로 풀림). 넉백 0. 정예·보스(Weight 0.1)는 안 끌림.
                    // Core WeaponShapes.FarthestWithin, 끌려가는 자리는 PulledTo 와 같은 셈을 세계 m 로(웹 pull_chain). 절기 흑룡탄은 BlackDragon(C-09e)
                    int n = WeaponGrowth.Projectiles(s.Def, s.level) + extraAmount;
                    float R = ws.Length(s.Def.Range * areaMultiplier * RangeMul(s));
                    float slow = WeaponGrowth.At(s.Def.Slow, s.level, 0f);
                    var chainEvo = EvoOf(s);
                    if (chainEvo != null) return BlackDragon(s, chainEvo, dmg, slow);
                    enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                    if (_near.Count == 0) { s.Fired--; return false; }
                    var agents = enemies.Sim.Agents;
                    _xs.Clear(); _ys.Clear();
                    foreach (int i in _near) { _xs.Add(agents[i].Pos.x); _ys.Add(agents[i].Pos.y); }
                    var far = WeaponShapes.FarthestWithin(P.x, P.y, _xs, _ys, R, n);
                    float pull = ws.Length(WeaponShapes.Trial.PullTo);
                    _filtered.Clear();
                    foreach (int k in far)
                    {
                        int i = _near[k];
                        var a = enemies.Sim.Get(i);
                        AddFxAt(new Vector2(a.Pos.x, a.Pos.y), 0f, Mathf.PI * 2f, a.Radius + ws.Length(0.2f), 0.2f, new Color(0.22f, 0.20f, 0.24f, 0.75f)); // 사슬이 걸린 자리
                        a.Slow = Mathf.Max(a.Slow, slow);
                        if (a.Weight >= 0.5f)
                        {
                            // PulledTo 는 경지 m 의 Trial.PullTo 를 쓰니 세계 m 로 같은 셈을 한다
                            float dx = a.Pos.x - P.x, dy = a.Pos.y - P.y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
                            float kk = Mathf.Min(1f, pull / d);
                            a.Pos = new float2(P.x + dx * kk, P.y + dy * kk);
                        }
                        enemies.Sim.Set(i, a);
                        if (!_filtered.Contains(i)) _filtered.Add(i);
                    }
                    Deal(s, dmg, enemies.Damage(_filtered, dmg, P, 0f));
                    break;
                }
                case "forward_claws":
                {
                    // 혈조(R-12Ua, 9장): 조준(사거리 1.25배 안 가까운 적, 없으면 바라보는 쪽) 가운데로 projectiles(3→5) 갈래 할퀴기,
                    // 준 피해의 lifesteal(1→2%) 만큼 기혈 회복. 같은 적은 한 번 쏠 때 한 갈래에만(Core WeaponShapes.ClawAngles·ClawHits, 웹 forward_claws).
                    // 절기 혈마수: 흡혈 lifesteal(4%), 극음이면 범위 × extremeYinRangeMultiplier. 넉백은 몸에서 바깥으로(웹은 갈래 방향)
                    var evo = EvoOf(s);
                    var lr = LifeRunner.Instance;
                    var gauge = lr != null && lr.Life != null ? lr.Life.Loadout.Gauge : null;
                    bool exYin = evo != null && gauge != null && gauge.Band == BloodRain.Core.Progression.YinYangBand.Extreme && gauge.Value < 0;
                    int n = WeaponGrowth.Projectiles(s.Def, s.level);
                    float R = ws.Length(s.Def.Range * areaMultiplier * RangeMul(s) * (exYin ? evo.Param("extremeYinRangeMultiplier", 1f) : 1f));
                    float ls = evo != null ? evo.Param("lifesteal") : WeaponGrowth.At(s.Def.Lifesteal, s.level, 0f);
                    float baseA = Nearest(R * 1.25f, out var cdir) ? Mathf.Atan2(cdir.y, cdir.x) : Mathf.Atan2(player.Facing.y, player.Facing.x);
                    float width = ws.Length(WeaponShapes.Trial.ClawWidth);
                    enemies.Sim.QueryCircle(new float2(P.x, P.y), R + width, _near);
                    var agents = enemies.Sim.Agents;
                    _filtered.Clear();
                    foreach (var a in WeaponShapes.ClawAngles(baseA, n))
                    {
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        AddFxAt(P + new Vector2(ca, sa) * R * 0.5f, a, 0.25f, R * 0.5f, 0.14f, evo != null ? new Color(0.62f, 0.08f, 0.10f, 0.8f) : new Color(0.45f, 0.10f, 0.12f, 0.7f));
                        foreach (int i in _near)
                        {
                            if (_filtered.Contains(i)) continue;
                            var e = agents[i];
                            // ClawHits 는 경지 m 의 Trial.ClawWidth 를 쓰니 세계 m 로 같은 셈
                            float dx = e.Pos.x - P.x, dy = e.Pos.y - P.y, along = dx * ca + dy * sa, perp = Mathf.Abs(-dx * sa + dy * ca);
                            if (along > 0f && along < R + e.Radius && perp < width + e.Radius) _filtered.Add(i);
                        }
                    }
                    BloodRain.Presentation.Fx.LanternLayer.Strike(P + new Vector2(Mathf.Cos(baseA), Mathf.Sin(baseA)) * R * 0.5f, R * 0.5f); // C-09b 친 자리 안 등롱
                    int got = enemies.Damage(_filtered, dmg, P, ws.Speed(Knock(s)));
                    Deal(s, dmg, got);
                    if (got > 0 && ls > 0f)
                    {
                        if (_vitals == null) _vitals = FindFirstObjectByType<PlayerVitals>();
                        if (_vitals != null) _vitals.Heal(dmg * ls * got * effects.LifestealMultiplier); // ST-02U 마도 2 흡혈 x2
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
            public bool Petal; // 화산 3 꽃잎(Weapon 없음)
            public HashSet<int> Hit;
            public int Bounce;                                  // C-09e 무영비도: 남은 튕김 수
            public BloodRain.Core.Data.EvolutionDef BounceEvo;  // 튕김 거리(bounceRange)를 읽는 절기
            public bool Poison;                                 // 절기면 '중독' 문턱 없이도 독
        }

        readonly List<Proj> _projs = new List<Proj>();
        readonly List<int> _targets = new List<int>();
        readonly List<int> _one1 = new List<int>(1);

        void Shoot(Slot s, float angle, float speed, float radius, float dmg, int pierce, float life, int targetId, float homing, float knock, Color c)
        {
            if (_projs.Count >= DataBoot.Data.Balance.Limits.Projectiles) return;
            _projs.Add(new Proj
            {
                Pos = P, Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * (s != null ? projectileSpeedMultiplier : 1f), Radius = radius, Damage = dmg, Life = life,
                Homing = homing, Knock = knock, Pierce = pierce, Target = targetId, Weapon = s, Color = c, Hit = new HashSet<int>(),
            });
        }

        /// <summary>R-15U 팔방풍우: 무공 칸 없이 장풍 탄 한 발(다 뚫음). angle 라디안, 속도·반경·넉백은 축척을 곱한 세계 값.</summary>
        public void ShootFree(float angle, float speed, float radius, float dmg, float life, float knock) =>
            Shoot(null, angle, speed, radius, dmg, 999, life, 0, 0f, knock, new Color(0.95f, 0.85f, 0.55f, 0.7f));
        /// <summary>바라보는 쪽(단위 벡터).</summary>
        public Vector2 Facing => player != null ? new Vector2(player.Facing.x, player.Facing.y) : Vector2.right;
        /// <summary>시험용: 날고 있는 탄 수.</summary>
        public int ProjectileCount => _projs.Count;

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
                BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(p.Pos.x, p.Pos.y), p.Radius); // C-09b 친 자리 안 등롱
                bool dead = p.Life <= 0f;
                foreach (int j in _near)
                {
                    if (dead) break;
                    int id = sim.Agents[j].Id;
                    if (p.Hit.Contains(id)) continue;
                    p.Hit.Add(id);
                    _one1.Clear();
                    _one1.Add(j);
                    _petalHit = p.Petal;
                    // ST-02U 비도술 독: 문턱 '중독'부터 맞힌 피해 × poisonDamage 를 poisonSeconds 동안 초당 지속 피해로(맞히기 전에 건다)
                    if (p.Weapon != null && p.Weapon.Def.PoisonDamage is float pd && (p.Poison || Has(p.Weapon, "중독")))
                        enemies.AddDot(_one1, p.Weapon.Def.PoisonSeconds ?? 0f, p.Damage * pd * enemies.dotMultiplier, true);
                    int got = enemies.Damage(_one1, p.Damage, p.Pos, p.Knock);
                    _petalHit = false;
                    if (p.Weapon != null) Deal(p.Weapon, p.Damage, got);
                    // C-09e 무영비도: 맞힌 자리에서 bounceRange 안 아직 안 맞힌 가장 가까운 적으로 튕김(관통을 안 씀). 웹 pr.bounce
                    if (p.Bounce > 0 && p.BounceEvo != null)
                    {
                        p.Bounce--;
                        int nx = BounceTarget(p);
                        if (nx >= 0)
                        {
                            var ne = sim.Agents[nx];
                            p.Vel = (new Vector2(ne.Pos.x, ne.Pos.y) - p.Pos).normalized * p.Vel.magnitude;
                            p.Target = ne.Id;
                            p.Life = Mathf.Max(p.Life, 0.6f);
                            AddFxAt(p.Pos, 0f, Mathf.PI * 2f, p.Radius * 2.5f, 0.15f, new Color(0.70f, 0.85f, 0.95f, 0.6f));
                            break;
                        }
                    }
                    if (p.Pierce-- <= 0) dead = true;
                    break; // 한 프레임에 하나(쓰러짐으로 자리 번호가 바뀌니 다음 프레임에 다시 찾는다)
                }
                if (dead) _projs.RemoveAt(i);
                else _projs[i] = p;
            }
        }

        readonly List<int> _bounceNear = new List<int>(), _bounceEx = new List<int>();
        readonly List<float> _bxs = new List<float>(), _bys = new List<float>();

        /// <summary>C-09e 무영비도 다음 튕길 적(시뮬 자리 번호, 없으면 -1). Core EvolvedHits.NextBounce 는 경지 m 라 세계 좌표를 축척으로 나눠 넘긴다.</summary>
        int BounceTarget(Proj p)
        {
            var ws = World;
            float unit = ws.Length(1f);
            enemies.Sim.QueryCircle(new float2(p.Pos.x, p.Pos.y), p.BounceEvo.Param("bounceRange") * unit, _bounceNear);
            var agents = enemies.Sim.Agents;
            _bxs.Clear(); _bys.Clear(); _bounceEx.Clear();
            for (int k = 0; k < _bounceNear.Count; k++)
            {
                var a = agents[_bounceNear[k]];
                _bxs.Add(a.Pos.x / unit); _bys.Add(a.Pos.y / unit);
                if (p.Hit.Contains(a.Id) || a.Health <= 0f) _bounceEx.Add(k);
            }
            int got = EvolvedHits.NextBounce(p.BounceEvo, p.Pos.x / unit, p.Pos.y / unit, _bxs, _bys, _bounceEx);
            return got >= 0 ? _bounceNear[got] : -1;
        }

        // ── C-09e 흑룡탄 ──
        readonly List<int> _gather = new List<int>();

        /// <summary>절기 흑룡탄(웹 pull_chain 절기): gatherRadius 안 적을 gatherSeconds 동안 몸 앞 Trial.PullTo 한곳으로 모은 뒤
        /// (정예·보스는 안 끌림) 반경 explosionRadius 폭발, 한 방의 damageMultiplier 배. 모으는 칸은 Core WeaponShapes.GatherSteps·GatherFraction.</summary>
        bool BlackDragon(Slot s, BloodRain.Core.Data.EvolutionDef evo, float dmg, float slow)
        {
            var ws = World;
            float GR = ws.Length(evo.Param("gatherRadius") * areaMultiplier);
            enemies.Sim.QueryCircle(new float2(P.x, P.y), GR, _gather);
            if (_gather.Count == 0) { s.Fired--; return false; }
            var f = Facing;
            var (cxk, cyk) = WeaponShapes.GatherCenter(0f, 0f, f.x, f.y); // 몸에서 경지 m 만큼 앞
            Vector2 c = P + new Vector2(cxk, cyk) * ws.Length(1f);
            float secs = evo.Param("gatherSeconds");
            int steps = WeaponShapes.GatherSteps(secs);
            // 사슬: 몸에서 가까운 적 여덟에게 어두운 줄기
            var agents = enemies.Sim.Agents;
            int shown = 0;
            foreach (int i in _gather)
            {
                if (shown++ >= 8) break;
                var e = agents[i]; Vector2 d = new Vector2(e.Pos.x, e.Pos.y) - P;
                AddFxAt(P, Mathf.Atan2(d.y, d.x), 0.05f, d.magnitude, secs, new Color(0.12f, 0.10f, 0.16f, 0.8f));
            }
            AddFxAt(c, 0f, Mathf.PI * 2f, ws.Length(0.8f), secs, new Color(0.15f, 0.08f, 0.20f, 0.35f)); // 모이는 자리
            for (int k = 1; k <= steps; k++)
            {
                int kk = k;
                Later(kk * secs / steps, () =>
                {
                    float frac = WeaponShapes.GatherFraction(kk, steps);
                    enemies.Sim.QueryCircle(new float2(c.x, c.y), GR, _gather);
                    foreach (int i in _gather)
                    {
                        var a = enemies.Sim.Get(i);
                        if (a.Weight < 0.5f || a.Health <= 0f) continue;
                        a.Pos = new float2(a.Pos.x + (c.x - a.Pos.x) * frac, a.Pos.y + (c.y - a.Pos.y) * frac);
                        a.Slow = Mathf.Max(a.Slow, slow);
                        enemies.Sim.Set(i, a);
                    }
                    if (kk != steps) return;
                    float ER = ws.Length(evo.Param("explosionRadius") * areaMultiplier * explosionAreaMultiplier);
                    float rot = UnityEngine.Random.value * Mathf.PI * 2f;
                    bool ink = AddSpark("fx_bomb_ink_1", c, rot, ER * 2.4f, 0.55f, 0.08f, false, 1f, new Color(0.10f, 0.06f, 0.14f, 1f));
                    if (!ink) AddFxAt(c, 0f, Mathf.PI * 2f, ER, 0.35f, new Color(0.20f, 0.10f, 0.28f, 0.75f));
                    enemies.Sim.QueryCircle(new float2(c.x, c.y), ER, _gather);
                    BloodRain.Presentation.Fx.LanternLayer.Strike(c, ER);
                    float hit = dmg * evo.Param("damageMultiplier", 1f);
                    Deal(s, hit, enemies.Damage(_gather, hit, c, 0f));
                });
            }
            return true;
        }

        // ── C-09e 오뢰정법 번개 옮김 ──
        readonly List<int> _thCand = new List<int>(), _thStruck = new List<int>();
        readonly List<(int id, float scale, Vector2 from)> _thHops = new List<(int, float, Vector2)>();

        /// <summary>낙뢰 한 곳(_near 가 맞은 적)에서 옮아갈 적을 맞히기 전에 정해 둔다(맞혀 쓰러뜨리면 자리 번호가 바뀌니 Id 로).</summary>
        void PlanThunderChains(BloodRain.Core.Data.EvolutionDef evo, Vector2 at, float R)
        {
            var ws = World;
            _thHops.Clear();
            if (_near.Count == 0) return;
            float unit = ws.Length(1f);
            float reach = R + evo.Param("lightningRange") * unit * Mathf.Max(1f, evo.Param("lightningChains"));
            enemies.Sim.QueryCircle(new float2(at.x, at.y), reach, _thCand);
            var agents = enemies.Sim.Agents;
            _bxs.Clear(); _bys.Clear(); _thStruck.Clear();
            for (int k = 0; k < _thCand.Count; k++)
            {
                var a = agents[_thCand[k]];
                _bxs.Add(a.Pos.x / unit); _bys.Add(a.Pos.y / unit);
                if (_near.Contains(_thCand[k])) _thStruck.Add(k);
            }
            foreach (var h in EvolvedHits.FiveThunders(evo, _thStruck, _bxs, _bys))
                _thHops.Add((agents[_thCand[h.To]].Id, h.DamageScale, new Vector2(_bxs[h.From] * unit, _bys[h.From] * unit)));
        }

        void StrikeThunderChains(Slot s, float dmg, float shock)
        {
            var ws = World;
            foreach (var (id, scale, from) in _thHops)
            {
                int i = IndexOf(id);
                if (i < 0) continue;
                var a = enemies.Sim.Get(i);
                var to = new Vector2(a.Pos.x, a.Pos.y);
                // 옮아가는 줄기: 앞 적에서 다음 적 쪽으로 가는 금빛 부채꼴(아주 좁게), 다음 적 자리에 작은 낙뢰
                Vector2 d = to - from;
                AddFxAt(from, Mathf.Atan2(d.y, d.x), 0.06f, Mathf.Max(d.magnitude, ws.Length(0.2f)), 0.2f, new Color(1f, 0.90f, 0.45f, 0.85f));
                if (!AddBolt(to)) AddFxAt(to, 0f, Mathf.PI * 2f, ws.Length(0.5f), 0.2f, new Color(1f, 0.92f, 0.55f, 0.8f));
                if (shock > 0f) { a.Stun = Mathf.Max(a.Stun, shock); enemies.Sim.Set(i, a); }
                _one1.Clear(); _one1.Add(i);
                Deal(s, dmg * scale, enemies.Damage(_one1, dmg * scale, from, 0f));
            }
            _thHops.Clear();
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
            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(P.x, P.y), f.Radius); // C-09b 친 자리 안 등롱
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

        int HitArc(Vector2 c, float R, float a, float dmg, float knock, float stun = 0f)
        {
            enemies.Sim.QueryCircle(new float2(c.x, c.y), R, _near);
            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(c.x, c.y), R); // C-09b 친 자리 안 등롱
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var agents = enemies.Sim.Agents;
            _filtered.Clear();
            foreach (int i in _near)
            {
                var e = agents[i];
                float dx = e.Pos.x - c.x, dy = e.Pos.y - c.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (dx * ca + dy * sa >= -0.1f * d) _filtered.Add(i); // 웹과 같은 반원 판정
            }
            if (stun > 0f) // 팔선과해 기절: 피해(쓰러짐으로 자리 번호가 바뀜) 전에 건다. 땅의 주인은 무리에 없다
                foreach (int i in _filtered)
                {
                    var e = enemies.Sim.Get(i);
                    e.Stun = Mathf.Max(e.Stun, stun);
                    enemies.Sim.Set(i, e);
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

        // ── C-06c 모양 있는 진전 효과(SynergyEffects.Tier 의 params, 숫자 뜻은 SynergyEffects.cs 머리 주석) ──
        // 남궁 3 하늘 검기(sky_sword), 당가 5 독침(needle_rain). 수로채 3 끌기는 PlayerController.Drag.
        // 화산 3 꽃잎은 아래 OnCrit(치명타는 EnemyField.Damage), 당가 3 독 번짐·독침 중독은 EnemyField 의 중독 상태(StepStatus).
        // 독침 damage 는 '× 공력' 인데 무공 한 방에도 공력이 아직 안 곱해져 params 값 그대로 쓴다.
        int _swordAttacks;
        float _needleLeft = -1f;
        static readonly Color NeedleInk = new Color(0.20f, 0.42f, 0.16f, 1f);

        void SkySword(Slot s)
        {
            var t = effects.Tier("sky_sword");
            if (t == null || s.Def.Category != "sword") return;
            _swordAttacks++;
            int every = Mathf.Max(1, Mathf.RoundToInt(t.Param("everyAttacks")));
            if (_swordAttacks % every != 0) return;
            var ws = World;
            if (!Nearest(ws.Length(t.Param("searchRange")), out var dir)) return;
            var at = P + dir;
            float R = ws.Length(t.Param("radius")) * areaMultiplier;
            float dmg = Damage(s) * t.Param("damageMultiplier");
            float delay = t.Param("delaySeconds");
            AddFxAt(at, 0f, Mathf.PI * 2f, R, delay, new Color(0.55f, 0.75f, 0.95f, 0.25f)); // 떨어질 자리
            Later(delay, () =>
            {
                if (!AddSpark("fx_sword_qi_glow", at, -Mathf.PI * 0.5f, R * 2.4f, 0.35f, 0.08f))
                    AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.25f, new Color(0.8f, 0.9f, 1f, 0.8f));
                enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
                BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(at.x, at.y), R); // C-09b 친 자리 안 등롱
                Deal(s, dmg, enemies.Damage(_near, dmg, at, 0f));
                SkySwordCount++;
            });
        }

        /// <summary>하늘 검기가 떨어진 횟수(시험·디버그용).</summary>
        public int SkySwordCount { get; private set; }
        /// <summary>독침이 박힌 적 수 합(시험·디버그용).</summary>
        public int NeedleHits { get; private set; }

        void StepNeedles(float dt)
        {
            var t = effects.Tier("needle_rain");
            if (t == null) { _needleLeft = -1f; return; }
            float every = Mathf.Max(0.5f, t.Param("intervalSeconds"));
            if (_needleLeft < 0f) _needleLeft = every;
            _needleLeft -= dt;
            if (_needleLeft > 0f) return;
            _needleLeft += every;
            var cam = Camera.main;
            if (cam == null) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            enemies.Sim.QueryCircle(new float2(c.x, c.y), Mathf.Sqrt(w * w + h * h), _near);
            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(c.x, c.y), Mathf.Sqrt(w * w + h * h)); // C-09b 친 자리 안 등롱
            var agents = enemies.Sim.Agents;
            _filtered.Clear();
            foreach (int i in _near)
            {
                var p = agents[i].Pos;
                if (Mathf.Abs(p.x - c.x) <= w && Mathf.Abs(p.y - c.y) <= h) _filtered.Add(i);
            }
            int shown = 0;
            foreach (int i in _filtered)
            {
                if (shown++ >= 80) break; // 그림만 줄인다(피해는 모두)
                var p = agents[i].Pos;
                AddSpark("fx_dagger_ink", new Vector2(p.x, p.y), -Mathf.PI * 0.5f, 0f, 0.3f, 0.06f, false, 0.85f, NeedleInk);
            }
            NeedleHits += enemies.Damage(_filtered, t.Param("damage"), c, 0f, null, t.Param("poisonSeconds"));
        }

        // 화산 3 꽃잎(crit_petals): 무공 치명타가 난 적 자리에서 petals 장이 사방으로. 꽃잎이 낸 치명타는 다시 꽃잎을 안 낸다
        static readonly Color PetalColor = new Color(0.91f, 0.62f, 0.68f, 0.95f);
        static readonly Color PetalInk = new Color(0.62f, 0.20f, 0.30f, 1f);
        static readonly Color PoisonColor = new Color(0.37f, 0.50f, 0.29f, 0.45f);
        bool _hooked, _petalHit;
        /// <summary>꽃잎이 나간 장 수(시험·디버그용).</summary>
        public int PetalCount { get; private set; }

        void Hook()
        {
            if (_hooked || enemies == null) return;
            _hooked = true;
            enemies.Crit += OnCrit;
            enemies.PoisonSpread += (at, r) => AddFxAt(at, 0f, Mathf.PI * 2f, r, 0.35f, PoisonColor);
        }

        void OnDestroy()
        {
            if (_hooked && enemies != null) enemies.Crit -= OnCrit;
        }

        void OnCrit(Vector2 at)
        {
            if (_petalHit) return;
            var t = effects.Tier("crit_petals");
            if (t == null) return;
            var ws = World;
            int n = Mathf.Max(1, Mathf.RoundToInt(t.Param("petals")));
            float speed = ws.Length(t.Param("speed")), radius = ws.Length(t.Param("radius"));
            for (int i = 0; i < n; i++)
            {
                if (_projs.Count >= DataBoot.Data.Balance.Limits.Projectiles) return;
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                _projs.Add(new Proj
                {
                    Pos = at, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, Radius = radius, Damage = t.Param("damage"),
                    Life = t.Param("lifeSeconds"), Pierce = 0, Color = PetalColor, Hit = new HashSet<int>(), Petal = true,
                });
                PetalCount++;
            }
        }

        void Later(float delay, Action a)
        {
            if (delay <= 0f) a();
            else _pending.Add(new Pending { Delay = delay, Apply = a });
        }

        // ── 붓 획(V-04) ─────────────────────────────────────────
        struct Brush
        {
            public Texture2D Tex, Wash, Gold;
            public float Angle, Size, Span, Age;
            public Color Color;
            public Vector4 Rect;   // 층 셋이 같은 자리로 잘려 있다
        }

        static readonly Vector4 FullRect = new Vector4(0f, 0f, 1f, 1f);
        Dictionary<string, Vector4> _crops;

        /// <summary>텍스처가 512 칸에서 잘린 자리(uv). 이름 끝 _stroke·_wash·_gold 를 떼고 brushCrops 에서 찾는다.</summary>
        Vector4 CropOf(Texture2D tex)
        {
            if (tex == null) return FullRect;
            if (_crops == null)
            {
                _crops = new Dictionary<string, Vector4>();
                foreach (var c in brushCrops)
                    if (!string.IsNullOrEmpty(c.name)) _crops[c.name] = c.rect;
            }
            var n = tex.name;
            int k = n.LastIndexOf('_');
            if (k > 0) n = n.Substring(0, k);
            return _crops.TryGetValue(n, out var r) ? r : FullRect;
        }

        /// <summary>몸이 가운데인 한 변 1 칸 안에서 잘린 사각형 자리로 옮기고 줄인다(사각형 메시는 -0.5~0.5).</summary>
        static Matrix4x4 CropMatrix(Vector4 r) =>
            Matrix4x4.TRS(new Vector3(r.x + r.z * 0.5f - 0.5f, r.y + r.w * 0.5f - 0.5f, 0f), Quaternion.identity, new Vector3(r.z, r.w, 1f));

        static readonly Color BrushGold = new Color32(0xC9, 0xA2, 0x27, 0xFF); // vfx.md gold

        /// <summary>vfx.md 단계. 셈은 Core `WeaponGrowth.VisualTier`(마일스톤 없으면 Lv4·Lv7, 하나면 Lv7 기준).</summary>
        static int BrushTier(Slot s) => s.Def == null ? 1 : WeaponGrowth.VisualTier(s.Def, s.level);

        static Texture2D Pick(Texture2D[] arr, int i) => arr != null && arr.Length > 0 ? arr[i % arr.Length] : null;

        readonly List<Brush> _brushes = new List<Brush>();
        Mesh _quad;
        MaterialPropertyBlock _brushProps;
        static readonly int IdMainTex = Shader.PropertyToID("_MainTex");
        static readonly int IdColor = Shader.PropertyToID("_Color");
        static readonly int IdProgress = Shader.PropertyToID("_Progress");
        static readonly int IdSpan = Shader.PropertyToID("_Span");
        static readonly int IdKind = Shader.PropertyToID("_Kind");
        static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
        static readonly int IdRect = Shader.PropertyToID("_Rect");

        /// <summary>그 무공의 붓 획이 있으면 띄우고 true. 지금은 삼재검법 1~3단(2단부터 담채, 3단부터 금빛 층). 그 단 텍스처가 없으면 아래 단으로.</summary>
        bool AddBrush(Slot s, float angle)
        {
            if (brushMaterial == null || s.id != "samjae_sword") return false;
            int tier = BrushTier(s);
            int i = s.Fired;
            Brush b;
            if (tier >= 3 && Pick(samjaeTier3, i) != null)
                b = new Brush { Tex = Pick(samjaeTier3, i), Wash = Pick(samjaeTier3Wash, i), Gold = Pick(samjaeTier3Gold, i), Size = World.Length(samjaeTier3Meters), Span = samjaeTier3Span };
            else if (tier >= 2 && Pick(samjaeTier2, i) != null)
                b = new Brush { Tex = Pick(samjaeTier2, i), Wash = Pick(samjaeTier2Wash, i), Size = World.Length(samjaeTier2Meters), Span = samjaeTier2Span };
            else if (Pick(samjaeTier1, i) != null)
                b = new Brush { Tex = Pick(samjaeTier1, i), Size = World.Length(samjaeTier1Meters), Span = samjaeTier1Span };
            else return false;
            b.Angle = angle; b.Color = StrokeInk; b.Rect = CropOf(b.Tex);
            _brushes.Add(b);
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
                var crop = CropMatrix(b.Rect);
                float progress = Mathf.Clamp01(b.Age / BrushDraw);
                float dissolve = Mathf.Clamp01((b.Age - BrushDraw - BrushHold) / (BrushLife - BrushDraw - BrushHold));
                var rot = Quaternion.Euler(0, 0, b.Angle * Mathf.Rad2Deg);
                // 층 순서: 담채(아래) → 본 획 → 금빛(위). z 가 카메라 쪽일수록 나중에 그려진다.
                if (b.Wash != null && !BloodRain.Presentation.Fx.FxSettings.Simplified) // 효과 간소화면 담채를 건너뜀
                {
                    // 담채는 획을 따라 번진 뒤 천천히 커지며 옅어진다(vfx.md '담채 번짐'). 흩어짐은 본 획의 절반만.
                    float life = b.Age / BrushLife;
                    float grow = 1f + 0.12f * life;
                    DrawBrushLayer(rp, b.Wash, MistWash, progress, dissolve * 0.5f, b.Span, 0.55f * (1f - life),
                        Matrix4x4.TRS(new Vector3(c.x, c.y, -0.26f), rot, new Vector3(b.Size * grow, b.Size * grow, 1f)) * crop, b.Rect);
                }
                var m = Matrix4x4.TRS(new Vector3(c.x, c.y, -0.27f), rot, new Vector3(b.Size, b.Size, 1f)) * crop;
                DrawBrushLayer(rp, b.Tex, b.Color, progress, dissolve, b.Span, 1f, m, b.Rect);
                if (b.Gold != null)
                    DrawBrushLayer(rp, b.Gold, BrushGold, progress, dissolve, b.Span, 1f,
                        Matrix4x4.TRS(new Vector3(c.x, c.y, -0.28f), rot, new Vector3(b.Size, b.Size, 1f)) * crop, b.Rect);
            }
        }

        void DrawBrushLayer(RenderParams rp, Texture2D tex, Color color, float progress, float dissolve, float span, float alpha, Matrix4x4 m, Vector4 rect)
        {
            _brushProps.Clear();
            _brushProps.SetTexture(IdMainTex, tex);
            _brushProps.SetColor(IdColor, color);
            _brushProps.SetFloat(IdProgress, progress);
            _brushProps.SetFloat(IdDissolve, dissolve);
            _brushProps.SetFloat(IdSpan, span);
            _brushProps.SetFloat(IdKind, 0f);
            _brushProps.SetFloat(IdAlpha, alpha);
            _brushProps.SetVector(IdRect, rect);
            Graphics.RenderMesh(rp, _quad, 0, m);
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
            if (s == null) { f = Ai("fx_petal_ink"); radiusTimes = 3.2f; return f != null; } // 화산 3 꽃잎
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
                DrawAi(pf, p.Pos - dir * size * 0.3f, Mathf.Atan2(dir.y, dir.x), size, Mathf.Clamp01(p.Age / 0.06f), dissolve, 1f, p.Petal ? PetalInk : StrokeInk);
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
