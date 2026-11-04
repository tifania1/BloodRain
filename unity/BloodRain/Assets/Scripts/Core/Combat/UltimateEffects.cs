#nullable enable
// 오의를 쓴 뒤 남는 효과(기획서 5장 '오의 상세'). 한 방 피해는 Ultimates, 여기는 그 밖의 것. 값은 characters.json ultimates[].params.
//   천설부(곤륜)   화면 안 모든 적 freezeSeconds(3초) 얼림. 피해 공력 x8 은 강시 계열(enemies.json family "jiangshi")에만.
//                  땅의 주인도 얼린다(5장 '모든 적'). 얼어 있는 동안은 6장 '보스가 기절한 동안은 패턴 시계도 멈춘다'와 같이 다룬다.
//                  부적 진전 '정신부로 멈춘 적이 두 배'(talisman_freeze)는 정신부 무공 몫이라 여기 곱하지 않는다.
//   천마해체(마교) durationSeconds(8초) 동안 초당 최대 기혈 × healthCostPerSecond(2%) 소모, 모든 피해 × damageMultiplier(2), 흡혈 + lifesteal(10%).
//                  음양 게이지가 시작값으로 돌아가고 마공 심마환영이 사라지는 것은 Life.UseUltimate(혈공 BloodArt.OnUltimate)가 이미 한다.
//                  기혈 소모가 기혈을 바닥까지 깎을 수 있는지는 5장에 따로 없어 그대로 뺀다('위험을 걸고 몰아치기').
//                  흡혈도 회복이라 마교 '회복 효과 -50%'(SectTrait.HealingMultiplier)와 마도 진전 '흡혈 두 배'가 곱해진다(LifestealHeal).
// 현경 '오의 2연속'(Life.UltimateCasts)이면 천설부는 두 번 얼리고(시간은 늘지 않고 새로 3초), 천마해체는 8초를 새로 시작한다(겹쳐 16초가 되지 않는다).
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public static class UltimateEffects
    {
        public const string CheonseolBu = "cheonseol_bu";
        public const string CheonmaHaeche = "cheonma_haeche";
        /// <summary>천설부 피해가 들어가는 적 계열.</summary>
        public const string JiangshiFamily = "jiangshi";

        /// <summary>오의가 적을 얼리는 초(천설부 3, 나머지 0).</summary>
        public static float FreezeSeconds(UltimateDef ult) => ult.Param("freezeSeconds", 0f);

        /// <summary>그 계열 적이 오의 한 방을 맞는가. 천설부는 강시 계열만, 나머지 오의는 모두.</summary>
        public static bool Damages(UltimateDef ult, string enemyFamily) =>
            ult.Id != CheonseolBu || enemyFamily == JiangshiFamily;

        /// <summary>그 적에게 들어가는 오의 한 방 피해. startWeaponHit 는 Ultimates.StartWeaponHit, powerMultiplier 는 Life.UltimatePowerMultiplier.</summary>
        public static float HitDamageOn(UltimateDef ult, string enemyFamily, float startWeaponHit, float powerMultiplier = 1f) =>
            Damages(ult, enemyFamily) ? Ultimates.HitDamage(ult, startWeaponHit, powerMultiplier) : 0f;

        /// <summary>적 하나를 맞힌 피해로 채우는 기혈. lifesteal 은 능력치 흡혈 + 천마해체 몫(DemonDissolution.LifestealBonus),
        /// lifestealMultiplier 는 SynergyEffects.LifestealMultiplier, healingMultiplier 는 SectTrait.HealingMultiplier.</summary>
        public static float LifestealHeal(float damageDealt, float lifesteal, float lifestealMultiplier = 1f, float healingMultiplier = 1f) =>
            damageDealt <= 0f || lifesteal <= 0f ? 0f : damageDealt * lifesteal * lifestealMultiplier * healingMultiplier;
    }

    /// <summary>천마해체 8초. Life 가 마교 몸이면 들고, UseUltimate 때 시작하고 Tick 이 세상 시간으로 줄인다.
    /// Sim 은 켜져 있는 동안 내는 피해에 DamageMultiplier 를 곱하고, 매 프레임 HealthCostPerSecond(최대 기혈) × dt 만큼 기혈을 뺀다.</summary>
    public sealed class DemonDissolution
    {
        readonly float _duration;
        readonly float _costPerSecond;
        readonly float _damageMultiplier;
        readonly float _lifesteal;

        public DemonDissolution(UltimateDef ult)
        {
            _duration = ult.Param("durationSeconds");
            _costPerSecond = ult.Param("healthCostPerSecond");
            _damageMultiplier = ult.Param("damageMultiplier");
            _lifesteal = ult.Param("lifesteal");
        }

        /// <summary>남은 초.</summary>
        public float SecondsLeft { get; private set; }
        public bool IsActive => SecondsLeft > 0f;

        /// <summary>새로 8초. 켜져 있을 때 또 쓰면 남은 시간을 8초로 되돌린다(더하지 않는다).</summary>
        public void Start() => SecondsLeft = _duration;

        /// <summary>세상 시간 dt.</summary>
        public void Tick(float dt)
        {
            if (SecondsLeft <= 0f) return;
            SecondsLeft = SecondsLeft > dt ? SecondsLeft - dt : 0f;
        }

        /// <summary>플레이어가 내는 모든 피해에 곱한다(켜져 있으면 2, 아니면 1).</summary>
        public float DamageMultiplier => IsActive ? _damageMultiplier : 1f;

        /// <summary>능력치 흡혈에 더하는 몫(켜져 있으면 0.1).</summary>
        public float LifestealBonus => IsActive ? _lifesteal : 0f;

        /// <summary>초당 빼는 기혈(최대 기혈 × 2%). 꺼져 있으면 0.</summary>
        public float HealthCostPerSecond(float maxHealth) => IsActive ? maxHealth * _costPerSecond : 0f;
    }
}
