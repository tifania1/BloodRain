using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>LK-01 연환 효과 숫자: synergies.json links[].params 가 기획서 20장 연환 표 글과 같은지, 켜진 연환만 값을 주는지.</summary>
    public class LinkParamsTests
    {
        readonly GameData _d = TestData.Load();

        // 연환 id → (params 키, 값, 기획서 그 줄에 있어야 할 글). @ 로 시작하면 표 아래 '표에 없는 세부 값' 줄에서 찾는다.
        static readonly Dictionary<string, (string Key, float Value, string Text)[]> Expected = new Dictionary<string, (string, float, string)[]>
        {
            ["ssanggeom"] = new[] { ("damageBonus", 0.6f, "피해 +60%") },
            ["geomhwa"] = new[] { ("petals", 3f, "꽃잎 세 장"), ("petalDamageRatio", 1f, "&검화 꽃잎 한 장은 낙화검우 꽃잎 한 방") },
            ["gwonjang"] = new[] { ("palmDamageRatio", 0.5f, "장풍 피해 50%"), ("palmSpeed", 9f, "@권장합일 장풍은 초속 9m"), ("palmLifeSeconds", 0.9f, "@9m·0.9초"), ("palmRadius", 0.45f, "@0.9초·반경 0.45m") },
            ["geumganghu"] = new[] { ("radiusBonus", 0.4f, "음파 반경 +40%"), ("stunChanceMultiplier", 2f, "기절 확률 2배") },
            ["dokbi"] = new[] { ("extraPierce", 1f, "관통 +1") },
            ["amgi"] = new[] { ("daggers", 1f, "비도 한 자루") },
            ["punghwa"] = new[] { ("sizeBonus", 0.3f, "크기 +30%"), ("burnSeconds", 2f, "@풍화 화상은 2초"), ("burnDps", 6f, "@2초·초당 6") },
            ["jeomhyeol"] = new[] { ("stopSeconds", 0.5f, "0.5초 멈추고") },
            ["noehwa"] = new[] { ("radiusMultiplier", 2f, "두 배 범위"), ("nearRadiusRatio", 1f, "&뇌화 '가까이'는 벽력탄 폭발 반경 안") },
            ["hyeoldok"] = new[] { ("spreadRadius", 2f, "주변 2m") },
            ["chwijung"] = new[] { ("seconds", 2f, "2초 동안"), ("dpsRatio", 0.3f, "&취중매화 회오리는 낙화검우 초당 피해의 30%") },
            ["hyeolchang"] = new[] { ("bleedSeconds", 3f, "3초 출혈"), ("lifestealMultiplier", 3f, "흡혈 3배"), ("bleedPerSecondRatio", 0.1f, "&혈창 출혈은 이화창 한 방의 10%를 초마다") },
            // LK-01a(2026-11-20): 표에 숫자가 없던 넷은 짝 무공 값을 빌린다('짝 무공 값을 빌리는 것' 문단)
            ["inroe"] = new[] { ("boltDamageRatio", 1f, "&인뢰검 낙뢰와 감전은 뇌부 한 방 그대로") },
            ["geommyeong"] = new[] { ("damageRatio", 0.5f, "&검명 작은 음파는 음공 한 번의 피해·반경 50%"), ("radiusRatio", 0.5f, "&음공 한 번의 피해·반경 50%") },
            ["charyeok"] = new[] { ("impactDamageRatio", 1f, "&차력타력·견인타 부딪힘은 태극검·취선봉 한 방") },
            ["gyeonin"] = new[] { ("impactDamageRatio", 1f, "&차력타력·견인타 부딪힘은 태극검·취선봉 한 방") },
        };

        /// <summary>표에 숫자가 없어 params 를 안 둔 연환. LK-01a(2026-11-20)로 다 채워 비었다.</summary>
        static readonly string[] NoNumbers = { };

        static string DetailLine => TestData.Gdd.Split('\n').Single(x => x.StartsWith("표에 없는 세부 값: 권장합일"));
        static string BorrowLine => TestData.Gdd.Split('\n').Single(x => x.StartsWith("짝 무공 값을 빌리는 것"));

        static string GddRow(LinkDef l)
        {
            var row = TestData.Gdd.Split('\n').FirstOrDefault(x => x.StartsWith($"| {l.NameKo}({l.Hanja}) |"));
            Assert.True(row != null, $"기획서 20장 연환 표에 {l.NameKo} 줄이 없음");
            return row!;
        }

        [Fact] public void 연환_params_가_기획서_표_글과_같다()
        {
            foreach (var l in _d.Synergies.Links)
            {
                if (NoNumbers.Contains(l.Id)) { Assert.Null(l.Params); continue; }
                Assert.True(Expected.ContainsKey(l.Id), $"{l.Id} 를 Expected 나 NoNumbers 에 넣어야 함");
                var row = GddRow(l);
                Assert.Equal(Expected[l.Id].Length, l.Params!.Count);
                foreach (var (key, value, text) in Expected[l.Id])
                {
                    Assert.Equal(value, l.Param(key), 3);
                    if (text.StartsWith("@")) Assert.Contains(text.Substring(1), DetailLine);
                    else if (text.StartsWith("&")) Assert.Contains(text.Substring(1), BorrowLine);
                    else Assert.Contains(text, row);
                }
            }
        }

        [Fact] public void 켜진_연환만_값을_준다()
        {
            var on = Synergy.Compute(_d, L(("samjae_sword", 4), ("changcheon_qi", 4)));
            Assert.Equal(0.6f, on.LinkParam("ssanggeom", "damageBonus")!.Value, 3);
            Assert.Null(on.LinkParam("gwonjang", "palmDamageRatio"));
            var off = Synergy.Compute(_d, L(("samjae_sword", 4), ("changcheon_qi", 3)));
            Assert.Null(off.LinkParam("ssanggeom", "damageBonus"));
            // 잠긴 비급(복원 안 함)도 값이 없다
            var locked = Synergy.Compute(_d, L(("samjae_sword", 4), ("changcheon_qi", 4)), null, new string[0]);
            Assert.Null(locked.LinkParam("ssanggeom", "damageBonus"));
        }

        [Fact] public void 없는_키는_예외() =>
            Assert.Throws<KeyNotFoundException>(() => _d.Synergies.Links.First(l => l.Id == "ssanggeom").Param("nope"));

        static Loadout L(params (string, int)[] ws) => TestData.Build("jin_sowoon", ws);
    }
}
