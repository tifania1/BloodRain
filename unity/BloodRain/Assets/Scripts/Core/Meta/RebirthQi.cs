#nullable enable
// 윤회의 기 정산(기획서 10장 '윤회의 기').
//   윤회의 기 = (1 + 성질 보너스) × (perOrbValue × Σ(진기 구슬 값 × 그 자리 혈운 농도) + 보너스 + leftoverSilverRate × 남은 은자)
//   진기 구슬 값은 심득 배율을 곱하기 전 값이다(RunXp.Absorb 에 넘기는 값 그대로). 심득은 레벨만 올리고 윤회의 기는 늘리지 않는다.
//   보너스가 '처음'인지는 계정 기록을 아는 쪽(Meta.LifeLog)이 정해서 넣는다. 장부는 받아 적고 더하기만 한다.
//   정산 화면 내역은 줄마다 정수로 반올림하고 합계는 줄의 합이다. 화면 숫자를 더하면 합계와 같다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public enum RebirthQiBonusKind { BossFirstKill, BossRepeatKill, FirstVisitRegion, FirstReachRealm }

    /// <summary>정산 화면 줄. 위에서부터 이 순서로 보여 준다.</summary>
    public enum RebirthQiLineKind { Orbs, BossFirstKill, BossRepeatKill, FirstVisitRegion, FirstReachRealm, Silver, Traits }

    /// <summary>보너스 하나(무엇 때문에, 얼마).</summary>
    public readonly struct RebirthQiBonusEntry
    {
        public readonly RebirthQiBonusKind Kind;
        /// <summary>지역 id(땅의 주인·처음 밟은 땅) 또는 경지 id(처음 오른 경지).</summary>
        public readonly string Id;
        public readonly int Amount;
        public RebirthQiBonusEntry(RebirthQiBonusKind kind, string id, int amount) { Kind = kind; Id = id; Amount = amount; }
    }

    /// <summary>정산 화면 한 줄: 종류, 몇 번(구슬 수·보너스 횟수, 은자는 남은 은자, 성질은 0), 윤회의 기.</summary>
    public readonly struct RebirthQiLine
    {
        public readonly RebirthQiLineKind Kind;
        public readonly long Count;
        public readonly int Amount;
        public RebirthQiLine(RebirthQiLineKind kind, long count, int amount) { Kind = kind; Count = count; Amount = amount; }
        public override string ToString() => $"{Kind} x{Count} = {Amount}";
    }

    /// <summary>한 생의 윤회의 기 정산 결과.</summary>
    public sealed class RebirthQiResult
    {
        /// <summary>진기 몫: perOrbValue × Σ(구슬 값 × 농도), 반올림.</summary>
        public int FromOrbs { get; }
        /// <summary>보너스 하나하나(받은 순서).</summary>
        public IReadOnlyList<RebirthQiBonusEntry> Bonuses { get; }
        public int FromBonuses => Bonuses.Sum(b => b.Amount);
        /// <summary>남은 은자 몫: leftoverSilverRate × 남은 은자, 반올림.</summary>
        public int FromSilver { get; }
        public int LeftoverSilver { get; }
        /// <summary>성질 배율 전 합.</summary>
        public int Subtotal => FromOrbs + FromBonuses + FromSilver;
        public float TraitBonus { get; }
        /// <summary>성질 보너스로 더해진 몫: Subtotal × 성질 보너스, 반올림.</summary>
        public int FromTraits { get; }
        public int Total => Subtotal + FromTraits;
        /// <summary>흡수한 구슬 수와 구슬 값 합(심득 전, 농도 곱하기 전). 정산 화면 설명용.</summary>
        public long OrbCount { get; }
        public double OrbValue { get; }

        public RebirthQiResult(int fromOrbs, IReadOnlyList<RebirthQiBonusEntry> bonuses, int fromSilver, int leftoverSilver,
            float traitBonus, int fromTraits, long orbCount, double orbValue)
        {
            FromOrbs = fromOrbs;
            Bonuses = bonuses;
            FromSilver = fromSilver;
            LeftoverSilver = leftoverSilver;
            TraitBonus = traitBonus;
            FromTraits = fromTraits;
            OrbCount = orbCount;
            OrbValue = orbValue;
        }

        /// <summary>보너스 종류 하나의 합.</summary>
        public int BonusOf(RebirthQiBonusKind kind) => Bonuses.Where(b => b.Kind == kind).Sum(b => b.Amount);

        /// <summary>정산 화면 내역. 진기 → 보너스 종류별(받은 것만) → 은자 → 성질(보너스가 있을 때만). Amount 를 더하면 Total.</summary>
        public IReadOnlyList<RebirthQiLine> Lines
        {
            get
            {
                var lines = new List<RebirthQiLine> { new RebirthQiLine(RebirthQiLineKind.Orbs, OrbCount, FromOrbs) };
                foreach (RebirthQiBonusKind kind in Enum.GetValues(typeof(RebirthQiBonusKind)))
                {
                    var of = Bonuses.Where(b => b.Kind == kind).ToList();
                    if (of.Count > 0) lines.Add(new RebirthQiLine(LineOf(kind), of.Count, of.Sum(b => b.Amount)));
                }
                lines.Add(new RebirthQiLine(RebirthQiLineKind.Silver, LeftoverSilver, FromSilver));
                if (TraitBonus != 0) lines.Add(new RebirthQiLine(RebirthQiLineKind.Traits, 0, FromTraits));
                return lines;
            }
        }

        static RebirthQiLineKind LineOf(RebirthQiBonusKind kind) => kind switch
        {
            RebirthQiBonusKind.BossFirstKill => RebirthQiLineKind.BossFirstKill,
            RebirthQiBonusKind.BossRepeatKill => RebirthQiLineKind.BossRepeatKill,
            RebirthQiBonusKind.FirstVisitRegion => RebirthQiLineKind.FirstVisitRegion,
            _ => RebirthQiLineKind.FirstReachRealm,
        };
    }

    /// <summary>한 생의 윤회의 기 장부. 구슬을 먹을 때마다, 보너스를 받을 때마다 적고, 생이 끝나면 Settle.</summary>
    public sealed class RebirthQiLedger
    {
        readonly RebirthQiDef _def;
        readonly List<RebirthQiBonusEntry> _bonuses = new List<RebirthQiBonusEntry>();
        double _weighted;
        double _raw;
        long _orbs;

        /// <param name="traitBonus">타고난 성질의 윤회의 기 보너스 합(RebirthBody.RebirthQiBonus).</param>
        public RebirthQiLedger(RebirthQiDef def, float traitBonus = 0f)
        {
            _def = def;
            TraitBonus = traitBonus;
        }

        public float TraitBonus { get; }
        /// <summary>Σ(구슬 값 × 농도).</summary>
        public double WeightedOrbValue => _weighted;
        public double OrbValue => _raw;
        public long OrbCount => _orbs;
        public IReadOnlyList<RebirthQiBonusEntry> Bonuses => _bonuses;

        /// <summary>진기 구슬 하나(또는 한 프레임에 같은 농도에서 먹은 합). 값은 심득 배율 전.</summary>
        public void AddOrb(double orbValue, float density, int orbs = 1)
        {
            if (orbValue < 0) throw new ArgumentOutOfRangeException(nameof(orbValue));
            if (density <= 0) throw new ArgumentOutOfRangeException(nameof(density));
            _weighted += orbValue * density;
            _raw += orbValue;
            _orbs += orbs;
        }

        /// <summary>이 종류의 보너스 한 번이 몇인지(cheonha.json rebirthQi).</summary>
        public int AmountOf(RebirthQiBonusKind kind) => kind switch
        {
            RebirthQiBonusKind.BossFirstKill => _def.BossFirstKill,
            RebirthQiBonusKind.BossRepeatKill => _def.BossRepeatKill,
            RebirthQiBonusKind.FirstVisitRegion => _def.FirstVisitRegion,
            _ => _def.FirstReachRealm,
        };

        /// <summary>보너스를 적고 그 양을 돌려준다. '처음'인지는 부르는 쪽이 이미 가렸다.</summary>
        public int AddBonus(RebirthQiBonusKind kind, string id)
        {
            int amount = AmountOf(kind);
            _bonuses.Add(new RebirthQiBonusEntry(kind, id, amount));
            return amount;
        }

        /// <summary>지금까지의 정산. 장부는 그대로라 생 도중에 불러도 된다(멈춤 화면 미리보기).</summary>
        public RebirthQiResult Settle(int leftoverSilver)
        {
            int silver = Math.Max(0, leftoverSilver);
            int fromOrbs = Round(_def.PerOrbValue * _weighted);
            int fromSilver = Round(_def.LeftoverSilverRate * silver);
            int subtotal = fromOrbs + _bonuses.Sum(b => b.Amount) + fromSilver;
            int fromTraits = Round(subtotal * (double)TraitBonus);
            return new RebirthQiResult(fromOrbs, _bonuses.ToList(), fromSilver, silver, TraitBonus, fromTraits, _orbs, _raw);
        }

        static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);
    }
}
