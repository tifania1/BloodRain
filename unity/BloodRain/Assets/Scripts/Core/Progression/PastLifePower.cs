#nullable enable
// 전생의 공력(설계안 docs/design/cheonha_expansion_full.md '회귀 가속' — '전생의 공력 (추천 규칙)', backlog GU-11f).
// 윤회의 기가 '세기'라면 이것은 '빠르기'다. 넘어 본 경지 구간은 필요한 진기가 줄어든다.
//   - 구간 = 한 경지에서 다음 경지로 가는 레벨들. 구간은 '가는 경지'(그 끝의 경지)로 부른다(삼류 → 이류 구간 = iryu).
//   - 그 구간을 넘은 횟수 n 마다 필요한 진기가 CutPerCrossing(4%)씩 줄고, MaxCut(30%)에서 멈춘다.
//   - 지금까지 가장 높이 오른 경지로 가는 구간은 줄어들지 않는다. 맨 앞은 늘 제 속도.
// 값 4%·30% 는 설계안 추천(시뮬레이션 전)이라 아직 데이터로 옮기지 않았다. 시뮬레이터 곡선(design/sim/acceleration.md)을 본 뒤 정한다.
// 아직 Soul 저장·Life 에 물리지 않았다. RunXp.PastLife 에 넣으면 그 판의 필요 진기가 줄어든다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public sealed class PastLifePower
    {
        public const float DefaultCutPerCrossing = 0.04f;
        public const float DefaultMaxCut = 0.30f;

        readonly List<RealmDef> _realms;
        readonly Dictionary<string, int> _crossings;
        readonly int _highest;

        public float CutPerCrossing { get; }
        public float MaxCut { get; }

        /// <param name="realms">경지 전부(순서는 레벨로 맞춘다).</param>
        /// <param name="crossings">가는 경지 id → 그 구간을 넘은 횟수(지난 생들 합).</param>
        /// <param name="highestRealm">지금까지 가장 높이 오른 경지 id. 없으면(첫 생) 삼류.</param>
        public PastLifePower(IEnumerable<RealmDef> realms, IReadOnlyDictionary<string, int> crossings, string? highestRealm,
            float cutPerCrossing = DefaultCutPerCrossing, float maxCut = DefaultMaxCut)
        {
            _realms = realms.OrderBy(r => r.Level).ToList();
            if (_realms.Count == 0) throw new ArgumentException("경지가 하나도 없습니다", nameof(realms));
            if (cutPerCrossing < 0 || maxCut < 0 || maxCut >= 1) throw new ArgumentOutOfRangeException(nameof(maxCut), "줄이는 몫은 0 이상, 상한은 1 미만");
            _crossings = new Dictionary<string, int>();
            foreach (var kv in crossings)
            {
                if (_realms.All(r => r.Id != kv.Key)) throw new ArgumentException($"모르는 경지: {kv.Key}", nameof(crossings));
                if (kv.Value < 0) throw new ArgumentOutOfRangeException(nameof(crossings), $"{kv.Key} 넘은 횟수가 0보다 작습니다");
                _crossings[kv.Key] = kv.Value;
            }
            _highest = highestRealm == null ? 0 : _realms.FindIndex(r => r.Id == highestRealm);
            if (_highest < 0) throw new ArgumentException($"모르는 경지: {highestRealm}", nameof(highestRealm));
            CutPerCrossing = cutPerCrossing;
            MaxCut = maxCut;
        }

        /// <summary>지난 생이 하나도 없는 혼. 아무것도 줄지 않는다.</summary>
        public static PastLifePower None(IEnumerable<RealmDef> realms) =>
            new PastLifePower(realms, new Dictionary<string, int>(), null);

        /// <summary>모든 생이 같은 경지까지 올랐다고 칠 때(시뮬레이터). 삼류 다음부터 reachedRealm 까지 구간마다 lives 번.</summary>
        public static PastLifePower AfterLives(IEnumerable<RealmDef> realms, int lives, string reachedRealm,
            float cutPerCrossing = DefaultCutPerCrossing, float maxCut = DefaultMaxCut)
        {
            var list = realms.OrderBy(r => r.Level).ToList();
            int top = list.FindIndex(r => r.Id == reachedRealm);
            if (top < 0) throw new ArgumentException($"모르는 경지: {reachedRealm}", nameof(reachedRealm));
            var c = new Dictionary<string, int>();
            for (int i = 1; i <= top; i++) c[list[i].Id] = lives;
            return new PastLifePower(list, c, lives > 0 ? reachedRealm : null, cutPerCrossing, maxCut);
        }

        /// <summary>그 레벨에서 다음 레벨로 갈 때 들어 있는 구간(가는 경지 순번). 마지막 경지 위는 -1(구간 없음).</summary>
        public int SegmentOf(int level)
        {
            int at = 0;
            for (int i = 0; i < _realms.Count; i++)
                if (_realms[i].Level <= level) at = i;
            return at + 1 < _realms.Count ? at + 1 : -1;
        }

        public int Crossings(string realmId) => _crossings.TryGetValue(realmId, out var n) ? n : 0;

        /// <summary>가는 경지 순번 segment 구간의 줄어드는 몫(0~MaxCut).</summary>
        public float CutForSegment(int segment)
        {
            if (segment <= 0 || segment >= _realms.Count) return 0f;
            if (segment >= _highest) return 0f; // 가장 높이 오른 경지로 가는 구간과 그 위는 제 속도
            return Math.Min(MaxCut, CutPerCrossing * Crossings(_realms[segment].Id));
        }

        /// <summary>그 레벨에서 다음 레벨로 갈 때 필요한 진기에 곱하는 값(1 - 줄어드는 몫).</summary>
        public float RequiredScale(int level)
        {
            int seg = SegmentOf(level);
            return seg < 0 ? 1f : 1f - CutForSegment(seg);
        }
    }
}
