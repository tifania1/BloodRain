// 한 판 진행기. 진기 구슬을 흡수하면 레벨이 몇 번 오르는지, 그 사이 어느 경지를 돌파했는지 알려 준다.
// 화면 쪽은 결과를 받아 레벨업 카드(LevelUps 번)와 돌파 연출(Breakthroughs 순서대로)을 큐에 쌓으면 된다.
// 심득(Growth) 배율 때문에 진기가 소수가 될 수 있어 double로 들고 있는다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public readonly struct AbsorbResult
    {
        public readonly int FromLevel;
        public readonly int ToLevel;
        public readonly IReadOnlyList<RealmDef> Breakthroughs;

        public AbsorbResult(int fromLevel, int toLevel, IReadOnlyList<RealmDef> breakthroughs)
        {
            FromLevel = fromLevel;
            ToLevel = toLevel;
            Breakthroughs = breakthroughs;
        }

        public int LevelUps => ToLevel - FromLevel;
    }

    public sealed class RunXp
    {
        static readonly IReadOnlyList<RealmDef> None = Array.Empty<RealmDef>();

        readonly XpCurve _curve;
        readonly Realms _realms;
        readonly string _mode;

        public int Level { get; private set; } = 1;
        /// <summary>지금 레벨에서 모은 진기(다음 레벨까지 Required 만큼 필요).</summary>
        public double Xp { get; private set; }
        /// <summary>판 전체에서 흡수한 진기(배율 적용 뒤).</summary>
        public double Total { get; private set; }
        /// <summary>심득 배율. 판 도중 심법·깨달음으로 바뀔 수 있다.</summary>
        public float Growth { get; set; }

        public RunXp(XpCurve curve, Realms realms, string mode = "normal", float growth = 1f)
        {
            _curve = curve;
            _realms = realms;
            _mode = mode;
            Growth = growth;
        }

        public int Required => _curve.Required(Level);
        /// <summary>경험치 막대 채움 비율(0~1).</summary>
        public double Progress => Xp / Required;
        public RealmDef Realm => _realms.For(Level, _mode);

        /// <summary>구슬 하나(또는 한 프레임 흡수 합계)를 더한다. 배율은 여기서 곱한다.</summary>
        public AbsorbResult Absorb(double orbXp)
        {
            if (orbXp < 0) throw new ArgumentOutOfRangeException(nameof(orbXp));
            double gained = orbXp * Growth;
            Total += gained;
            Xp += gained;

            int from = Level;
            while (Xp >= Required)
            {
                Xp -= Required;
                Level++;
            }
            if (Level == from) return new AbsorbResult(from, from, None);

            var list = new List<RealmDef>(_realms.Breakthroughs(from, Level, _mode));
            return new AbsorbResult(from, Level, list.Count == 0 ? None : list);
        }
    }
}
