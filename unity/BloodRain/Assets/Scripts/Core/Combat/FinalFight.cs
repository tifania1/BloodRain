// 종장 혈마 단목현 싸움의 차례(기획서 6장 '종장 보스', bosspatterns.json final). T-14 뼈대, T-39 단계 패턴 수치.
// 땅의 주인 일곱의 혈영(기준 체력 × 0.4, 패턴은 그 보스 것)을 차례로 넘긴 뒤 혈의 → 혈해 → 진혈마.
// 진엔딩 조건을 채웠으면 진혈마 기혈이 10%에 닿을 때 목 노인 컷신으로 넘어간다(DL-11, 2026-11-05 사람이 추천대로):
//   싸움을 멈추고 진엔딩 컷 1(목 노인 "오래 앓았구나.")을 보여 준 뒤 고르는 화면. 봉인·계승이면 싸움으로 돌아가 마저 쓰러뜨린 뒤 그 엔딩 컷,
//   의심이면 싸움이 거기서 끝나고 진엔딩 컷 2부터(FinalDialogue.EndingCutsAfterTrueCut). 컷은 한 싸움에 한 번만.
// 기혈은 기준값이다. 실제 기혈은 싸움을 건 분으로 BossAwakening.Health 와 같은 식을 곱한다.
#nullable enable
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
            if (_def.Shadows.Count == 0) throw new InvalidOperationException("bosspatterns.json final 에 혈영(shadows)이 없습니다"); // RisingShadow 가 % 로 돈다
        }

        /// <summary>지금 몇 번째 상대인가(0부터, 혈영 일곱 다음 단계 셋).</summary>
        public int Index { get; private set; }
        public int Count => _def.Shadows.Count + _def.Stages.Count;
        public bool Done => Index >= Count;
        public bool IsShadow => !Done && Index < _def.Shadows.Count;
        /// <summary>진혈마 10% 에서 목 노인 컷으로 넘어갔다(한 싸움에 한 번).</summary>
        public bool CutToTrueEnding { get; private set; }
        /// <summary>목 노인 컷 뒤 엔딩을 고르는 중이라 싸움이 멈춰 있다. ChooseAtTrueCut 로 푼다.</summary>
        public bool AwaitingEndingChoice { get; private set; }
        /// <summary>목 노인 컷 뒤 고른 엔딩(고르기 전이면 null).</summary>
        public string? ChosenAtTrueCut { get; private set; }

        /// <summary>지금 상대의 보스 id(혈영이면 그 땅의 주인, 단계면 혈마).</summary>
        public string BossId => Done ? throw new InvalidOperationException("종장이 끝났습니다") : IsShadow ? _def.Shadows[Index] : _def.Boss;

        /// <summary>지금 혈마 단계. 혈영 차례면 null.</summary>
        public FinalStageDef? Stage => Done || IsShadow ? null : _def.Stages[Index - _def.Shadows.Count];

        /// <summary>지금 상대의 기준 기혈.</summary>
        public float BaseHealth => IsShadow
            ? _data.Bosses.First(b => b.Id == BossId).Health * _def.ShadowHealthRatio
            : Stage?.Health ?? 0f;

        /// <summary>혈영이면 그 보스의 패턴 시간표를 그대로, 혈마 단계면 그 단계의 시간표. 다 끝났으면 null.</summary>
        public BossPatternSetDef? Patterns => Done ? null : IsShadow ? _data.BossPatternSet(BossId) : Stage!.Set;

        /// <summary>혈해의 혈영 솟음이 n번째(0부터)로 불러낼 땅의 주인. shadows 차례로 돈다.</summary>
        public string RisingShadow(int n) => _def.Shadows[((n % _def.Shadows.Count) + _def.Shadows.Count) % _def.Shadows.Count];

        /// <summary>솟은 혈영의 기준 기혈(그 보스 기준 체력 × 패턴의 SummonHealthRatio).</summary>
        public float RisingShadowHealth(int n, BossPatternDef pattern) =>
            _data.Bosses.First(b => b.Id == RisingShadow(n)).Health * pattern.SummonHealthRatio;

        /// <summary>지금 상대를 쓰러뜨렸다. 다음 차례로.</summary>
        public void Defeat()
        {
            if (Done) throw new InvalidOperationException("종장이 끝났습니다");
            if (AwaitingEndingChoice) throw new InvalidOperationException("목 노인 컷 뒤 엔딩을 고르는 중입니다");
            Index++;
        }

        /// <summary>진엔딩 컷으로 넘어갈 때인가. 넘어가면 싸움이 멈추고(AwaitingEndingChoice) 진엔딩 컷 1 → 고르는 화면.
        /// 한 싸움에 한 번만 true.</summary>
        public bool CheckTrueEnding(float healthRatio, bool trueEndingMet)
        {
            if (CutToTrueEnding) return false;
            var st = Stage;
            if (st == null || st.TrueEndingCutAt <= 0 || !trueEndingMet || healthRatio > st.TrueEndingCutAt) return false;
            CutToTrueEnding = true;
            AwaitingEndingChoice = true;
            return true;
        }

        /// <summary>목 노인 컷 뒤 고르는 화면에서 엔딩을 골랐다. 의심(true)이면 싸움이 끝나고(Done) false,
        /// 봉인·계승이면 싸움으로 돌아가(진혈마 남은 기혈 그대로) true — 마저 쓰러뜨리면(Defeat) 그 엔딩 컷.</summary>
        public bool ChooseAtTrueCut(string ending)
        {
            if (!AwaitingEndingChoice) throw new InvalidOperationException("목 노인 컷 뒤 고를 때가 아닙니다");
            if (string.IsNullOrEmpty(ending)) throw new ArgumentException("엔딩이 비었습니다", nameof(ending));
            AwaitingEndingChoice = false;
            ChosenAtTrueCut = ending;
            if (ending == "true") { Index = Count; return false; }
            return true;
        }
    }
}
