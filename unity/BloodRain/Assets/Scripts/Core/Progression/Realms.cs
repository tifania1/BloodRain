// 경지(삼류~현경). 레벨이 경지 문턱을 넘는 순간이 '돌파' 이벤트다.
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Progression
{
    public sealed class Realms
    {
        readonly List<RealmDef> _ordered;

        public Realms(IEnumerable<RealmDef> realms)
        {
            _ordered = realms.OrderBy(r => r.Level).ToList();
        }

        /// <summary>모드에 따라 현경처럼 특정 모드에서만 열리는 경지를 뺀다.</summary>
        public RealmDef For(int level, string mode = "normal")
        {
            RealmDef current = _ordered[0];
            foreach (var r in _ordered)
            {
                if (r.Level > level) break;
                if (r.OnlyIn != null && !r.OnlyIn.Contains(mode)) continue;
                current = r;
            }
            return current;
        }

        /// <summary>fromLevel에서 toLevel로 오르는 동안 새로 도달한 경지들(돌파 연출 순서대로).</summary>
        public IEnumerable<RealmDef> Breakthroughs(int fromLevel, int toLevel, string mode = "normal")
        {
            foreach (var r in _ordered)
            {
                if (r.Level <= fromLevel || r.Level > toLevel) continue;
                if (r.OnlyIn != null && !r.OnlyIn.Contains(mode)) continue;
                yield return r;
            }
        }
    }
}
