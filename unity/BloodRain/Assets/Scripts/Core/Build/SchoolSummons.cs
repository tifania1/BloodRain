#nullable enable
// 판 동안 시계와 횟수를 들고 있어야 하는 진전 효과(synergies.json, 기획서 20장 진전 표).
//   arhat_ring     소림 4  기혈 비율이 healthThreshold 이상이었다가 그 아래로 떨어지는 순간 seconds 동안 나한 허상 images 개가 몸을 둘러싼다.
//                          판마다 timesPerRun 번. 둘러싼 동안은 다시 켜지지 않고, 다시 켜지려면 기혈이 문턱 위로 올라갔다가 또 떨어져야 한다.
//                          단계를 얻을 때 이미 문턱 아래였으면 그때는 켜지지 않는다('떨어지면').
//   beggar_allies  개방 4  단계가 켜져 있는 동안 시계가 돌아 intervalSeconds 마다 개방 제자 allies 명이 seconds 동안 함께 싸운다.
//                          첫 제자는 단계를 얻고 intervalSeconds 뒤. 단계가 꺼지면 시계는 멈췄다가 다시 켜지면 이어 간다.
//   허상·제자 숫자(R-07b, 2026-11-05 사람이 추천대로, 20장 표 아래):
//     허상은 몸 둘레 radius(1.8m, 나한권 사거리) 원, 닿는 잡몹·정예를 knockback(8, 각성 파동 eliteKnockback)으로 밀고 피해는 없다. 땅의 주인은 안 밀린다(ArhatPush).
//     제자는 적이 노리지 않는 동료(기혈 없음), seconds 뒤 사라진다. 각자 attackSeconds(1초)마다 가장 가까운 적에게 장풍 Lv1 한 대 — damage(20) x 공력, 넉백 knockback(5).
//     탄 모양은 weapons.json palm_wind shot 그대로. 첫 대는 나타나고 attackSeconds 뒤(SummonEvents.AllyVolleys).
//   그리는 것과 싸우는 모양은 Unity.
//
// 한 생에 하나 만들고, 프레임마다 그때의 SynergyEffects 와 기혈 비율로 Tick 을 부른다(진전이 바뀌어도 이 상태는 그대로 이어 간다).
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    /// <summary>한 번의 Tick 에서 새로 일어난 일.</summary>
    public readonly struct SummonEvents
    {
        /// <summary>이번에 나한 허상이 켜졌다.</summary>
        public readonly bool ArhatStarted;
        /// <summary>이번에 나타난 개방 제자 수(시간이 한꺼번에 많이 흐르면 여러 무리).</summary>
        public readonly int AlliesSpawned;
        /// <summary>이번에 제자마다 장풍을 몇 대 치나(1초마다 한 대). 제자 한 명이 이만큼, 다 함께는 x 지금 제자 수.</summary>
        public readonly int AllyVolleys;

        public SummonEvents(bool arhatStarted, int alliesSpawned, int allyVolleys = 0)
        { ArhatStarted = arhatStarted; AlliesSpawned = alliesSpawned; AllyVolleys = allyVolleys; }
    }

    public sealed class SchoolSummons
    {
        float _lastHealthRatio = 1f;
        float _beggarClock;
        float _allyAttackClock;

        /// <summary>이 판에서 나한 허상이 켜진 횟수.</summary>
        public int ArhatUsed { get; private set; }
        /// <summary>나한 허상이 남은 초. 0 이면 꺼져 있다.</summary>
        public float ArhatSecondsLeft { get; private set; }
        public bool ArhatActive => ArhatSecondsLeft > 0f;
        /// <summary>마지막으로 나타난 개방 제자들이 남은 초. 0 이면 없다.</summary>
        public float AlliesSecondsLeft { get; private set; }
        public bool AlliesActive => AlliesSecondsLeft > 0f;

        /// <summary>다음 개방 제자까지 남은 초(단계가 없으면 null). HUD 용.</summary>
        public float? SecondsToNextAllies(SynergyEffects fx) =>
            fx.Tier("beggar_allies") is SchoolTierDef t ? t.Param("intervalSeconds") - _beggarClock : (float?)null;

        /// <summary>나한 허상이 이 적을 밀어내는 넉백 세기. 허상이 꺼져 있거나 둘레(radius × 축척) 밖이거나 땅의 주인이면 0. 피해는 없다.</summary>
        public float ArhatPush(SynergyEffects fx, float distance, bool isBoss, float scale = 1f) =>
            ArhatActive && !isBoss && fx.Tier("arhat_ring") is SchoolTierDef a && distance <= a.Param("radius") * scale ? a.Param("knockback") : 0f;

        /// <summary>개방 제자 장풍 한 대: 피해(damage x 공력)와 넉백. 단계가 없으면 (0, 0).</summary>
        public static (float Damage, float Knockback) AllyShot(SynergyEffects fx, float might) =>
            fx.Tier("beggar_allies") is SchoolTierDef b ? (b.Param("damage") * might, b.Param("knockback")) : (0f, 0f);

        /// <summary>지금 함께 싸우는 개방 제자 수(마지막 무리, 사라지면 0).</summary>
        public int AlliesPresent { get; private set; }

        public SummonEvents Tick(SynergyEffects fx, float healthRatio, float dt)
        {
            if (dt < 0f) dt = 0f;
            // 제자 장풍: 이번 dt 동안 아직 남아 있던 시간만큼 attackSeconds 마다 한 대(사라지는 순간까지).
            int volleys = 0;
            if (AlliesSecondsLeft > 0f && fx.Tier("beggar_allies") is SchoolTierDef bt && bt.Param("attackSeconds") > 0f)
            {
                float every = bt.Param("attackSeconds");
                _allyAttackClock += System.Math.Min(dt, AlliesSecondsLeft);
                while (_allyAttackClock >= every - 1e-4f) { _allyAttackClock -= every; volleys++; }
            }
            ArhatSecondsLeft = ArhatSecondsLeft > dt ? ArhatSecondsLeft - dt : 0f;
            AlliesSecondsLeft = AlliesSecondsLeft > dt ? AlliesSecondsLeft - dt : 0f;

            bool arhat = false;
            if (fx.Tier("arhat_ring") is SchoolTierDef a)
            {
                float th = a.Param("healthThreshold");
                if (_lastHealthRatio >= th && healthRatio < th && !ArhatActive && ArhatUsed < (int)a.Param("timesPerRun"))
                {
                    ArhatUsed++;
                    ArhatSecondsLeft = a.Param("seconds");
                    arhat = true;
                }
            }
            _lastHealthRatio = healthRatio;

            int allies = 0;
            if (fx.Tier("beggar_allies") is SchoolTierDef b)
            {
                float interval = b.Param("intervalSeconds");
                _beggarClock += dt;
                while (interval > 0f && _beggarClock >= interval)   // 간격 0 이면 프레임이 멈추지 않게
                {
                    _beggarClock -= interval;
                    allies += (int)b.Param("allies");
                    AlliesSecondsLeft = b.Param("seconds");
                    AlliesPresent = (int)b.Param("allies");
                    _allyAttackClock = 0f;   // 새 무리는 나타나고 attackSeconds 뒤 첫 대
                }
            }
            if (!AlliesActive && allies == 0) AlliesPresent = 0;
            return new SummonEvents(arhat, allies, volleys);
        }
    }
}
