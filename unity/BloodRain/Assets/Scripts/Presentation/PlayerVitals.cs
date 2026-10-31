using System;
using BloodRain.Core.Meta;
using BloodRain.Presentation.Meta;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 플레이어 기혈과 맞기(C-03). 적 접촉은 EnemyField 가 격자로 찾아 Hit 으로 넘긴다.
    //   받는 피해 = max(contact.minDamage, 피해 − 방어)(웹 hurtPlayer 와 같다. 방어는 능력치가 붙으면).
    //   경공 대시 무적 중이거나 맞은 뒤 무적(웹 프로토타입 0.5초, balance.json 에 아직 없음) 중이면 무시.
    // 쓰러지면 Died 를 부른다. 정산(TU-13)으로 넘기는 연결은 C-10 에서. 시험 중엔 refillOnDeath 로 다시 채운다.
    public sealed class PlayerVitals : MonoBehaviour
    {
        [Tooltip("맞은 뒤 무적(초). 웹 프로토타입 값")]
        public float hitIframes = 0.5f;
        public float armor;
        public bool refillOnDeath = true;
        public bool showStats = true;

        public float MaxHealth { get; private set; } = 100f;
        public float Health { get; private set; } = 100f;
        public int Hits { get; private set; }
        public float DamageTaken { get; private set; }
        public event Action Died;

        float _iframe;
        float _minDamage = 1f;
        PlayerController _move;
        SpriteRenderer _sr;
        Color _baseColor;
        float _flash;

        void Awake()
        {
            var data = DataBoot.Data;
            var body = RebirthHandoff.Body ?? Rebirth.Prologue(data);
            MaxHealth = Health = body.Character.MaxHealth;
            _minDamage = data.Balance.Contact.MinDamage;
            _move = GetComponent<PlayerController>();
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        public bool Invulnerable => _iframe > 0f || (_move != null && _move.Invulnerable);

        /// <summary>피해를 받는다. 실제로 들어갔으면 true.</summary>
        public bool Hit(float damage)
        {
            if (Invulnerable || Health <= 0f) return false;
            float taken = Mathf.Max(_minDamage, damage - armor);
            Health -= taken;
            DamageTaken += taken;
            Hits++;
            _iframe = hitIframes;
            _flash = 0.25f;
            if (Health <= 0f)
            {
                Health = 0f;
                Died?.Invoke();
                if (refillOnDeath) Health = MaxHealth;
            }
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _iframe = Mathf.Max(0f, _iframe - dt);
            _flash = Mathf.Max(0f, _flash - dt);
            if (_sr != null) _sr.color = _flash > 0f ? Color.Lerp(_baseColor, Color.white, _flash / 0.25f) : _baseColor;
        }

        void OnGUI()
        {
            if (!showStats) return;
            GUI.Label(new Rect(12, 32, 520, 22), $"기혈 {Health:0}/{MaxHealth:0}  맞음 {Hits}  받은 피해 {DamageTaken:0}");
        }
    }
}
