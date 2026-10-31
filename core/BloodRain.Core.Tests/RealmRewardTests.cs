using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>balance.json realms 의 reward 낱말이 기획서 3장 경지표 '돌파 보상' 칸과 한 칸씩 맞는지.
    /// 낱말을 숫자로 모으는 건 Core RealmRewards(RealmRewardSumTests), 효과를 얹는 건 Unity 돌파 화면(C-07) 몫이라 문구 대조를 여기서 한다.</summary>
    public class RealmRewardTests
    {
        readonly GameData _d = TestData.Load();

        /// <summary>낱말 → 기획서 문구. 절기 그릇은 몇 번째 그릇인지 숫자가 붙어서 따로 센다.</summary>
        static readonly Dictionary<string, string> Phrase = new Dictionary<string, string>
        {
            ["insight_choice"] = "깨달음 3택1",
            ["max_health_10"] = "최대 기혈 +10",
            ["dash_charge_1"] = "경공 충전 +1",
            ["ultimate_power_25"] = "오의 위력 +25%",
            ["extra_slot"] = "무공 슬롯 +1(공격 또는 심법 7칸째)",
            ["damage_taken_minus10"] = "받는 피해 -10%",
            ["double_ultimate"] = "오의 2연속 사용",
        };

        /// <summary>그림만 바뀌어 데이터에 낱말이 없는 보상.</summary>
        static readonly HashSet<string> CosmeticOnly = new HashSet<string> { "외형 변화" };

        static string[] Tokens(string? reward) =>
            reward == null ? new string[0] : reward.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();

        [Fact] public void 경지마다_보상_낱말이_경지표_칸과_같다()
        {
            var table = TestData.GddTable(TestData.GddSection("경지표"));
            Assert.Equal(_d.Balance.Realms.Count, table.Count);
            int vessel = 0;
            for (int i = 0; i < table.Count; i++)
            {
                var realm = _d.Balance.Realms[i];
                var row = table[i];
                Assert.Equal(realm.NameKo, row[0]);
                Assert.Equal(realm.Level.ToString(), row[1]);

                var want = new List<string>();
                foreach (var t in Tokens(realm.Reward))
                {
                    if (t == "jeolgi_vessel") { want.Add($"절기 그릇 {++vessel}"); continue; }
                    Assert.True(Phrase.ContainsKey(t), $"{realm.NameKo}: 모르는 보상 낱말 '{t}'");
                    want.Add(Phrase[t]);
                }
                var cell = row[3] == "없음" ? new List<string>()
                    : row[3].Split(", ").Select(x => x.Trim()).Where(x => !CosmeticOnly.Contains(x)).ToList();
                Assert.Equal(cell, want);
            }
            Assert.Equal(4, vessel); // 절기 그릇 넷(8장 '절기 그릇과 각성')
        }

        [Fact] public void 일곱째_칸이_열리는_경지가_extra_slot_을_가진_경지다()
        {
            var withSlot = _d.Balance.Realms.Where(r => Tokens(r.Reward).Contains("extra_slot")).Select(r => r.Id).ToList();
            Assert.Equal(new[] { _d.Balance.Slots.ExtraSlotAtRealm }, withSlot);
        }

        [Fact] public void 같은_낱말이_한_경지에_두_번_나오지_않는다()
        {
            foreach (var r in _d.Balance.Realms)
            {
                var t = Tokens(r.Reward);
                Assert.Equal(t.Length, t.Distinct().Count());
            }
        }
    }
}
