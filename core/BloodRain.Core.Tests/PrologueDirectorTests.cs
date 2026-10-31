using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>서장 10분의 생성(기획서 4장 '서장 상세 흐름', chapters.json prologue).</summary>
    public class PrologueDirectorTests
    {
        readonly GameData _d = TestData.Load();
        PrologueDirector New(int seed = 1) => new PrologueDirector(_d, new Random(seed));
        ChapterDef Prologue => _d.Chapters.Single(c => c.Id == "prologue");

        [Fact] public void 부족분의_30퍼센트를_서장_가중치표에서_만든다()
        {
            var pd = New();
            var s = pd.Second(0, 1, 0);
            Assert.Equal(5, s.Spawns.Count); // 15 x 0.3 = 4.5 → 5
            var band = Prologue.Bands.First();
            Assert.All(s.Spawns, x => Assert.True(band.WeightOf(x.Enemy) > 0, x.Enemy));
            Assert.Empty(pd.Second(1f / 60, 1, 500).Spawns);
        }

        [Fact] public void 장삼_경고_전_30초는_만들지_않고_장삼이_나오면_다시_만든다()
        {
            var pd = New();
            float stop = Prologue.StopSpawningAtMinute!.Value;
            Assert.Equal(8.5f, stop);
            Assert.Equal(0f, pd.TargetAt(8.75f));
            var held = pd.Second(8.75f, 20, 0);
            Assert.Empty(held.Spawns);
            Assert.False(held.Held); // 멈춤(Hold)이 아니라 곡선의 생성 중지
            Assert.NotEmpty(pd.Second(9f, 20, 0).Spawns);
        }

        [Fact] public void 사건은_시각마다_한_번씩_돌진_정예_장삼_순서()
        {
            var pd = New();
            var seen = new List<string>();
            for (int sec = 0; sec <= 600; sec++)
                seen.AddRange(pd.Second(sec / 60f, 1, 9999).Events.Select(e => $"{e.Wave.Def.Kind}:{e.Enemy}"));
            Assert.Equal(new[] { "charge:hyeolin", "elite:gangsi_axeman", "boss:jang_sam" }, seen);
        }

        [Fact] public void 돌파_멈춤_중에는_안_만들지만_사건은_낸다()
        {
            var pd = New();
            pd.OnBreakthrough();
            Assert.Equal(_d.Cheonha.Breakthrough.SpawnHoldSeconds, pd.HoldRemaining, 3);
            var s = pd.Second(3.5f, 10, 0);
            Assert.True(s.Held);
            Assert.Empty(s.Spawns);
            Assert.Single(s.Events);
            pd.Tick(10f);
            Assert.NotEmpty(pd.Second(3.6f, 10, 0).Spawns);
        }

        [Fact] public void 덩이는_한_생과_같이_그_순간_경지로()
        {
            var pd = New();
            var s = pd.Second(4f, 10, 0);
            int realm = pd.Scale.RealmIndex(10);
            Assert.All(s.Spawns, x => { Assert.Equal(realm, x.BornRealm); Assert.Equal(pd.Scale.ClusterSize(realm), x.Members); });
        }

        [Fact] public void 목표_배율은_기본_1이고_바꾸면_곱한다()
        {
            var pd = New();
            float baseTarget = pd.TargetAt(9.5f);
            Assert.Equal(new SpawnCurve(_d.Balance.SpawnTarget).TargetAt(9.5f), baseTarget, 3);
            pd.TargetScale = 0.4f;
            Assert.Equal(baseTarget * 0.4f, pd.TargetAt(9.5f), 3);
        }

        [Fact] public void 같은_시드면_같은_적_순서()
        {
            string Run(int seed)
            {
                var pd = New(seed);
                return string.Join(",", Enumerable.Range(0, 120).SelectMany(sec => pd.Second(sec / 60f * 5, 1, 0).Spawns.Select(x => x.Enemy)));
            }
            Assert.Equal(Run(7), Run(7));
        }
    }
}
