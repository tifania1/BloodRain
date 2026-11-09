using System;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Meta;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 플레이어 기혈과 맞기(C-03). 적 접촉은 EnemyField 가 격자로 찾아 Hit 으로 넘긴다.
    //   받는 피해 = max(contact.minDamage, 피해 − 방어)(웹 hurtPlayer 와 같다. 방어는 능력치가 붙으면).
    //   경공 대시 무적 중이거나 맞은 뒤 무적(웹 프로토타입 0.5초, balance.json 에 아직 없음) 중이면 무시.
    // 쓰러지면 Died 를 부른다. Run 씬에선 LifeRunner 가 받아 정산(천하 몸)이나 서장 각성·처음부터로 넘긴다(LF-03).
    // 시험 씬에선 refillOnDeath 로 다시 채운다(Run 메뉴는 끔).
    public sealed class PlayerVitals : MonoBehaviour
    {
        [Tooltip("맞은 뒤 무적(초). 웹 프로토타입 값")]
        public float hitIframes = 0.5f;
        public float armor;
        /// <summary>C-07a 경지 보상의 받는 피해 배율(Life.DamageTakenMultiplier). 호신을 뺀 뒤에 곱한다. LifeRunner 가 넣는다.</summary>
        [System.NonSerialized] public float takenMultiplier = 1f;
        /// <summary>C-06b 무당 3 사량발천근. 호신을 뺀 받는 피해를 넘기면 실제로 받을 피해를 돌려준다(LifeRunner 가 넣음). 되돌림은 아직 없음.</summary>
        [System.NonSerialized] public System.Func<float, float> deflect;
        /// <summary>R-04Ua 회피 확률(StatSheet.Evade, 수로채 움직이는 동안 15%). 피격 직전에 Evade.Roll 로 굴리고, 피하면 피해·넉백 없이 맞은 뒤 무적만 준다.</summary>
        [System.NonSerialized] public float evadeChance;
        /// <summary>C-07b 금종조. 피해 직전 참이면 막는다(InsightEffects.TryBlock).</summary>
        [System.NonSerialized] public System.Func<bool> block;
        public int Blocks { get; private set; }
        /// <summary>R-04Ua 기혈 회복 전부에 곱하는 수(SectTrait.HealingMultiplier, 마교 0.5). Heal 과 초당 회복이 곱한다.</summary>
        [System.NonSerialized] public float healingMultiplier = 1f;
        /// <summary>R-04Ua 초당 회복(StatSheet.Recovery). LifeRunner 가 넣는다.</summary>
        [System.NonSerialized] public float recoveryPerSecond;
        public bool refillOnDeath = true;
        public bool showStats = true;

        public float MaxHealth { get; private set; } = 100f;
        public float Health { get; private set; } = 100f;
        public int Hits { get; private set; }
        public float DamageTaken { get; private set; }
        /// <summary>R-04Ua 피한 횟수(디버그 줄).</summary>
        public int Evades { get; private set; }
        /// <summary>R-04Ua 회복으로 실제 찬 기혈 합(디버그 줄).</summary>
        public float Healed { get; private set; }
        public event Action Died;
        /// <summary>피해가 실제로 들어갔다(피함·금종조 제외). C-09a 오의 게이지가 받는다.</summary>
        public event Action Damaged;

        float _iframe;
        float _minDamage = 1f;
        PlayerController _move;
        SpriteRenderer _sr;
        PlayerLook _look; // BD-01U 몸 그림이 있으면 그쪽에 흰 번쩍임(셰이더)·청록 잔상
        Color _baseColor;
        float _flash;
        float _evadeFlash;
        readonly System.Random _rng = new System.Random();

        void Awake()
        {
            var data = DataBoot.Data;
            var body = RebirthHandoff.Body ?? Rebirth.Prologue(data);
            MaxHealth = Health = body.Character.MaxHealth;
            _minDamage = data.Balance.Contact.MinDamage;
            _move = GetComponent<PlayerController>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
            _look = GetComponent<PlayerLook>();
        }

        public bool Invulnerable => _iframe > 0f || (_move != null && _move.Invulnerable);

        /// <summary>시험용: 비전 취선으로 피한 수(Evades 에도 들어감).</summary>
        public int DrunkenEvades { get; private set; }

        /// <summary>피해를 받는다. 실제로 들어갔으면 true.</summary>
        public bool Hit(float damage)
        {
            if (Invulnerable || Health <= 0f) return false;
            // SC-01U 비전 취선(drunken_step): 능력치 회피와 따로 한 번 더 굴린다(Core SecretEffects.Evades)
            var secrets = Hud.SecretWatch.Instance;
            bool drunkenEvade = secrets != null && secrets.Effects.EvadeChance > 0f && secrets.Effects.Evades(_rng.NextDouble());
            if (drunkenEvade) DrunkenEvades++;
            if (drunkenEvade || BloodRain.Core.Build.Evade.Roll(evadeChance, _rng))
            {
                // 피함: 피해·넉백 없음. 같은 접촉에 매 프레임 다시 굴리지 않게 맞은 뒤 무적은 준다(짧은 잔상)
                Evades++;
                _iframe = hitIframes;
                _evadeFlash = 0.3f;
                return false;
            }
            if (block != null && block())
            {
                // C-07b 금종조: 이번 피해는 없던 일로(맞은 뒤 무적은 준다)
                Blocks++;
                _iframe = hitIframes;
                _evadeFlash = 0.3f;
                return false;
            }
            float taken = Mathf.Max(_minDamage, damage - armor) * takenMultiplier;
            if (deflect != null) taken = Mathf.Max(0f, deflect(taken));
            Health -= taken;
            DamageTaken += taken;
            Hits++;
            _iframe = hitIframes;
            _flash = SettingsScreen.Current.FlashOn ? 0.25f : 0f; // WP-57U 설정 '피격 번쩍임'
            Damaged?.Invoke();
            if (Health <= 0f)
            {
                Health = 0f;
                Died?.Invoke();
                if (refillOnDeath) Health = MaxHealth;
            }
            return true;
        }

        /// <summary>C-06a 최대 기혈을 바꾼다. 늘면 는 만큼 지금 기혈도 채우고, 줄면 넘친 만큼만 깎는다.</summary>
        public void SetMaxHealth(float max)
        {
            max = Mathf.Max(1f, max);
            if (Mathf.Approximately(max, MaxHealth)) return;
            float gain = max - MaxHealth;
            MaxHealth = max;
            if (Health > 0f) Health = Mathf.Clamp(Health + Mathf.Max(0f, gain), 1f, MaxHealth);
        }

        /// <summary>시험: 지금 쓰러진다(메뉴 '지금 쓰러지기').</summary>
        public void ForceDown()
        {
            if (Health <= 0f) return;
            Health = 0f;
            Died?.Invoke();
            if (refillOnDeath) Health = MaxHealth;
        }

        /// <summary>쓰러진 뒤 기혈을 이만큼으로 되살린다(서장 각성은 1). 맞은 뒤 무적도 준다.</summary>
        public void Revive(float health)
        {
            Health = Mathf.Clamp(health, 1f, MaxHealth);
            _iframe = hitIframes;
        }

        /// <summary>R4-10U 천마해체 기혈 소모. 호신·회피·무적을 거치지 않고 그대로 뺀다(5장: 바닥까지 깎을 수 있다). 실제로 뺀 양.</summary>
        public float Drain(float amount)
        {
            if (amount <= 0f || Health <= 0f) return 0f;
            float before = Health;
            Health = Mathf.Max(0f, Health - amount);
            Drained += before - Health;
            if (Health <= 0f)
            {
                Died?.Invoke();
                if (refillOnDeath) Health = MaxHealth;
            }
            return before - Health;
        }
        /// <summary>시험용: Drain 으로 뺀 기혈 합.</summary>
        public float Drained { get; private set; }

        /// <summary>기혈을 채운다(최대를 넘지 않음). 실제로 찬 양.</summary>
        public float Heal(float amount)
        {
            if (amount <= 0f || Health <= 0f) return 0f;
            float before = Health;
            Health = Mathf.Min(MaxHealth, Health + amount * healingMultiplier);
            Healed += Health - before;
            return Health - before;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _iframe = Mathf.Max(0f, _iframe - dt);
            _flash = Mathf.Max(0f, _flash - dt);
            _evadeFlash = Mathf.Max(0f, _evadeFlash - dt);
            if (recoveryPerSecond > 0f && Health < MaxHealth) Heal(recoveryPerSecond * dt);
            if (_sr != null)
            {
                if (_evadeFlash > 0f)
                {
                    // 피한 잔상: 옅은 청록으로 반쯤 비친다
                    var ghost = new Color(0.55f, 0.9f, 0.95f, 0.35f);
                    _sr.color = Color.Lerp(_baseColor, ghost, _evadeFlash / 0.3f);
                }
                else _sr.color = _flash > 0f ? Color.Lerp(_baseColor, Color.white, _flash / 0.25f) : _baseColor;
            }
            if (_look != null && _look.Renderer != null)
            {
                // 13장 '피격: 흰색 번쩍임 셰이더'. 피함·금종조는 지금처럼 청록으로 반쯤 비침
                var ghost = new Color(0.55f, 0.9f, 0.95f, 0.35f);
                _look.Renderer.color = _evadeFlash > 0f ? Color.Lerp(Color.white, ghost, _evadeFlash / 0.3f) : Color.white;
                _look.Flash = _evadeFlash > 0f ? 0f : _flash / 0.25f;
            }
        }

        void OnGUI()
        {
            if (!showStats) return;
            GUI.Label(new Rect(12, 32, 520, 22), $"기혈 {Health:0}/{MaxHealth:0}  맞음 {Hits}  받은 피해 {DamageTaken:0}  피함 {Evades}  회복 {Healed:0.#}");
        }
    }
}
