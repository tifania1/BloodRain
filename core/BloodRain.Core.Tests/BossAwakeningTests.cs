using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>땅의 주인이 깨어나는 규칙(World.BossAwakening). 기획서 7장 '땅의 주인이 깨어나는 규칙', 6장 땅의 주인, 10장 해금 표.</summary>
    public class BossAwakeningTests
    {
        readonly Data.GameData _d = TestData.Load();
        Regions Map => new Regions(_d);
        BossAwakening Life(string birth, BossRecord? record = null) => new BossAwakening(_d, birth, record ?? new BossRecord());
        int Level(string realm) => _d.Balance.Realms.Single(r => r.Id == realm).Level;

        // 출생지마다 절정·초절정·화경 돌파 때 새로 깨어나는 땅(cheonha.json 지역 순서).
        // 지역 위치를 옮기면 이 표가 바뀐다. 바뀐 표가 7장 본문('숭산이면 넷, 곤륜이면 하나', '화경이면 일곱')과 맞는지 보고 고친다.
        static readonly Dictionary<string, string[][]> AwakenTable = new Dictionary<string, string[][]>
        {
            ["songshan"]      = new[] { new[] { "songshan", "janggang", "wudangsan", "huashan" }, new[] { "danggata", "kunlun", "sipmandaesan" }, new string[0] },
            ["gaebang_hq"]    = new[] { new[] { "songshan", "janggang", "wudangsan", "huashan" }, new[] { "danggata", "kunlun", "sipmandaesan" }, new string[0] },
            ["namgung_manor"] = new[] { new[] { "songshan", "janggang" }, new[] { "danggata", "wudangsan", "huashan", "sipmandaesan" }, new[] { "kunlun" } },
            ["danggata"]      = new[] { new[] { "danggata", "wudangsan", "huashan", "sipmandaesan" }, new[] { "songshan", "janggang", "kunlun" }, new string[0] },
            ["janggang"]      = new[] { new[] { "songshan", "janggang", "wudangsan", "huashan", "sipmandaesan" }, new[] { "danggata", "kunlun" }, new string[0] },
            ["wudangsan"]     = new[] { new[] { "songshan", "danggata", "janggang", "wudangsan", "huashan", "sipmandaesan" }, new[] { "kunlun" }, new string[0] },
            ["huashan"]       = new[] { new[] { "songshan", "danggata", "janggang", "wudangsan", "huashan" }, new[] { "kunlun", "sipmandaesan" }, new string[0] },
            ["kunlun"]        = new[] { new[] { "kunlun" }, new[] { "songshan", "danggata", "janggang", "wudangsan", "huashan", "sipmandaesan" }, new string[0] },
            ["sipmandaesan"]  = new[] { new[] { "danggata", "janggang", "wudangsan", "sipmandaesan" }, new[] { "songshan", "huashan", "kunlun" }, new string[0] },
        };

        [Fact] public void 출생지_아홉_곳의_경지별로_깨어나는_땅의_주인()
        {
            Assert.Equal(_d.Cheonha.Sects.Select(s => s.Region).OrderBy(x => x), AwakenTable.Keys.OrderBy(x => x));
            var steps = new[] { "jeoljeong", "chojeoljeong", "hwagyeong" };
            int bossCount = _d.Cheonha.BossRegions.Count();
            foreach (var kv in AwakenTable)
            {
                var life = Life(kv.Key);
                // 일류까지는 아무도 깨어나지 않는다
                foreach (var lv in new[] { 1, Level("iryu"), Level("ilryu"), Level("jeoljeong") - 1 })
                    Assert.Empty(life.UpdateRealm(lv));
                for (int i = 0; i < steps.Length; i++)
                    Assert.Equal(kv.Value[i], life.UpdateRealm(Level(steps[i])).Select(r => r.Id).ToArray());
                Assert.Empty(life.UpdateRealm(Level("hyeongyeong")));     // 봉인석이 없으면 현경에서 새로 깨어나는 곳은 없다
                Assert.Equal(bossCount, life.Awake.Count);                 // 화경이면 일곱이 모두
                Assert.NotEmpty(kv.Value[0]);                              // 절정 장막 안에 하나 이상
            }
            // 7장 본문의 예
            Assert.Equal(4, AwakenTable["songshan"][0].Length);
            Assert.Equal(new[] { "kunlun" }, AwakenTable["kunlun"][0]);
        }

        [Fact] public void 장막_안인지는_출생지에서_보스_지역까지_거리로_본다()
        {
            var life = Life("songshan");
            var scale = new Scale(_d);
            int peak = scale.RealmIndex("jeoljeong");
            foreach (var r in life.BossRegions.Where(r => !r.Final))
            {
                bool inside = Map.Distance("songshan", r.Id) <= scale.BoundaryRadius(peak)!.Value;
                Assert.Equal(inside, life.ShouldBeAwake(r, peak, 0));
            }
            Assert.Equal(AwakenTable["songshan"][0], life.AwakeAt(peak).Select(r => r.Id));
            Assert.Empty(life.AwakeAt(peak - 1));
        }

        [Fact] public void 깨어난_땅_한가운데_화면_대각선_절반_안에_들어서면_싸움이_시작된다()
        {
            var life = Life("songshan");
            int lv = Level("jeoljeong");
            var center = Map.Center("songshan");
            float radius = life.EngageRadius(lv);
            Assert.Equal(new Scale(_d).ViewDiagonal(lv) * _d.Cheonha.Bosses.EngageViewMultiple, radius, 3);

            Assert.Null(life.TryEngage(center, lv - 1, 11));           // 아직 절정 전: 깨어난 곳이 없다
            life.UpdateRealm(lv);
            Assert.Null(life.TryEngage(center + new Vector2(radius + 1, 0), lv, 12));
            Assert.Equal(1f, life.SpawnTargetScale);

            var fight = life.TryEngage(center + new Vector2(radius - 1, 0), lv, 12);
            Assert.NotNull(fight);
            Assert.Equal("wongak", fight!.Boss.Id);
            Assert.Equal(3, fight.Realm);
            Assert.False(fight.IsShadow);
            Assert.False(fight.IsFinal);
            Assert.Equal(BossRegionState.Fighting, life.State("songshan"));
            Assert.Equal(_d.Cheonha.Bosses.SpawnTargetScale, life.SpawnTargetScale);
            Assert.Equal(0.4f, life.SpawnTargetScale, 4);
            Assert.Null(life.TryEngage(center, lv, 12));                // 싸우는 중엔 새 싸움이 없다

            // 기혈 = 기준 × (1 + 0.08 × 12) ÷ 2.6: 12분이면 0.75배 남짓
            Assert.Equal(90000f * (1 + 0.08f * 12) / 2.6f, fight.MaxHealth, 0);
            Assert.Equal(0.75, Math.Round(fight.MaxHealth / 90000f, 2));
        }

        [Fact] public void 잠든_땅에는_들어서도_싸움이_없다()
        {
            var life = Life("songshan");
            life.UpdateRealm(Level("jeoljeong"));
            Assert.Equal(BossRegionState.Dormant, life.State("danggata"));
            Assert.Null(life.TryEngage(Map.Center("danggata"), Level("jeoljeong"), 13));
            Assert.Throws<InvalidOperationException>(() => life.Engage("danggata", Level("jeoljeong"), 13));
        }

        [Theory]
        [InlineData(20, 1.0)]
        [InlineData(12, 0.754)]
        [InlineData(30, 1.308)]
        public void 보스_기혈은_싸움을_건_시각을_따른다(float minute, double ratio)
        {
            var life = Life("songshan");
            foreach (var b in _d.Bosses.Where(b => b.Chapter != "prologue"))
                Assert.Equal(b.Health * ratio, life.Health(b, minute), b.Health * 0.001);
        }

        [Fact] public void 싸움_도중_돌파해도_보스는_싸움을_건_순간의_축척이고_밟히지_않는다()
        {
            var life = Life("songshan");
            life.UpdateRealm(Level("jeoljeong"));
            var fight = life.Engage("huashan", Level("jeoljeong") + 5, 15);
            var woke = life.UpdateRealm(Level("chojeoljeong"));
            Assert.Equal(AwakenTable["songshan"][1], woke.Select(r => r.Id));
            Assert.Same(fight, life.Fight);
            Assert.Equal(3, life.Fight!.Realm);
            var scale = new Scale(_d);
            Assert.False(scale.Crushes(fight.Realm, scale.RealmIndex("chojeoljeong"), boss: true));
        }

        [Fact] public void 첫_처치는_봉인석과_문파_해금과_윤회의_기_300과_보갑()
        {
            var rules = _d.Cheonha.Bosses;
            foreach (var region in _d.Cheonha.BossRegions)
            {
                var life = Life(region.Id);
                life.UpdateRealm(Level("jeoljeong"));
                life.Engage(region.Id, Level("jeoljeong"), 14);
                var kill = life.Defeat();
                Assert.True(kill.First);
                Assert.False(kill.IsFinal);
                Assert.Equal(region.SealStone, kill.SealStone);
                Assert.NotNull(kill.SealStone);
                Assert.Equal(rules.FirstKill.RebirthQi, kill.RebirthQi);
                Assert.Equal(300, kill.RebirthQi);
                Assert.Equal(5, kill.BloodCrystal); // 첫 처치에도 혈정 5(T-29)
                Assert.True(kill.Chest);
                var sect = _d.Cheonha.Sect(region.Sect!);
                Assert.Equal(sect.Unlock == "start" ? null : sect.Id, kill.UnlockSect);
                Assert.Equal(sect.Leader, kill.Leader);
                Assert.Equal(BossRegionState.Defeated, life.State(region.Id));
                Assert.False(life.InFight);
                Assert.Equal(1f, life.SpawnTargetScale);
                Assert.Null(life.TryEngage(Map.Center(region.Id), Level("jeoljeong"), 15)); // 이번 생엔 다시 안 일어난다
            }
        }

        [Fact] public void 첫_처치_보상이_기획서_해금_표와_같다()
        {
            var rows = TestData.GddTable(TestData.GddSection("해금과 도전 과제"));
            int seen = 0;
            foreach (var row in rows)
            {
                var m = Regex.Match(row[0], "^(.+) 첫 처치$");
                if (!m.Success) continue;
                var boss = _d.Bosses.Single(b => b.NameKo.EndsWith(m.Groups[1].Value));
                var region = _d.Cheonha.Regions.Single(r => r.Boss == boss.Id);
                var life = Life(region.Id);
                life.UpdateRealm(Level("jeoljeong"));
                life.Engage(region.Id, Level("jeoljeong"), 14);
                var kill = life.Defeat();

                string? sect = null, leader = null;
                var weapons = new List<string>();
                foreach (var item in row[1].Split(", "))
                {
                    var reborn = Regex.Match(item, "^(.+?)(?:으로|로) 환생$");
                    var joins = Regex.Match(item, "^(.+?) (?:객잔 )?합류$");
                    if (reborn.Success) sect = _d.Cheonha.Sects.Single(s => s.NameKo == reborn.Groups[1].Value).Id;
                    else if (joins.Success) leader = _d.Characters.Single(c => c.NameKo == joins.Groups[1].Value).Id;
                    else if (_d.Weapons.Any(w => w.NameKo == item)) weapons.Add(_d.Weapons.Single(w => w.NameKo == item).Id);
                    else Assert.Equal("장경각", item); // 장경각은 시설(봉인석 1개)이라 여기 보상이 아니다
                }
                Assert.Equal(sect, kill.UnlockSect);
                Assert.Equal(leader, kill.Leader);
                Assert.Equal(weapons, kill.UnlockWeapons);
                seen++;
            }
            Assert.Equal(_d.Cheonha.BossRegions.Count(), seen);
        }

        [Fact] public void 이미_쓰러뜨린_땅의_주인은_혈영으로_일어나_윤회의_기_100과_혈정_5()
        {
            var record = new BossRecord(new[] { "songshan" }, sealStones: 1);
            var life = Life("songshan", record);
            life.UpdateRealm(Level("jeoljeong"));
            var fight = life.Engage("songshan", Level("jeoljeong"), 13);
            Assert.True(fight.IsShadow);
            var kill = life.Defeat();
            Assert.False(kill.First);
            Assert.Equal(_d.Cheonha.Bosses.RepeatKill.RebirthQi, kill.RebirthQi);
            Assert.Equal(100, kill.RebirthQi);
            Assert.Equal(5, kill.BloodCrystal);
            Assert.True(kill.Chest);
            Assert.Null(kill.SealStone);
            Assert.Null(kill.UnlockSect);
            Assert.Null(kill.Leader);
            Assert.Empty(kill.UnlockWeapons);
            record.Apply(kill);
            Assert.Equal(1, record.SealStones);
        }

        [Fact] public void 처치를_기록에_반영하면_봉인석이_쌓인다()
        {
            var record = new BossRecord();
            var life = Life("wudangsan", record);
            life.UpdateRealm(Level("jeoljeong"));
            foreach (var id in new[] { "wudangsan", "huashan" })
            {
                life.Engage(id, Level("jeoljeong"), 14);
                record.Apply(life.Defeat());
            }
            Assert.Equal(2, record.SealStones);
            Assert.True(record.HasDefeated("huashan"));
            Assert.False(record.HasDefeated("kunlun"));
        }

        [Fact] public void 봉마곡은_봉인석_일곱과_현경이_있어야_열린다()
        {
            var record = new BossRecord(_d.Cheonha.BossRegions.Select(r => r.Id), sealStones: 6);
            var life = Life("songshan", record);
            Assert.False(life.ShouldBeAwake(_d.Cheonha.Region("bongmagok"), new Scale(_d).TopRealm, 6));
            life.UpdateRealm(Level("hwagyeong"));
            Assert.DoesNotContain(life.Awake, r => r.Final);
            Assert.Empty(life.UpdateRealm(Level("hyeongyeong")));      // 여섯이면 현경이어도 안 열린다

            record.SealStones = 7;
            Assert.Equal(new[] { "bongmagok" }, life.Refresh().Select(r => r.Id));
            var fight = life.TryEngage(Map.Center("bongmagok"), Level("hyeongyeong"), 30);
            Assert.NotNull(fight);
            Assert.True(fight!.IsFinal);
            Assert.Equal("danmok_hyeon", fight.Boss.Id);
            var kill = life.Defeat();
            Assert.True(kill.IsFinal);
            Assert.False(kill.Chest);
            Assert.Equal(0, kill.RebirthQi);

            // 일곱을 모았어도 화경이면 안 열린다
            var early = Life("songshan", new BossRecord(sealStones: 7));
            early.UpdateRealm(Level("hwagyeong"));
            Assert.DoesNotContain(early.Awake, r => r.Final);
            Assert.Equal(7, _d.Cheonha.Bosses.FinalRequires.SealStones);
        }
    }
}
