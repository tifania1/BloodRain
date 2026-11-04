using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>QA-05 보강(2026-11-04 Core 점검 2): 기믹 dt 가드·경지 보상 onlyIn·깨어남 확인을 더 보고, 코드가 하나만 아는 규칙 값을 DataCheck 가 잡는지.</summary>
    public class CoreQa2Tests
    {
        readonly GameData _d = TestData.Load();

        [Fact] public void 매화_더미는_dt가_0_이하면_다시_피는_시간이_그대로()
        {
            var h = new PlumHeapGimmick(_d.Gimmicks.PlumHeaps);
            Assert.True(h.TryUse(0f, 100) > 0f);
            float left = h.RegrowRemaining;
            h.Tick(-5f);
            h.Tick(0f);
            Assert.Equal(left, h.RegrowRemaining);
        }

        [Fact] public void 빙판은_dt가_0_이하면_속도가_그대로()
        {
            var ice = _d.Gimmicks.IceFloor;
            var cur = new Vector2(3, 0);
            var want = new Vector2(0, 3);
            Assert.Equal(cur, IceFloorGimmick.Step(ice, cur, want, 0f, onIce: true));
            Assert.Equal(cur, IceFloorGimmick.Step(ice, cur, want, -0.1f, onIce: true));
            Assert.NotEqual(cur, IceFloorGimmick.Step(ice, cur, want, 0.1f, onIce: true));
        }

        [Fact] public void 경지_보상은_onlyIn_경지를_그_모드에서만_센다()
        {
            var realms = _d.Balance.Realms.Select(r => new RealmDef { Id = r.Id, NameKo = r.NameKo, Level = r.Level, Reward = r.Reward, OnlyIn = r.OnlyIn }).ToList();
            Assert.All(realms, r => Assert.Null(r.OnlyIn));   // 지금 데이터엔 없다
            var top = realms.Max(r => r.Level);
            var all = RealmRewards.Upto(realms, top);
            var hyeon = realms.OrderBy(r => r.Level).Last();
            Assert.True(RealmRewards.Of(hyeon).DoubleUltimate);
            hyeon.OnlyIn = new List<string> { "endless" };
            Assert.False(RealmRewards.Upto(realms, top).DoubleUltimate);
            Assert.True(RealmRewards.Upto(realms, top, "endless").DoubleUltimate);
            Assert.Equal(all.MaxHealthFlat, RealmRewards.Upto(realms, top).MaxHealthFlat);
        }

        [Fact] public void 깨어난_곳이_Tick_전에_싸움으로_끝났으면_경고를_걸지_않는다()
        {
            var b = new BossAwakening(_d, "songshan", new BossRecord());
            var center = new Regions(_d).Center("songshan");
            Assert.Contains(b.UpdateRealm(35), r => r.Id == "songshan");
            b.Engage("songshan", 35, 12);   // 연출로 바로 싸움(Tick 이 아직 깨어남을 안 봄)
            Assert.Null(b.Tick(0.1f, center, 35, 12));   // 싸우는 중
            b.Defeat();
            Assert.Null(b.Tick(0.1f, center, 35, 13));
            Assert.Null(b.GraceRegionId);   // 쓰러진 땅에 경고가 걸리지 않는다
        }

        [Fact] public void 금종조_대기_시간은_막은_뒤_간격에서_줄어든다()
        {
            var g = _d.Insight("geumjong");
            var fx = new InsightEffects("samjae_sword");
            fx.Take(g, new YinYangGauge(_d.Balance.Yinyang, 0));
            Assert.Equal(0f, fx.BlockCooldown);
            Assert.True(fx.TryBlock());
            Assert.Equal(g.Param("intervalSeconds"), fx.BlockCooldown);
            fx.Tick(3f, true, 100f);
            Assert.Equal(g.Param("intervalSeconds") - 3f, fx.BlockCooldown, 3);
        }

        // ---- 글로 적은 규칙은 코드가 아는 값만(DataCheck) ----

        static GameData Fresh() => GameData.FromJson(TestData.ReadFiles());

        static void Broken(Action<GameData> breakIt, string expect)
        {
            var d = Fresh();
            breakIt(d);
            Assert.Contains(DataCheck.Problems(d), x => x.Contains(expect));
        }

        [Fact] public void 코드가_모르는_규칙_값은_읽을_때_막는다()
        {
            Assert.Equal(1f, _d.Insight("geumjong").Param("blocks"));
            Broken(d => d.Cheonha.Cluster.XpPerCluster = "members", "cheonha.cluster.xpPerCluster");
            Broken(d => d.Cheonha.Cluster.KillCount = "one", "cheonha.cluster.killCount");
            Broken(d => d.Cheonha.Cluster.DrawMembers = "all", "cheonha.cluster.drawMembers");
            Broken(d => d.Cheonha.Density.Blend = "nearest_one", "cheonha.density.blend");
            Broken(d => d.Cheonha.Cycle.Resets.Add("training"), "cheonha.cycle.resets");
            Broken(d => d.Cheonha.Cycle.Keeps.Add("sealStones"), "cheonha.cycle sealStones");
            Broken(d => d.Insight("geumjong").Params["blocks"] = 2, "insights.geumjong.blocks");
        }
    }
}
