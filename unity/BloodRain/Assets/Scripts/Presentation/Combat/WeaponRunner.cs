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
                // C-09c 만천성지: 초당 발 수 × fireRateMultiplier(웹 random_rapid 쿨다운)
                if (s.Def.ShotsPerSecond != null && EvoOf(s) is BloodRain.Core.Data.EvolutionDef rateEvo)
                    cd = Mathf.Max(0.02f, cd / Mathf.Max(0.01f, rateEvo.Param("fireRateMultiplier", 1f)));
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

        // LK-01Ua 연환: 켜진 연환의 값(아니면 null — 비급 잠김도 null). 상태는 LevelUpScreen.Synergy(복원한 비급까지 넘기는 같은 셈)를 0.5초마다 다시 셈
        BloodRain.Core.Build.SynergyState _linkState;
        float _linkAt = -1f;
        BloodRain.Presentation.Hud.LevelUpScreen _levelUp;
        float? Link(string id, string key)
        {
            RefreshLinks();
            return _linkState?.LinkParam(id, key);
        }
        /// <summary>LK-01Ub 값(params)이 없는 연환이 켜져 있나(인뢰검·검명·차력타력·견인타).</summary>
        bool LinkOn(string id)
        {
            RefreshLinks();
            return _linkState != null && _linkState.HasLink(id);
        }
        void RefreshLinks()
        {
            if (Time.unscaledTime < _linkAt) return;
            _linkAt = Time.unscaledTime + 0.5f;
            if (_levelUp == null) _levelUp = FindFirstObjectByType<BloodRain.Presentation.Hud.LevelUpScreen>();
            try { _linkState = _levelUp != null ? _levelUp.Synergy : null; } catch (Exception) { _linkState = null; }
        }

        // LK-01Ub 연환 나머지가 들고 있는 판 상태(세상 시각 Time.time). 표에 '언제까지'가 없는 것은 짝 무공 쿨 한 번·1초로 둔다(backlog LK-01a 결정 전)
        float _fistUntil;                                                                    // 금강후: 나한권이 '때리는 동안'(마지막 연타 + 쿨 한 번)
        readonly Dictionary<int, float> _pressed = new Dictionary<int, float>();             // 점혈창: 지풍에 맞아 멈춘 적 Id → 풀리는 시각
        readonly Dictionary<int, float> _bleed = new Dictionary<int, float>();               // 혈창: 출혈 중인 적 Id → 끝나는 시각
        struct Blast { public Vector2 At; public float R, T; }
        readonly List<Blast> _blasts = new List<Blast>();                                    // 뇌화: 최근 벽력탄 폭발(1초 안)
        readonly List<int> _spread = new List<int>();
        readonly List<Vector2> _spreadAt = new List<Vector2>();
        const float NoehwaWindowSeconds = 1f, WhirlTurnPerSecond = 6f;                     // 뇌화 '가까이'의 시간 쪽, 취중매화 도는 빠르기(모양만)

        /// <summary>시험용: 연환 효과가 난 횟수(연환 id → 번). LinkTestMenu 가 찍는다.</summary>
        public readonly Dictionary<string, int> LinkFires = new Dictionary<string, int>();
        void Fired(string link) { LinkFires.TryGetValue(link, out int n); LinkFires[link] = n + 1; }

        readonly Dictionary<int, float> _pulled = new Dictionary<int, float>();              // 차력타력·견인타: 사슬에 끌려온 적 Id → 1초 동안
        readonly List<Vector2> _bladeAt = new List<Vector2>();                               // 검명: 지금 도는 태극검 자리
        float _bladeAtTime = -1f;
        readonly List<int> _rebound = new List<int>(), _reboundNear = new List<int>();
        const float PulledSeconds = 1f, ReboundKnockTimes = 3f;                              // 끌려온 적을 받아칠 수 있는 시간, 되튕길 때 넉백 배수(모양만, LK-01a 결정 전)

        /// <summary>LK-01Ub 차력타력·견인타: 끌려온 적(Id)을 몸 바깥으로 크게 튕기고(넉백 × ReboundKnockTimes), 그 자리 1m 안 다른 적과 부딪혀 둘 다 피해(짝 무공 한 방).</summary>
        void Rebound(List<int> ids, Slot by, float dmg, string link)
        {
            var ws = World;
            float kb = ws.Speed(Mathf.Max(Knock(by), 3f) * ReboundKnockTimes);
            foreach (int id in ids)
            {
                _pulled.Remove(id);
                int i = IndexOf(id);
                if (i < 0) continue;
                var at = enemies.Sim.Agents[i].Pos;
                var at2 = new Vector2(at.x, at.y);
                enemies.Sim.QueryCircle(at, ws.Length(1f), _reboundNear);
                _reboundNear.Remove(i);
                AddFxAt(at2, 0f, Mathf.PI * 2f, ws.Length(1f), 0.25f, new Color(0.95f, 0.90f, 0.80f, 0.55f));
                _one1.Clear(); _one1.Add(i);
                // 부딪힌 쪽 먼저(튕길 적이 쓰러지면 자리 번호가 바뀐다)
                if (_reboundNear.Count > 0) Deal(by, dmg, enemies.Damage(_reboundNear, dmg, at2, kb * 0.3f));
                i = IndexOf(id);
                if (i < 0) continue;
                _one1.Clear(); _one1.Add(i);
                Deal(by, dmg, enemies.Damage(_one1, dmg, P, kb));
                Fired(link);
            }
        }

        static void Trim(Dictionary<int, float> d)
        {
            if (d.Count < 2048) return;
            var old = new List<int>();
            foreach (var kv in d) if (kv.Value < Time.time) old.Add(kv.Key);
            foreach (var k in old) d.Remove(k);
        }

        bool SweepActive()
        {
            foreach (var f in _fx) if (f.Sweep && f.Age < f.Life) return true;
            return false;
        }

        Slot SlotOf(string id)
        {
            foreach (var s in slots) if (s.id == id) return s;
            return null;
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
                    float baseA = AimAngle(Aim.MeleeReach(R)); // WP-52U
                    var dirs = EvolvedHits.FrontArcDirections(baseA, evo != null, Has(s, "앞뒤 베기"));
                    int strikes = Has(s, "3연 베기") ? 3 : 1;
                    float gap = Gap(s, 0.12f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < strikes; k++)
                        Later(k * gap, () =>
                        {
                            // LK-01Ua 연환 쌍검합벽: 창천검기가 도는 동안 삼재검법 피해 × (1 + damageBonus)·금빛(웹 front_arc sg)
                            bool sg = Link("ssanggeom", "damageBonus") is float sgb && SweepActive();
                            float d2 = sg ? dmg * (1f + Link("ssanggeom", "damageBonus").Value) : dmg;
                            foreach (var a in dirs)
                            {
                                // 절기면 금빛 부채꼴을 붓 획 위에 한 겹 더(웹 gold)
                                if (!AddBrush(s, a) || evo != null || sg) AddFx(a, Mathf.PI, R, 0.22f, evo != null || sg ? new Color(1f, 0.82f, 0.38f, 0.55f) : new Color(0.95f, 0.93f, 0.85f, 0.75f));
                                Deal(s, d2, HitArc(P, R, a, d2, kb));
                            }
                            // LK-01Ub 연환 검화: 벤 자리(첫 방향 사거리 0.6)에서 낙화검우 꽃잎 petals 장이 피어 흩어짐. 꽃잎 한 장 = 낙화검우 한 방(표에 피해가 없어 LK-01a 결정 전)
                            if (Link("geomhwa", "petals") is float pn && SlotOf("falling_petals") is Slot pet)
                            {
                                var psh = ShotOf(pet);
                                var cut = P + new Vector2(Mathf.Cos(dirs[0]), Mathf.Sin(dirs[0])) * R * 0.6f; Fired("geomhwa");
                                for (int q = 0; q < Mathf.RoundToInt(pn); q++)
                                    Shoot(pet, dirs[0] + UnityEngine.Random.Range(-1f, 1f), ws.Speed(psh.Speed), ws.Length(psh.Radius), Damage(pet), 0, psh.LifeSeconds, 0, 0f, ws.Speed(Knock(pet)),
                                          new Color(0.95f, 0.60f, 0.68f, 0.9f), cut);
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
                    _fistUntil = Time.time + hits * gap + WeaponGrowth.Cooldown(s.Def, s.level); // LK-01Ub 금강후가 보는 '때리는 동안'
                    // LK-01Ua 연환 권장합일: 마지막 주먹에 장풍 한 발(장풍 한 방 × palmDamageRatio, 관통, 속도·반경·수명은 synergies.json params)
                    if (Link("gwonjang", "palmDamageRatio") is float pdr && SlotOf("palm_wind") is Slot palm)
                    {
                        float pSpeed = Link("gwonjang", "palmSpeed") ?? 9f, pLife = Link("gwonjang", "palmLifeSeconds") ?? 0.9f, pRad = Link("gwonjang", "palmRadius") ?? 0.45f;
                        Later((hits - 1) * gap, () =>
                        {
                            float pa = AimAngle(ws.Length(Aim.ProjectileReach(pSpeed, pLife))); // WP-52U
                            Shoot(palm, pa, ws.Speed(pSpeed), ws.Length(pRad), Damage(palm) * pdr, 999, pLife, 0, 0f, ws.Speed(3f), new Color(0.95f, 0.85f, 0.55f, 0.6f));
                        });
                    }
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
                        float a0 = AimAngle(ws.Length(Aim.ProjectileReach(spd, life))); // WP-52U
                        foreach (var a in EvolvedHits.FanAngles(fistEvo, a0))
                            Shoot(s, a, ws.Speed(spd), ws.Length(fistEvo.Param("projectileRadius")), dmg * fistEvo.Param("projectileDamage"), 999, life, 0, 0f, kb,
                                  new Color(1f, 0.82f, 0.38f, 0.8f));
                    }
                    break;
                }
                case "orbit_slash":
                {
                    // C-09c 절기 천뢰검진: 범위 × rangeMultiplier, sweeps 겹, 금빛. 닿은 적마다 낙뢰는 SweepHits(웹 orbit_slash)
                    var arrayEvo = EvoOf(s);
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier * RangeMul(s) * (arrayEvo != null ? arrayEvo.Param("rangeMultiplier", 1f) : 1f)); // Lv8 범위 +40%(areaLv8)
                    int sweeps = arrayEvo != null ? Mathf.Max(1, (int)arrayEvo.Param("sweeps", 1f)) : Has(s, "2회 회전") ? 2 : 1;
                    _fx.Add(new Fx
                    {
                        Angle = UnityEngine.Random.value * Mathf.PI * 2f, Span = 0.6f, Radius = R, Life = (s.Def.SweepSeconds ?? 0.36f) * sweeps,
                        Color = arrayEvo != null ? new Color(0.95f, 0.82f, 0.40f, 0.85f) : new Color(0.55f, 0.75f, 0.95f, 0.8f), Sweep = true, Sweeps = sweeps, Weapon = s, Damage = dmg, Hit = new HashSet<int>(),
                    });
                    // LK-01Ub 연환 인뢰검: 낙뢰가 검기 원을 타고 한 바퀴(뇌부 개수만큼 고르게, 검기 한 바퀴 시간 동안) 돌고, 다 돈 뒤 원 안 적 감전.
                    // 낙뢰 하나 = 뇌부 한 방·뇌부 반경, 감전 = 뇌부 shockSeconds(표에 피해가 없어 LK-01a 결정 전)
                    if (LinkOn("inroe") && SlotOf("thunder_talisman") is Slot tal)
                    {
                        int bolts = Mathf.Max(1, WeaponGrowth.Projectiles(tal.Def, tal.level));
                        float round = (s.Def.SweepSeconds ?? 0.36f) * sweeps, a0 = UnityEngine.Random.value * Mathf.PI * 2f;
                        float br = ws.Length(tal.Def.StrikeRadius ?? 1.2f), shock = tal.Def.ShockSeconds ?? 0.5f, tdmg = Damage(tal);
                        for (int k = 0; k < bolts; k++)
                        {
                            float ba = a0 + Mathf.PI * 2f * k / bolts;
                            Later(round * k / bolts, () =>
                            {
                                var at = P + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * R;
                                if (!AddBolt(at)) AddFxAt(at, 0f, Mathf.PI * 2f, br, 0.25f, new Color(1f, 0.92f, 0.55f, 0.8f));
                                enemies.Sim.QueryCircle(new float2(at.x, at.y), br, _reboundNear);
                                Deal(tal, tdmg, enemies.Damage(_reboundNear, tdmg, at, 0f));
                            });
                        }
                        Later(round, () =>
                        {
                            AddFxAt(P, 0f, Mathf.PI * 2f, R, 0.3f, new Color(1f, 0.92f, 0.55f, 0.25f));
                            enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _reboundNear);
                            foreach (int i in _reboundNear) { var ea = enemies.Sim.Get(i); ea.Stun = Mathf.Max(ea.Stun, shock); enemies.Sim.Set(i, ea); }
                            Fired("inroe");
                        });
                    }
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
                    // EV-01U 독무 둔화: 틱마다 안 적 Slow = max(Slow, slow 15→30%) — 쇄겸술과 같은 칸, 풀림은 slowFadePerSecond(웹 e.slow)
                    float mistSlow = WeaponGrowth.At(s.Def.Slow, s.level, 0f);
                    if (mistSlow > 0f)
                        foreach (int i in _near) { var sa = enemies.Sim.Get(i); sa.Slow = Mathf.Max(sa.Slow, mistSlow); enemies.Sim.Set(i, sa); }
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
                    // LK-01Ua 연환 독비: 관통 + extraPierce, 늘 중독(웹 homing_dagger)
                    float? dokbi = Link("dokbi", "extraPierce");
                    int pierce = WeaponGrowth.Pierce(s.Def, s.level) + (dokbi != null ? Mathf.RoundToInt(dokbi.Value) : 0);
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
                        if ((daggerEvo != null || dokbi != null) && _projs.Count > before)
                        {
                            var pr = _projs[_projs.Count - 1];
                            if (daggerEvo != null) { pr.Bounce = (int)daggerEvo.Param("bounces"); pr.BounceEvo = daggerEvo; }
                            pr.Poison = true;
                            _projs[_projs.Count - 1] = pr;
                        }
                    }
                    break;
                }
                case "slow_projectile":
                {
                    // 장풍: 바라보는 쪽으로 shot(초속 5m, 반경 0.55m x areaLv8 1.6, 2.6초), 다 뚫고(pierce 999), 넉백 5. Lv6 2연발
                    // EV-01U 절기 파산장: 발 수 + extraShots, 반경 × sizeMultiplier, 맞힐 때마다 explodeRadius 폭발(만천성지와 같은 pr.Explode 길,
                    //   explodeOnCrit 이 없으니 늘 터짐). 웹 case 'slow_projectile' 의 w.evolved 갈래와 같은 셈
                    var palmEvo = EvoOf(s);
                    int shots = (Has(s, "2연발") ? 2 : 1) + (palmEvo != null ? (int)palmEvo.Param("extraShots", 0f) : 0);
                    float size = palmEvo != null ? palmEvo.Param("sizeMultiplier", 1f) : 1f;
                    var shot = ShotOf(s);
                    int pierce = WeaponGrowth.Pierce(s.Def, s.level);
                    float gap = Gap(s, 0.15f), kb = ws.Speed(Knock(s));
                    for (int k = 0; k < shots; k++)
                        Later(k * gap, () =>
                        {
                            float r = ws.Length(shot.Radius * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier * size);
                            int before = _projs.Count;
                            Shoot(s, AimAngle(ws.Length(Aim.ProjectileReach(shot.Speed, shot.LifeSeconds * durationMultiplier))), ws.Speed(shot.Speed), r, dmg, pierce, shot.LifeSeconds * durationMultiplier, 0, shot.Homing, kb,
                                  palmEvo != null ? new Color(1f, 0.80f, 0.35f, 0.65f) : new Color(0.95f, 0.85f, 0.55f, 0.55f));
                            if (palmEvo != null && _projs.Count > before)
                            {
                                var pr = _projs[_projs.Count - 1]; pr.Explode = palmEvo; _projs[_projs.Count - 1] = pr;
                            }
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
                    int before = _projs.Count;
                    Shoot(s, Mathf.Atan2(e.Pos.y - P.y, e.Pos.x - P.x), ws.Speed(shot.Speed), ws.Length(shot.Radius), dmg,
                          WeaponGrowth.Pierce(s.Def, s.level), shot.LifeSeconds, 0, shot.Homing, ws.Speed(Knock(s)),
                          new Color(1f, 0.95f, 0.6f, 1f));
                    if (EvoOf(s) is BloodRain.Core.Data.EvolutionDef starEvo && _projs.Count > before)
                    {
                        var pr = _projs[_projs.Count - 1]; pr.Explode = starEvo; _projs[_projs.Count - 1] = pr; // C-09c 만천성지
                    }
                    break;
                }
                case "random_bomb":
                {
                    // 벽력탄: 7m 안 가까운 20명 가운데 무작위 자리(없으면 둘레 4m)에 0.6초 뒤 터짐, 반경 2→3m, 넉백 3.
                    // 개수 projectiles, 거리 seekRange, 퓨즈 fuseSeconds, 반경 range x areaLv8, 넉백 knockback(weapons.json).
                    // 화상(burnPerSecond·burnSeconds)은 터질 때 EnemyField.AddDot 으로(ST-02U)
                    // C-09c 절기 벽력천화: + extraProjectiles 개, 한 개마다 Core EvolvedHits.ChainBombs 로 chain 번 이어 터지고
                    //   마지막 폭발 자리에 FireField(반경 × fireRadiusMultiplier) 불바다 fireSeconds 초(웹 bomb·fireField)
                    var bombEvo = EvoOf(s);
                    int n = WeaponGrowth.Projectiles(s.Def, s.level) + extraAmount + (bombEvo != null ? (int)bombEvo.Param("extraProjectiles", 0f) : 0); // C-06a 개수
                    NearestN(ws.Length(s.Def.SeekRange ?? 7f), 20, _targets);
                    float R = ws.Length(s.Def.Range * WeaponGrowth.Area(s.Def, s.level) * areaMultiplier * explosionAreaMultiplier * RangeMul(s));
                    float fuse = s.Def.FuseSeconds ?? 0.6f, kb = ws.Speed(Knock(s));
                    for (int k = 0; k < n; k++)
                    {
                        Vector2 at;
                        if (_targets.Count > 0) { var e = enemies.Sim.Agents[_targets[UnityEngine.Random.Range(0, _targets.Count)]]; at = new Vector2(e.Pos.x, e.Pos.y); }
                        else at = P + new Vector2(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-4f, 4f)) * ws.Length(1f);
                        AddFxAt(at, 0f, Mathf.PI * 2f, R, fuse, new Color(0.70f, 0.15f, 0.12f, 0.18f)); // 떨어질 자리 표시
                        if (bombEvo == null) { Later(fuse, () => BombBlast(s, at, R, dmg, kb)); continue; }
                        var chain = EvolvedHits.ChainBombs(bombEvo, at.x, at.y, R, () => UnityEngine.Random.value);
                        foreach (var b in chain)
                        {
                            var bb = b;
                            Later(fuse + bb.Delay, () => BombBlast(s, new Vector2(bb.X, bb.Y), bb.Radius, dmg * bb.DamageScale, kb));
                        }
                        var last = chain[chain.Count - 1];
                        var (fr, fs) = EvolvedHits.FireField(bombEvo, last);
                        if (fs > 0f) Later(fuse + last.Delay, () => BurnField(s, new Vector2(last.X, last.Y), fr, fs * durationMultiplier));
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
                            Noehwa(at);
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
                            // LK-01Ub 연환 견인타: 봉이 닿는 원(R) 안 끌려오는 적을 받아쳐 날려 보냄, 부딪히면 둘 다 피해(취선봉 한 방, LK-01a 결정 전)
                            if (_pulled.Count > 0 && LinkOn("gyeonin"))
                            {
                                _rebound.Clear();
                                enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _reboundNear);
                                foreach (int j in _reboundNear) { int pid = enemies.Sim.Agents[j].Id; if (_pulled.TryGetValue(pid, out var pu) && pu > Time.time && !_rebound.Contains(pid)) _rebound.Add(pid); }
                                if (_rebound.Count > 0) Rebound(_rebound, s, dmg, "gyeonin");
                            }
                            // LK-01Ub 연환 취중매화: 봉이 닿는 원(R) 안 낙화검우 꽃잎을 감아 seconds 초 몸 둘레 회오리로(피해는 꽃잎 한 방 그대로 — LK-01a 결정 전)
                            if (Link("chwijung", "seconds") is float ws_)
                                for (int q = 0; q < _projs.Count; q++)
                                {
                                    var pr = _projs[q];
                                    if (pr.Weapon == null || pr.Weapon.id != "falling_petals" || pr.Whirl > 0f) continue;
                                    var off = pr.Pos - P;
                                    if (off.magnitude > R) continue;
                                    pr.Whirl = ws_ * durationMultiplier; pr.WhirlR = Mathf.Max(off.magnitude, R * 0.5f); pr.WhirlA = Mathf.Atan2(off.y, off.x);
                                    pr.Pierce = 999; pr.Color = new Color(0.98f, 0.62f, 0.70f, 0.95f); Fired("chwijung");
                                    _projs[q] = pr;
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
                            _bladeAt.Clear(); _bladeAtTime = Time.time; // LK-01Ub 검명이 보는 검 자리
                            bool charyeok = LinkOn("charyeok");
                            _rebound.Clear();
                            for (int i = 0; i < n; i++)
                            {
                                float a = WeaponShapes.BladeAngle(a0, now, i, n);
                                var at = P + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R;
                                _bladeAt.Add(at);
                                // LK-01Ub 연환 차력타력: 끌려온 적이 검에 닿으면 되튕겨 뒤의 적과 충돌(태극검 한 방, LK-01a 결정 전)
                                if (charyeok && _pulled.Count > 0)
                                {
                                    enemies.Sim.QueryCircle(new float2(at.x, at.y), bladeR, _reboundNear);
                                    foreach (int j in _reboundNear) { int pid = agents[j].Id; if (_pulled.TryGetValue(pid, out var pu) && pu > Time.time && !_rebound.Contains(pid)) _rebound.Add(pid); }
                                }
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
                            if (_rebound.Count > 0) Rebound(_rebound, s, bdmg, "charyeok");
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
                            Trim(_pulled);
                            _pulled[a.Id] = Time.time + PulledSeconds; // LK-01Ub 차력타력·견인타가 보는 '끌려온 적'
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
                    float baseA = AimAngle(Aim.MeleeReach(R)); // WP-52U
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
                    // LK-01Ub 연환 혈독: 중독된 적을 할퀴면 그 자리 spreadRadius 안으로 독이 번짐(독무의 중독 초·독무 한 방을 초당으로 — 표에 피해가 없어 LK-01a 결정 전)
                    // 연환 혈창: 출혈 중인 적을 할퀸 몫은 흡혈 × lifestealMultiplier. 맞히기 전에 본다(쓰러지면 자리 번호가 바뀜)
                    float? hdR = Link("hyeoldok", "spreadRadius"), hcLs = Link("hyeolchang", "lifestealMultiplier");
                    int bleedHits = 0;
                    _spreadAt.Clear();
                    foreach (int i in _filtered)
                    {
                        int id = agents[i].Id;
                        if (hcLs != null && _bleed.TryGetValue(id, out var bt) && bt > Time.time) { bleedHits++; Fired("hyeolchang_steal"); }
                        if (hdR != null && enemies.IsPoisoned(id)) _spreadAt.Add(new Vector2(agents[i].Pos.x, agents[i].Pos.y));
                    }
                    int got = enemies.Damage(_filtered, dmg, P, ws.Speed(Knock(s)));
                    Deal(s, dmg, got);
                    if (_spreadAt.Count > 0 && SlotOf("poison_mist") is Slot mist)
                    {
                        float sr = ws.Length(hdR.Value), ps = (mist.Def.PoisonSeconds ?? 2f) * durationMultiplier, pdps = Damage(mist) * enemies.dotMultiplier;
                        foreach (var at in _spreadAt)
                        {
                            AddFxAt(at, 0f, Mathf.PI * 2f, sr, 0.35f, new Color(0.40f, 0.22f, 0.30f, 0.35f));
                            enemies.Sim.QueryCircle(new float2(at.x, at.y), sr, _spread);
                            enemies.Poison(_spread, ps, pdps); Fired("hyeoldok");
                        }
                    }
                    if (got > 0 && ls > 0f)
                    {
                        if (_vitals == null) _vitals = FindFirstObjectByType<PlayerVitals>();
                        float share = got + Mathf.Min(bleedHits, got) * ((hcLs ?? 1f) - 1f);
                        if (_vitals != null) _vitals.Heal(dmg * ls * share * effects.LifestealMultiplier); // ST-02U 마도 2 흡혈 x2
                    }
                    break;
                }
                case "ring_wave":
                {
                    // 음공(R-12Ub, 9장): 몸에서 반경 range 음파, stunChance(5→15%) 로 SonicStunSeconds 기절, 넉백 knockback.
                    // Lv6 '음파 2겹'은 SonicSecondRingDelay 뒤 한 번 더(Core WeaponShapes.RingDelays·RingStuns, 웹 ring_wave).
                    // 절기 파천금음: 기절 stunChance(30%). '겹칠수록 피해 증폭(stackDamageMax)'은 숫자가 없어 웹처럼 아직 안 붙임
                    var qinEvo = EvoOf(s);
                    float R = ws.Length(s.Def.Range * areaMultiplier * RangeMul(s));
                    float chance = qinEvo != null ? qinEvo.Param("stunChance") : WeaponGrowth.At(s.Def.StunChance, s.level, 0f);
                    float kb = ws.Speed(Knock(s));
                    // LK-01Ub 연환 금강후: 나한권으로 때리는 동안 반경 × (1 + radiusBonus), 기절 확률 × stunChanceMultiplier
                    bool roar = Link("geumganghu", "radiusBonus") != null && Time.time < _fistUntil;
                    if (roar)
                    {
                        R *= 1f + Link("geumganghu", "radiusBonus").Value;
                        chance *= Link("geumganghu", "stunChanceMultiplier") ?? 1f; Fired("geumganghu");
                    }
                    foreach (var delay in WeaponShapes.RingDelays(Has(s, "음파 2겹")))
                        Later(delay, () =>
                        {
                            AddFxAt(P, 0f, Mathf.PI * 2f, R, 0.35f, qinEvo != null || roar ? new Color(0.95f, 0.82f, 0.40f, 0.35f) : new Color(0.55f, 0.62f, 0.80f, 0.30f));
                            enemies.Sim.QueryCircle(new float2(P.x, P.y), R, _near);
                            BloodRain.Presentation.Fx.LanternLayer.Strike(P, R); // C-09b 친 자리 안 등롱
                            foreach (int i in _near) // 기절은 피해(쓰러짐으로 자리 번호가 바뀜) 전에. 땅의 주인은 무리에 없다
                                if (WeaponShapes.RingStuns(false, chance, UnityEngine.Random.value))
                                {
                                    var a = enemies.Sim.Get(i);
                                    a.Stun = Mathf.Max(a.Stun, WeaponShapes.Trial.SonicStunSeconds);
                                    enemies.Sim.Set(i, a);
                                }
                            Deal(s, dmg, enemies.Damage(_near, dmg, P, kb));
                            // LK-01Ub 연환 검명: 음파 안에서 도는 태극검마다 작은 음파 한 번 더(반경 절반·음공 한 방 — 표에 크기·피해가 없어 LK-01a 결정 전)
                            if (Time.time - _bladeAtTime < 0.2f && _bladeAt.Count > 0 && LinkOn("geommyeong"))
                                foreach (var bat in _bladeAt)
                                {
                                    if ((bat - P).magnitude > R) continue;
                                    AddFxAt(bat, 0f, Mathf.PI * 2f, R * 0.5f, 0.3f, new Color(0.80f, 0.85f, 0.95f, 0.35f));
                                    enemies.Sim.QueryCircle(new float2(bat.x, bat.y), R * 0.5f, _reboundNear);
                                    Deal(s, dmg, enemies.Damage(_reboundNear, dmg, bat, kb * 0.5f));
                                    Fired("geommyeong");
                                }
                        });
                    break;
                }
                case "pierce_thrust":
                {
                    // 이화창(R-12Ub, 9장): 앞 range 관통 찌르기 hits(1→3)회를 ThrustGapSeconds 간격, 가까운 적 쪽(사거리 1.25배 안, 없으면 바라보는 쪽).
                    // Lv7 '회전 찌르기'는 네 방향, 절기 백화난무창은 directions 방향(Core WeaponShapes.ThrustDirections·ThrustHits, 웹 pierce_thrust).
                    // '경공 경로를 따라 창격 폭풍'은 숫자가 없어 웹처럼 아직 안 붙임
                    var spearEvo = EvoOf(s);
                    float R = ws.Length(s.Def.Range * areaMultiplier * RangeMul(s));
                    int hits = Mathf.Max(1, Mathf.RoundToInt(WeaponGrowth.At(s.Def.Hits, s.level, 1f)));
                    float baseA = AimAngle(Aim.MeleeReach(R)); // WP-52U
                    var dirs = WeaponShapes.ThrustDirections(baseA, spearEvo != null, spearEvo != null ? (int)spearEvo.Param("directions", 1f) : 1, Has(s, "회전 찌르기"));
                    float kb = ws.Speed(Knock(s)), unit = ws.Length(1f);
                    for (int h = 0; h < hits; h++)
                        Later(h * WeaponShapes.ThrustGapSeconds, () =>
                        {
                            _filtered.Clear();
                            foreach (var a in dirs)
                            {
                                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                                var mid = P + new Vector2(ca, sa) * R * 0.5f;
                                AddFxAt(P, a, 0.10f, R, 0.12f, spearEvo != null ? new Color(1f, 0.82f, 0.38f, 0.8f) : new Color(0.85f, 0.85f, 0.90f, 0.8f));
                                enemies.Sim.QueryCircle(new float2(mid.x, mid.y), R * 0.5f + unit, _near);
                                BloodRain.Presentation.Fx.LanternLayer.Strike(mid, R * 0.5f);
                                var agents = enemies.Sim.Agents;
                                foreach (int i in _near)
                                {
                                    if (_filtered.Contains(i)) continue;
                                    var e = agents[i];
                                    // ThrustHits 는 경지 m 이라 세계 좌표를 축척으로 나눠 넘긴다
                                    if (WeaponShapes.ThrustHits((e.Pos.x - P.x) / unit, (e.Pos.y - P.y) / unit, e.Radius / unit, a, R / unit)) _filtered.Add(i);
                                }
                            }
                            // LK-01Ub 연환 혈창: 꿰뚫린 적 bleedSeconds 출혈(이화창 한 방을 그 초에 나눠 — 표에 출혈 피해가 없어 LK-01a 결정 전, '불붙음'은 아님)
                            if (Link("hyeolchang", "bleedSeconds") is float bs && _filtered.Count > 0)
                            {
                                float bsec = bs * durationMultiplier;
                                Trim(_bleed);
                                foreach (int i in _filtered) _bleed[enemies.Sim.Agents[i].Id] = Time.time + bsec;
                                enemies.AddDot(_filtered, bsec, dmg / bs * enemies.dotMultiplier, false, false); Fired("hyeolchang");
                            }
                            // LK-01Ub 연환 점혈창: 지풍에 맞아 멈춘 적을 찌르면 확정 치명타
                            if (Link("jeomhyeol", "stopSeconds") != null) enemies.sureCrit = id => { bool on = _pressed.TryGetValue(id, out var pt) && pt > Time.time; if (on) Fired("jeomhyeol_crit"); return on; };
                            Deal(s, dmg, enemies.Damage(_filtered, dmg, P, kb));
                            enemies.sureCrit = null;
                        });
                    break;
                }
                case "petal_scatter":
                {
                    // 낙화검우(R-12Ub, 9장): 꽃잎 검기 projectiles(6→14)+개수 발을 가까운 적 쪽 PetalSpreadRad 부채꼴 안 무작위로. 탄은 shot.
                    // Lv6 '관통 +1'. 절기 매화만개: 한 바퀴 고르게, 맞힐 때마다 기혈 healPerHit(Core WeaponShapes.PetalAngles, 웹 petal_scatter)
                    var petalEvo = EvoOf(s);
                    int n = WeaponGrowth.Projectiles(s.Def, s.level) + extraAmount;
                    var shot = ShotOf(s);
                    int pierce = Has(s, "관통 +1") ? 1 : 0;
                    float baseA = AimAngle(ws.Length(s.Def.Range)); // WP-52U(웹 petal_scatter 와 같은 사거리 안)
                    foreach (var a in WeaponShapes.PetalAngles(baseA, n, petalEvo != null, () => UnityEngine.Random.value))
                    {
                        int before = _projs.Count;
                        Shoot(s, a, ws.Speed(shot.Speed), ws.Length(shot.Radius), dmg, pierce, shot.LifeSeconds, 0, 0f, ws.Speed(Knock(s)),
                              petalEvo != null ? new Color(0.95f, 0.55f, 0.62f, 0.9f) : new Color(0.92f, 0.70f, 0.75f, 0.9f));
                        if (petalEvo != null && _projs.Count > before)
                        {
                            var pr = _projs[_projs.Count - 1]; pr.Heal = petalEvo.Param("healPerHit"); _projs[_projs.Count - 1] = pr;
                        }
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
            public BloodRain.Core.Data.EvolutionDef Explode;    // C-09c 만천성지: 맞힌 자리 작은 폭발(explodeOnCrit 이면 치명타일 때만)
            public float Heal;                                  // R-12Ub 매화만개: 맞힐 때마다 기혈 회복(healPerHit)
            public bool Fire;                                   // LK-01Ua 풍화: 불붙은 적을 지난 장풍이 불을 실음
            public float Whirl, WhirlR, WhirlA, WhirlHit;       // LK-01Ub 취중매화: 남은 회오리 초, 몸에서 거리·각, 같은 적 다시 맞히기까지
        }

        readonly List<Proj> _projs = new List<Proj>();
        readonly List<int> _targets = new List<int>();
        readonly List<int> _one1 = new List<int>(1);

        void Shoot(Slot s, float angle, float speed, float radius, float dmg, int pierce, float life, int targetId, float homing, float knock, Color c, Vector2? from = null)
        {
            if (_projs.Count >= DataBoot.Data.Balance.Limits.Projectiles) return;
            _projs.Add(new Proj
            {
                Pos = from ?? P, Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * (s != null ? projectileSpeedMultiplier : 1f), Radius = radius, Damage = dmg, Life = life,
                Homing = homing, Knock = knock, Pierce = pierce, Target = targetId, Weapon = s, Color = c, Hit = new HashSet<int>(),
            });
        }

        /// <summary>R-15U 팔방풍우: 무공 칸 없이 장풍 탄 한 발(다 뚫음). angle 라디안, 속도·반경·넉백은 축척을 곱한 세계 값.</summary>
        public void ShootFree(float angle, float speed, float radius, float dmg, float life, float knock) =>
            Shoot(null, angle, speed, radius, dmg, 999, life, 0, 0f, knock, new Color(0.95f, 0.85f, 0.55f, 0.7f));
        /// <summary>바라보는 쪽(단위 벡터).</summary>
        public Vector2 Facing => player != null ? new Vector2(player.Facing.x, player.Facing.y) : Vector2.right;

        // WP-52U 조준(기획서 12장 '화면 편의 규칙' 조준, Core Aim, 웹 aimAngle). 설정 낱말 GameSettings.AimModeKey("aimMode")를 PlayerPrefs 에 둔다.
        // 설정 화면(WP-57U)이 생기면 그 단추가 SetAimMode 를 부른다. 지금은 시험 메뉴 '조준 방식 바꾸기'.
        static AimMode _aimMode = AimMode.Nearest;
        static bool _aimLoaded;
        public static AimMode AimMode
        {
            get
            {
                if (!_aimLoaded) { _aimLoaded = true; _aimMode = Aim.Parse(PlayerPrefs.GetString(BloodRain.Core.Meta.GameSettings.AimModeKey, "near")); }
                return _aimMode;
            }
        }
        public static void SetAimMode(AimMode mode)
        {
            _aimMode = mode; _aimLoaded = true;
            PlayerPrefs.SetString(BloodRain.Core.Meta.GameSettings.AimModeKey, Aim.ToKey(mode));
            PlayerPrefs.Save();
        }

        /// <summary>방향 무공이 나갈 각도. 오른쪽 스틱 &gt; (가까운 적 방식이면) reach(화면 길이) 안 가장 가까운 적 &gt; 바라보는 쪽.
        /// reach 는 근접이면 Aim.MeleeReach(사거리), 탄이면 Aim.ProjectileReach(탄속, 수명)을 화면 길이로 바꾼 값.</summary>
        public float AimAngle(float reach)
        {
            var f = Facing;
            Vector2 stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero; // 데드존은 Input System 프로세서가 거름
            var mode = AimMode;
            Vector2 d = Vector2.zero;
            bool has = mode == AimMode.Nearest && stick == Vector2.zero && Nearest(reach, out d);
            return LastAim = Aim.Angle(mode, 0f, 0f, f.x, f.y, has, d.x, d.y, stick.x, stick.y);
        }
        /// <summary>시험용: 마지막으로 고른 조준 각(라디안).</summary>
        public float LastAim { get; private set; }
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
                if (p.Whirl > 0f)
                {
                    // LK-01Ub 취중매화: 봉에 감긴 꽃잎이 몸 둘레를 돈다. 다 뚫고, 0.5초마다 같은 적을 다시 맞힐 수 있음
                    p.Whirl -= dt; p.WhirlA += WhirlTurnPerSecond * dt; p.WhirlHit -= dt;
                    if (p.WhirlHit <= 0f) { p.Hit.Clear(); p.WhirlHit = 0.5f; }
                    p.Pos = P + new Vector2(Mathf.Cos(p.WhirlA), Mathf.Sin(p.WhirlA)) * p.WhirlR;
                    p.Life = p.Whirl > 0f ? Mathf.Max(p.Life, 0.05f) : 0f;
                }
                else p.Pos += p.Vel * dt;
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
                    // LK-01Ua 연환 풍화: 불붙은 적을 지난 장풍은 크기 × (1 + sizeBonus) 로 불을 싣고, 그 뒤 맞는 적은 화상 burnSeconds·초당 burnDps(params, × 지속·화상 배율)
                    if (p.Weapon != null && p.Weapon.id == "palm_wind" && !p.Fire && enemies.IsBurning(id) && Link("punghwa", "sizeBonus") is float sb)
                    {
                        p.Fire = true; p.Radius *= 1f + sb; p.Color = new Color(0.98f, 0.55f, 0.22f, 0.7f);
                    }
                    if (p.Fire) enemies.AddDot(_one1, (Link("punghwa", "burnSeconds") ?? 2f) * durationMultiplier, (Link("punghwa", "burnDps") ?? 6f) * burnMultiplier * enemies.dotMultiplier, false);
                    // LK-01Ub 연환 점혈창: 지풍에 맞은 적은 stopSeconds 멈춤(이화창 확정 치명은 pierce_thrust 에서)
                    if (p.Weapon != null && p.Weapon.id == "flash_finger" && Link("jeomhyeol", "stopSeconds") is float stopS)
                    {
                        var sa = sim.Get(j);
                        sa.Stun = Mathf.Max(sa.Stun, stopS);
                        sim.Set(j, sa);
                        Trim(_pressed);
                        _pressed[id] = Time.time + stopS; Fired("jeomhyeol");
                    }
                    int critsBefore = enemies.CritCount;
                    var hitAt = sim.Agents[j].Pos; // 맞히면 쓰러져 자리 번호가 바뀔 수 있으니 먼저
                    int got = enemies.Damage(_one1, p.Damage, p.Pos, p.Knock);
                    _petalHit = false;
                    if (p.Weapon != null) Deal(p.Weapon, p.Damage, got);
                    // LK-01Ua 연환 암기연환: 섬광지 치명타면 그 적에게 비도 한 자루(비도 한 방, 따라감 — 초속 14m·0.25m·1초·따라가기 8, 웹 값)
                    if (p.Weapon != null && p.Weapon.id == "flash_finger" && enemies.CritCount > critsBefore && Link("amgi", "daggers") is float dn && SlotOf("flying_dagger") is Slot dag)
                    {
                        // 탄 목록 끝에 붙이니 뒤에서부터 도는 이 줄의 i 는 그대로다
                        var ws2 = World;
                        float da = Mathf.Atan2(hitAt.y - P.y, hitAt.x - P.x);
                        int nd = Mathf.Max(1, Mathf.RoundToInt(dn));
                        for (int q = 0; q < nd; q++)
                            Shoot(dag, da + (q - (nd - 1) / 2f) * 0.15f, ws2.Speed(14f), ws2.Length(0.25f), Damage(dag), 1, 1f, id, 8f, 0f, new Color(0.80f, 0.82f, 0.88f, 1f));
                    }
                    if (p.Heal > 0f && got > 0) // R-12Ub 매화만개(웹 pr.heal)
                    {
                        if (_vitals == null) _vitals = FindFirstObjectByType<PlayerVitals>();
                        if (_vitals != null) _vitals.Heal(p.Heal);
                    }
                    // C-09c 만천성지: 맞힌 자리 explodeRadius 폭발, 맞은 적 말고 둘레에 한 방 × explodeDamage(웹 pr.explode)
                    if (p.Explode != null && got > 0 && EvolvedHits.Explodes(p.Explode, enemies.CritCount > critsBefore))
                    {
                        float er = World.Length(p.Explode.Param("explodeRadius"));
                        float ed = p.Damage * p.Explode.Param("explodeDamage");
                        if (!AddSpark("fx_bomb_glow", p.Pos, UnityEngine.Random.value * Mathf.PI * 2f, er * 2.2f, 0.25f, 0.05f))
                            AddFxAt(p.Pos, 0f, Mathf.PI * 2f, er, 0.25f, new Color(1f, 0.92f, 0.55f, 0.75f));
                        sim.QueryCircle(new float2(p.Pos.x, p.Pos.y), er, _bounceNear);
                        _bounceEx.Clear();
                        foreach (int k in _bounceNear) if (sim.Agents[k].Id != id) _bounceEx.Add(k);
                        if (p.Weapon != null) Deal(p.Weapon, ed, enemies.Damage(_bounceEx, ed, p.Pos, 0f));
                    }
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

        // ── 벽력탄 폭발 한 번(C-09c 벽력천화 연쇄도 같은 것) ──
        void BombBlast(Slot s, Vector2 at, float R, float dmg, float kb, bool record = true)
        {
            if (record) { if (_blasts.Count >= 64) _blasts.RemoveAt(0); _blasts.Add(new Blast { At = at, R = R, T = Time.time }); } // LK-01Ub 뇌화가 보는 최근 폭발
            // 벽력탄: 터진 자리에 먹 번짐(fx_bomb_ink_1·2 번갈아) 위로 불꽃 빛(fx_bomb_glow)
            float rot = UnityEngine.Random.value * Mathf.PI * 2f;
            bool ink = AddSpark(s.Fired % 2 == 0 ? "fx_bomb_ink_1" : "fx_bomb_ink_2", at, rot, R * 2.4f, 0.55f, 0.08f);
            bool glow = AddSpark("fx_bomb_glow", at, rot, R * 2.2f, 0.35f, 0.06f);
            if (!ink && !glow) AddFxAt(at, 0f, Mathf.PI * 2f, R, 0.35f, new Color(0.95f, 0.55f, 0.20f, 0.7f));
            enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
            BloodRain.Presentation.Fx.LanternLayer.Strike(new Vector2(at.x, at.y), R); // C-09b 친 자리 안 등롱
            // ST-02U 화상: 폭발 원 안 적에 burnPerSecond(Lv1→Lv8) × 화상 배율 × 지속 피해 배율, burnSeconds × 지속.
            // 맞히기 전에 건다(맞혀 쓰러뜨리면 자리 번호가 바뀐다)
            enemies.AddDot(_near, (s.Def.BurnSeconds ?? 0f) * durationMultiplier, BurnDps(s), false);
            Deal(s, dmg, enemies.Damage(_near, dmg, at, kb));
        }

        /// <summary>LK-01Ub 연환 뇌화: 낙뢰가 최근(NoehwaWindowSeconds 안) 폭발 원 안에 떨어지면 그 자리에서 반경 × radiusMultiplier 로 한 번 더(벽력탄 한 방).
        /// '가까이'는 표에 거리가 없어 폭발 원 안·1초 안으로 둔다(LK-01a 결정 전). 한 폭발은 한 번만.</summary>
        void Noehwa(Vector2 at)
        {
            for (int k = _blasts.Count - 1; k >= 0; k--) if (_blasts[k].T < Time.time - NoehwaWindowSeconds) _blasts.RemoveAt(k);
            if (_blasts.Count == 0 || !(Link("noehwa", "radiusMultiplier") is float rm) || !(SlotOf("thunder_bomb") is Slot bomb)) return;
            for (int k = 0; k < _blasts.Count; k++)
            {
                var b = _blasts[k];
                if ((at - b.At).magnitude > b.R) continue;
                _blasts.RemoveAt(k); Fired("noehwa");
                AddFxAt(b.At, 0f, Mathf.PI * 2f, b.R * rm, 0.3f, new Color(1f, 0.85f, 0.45f, 0.45f));
                BombBlast(bomb, b.At, b.R * rm, Damage(bomb), World.Speed(Knock(bomb)), false);
                return;
            }
        }

        float BurnDps(Slot s) => WeaponGrowth.At(s.Def.BurnPerSecond, s.level) * burnMultiplier * enemies.dotMultiplier;

        /// <summary>C-09c 벽력천화 불바다: left 초 동안 burnTickSeconds 마다 원 안 적의 화상을 다시 채운다(웹 fireField).</summary>
        void BurnField(Slot s, Vector2 at, float R, float left)
        {
            float tick = DataBoot.Data.Balance.EnemyStatus.BurnTickSeconds;
            AddFxAt(at, 0f, Mathf.PI * 2f, R, tick * 1.2f, new Color(0.90f, 0.35f, 0.12f, 0.22f));
            enemies.Sim.QueryCircle(new float2(at.x, at.y), R, _near);
            enemies.AddDot(_near, (s.Def.BurnSeconds ?? 0f) * durationMultiplier, BurnDps(s), false);
            if (left > tick) Later(tick, () => BurnField(s, at, R, left - tick));
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
                if (d.sqrMagnitude > 1e-4f) // 천뢰검진 첫 줄기(하늘에서)는 줄기 선 없이 낙뢰만
                    AddFxAt(from, Mathf.Atan2(d.y, d.x), 0.06f, d.magnitude, 0.2f, new Color(1f, 0.90f, 0.45f, 0.85f));
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
            // C-09c 천뢰검진: 닿은 적마다 lightningChance 로 낙뢰(× lightningDamage), 곁의 적으로 옮아감 — 맞히기 전에 Id 로 정해 둔다
            var skyEvo = f.Weapon != null ? EvoOf(f.Weapon) : null;
            _thHops.Clear();
            if (skyEvo != null && skyEvo.Param("lightningChance", 0f) > 0f)
                foreach (int i in _filtered)
                    if (EvolvedHits.SkyLightningRolls(skyEvo, UnityEngine.Random.value)) PlanSkyLightning(skyEvo, i);
            _skyPlan.Clear(); _skyPlan.AddRange(_thHops);
            Deal(f.Weapon, f.Damage, enemies.Damage(_filtered, f.Damage, P, World.Speed(Knock(f.Weapon))));
            if (_skyPlan.Count > 0) { _thHops.Clear(); _thHops.AddRange(_skyPlan); StrikeThunderChains(f.Weapon, f.Damage, 0f); }
        }

        readonly List<(int id, float scale, Vector2 from)> _skyPlan = new List<(int, float, Vector2)>();

        /// <summary>천뢰검진 낙뢰 한 줄기(first 는 시뮬 자리 번호)를 _thHops 뒤에 덧붙인다. 첫 줄기는 from = 그 적 자리(줄기 선 없음).</summary>
        void PlanSkyLightning(BloodRain.Core.Data.EvolutionDef evo, int first)
        {
            var ws = World;
            float unit = ws.Length(1f);
            var agents = enemies.Sim.Agents;
            var c = agents[first].Pos;
            float reach = evo.Param("lightningRange") * unit * Mathf.Max(1f, evo.Param("lightningChains"));
            enemies.Sim.QueryCircle(c, reach, _thCand);
            int local = _thCand.IndexOf(first);
            if (local < 0) { _thCand.Add(first); local = _thCand.Count - 1; }
            _bxs.Clear(); _bys.Clear();
            foreach (int k in _thCand) { _bxs.Add(agents[k].Pos.x / unit); _bys.Add(agents[k].Pos.y / unit); }
            foreach (var h in EvolvedHits.SkyLightning(evo, local, _bxs, _bys))
            {
                int from = h.From < 0 ? h.To : h.From;
                _thHops.Add((agents[_thCand[h.To]].Id, h.DamageScale, new Vector2(_bxs[from] * unit, _bys[from] * unit)));
            }
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
            _brushProps.SetFloat(IdAlpha, alpha * _mine);
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
            _brushProps.SetFloat(IdAlpha, alpha * _mine);
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

        // WP-57U 설정 '내 이펙트 투명도'(1·0.75·0.5·0.25·0). 여기서 그리는 것은 다 내 무공이라 셋(DrawBrushLayer·Layer·Draw) 알파에 곱한다.
        // 적 탄·예고는 다른 층이라 그대로.
        float _mine = 1f;

        void LateUpdate()
        {
            if (player == null) return;
            _mine = Mathf.Clamp01(BloodRain.Presentation.Meta.SettingsScreen.Current.EffectsAlpha);
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
            col.a *= _mine;
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
