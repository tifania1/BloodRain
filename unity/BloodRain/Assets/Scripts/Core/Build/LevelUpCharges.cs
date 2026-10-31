#nullable enable
// 레벨업 화면의 다시 고르기·파기·정진 남은 횟수(기획서 '8. 전투 시스템' 레벨업 규칙).
// 한 판에 쓸 수 있는 횟수 = 연무장(StatKind.Rerolls/Banishes/Skips) + 몸의 성질 같은 덧셈
//   + 다시 고르기만 캐릭터 특성 몫(balance.json levelUp.traitRerolls, 진소운 +2).
// 판 안에서 늘어나는 몫(보부상의 다시 고르기 권 등)은 Add 로 더한다.
// 정진은 진기 게이지 20(UltimateCharge.OnSkip), 파기는 Loadout.Banish 로 이어진다.
using System;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public enum LevelUpAction { Reroll, Banish, Skip }

    public sealed class LevelUpCharges
    {
        public int Rerolls { get; private set; }
        public int Banishes { get; private set; }
        public int Skips { get; private set; }

        public LevelUpCharges(int rerolls, int banishes, int skips)
        {
            if (rerolls < 0 || banishes < 0 || skips < 0) throw new ArgumentOutOfRangeException("횟수는 0 이상입니다");
            Rerolls = rerolls;
            Banishes = banishes;
            Skips = skips;
        }

        /// <summary>이 캐릭터의 특성이 주는 다시 고르기 몫. 데이터에 없으면 0.</summary>
        public static int TraitRerolls(LevelUpDef rules, CharacterDef character) =>
            character.Trait != null && rules.TraitRerolls.TryGetValue(character.Trait, out var n) ? n : 0;

        /// <summary>판을 시작할 때의 횟수. bonus 는 연무장 + 몸의 성질을 더한 것.</summary>
        public static LevelUpCharges Start(LevelUpDef rules, CharacterDef character, StatBonus bonus) =>
            new LevelUpCharges(
                Count(bonus[StatKind.Rerolls]) + TraitRerolls(rules, character),
                Count(bonus[StatKind.Banishes]),
                Count(bonus[StatKind.Skips]));

        static int Count(float v) => Math.Max(0, (int)Math.Round(v));

        public int Left(LevelUpAction a) => a switch
        {
            LevelUpAction.Reroll => Rerolls,
            LevelUpAction.Banish => Banishes,
            _ => Skips,
        };

        public bool Can(LevelUpAction a) => Left(a) > 0;

        /// <summary>판 안에서 얻은 횟수(다시 고르기 권 등).</summary>
        public void Add(LevelUpAction a, int n = 1)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
            switch (a)
            {
                case LevelUpAction.Reroll: Rerolls += n; break;
                case LevelUpAction.Banish: Banishes += n; break;
                default: Skips += n; break;
            }
        }

        void Use(LevelUpAction a)
        {
            if (!Can(a)) throw new InvalidOperationException($"{a} 횟수가 남지 않았습니다");
            switch (a)
            {
                case LevelUpAction.Reroll: Rerolls--; break;
                case LevelUpAction.Banish: Banishes--; break;
                default: Skips--; break;
            }
        }

        /// <summary>다시 고르기. 새 선택지는 부른 쪽이 Loadout.RollLevelUp 으로 다시 뽑는다.</summary>
        public void Reroll() => Use(LevelUpAction.Reroll);

        /// <summary>파기. 그 선택지(무공·심법 id)를 이번 판에서 영영 뺀다.</summary>
        public void Banish(Loadout loadout, string id)
        {
            Use(LevelUpAction.Banish);
            loadout.Banish(id);
        }

        /// <summary>정진. 아무것도 안 고르고 진기 게이지를 정해진 양(20)만큼 얻는다.</summary>
        public void Skip(UltimateCharge charge)
        {
            Use(LevelUpAction.Skip);
            charge.OnSkip();
        }
    }
}
