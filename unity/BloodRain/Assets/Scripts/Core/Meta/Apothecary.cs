#nullable enable
// 약방 영약(기획서 10장 '영약(약방)', cheonha.json inn.elixirs). 약방에 들어갈 수 있으면 생을 시작하기 전에
// 혈정을 써서 elixirsPerLife 개까지 지니고 출발한다. 효과(부활·시작 레벨·오의 게이지·기혈·혈정 배율·음양 멈춤)는
// 이번 생 한정이고, 한 생에 거는 일은 Life 쪽 몫이다(backlog EL-02).
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public static class Apothecary
    {
        public const string FacilityId = "apothecary";

        /// <summary>약방에 들어가 영약을 고를 수 있다(열렸고 수리 중이 아님).</summary>
        public static bool IsAvailable(Soul soul) => Inn.CanEnter(soul, FacilityId);

        /// <summary>고른 영약을 지니고 출발한다: 약방이 열려 있고, 겹치지 않고, elixirsPerLife 개 이하이고, 혈정이 넉넉하면
        /// 혈정을 쓰고 그 영약 정의를 돌려준다. 하나라도 안 되면 아무것도 쓰지 않고 null.</summary>
        public static IReadOnlyList<ElixirDef>? TryPrepare(Soul soul, IReadOnlyList<string> ids)
        {
            var inn = soul.Data.Cheonha.Inn;
            if (ids.Count == 0) return new List<ElixirDef>();
            if (!IsAvailable(soul) || ids.Count > inn.ElixirsPerLife || ids.Distinct().Count() != ids.Count) return null;
            var picked = new List<ElixirDef>();
            foreach (var id in ids)
            {
                var e = inn.Elixirs.FirstOrDefault(x => x.Id == id);
                if (e == null) return null;
                picked.Add(e);
            }
            int cost = picked.Sum(e => e.Cost);
            if (!soul.TrySpendBloodCrystal(cost)) return null;
            return picked;
        }
    }
}
