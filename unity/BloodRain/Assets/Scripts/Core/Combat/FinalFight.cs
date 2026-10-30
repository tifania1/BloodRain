// 종장 혈마 단목현 싸움의 차례(기획서 6장 '종장 보스', bosspatterns.json final). T-14 뼈대.
// 땅의 주인 일곱의 혈영(기준 체력 × 0.4, 패턴은 그 보스 것)을 차례로 넘긴 뒤 혈의 → 혈해 → 진혈마.
// 진엔딩 조건을 채웠으면 진혈마 기혈이 10%에 닿을 때 목 노인 컷신으로 넘어간다.
// 기혈은 기준값이다. 실제 기혈은 싸움을 건 분으로 BossAwakening.Health 와 같은 식을 곱한다.
using System;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public sealed class FinalFight
    {
        readonly GameData _data;
        readonly FinalBossDef _def;

        public FinalFight(GameData data)
        {
            _data = data;
            _def = data.FinalBoss;
            if (_def.Stages.Count == 0) throw new InvalidOperationException("bosspatterns.json final 에 단계가 없습니다");
        }

        /// <summary>지금 몇 번째 상대인가(0부터, 혈영 일곱 다음 단계 셋).</summary>
        public int Index { get; private set; }
        public int Count => _def.Shadows.Count + _def.Stages.Count;
        public bool Done => Index >= Count;
        public bool IsShadow => !Done && Index < _def.Shadows.Count;
        /// <summary>진엔딩 컷으로 넘어갔다.</summary>
        public bool CutToTrueEnding { get; private set; }

        /// <summary>지금 상대의 보스 id(혈영이면 그 땅의 주인, 단계면 혈마).</summary>
        public string BossId => Done ? throw new InvalidOperationException("종장이 끝났습니다") : IsShadow ? _def.Shadows[Index] : _def.Boss;

        /// <summary>지금 혈마 단계. 혈영 차례면 null.</summary>
        public FinalStageDef? Stage => Done || IsShadow ? null : _def.Stages[Index - _def.Shadows.Count];

        /// <summary>지금 상대의 기준 기혈.</summary>
        public float BaseHealth => IsShadow
            ? _data.Bosses.First(b => b.Id == BossId).Health * _def.ShadowHealthRatio
            : Stage?.Health ?? 0f;

        /// <summary>혈영이면 그 보스의 패턴 시간표를 그대로 쓴다. 혈마 단계는 아직 수치가 없어 null.</summary>
        public BossPatternSetDef? Patterns => IsShadow ? _data.BossPatternSet(BossId) : null;

        /// <summary>지금 상대를 쓰러뜨렸다. 다음 차례로.</summary>
        public void Defeat()
        {
            if (Done) throw new InvalidOperationException("종장이 끝났습니다");
            Index++;
        }

        /// <summary>진엔딩 컷으로 넘어갈 때인가. 넘어가면 싸움은 그대로 끝난다(Done).</summary>
        public bool CheckTrueEnding(float healthRatio, bool trueEndingMet)
        {
            var st = Stage;
            if (st == null || st.TrueEndingCutAt <= 0 || !trueEndingMet || healthRatio > st.TrueEndingCutAt) return false;
            CutToTrueEnding = true;
            Index = Count;
            return true;
        }
    }
}
