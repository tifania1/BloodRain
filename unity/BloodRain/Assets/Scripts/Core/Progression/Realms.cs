// 경지(삼류~현경). 레벨이 경지 문턱을 넘는 순간이 '돌파' 이벤트다.
// 천하 구조(기획서 3·7장)에선 모든 생에서 현경까지 오른다. mode 인자는 balance.json realms 의 onlyIn
// (특정 모드에서만 열리는 경지)을 거르려고 남겨 둔 것이고, 지금 데이터엔 onlyIn 이 붙은 경지가 없어 어떤 값을 줘도 같다.
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

        /// <summary>레벨이 닿은 가장 높은 경지. onlyIn 이 붙은 경지는 그 모드에서만 센다(지금 데이터엔 없음).</summary>
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
