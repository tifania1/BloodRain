#nullable enable
// 구결(口訣) — 지난 생에 고른 레벨업 순서를 앞부분만 그대로 자동으로 고른다.
// 천하 확장 설계안 '회귀 가속'(사람이 2026-11-18 설계안대로 정함, 기획서 10장 '천하 확장: 전생의 공력'):
// "레벨업이 몰려오면 창이 흐름을 끊습니다. 지난 생의 고른 순서를 저장해 두면 앞부분은 그대로 자동으로 고릅니다."
// 한 걸음 = 무공·심법 하나를 고른 것("w:<id>"·"p:<id>"). 새로 얻기와 레벨 올리기를 가리지 않고, 금빛 대오 카드도 한 걸음.
// 은자·음식(Fallback)은 걸음으로 치지 않는다. 뜬 카드에 다음 걸음이 없으면 거기서 멈추고 그 생엔 다시 켜지지 않는다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Build
{
    public static class Gugyeol
    {
        /// <summary>고른 카드 한 장을 구결 한 걸음으로. 은자·음식이면 null.</summary>
        public static string? Step(Offer offer) => offer.Kind switch
        {
            OfferKind.NewWeapon or OfferKind.UpgradeWeapon => "w:" + offer.Id,
            OfferKind.NewPassive or OfferKind.UpgradePassive => "p:" + offer.Id,
            _ => null,
        };
    }

    /// <summary>한 생 동안 구결을 따라 고른다. 레벨업 화면이 뜰 때마다 Pick 을 부르고, null 이면 사람이 고른다.</summary>
    public sealed class GugyeolPlayer
    {
        readonly IReadOnlyList<string> _steps;

        /// <summary>따라 고른 걸음 수.</summary>
        public int Done { get; private set; }
        /// <summary>어긋나서(또는 끝까지 따라서) 멈췄나. 멈추면 그 생엔 다시 고르지 않는다.</summary>
        public bool Stopped { get; private set; }
        public int Count => _steps.Count;

        public GugyeolPlayer(IEnumerable<string> steps) { _steps = steps.ToList(); Stopped = _steps.Count == 0; }

        /// <summary>뜬 카드 가운데 다음 걸음과 같은 것을 고른다(같은 것이 여럿이면 금빛 대오 카드 먼저). 없으면 멈추고 null.</summary>
        public Offer? Pick(IReadOnlyList<Offer> offers)
        {
            if (Stopped) return null;
            if (Done >= _steps.Count) { Stopped = true; return null; }
            var next = _steps[Done];
            var hit = offers.Where(o => Gugyeol.Step(o) == next).OrderByDescending(o => o.Levels).ToList();
            if (hit.Count == 0) { Stopped = true; return null; }
            Done++;
            if (Done >= _steps.Count) Stopped = true;
            return hit[0];
        }
    }
}
