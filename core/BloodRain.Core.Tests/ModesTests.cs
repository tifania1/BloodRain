using System;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>R4-05: 모드 열림(기획서 7장 '모드', cheonha.json modes, Core Meta/Modes).</summary>
    public class ModesTests
    {
        readonly GameData _d = Load();
        CheonhaDef C => _d.Cheonha;
        static RebirthQiResult NoQi(GameData d) => new RebirthQiLedger(d.Cheonha.RebirthQi).Settle(0);

        LifeRecord Life(string sect, string way, params string[] bosses) =>
            new LifeRecord(sect, way, NoQi(_d), bosses, Array.Empty<string>(), "hyeongyeong", Array.Empty<string>());

        [Fact] public void 모드_표의_열리는_시점이_데이터와_같다()
        {
            var rows = GddTable(GddSection("모드"));
            Assert.Equal(rows.Count, C.Modes.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var m = C.Modes[i];
                Assert.StartsWith(m.NameKo, rows[i][0]);
                string expect = m.Require.Prologue ? "서장 뒤"
                    : m.Require.Ending == Soul.EndingSeal ? "봉인 엔딩"
                    : m.Require.SealStones > 0 ? $"봉인석 {m.Require.SealStones}개" : "?";
                Assert.Equal(expect, rows[i][2]);
            }
        }

        [Fact] public void 천하는_서장_뒤에_열리고_일일_도전은_봉인석_셋에_열린다()
        {
            var s = new Soul(_d);
            Assert.Empty(Modes.OpenModes(s));
            var life = s.BeginLife(Rebirth.Prologue(_d));
            var sum = s.EndLife(life.Finish("fallen", 0));
            Assert.Equal(new[] { "cheonha" }, sum.NewModes);
            var stones = C.BossRegions.Where(r => r.SealStone != null).Select(r => r.Id).Take(3).ToArray();
            s.EndLife(Life("shaolin", "fallen", stones.Take(2).ToArray()));
            Assert.False(Modes.IsOpen(s, "daily"));
            Assert.Equal(new[] { "daily" }, s.EndLife(Life("shaolin", "fallen", stones[2])).NewModes);
            Assert.Contains(Modes.Token("daily"), s.Unlocks);
        }

        [Fact] public void 혈월_천하와_보스_연전은_봉인_엔딩에_열리고_회귀_뒤에도_닫히지_않는다()
        {
            var s = new Soul(_d);
            s.EndLife(s.BeginLife(Rebirth.Prologue(_d)).Finish("fallen", 0));
            var all = C.BossRegions.Where(r => r.SealStone != null).Select(r => r.Id).ToArray();
            s.EndLife(Life("shaolin", "sealed", all));
            Assert.False(Modes.IsOpen(s, "bloodmoon"));     // 엔딩을 고르기 전
            s.ChooseEnding(Soul.EndingSeal, false);
            Assert.True(Modes.IsOpen(s, "bloodmoon"));
            Assert.True(Modes.IsOpen(s, "boss_rush"));
            Assert.Equal(0, s.SealStoneCount);              // 회귀로 봉인석이 지워져도
            Assert.True(Modes.IsOpen(s, "daily"));           // 한 번 열린 모드는 남는다
            Assert.Equal(new[] { "cheonha", "bloodmoon", "daily", "boss_rush" }, Modes.OpenModes(s));
            Assert.True(Modes.IsOpen(Soul.FromJson(_d, s.ToJson()), "boss_rush"));
        }

        [Fact] public void 없는_모드를_물으면_알려_준다() =>
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => Modes.IsOpen(new Soul(_d), "survival"));
    }
}
