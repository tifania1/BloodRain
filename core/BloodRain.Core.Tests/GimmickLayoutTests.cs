using System.Linq;
using System.Numerics;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>지역 안 기믹 배치(R-10 '표대로', gimmick_start_values.md 2절, gimmicks.json layout).</summary>
    public class GimmickLayoutTests
    {
        readonly Data.GameData _d = TestData.Load();
        Regions R => new Regions(_d);
        Data.GimmickLayoutDef L => _d.Gimmicks.Layout;
        float Factor(string realm) => new Scale(_d).Factor(new Scale(_d).RealmIndex(_d.Balance.Realms.Single(r => r.Id == realm).Level));

        [Fact] public void 배치_값은_시작값_표와_같다()
        {
            Assert.Equal(30, L.BellSpacing); Assert.Equal(60, L.PagodaSpacing); Assert.Equal(60, L.BambooSpacing); Assert.Equal(60, L.CliffSpacing);
            Assert.Equal(30, L.PlumSpacing); Assert.Equal(24, L.WaterSpacing); Assert.Equal(4.5f, L.WaterHalf); Assert.Equal(16, L.TaijiSpacing);
            Assert.Equal(28, L.CampfireSpacing); Assert.Equal(6, L.FogOffset); Assert.Equal(12, L.AltarRing); Assert.Equal(12, L.IslandRing); Assert.Equal(20, L.FinalArenaRadius);
            // 웹 프로토타입 CHG_LD(시안 LD 를 옮긴 것)와 같은 값
            var proto = TestData.ReadRepoFile("web", "prototype", "template.html");
            Assert.Contains($"waterSpacing: {L.WaterSpacing}, waterHalf: {L.WaterHalf}, waterWave: {L.WaterWave}, waterWaveLen: {L.WaterWaveLen}, taijiSpacing: {L.TaijiSpacing}, campfireSpacing: {L.CampfireSpacing}, pagodaSpacing: {L.PagodaSpacing}, bellSpacing: {L.BellSpacing}", proto);
        }

        [Fact] public void 범종은_한가운데에도_있고_탑림은_반_칸_비켜_한가운데가_빈다()
        {
            var r = R; var song = r.Region("songshan"); var c = r.Center(song);
            var bells = GimmickLayout.Near(r, song, L.BellSpacing, false, c, 100f);
            var pagodas = GimmickLayout.Near(r, song, L.PagodaSpacing, true, c, 100f);
            Assert.Contains(bells, p => Vector2.Distance(p, c) < 0.01f);
            Assert.DoesNotContain(pagodas, p => Vector2.Distance(p, c) < 1f);
            Assert.All(pagodas, p => Assert.Equal(L.PagodaSpacing / 2f, System.Math.Abs(p.X - c.X) % L.PagodaSpacing, 3));
            // 칸은 모두 숭산이 가장 가까운 자리
            Assert.All(bells.Concat(pagodas), p => Assert.Equal("songshan", r.Nearest(p, 0).Id));
            // 탑림 무더기 안 범종은 뺀다: 반 칸 격자 자리(30 의 홀수 배)는 범종 격자와 겹친다
            var kept = GimmickLayout.Without(bells, pagodas, _d.Gimmicks.PagodaForest.PatchRadius);
            Assert.True(kept.Count < bells.Count);
            Assert.Contains(kept, p => Vector2.Distance(p, c) < 0.01f);
        }

        [Fact] public void 현경이면_범종은_한가운데_하나고_탑림도_한가운데로_돌아온다()
        {
            var r = R; var song = r.Region("songshan");
            float k = Factor("hyeongyeong");
            var bells = GimmickLayout.All(r, song, L.BellSpacing * k, false);
            Assert.Single(bells);
            Assert.Equal(r.Center(song), bells[0]);
            var pagodas = GimmickLayout.All(r, song, L.PagodaSpacing * k, true);
            Assert.Single(pagodas);
            Assert.Equal(r.Center(song), pagodas[0]);
            // 삼류엔 수백 개
            Assert.True(GimmickLayout.All(r, song, L.BellSpacing, false).Count > 200);
        }
    }
}
