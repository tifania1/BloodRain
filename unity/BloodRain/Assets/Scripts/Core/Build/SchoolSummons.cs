#nullable enable
// 판 동안 시계와 횟수를 들고 있어야 하는 진전 효과(synergies.json, 기획서 20장 진전 표).
//   arhat_ring     소림 4  기혈 비율이 healthThreshold 이상이었다가 그 아래로 떨어지는 순간 seconds 동안 나한 허상 images 개가 몸을 둘러싼다.
//                          판마다 timesPerRun 번. 둘러싼 동안은 다시 켜지지 않고, 다시 켜지려면 기혈이 문턱 위로 올라갔다가 또 떨어져야 한다.
//                          단계를 얻을 때 이미 문턱 아래였으면 그때는 켜지지 않는다('떨어지면').
//   beggar_allies  개방 4  단계가 켜져 있는 동안 시계가 돌아 intervalSeconds 마다 개방 제자 allies 명이 seconds 동안 함께 싸운다.
//                          첫 제자는 단계를 얻고 intervalSeconds 뒤. 단계가 꺼지면 시계는 멈췄다가 다시 켜지면 이어 간다.
//   허상이 밀쳐 내는 세기·둘레, 제자의 피해·기혈·움직임은 기획서에 숫자가 없어 params 에 없다(backlog R-07b). 그리는 것과 싸우는 모양은 Unity.
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

        public SummonEvents(bool arhatStarted, int alliesSpawned) { ArhatStarted = arhatStarted; AlliesSpawned = alliesSpawned; }
    }

    public sealed class SchoolSummons
    {
        float _lastHealthRatio = 1f;
        float _beggarClock;

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

        public SummonEvents Tick(SynergyEffects fx, float healthRatio, float dt)
        {
            if (dt < 0f) dt = 0f;
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
                }
            }
            return new SummonEvents(arhat, allies);
        }
    }
}
