#nullable enable
// 무공이 레벨에 따라 자라는 값(기획서 9장 'Lv8까지 성장'). weapons.json 의 [Lv1, Lv8] 쌍을 선형으로 잇는다.
// 웹 프로토타입과 Unity WeaponRunner 가 lerp(1, 4, t) 처럼 박아 두던 숫자를 여기서 데이터로 읽는다.
// 개수(연타·비도·관통 …)는 반올림한다. t = (레벨 - 1) / 7 이라 반올림 경계(.5)에 걸리는 레벨은 없다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    public static class WeaponGrowth
    {
        public const int MaxLevel = 8;

        /// <summary>0(Lv1) ~ 1(Lv8).</summary>
        public static float T(int level) => Math.Clamp((level - 1) / (float)(MaxLevel - 1), 0f, 1f);

        /// <summary>[Lv1, Lv8] 쌍을 그 레벨로 잇는다. 쌍이 없으면 fallback.</summary>
        public static float At(List<float>? pair, int level, float fallback = 0f)
        {
            if (pair == null || pair.Count == 0) return fallback;
            if (pair.Count == 1) return pair[0];
            return pair[0] + (pair[1] - pair[0]) * T(level);
        }

        /// <summary>개수로 쓰는 값(반올림). 쌍이 없으면 fallback.</summary>
        public static int CountAt(List<float>? pair, int level, int fallback = 1)
            => pair == null || pair.Count == 0 ? fallback : (int)Math.Round(At(pair, level), MidpointRounding.AwayFromZero);

        public static int Hits(WeaponDef w, int level) => CountAt(w.Hits, level);
        public static int Projectiles(WeaponDef w, int level) => CountAt(w.Projectiles, level);
        /// <summary>관통 횟수. 없으면 0(첫 적에서 멈춘다).</summary>
        public static int Pierce(WeaponDef w, int level) => CountAt(w.Pierce, level, 0);

        /// <summary>반경(m). RangeLv8 이 있으면 Range → RangeLv8 로 잇고, 없으면 Range 그대로.</summary>
        public static float Range(WeaponDef w, int level)
            => w.RangeLv8.HasValue ? w.Range + (w.RangeLv8.Value - w.Range) * T(level) : w.Range;

        /// <summary>범위·크기 배율. 1 → AreaLv8.</summary>
        public static float Area(WeaponDef w, int level)
            => w.AreaLv8.HasValue ? 1f + (w.AreaLv8.Value - 1f) * T(level) : 1f;

        /// <summary>다음 발까지 초. 초당 발 수가 있으면 1 / 그 값, 아니면 Cooldown(독무처럼 0 이면 상시).</summary>
        public static float Cooldown(WeaponDef w, int level)
            => w.ShotsPerSecond != null && w.ShotsPerSecond.Count > 0 ? 1f / At(w.ShotsPerSecond, level) : w.Cooldown;

        /// <summary>이펙트 2단·3단이 시작되는 레벨(docs/design/vfx.md 단계 규칙).
        /// 마일스톤 둘 이상: 낮은 둘. 하나: Lv7 이상이면 3단 문턱(2단은 Lv4), 아니면 2단 문턱(3단은 Lv7). 없으면 Lv4·Lv7.</summary>
        public static (int second, int third) VisualTierLevels(WeaponDef w)
        {
            var lv = new List<int>();
            if (w.Milestones != null)
                foreach (var key in w.Milestones.Keys)
                    lv.Add(int.Parse(key, System.Globalization.CultureInfo.InvariantCulture));
            lv.Sort();
            if (lv.Count >= 2) return (lv[0], lv[1]);
            if (lv.Count == 1) return lv[0] >= 7 ? (4, lv[0]) : (lv[0], 7);
            return (4, 7);
        }

        /// <summary>그 레벨의 이펙트 단계 1~3(절기는 따로). WeaponRunner 의 붓 획 단계가 이것을 쓴다.</summary>
        public static int VisualTier(WeaponDef w, int level)
        {
            var (second, third) = VisualTierLevels(w);
            return level >= third ? 3 : level >= second ? 2 : 1;
        }

        /// <summary>그 레벨이면 붙어 있는 새 동작인지(이름으로). 이름이 데이터에 없으면 예외.</summary>
        public static bool HasMilestone(WeaponDef w, int level, string name)
        {
            if (w.Milestones != null)
                foreach (var kv in w.Milestones)
                    if (kv.Value == name) return level >= int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture);
            throw new KeyNotFoundException($"{w.Id} 에 '{name}' 동작이 없습니다");
        }
    }
}
