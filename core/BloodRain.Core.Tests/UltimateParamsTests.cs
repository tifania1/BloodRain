using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>오의 수치 칸(characters.json ultimates[].params) ↔ 기획서 5장 '오의 상세' 효과 칸(R-05).</summary>
    public class UltimateParamsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        readonly GameData _d = TestData.Load();

        UltimateDef U(string id) => _d.Ultimates.Single(u => u.Id == id);

        string Effect(UltimateDef u) =>
            TestData.GddTable(TestData.GddSection("오의 상세")).Single(r => r[0] == u.NameKo)[1];

        static string N(float v) => v.ToString(Inv);
        static string Pct(float v) => System.Math.Round(v * 100).ToString(Inv) + "%";

        /// <summary>오의 id → 효과 칸에 그대로 있어야 하는 글(데이터 값으로 만든다).</summary>
        Dictionary<string, string[]> Expected() => new Dictionary<string, string[]>
        {
            ["cheonroe_rak"] = new[] { $"무작위 {N(U("cheonroe_rak").Param("strikes"))}곳", $"{N(U("cheonroe_rak").Param("shockSeconds"))}초 감전" },
            ["saja_hu"] = new[] { $"반경 {N(U("saja_hu").Param("radius"))}m", $"{N(U("saja_hu").Param("stunSeconds"))}초 기절" },
            ["mancheon_hwau"] = new[] { $"{N(U("mancheon_hwau").Param("durationSeconds"))}초 동안", "초당 공력 x", "중독" },
            ["taiji_muryang"] = new[] { $"{N(U("taiji_muryang").Param("durationSeconds"))}초 동안", "받는 피해 무효" },
            ["palbang_pungu"] = new[] { $"{N(U("palbang_pungu").Param("directions"))}방향", $"{N(U("palbang_pungu").Param("volleys"))}연타" },
            ["cheonseol_bu"] = new[] { $"{N(U("cheonseol_bu").Param("freezeSeconds"))}초 얼림" },
            ["cheonma_haeche"] = new[]
            {
                $"{N(U("cheonma_haeche").Param("durationSeconds"))}초 동안",
                $"초당 기혈 {Pct(U("cheonma_haeche").Param("healthCostPerSecond"))} 소모",
                $"모든 피해 {N(U("cheonma_haeche").Param("damageMultiplier"))}배",
                $"흡혈 {Pct(U("cheonma_haeche").Param("lifesteal"))}",
            },
        };

        [Fact] public void 오의_숫자가_기획서_오의_상세와_같다()
        {
            foreach (var kv in Expected())
                foreach (var piece in kv.Value)
                    Assert.Contains(piece, Effect(U(kv.Key)));
        }

        [Fact] public void 숫자가_있는_오의는_params_가_있고_일검_역류만_없다()
        {
            var withParams = _d.Ultimates.Where(u => u.Params != null).Select(u => u.Id).OrderBy(x => x).ToList();
            Assert.Equal(Expected().Keys.OrderBy(x => x).ToList(), withParams);
            Assert.Null(U("il_geom").Params);
            Assert.Null(U("yeokryu").Params); // 물살 폭·시간은 사람 결정 대기(R-05b)
        }

        [Fact] public void params_는_모두_양수이고_표에_쓰인_칸만_있다()
        {
            var known = new HashSet<string>
            {
                "durationSeconds", "radius", "stunSeconds", "strikes", "shockSeconds", "directions", "volleys",
                "freezeSeconds", "healthCostPerSecond", "damageMultiplier", "lifesteal", "poisons", "mightPerSecond",
                "pulseSeconds", "blocksDamage", "reflectsProjectiles",
            };
            foreach (var u in _d.Ultimates.Where(u => u.Params != null))
                foreach (var kv in u.Params!)
                {
                    Assert.Contains(kv.Key, known);
                    Assert.True(kv.Value > 0, $"{u.Id}.{kv.Key} = {kv.Value}");
                }
        }

        /// <summary>숫자가 아니라 있다·없다를 뜻하는 칸, 요약 글의 '초당' 같은 말로만 드러나는 칸.</summary>
        static readonly HashSet<string> Flags = new HashSet<string> { "poisons", "mightPerSecond", "pulseSeconds", "blocksDamage", "reflectsProjectiles" };

        [Fact] public void 요약_글도_같은_숫자를_쓴다()
        {
            foreach (var kv in Expected())
            {
                var u = U(kv.Key);
                foreach (var key in u.Params!.Keys.Where(k => !Flags.Contains(k)))
                {
                    float v = u.Params[key];
                    string s = key is "healthCostPerSecond" or "lifesteal" ? Pct(v) : N(v);
                    Assert.Contains(s, u.Summary);
                }
            }
        }

        [Fact] public void 없는_칸은_예외_또는_기본값()
        {
            Assert.Throws<KeyNotFoundException>(() => U("saja_hu").Param("strikes"));
            Assert.Throws<KeyNotFoundException>(() => U("il_geom").Param("radius"));
            Assert.Equal(0f, U("il_geom").Param("radius", 0f));
            Assert.Equal(6f, U("saja_hu").Param("radius", 0f));
        }
    }
}
