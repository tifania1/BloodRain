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
            ["geomhwa"] = new[] { ("petals", 3f, "꽃잎 세 장") },
            ["gwonjang"] = new[] { ("palmDamageRatio", 0.5f, "장풍 피해 50%"), ("palmSpeed", 9f, "@권장합일 장풍은 초속 9m"), ("palmLifeSeconds", 0.9f, "@9m·0.9초"), ("palmRadius", 0.45f, "@0.9초·반경 0.45m") },
            ["geumganghu"] = new[] { ("radiusBonus", 0.4f, "음파 반경 +40%"), ("stunChanceMultiplier", 2f, "기절 확률 2배") },
            ["dokbi"] = new[] { ("extraPierce", 1f, "관통 +1") },
            ["amgi"] = new[] { ("daggers", 1f, "비도 한 자루") },
            ["punghwa"] = new[] { ("sizeBonus", 0.3f, "크기 +30%"), ("burnSeconds", 2f, "@풍화 화상은 2초"), ("burnDps", 6f, "@2초·초당 6") },
            ["jeomhyeol"] = new[] { ("stopSeconds", 0.5f, "0.5초 멈추고") },
            ["noehwa"] = new[] { ("radiusMultiplier", 2f, "두 배 범위") },
            ["hyeoldok"] = new[] { ("spreadRadius", 2f, "주변 2m") },
            ["chwijung"] = new[] { ("seconds", 2f, "2초 동안") },
            ["hyeolchang"] = new[] { ("bleedSeconds", 3f, "3초 출혈"), ("lifestealMultiplier", 3f, "흡혈 3배") },
        };

        /// <summary>표에 숫자가 없어 params 를 안 둔 연환(LK-01a 사람 결정 대기).</summary>
        static readonly string[] NoNumbers = { "inroe", "geommyeong", "charyeok", "gyeonin" };

        static string DetailLine => TestData.Gdd.Split('\n').Single(x => x.StartsWith("표에 없는 세부 값: 권장합일"));

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
