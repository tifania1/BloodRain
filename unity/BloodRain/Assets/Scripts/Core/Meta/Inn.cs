#nullable enable
// 청운객잔 시설(기획서 10장 '객잔 시설', cheonha.json inn).
//   조건을 갖추면 열린다. 혈정을 써서 짓는 시설(약방·대장간)은 조건을 갖춘 뒤 Build 로.
//   한 번 열린 시설은 혼 해금 토큰 facility:<id> 로 남아, 회귀로 봉인석이 지워져도 닫히지 않는다.
//   객잔에 머무는 대표 인물은 계정에서 처음 쓰러뜨린 땅의 주인 지역의 문파 leader(21장).
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public static class Inn
    {
        public const string TokenPrefix = "facility:";

        public static string Token(string facilityId) => TokenPrefix + facilityId;

        static InnDef Def(Soul soul) => soul.Data.Cheonha.Inn;

        /// <summary>시설이 열려 있다: 지었거나, 한 번 열려 혼에 남았거나, 짓지 않는 시설이고 지금 조건을 갖췄다.</summary>
        public static bool IsOpen(Soul soul, string facilityId) => IsOpen(soul, Def(soul).Facility(facilityId));

        static bool IsOpen(Soul soul, FacilityDef f) =>
            soul.Unlocks.Contains(Token(f.Id)) || (f.BuildBloodCrystal <= 0 && MeetsRequirement(soul, f));

        static bool Recorded(Soul soul, FacilityDef f) => soul.Unlocks.Contains(Token(f.Id));

        /// <summary>조건(서장·봉인석·땅의 주인·해금 토큰)을 갖췄다. 짓는 값은 따지지 않는다.</summary>
        public static bool MeetsRequirement(Soul soul, FacilityDef f)
        {
            var r = f.Require;
            if (r.Prologue && !soul.PrologueDone) return false;
            if (soul.SealStoneCount < r.SealStones) return false;
            if (r.BossKilled != null && !soul.BossesKilled.Contains(r.BossKilled)) return false;
            if (r.Unlock != null && !soul.IsUnlocked(r.Unlock)) return false;
            return true;
        }

        /// <summary>열린 시설 id(데이터 순서).</summary>
        public static IReadOnlyList<string> OpenFacilities(Soul soul) =>
            Def(soul).Facilities.Where(f => IsOpen(soul, f)).Select(f => f.Id).ToList();

        /// <summary>조건은 갖췄고 아직 안 지은, 혈정을 써서 짓는 시설.</summary>
        public static IReadOnlyList<FacilityDef> Buildable(Soul soul) =>
            Def(soul).Facilities.Where(f => f.BuildBloodCrystal > 0 && !IsOpen(soul, f) && MeetsRequirement(soul, f)).ToList();

        /// <summary>혈정을 써서 짓는다. 조건이 안 되거나 혈정이 모자라거나 이미 있으면 false.</summary>
        public static bool TryBuild(Soul soul, string facilityId)
        {
            var f = Def(soul).Facility(facilityId);
            if (f.BuildBloodCrystal <= 0) throw new InvalidOperationException($"짓는 시설이 아닙니다(조건만 갖추면 열림): {f.Id}");
            if (IsOpen(soul, f) || !MeetsRequirement(soul, f)) return false;
            if (!soul.TrySpendBloodCrystal(f.BuildBloodCrystal)) return false;
            soul.Unlock(Token(f.Id));
            return true;
        }

        /// <summary>조건만 갖추면 열리는 시설 가운데 처음 조건을 갖춘 것을 혼에 적고 그 id 를 돌려준다(정산 뒤·대화 뒤에 부른다).
        /// 적어 둔 시설은 회귀로 봉인석이 지워져도 열려 있다. 객잔 본채처럼 조건이 없는 시설은 처음부터 열려 있어 새로 열린 것으로 치지 않는다.</summary>
        public static IReadOnlyList<string> Refresh(Soul soul)
        {
            var opened = new List<string>();
            foreach (var f in Def(soul).Facilities)
            {
                if (f.BuildBloodCrystal > 0 || Recorded(soul, f) || !MeetsRequirement(soul, f)) continue;
                bool always = !f.Require.Prologue && f.Require.SealStones == 0 && f.Require.BossKilled == null && f.Require.Unlock == null;
                soul.Unlock(Token(f.Id));
                if (!always) opened.Add(f.Id);
            }
            return opened;
        }

        /// <summary>객잔에 머무는 대표 인물 id(그 땅의 주인을 처음 쓰러뜨린 문파의 leader, 지역 데이터 순서).</summary>
        public static IReadOnlyList<string> Residents(Soul soul)
        {
            var c = soul.Data.Cheonha;
            var list = new List<string>();
            foreach (var region in c.BossRegions)
            {
                if (region.Sect == null || !soul.BossesKilled.Contains(region.Id)) continue;
                var leader = c.Sects.FirstOrDefault(s => s.Id == region.Sect)?.Leader;
                if (!string.IsNullOrEmpty(leader) && !list.Contains(leader!)) list.Add(leader!);
            }
            return list;
        }
    }
}
