#nullable enable
// 서 있는 지역에 따라 갈리는 적을 찾는다(기획서 6장 '지역별 고유 적'·'정예(5분마다)', 7장 '지역과 혈운 농도').
//   - 지역 고유 적: enemies.json 에서 chapter 가 그 지역의 장 이름(cheonha.json regions[].chapter)인 적. 그 지역 안에서만 나온다.
//   - n번째 정예: 그 지역 장 이름의 enemies.json elites 표에서 min(n, 표 길이)번째. 넷째·다섯째는 셋째가 다시 나온다.
//     지역 정예가 없는 땅(청운진·개방 총타·남궁세가)은 흐름의 eliteFallback 지역(숭산) 표를 쓴다.
// 지금 어느 지역에 서 있는지는 Core/World 가 정한다. 여기서는 지역 id 를 받기만 한다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Spawning
{
    public sealed class RegionSpawns
    {
        readonly Dictionary<string, RegionDef> _regions = new Dictionary<string, RegionDef>();
        readonly Dictionary<string, IReadOnlyList<string>> _local = new Dictionary<string, IReadOnlyList<string>>();
        readonly Dictionary<string, IReadOnlyList<string>> _elites = new Dictionary<string, IReadOnlyList<string>>();

        /// <param name="flowId">정예 대신 쓸 지역(eliteFallback)을 적어 둔 흐름. 기본은 한 생.</param>
        public RegionSpawns(GameData data, string flowId = WaveTable.Life)
        {
            var flow = data.Chapters.FirstOrDefault(c => c.Id == flowId)
                ?? throw new KeyNotFoundException($"chapters.json 에 없는 흐름입니다: {flowId}");

            foreach (var r in data.Cheonha.Regions)
            {
                _regions[r.Id] = r;
                _local[r.Id] = r.Chapter == null
                    ? (IReadOnlyList<string>)Array.Empty<string>()
                    : data.Enemies.Where(e => e.Chapter == r.Chapter).Select(e => e.Id).ToList();
            }

            IReadOnlyList<string>? OwnTable(RegionDef r) =>
                r.Chapter != null && data.Elites.TryGetValue(r.Chapter, out var t) && t.Count > 0 ? t : null;

            var fb = flow.EliteFallback;
            IReadOnlyList<string>? fallback = null;
            if (fb != null)
            {
                if (!_regions.TryGetValue(fb.Region, out var fr))
                    throw new InvalidOperationException($"chapters.json {flowId}: eliteFallback.region '{fb.Region}' 이 cheonha.json 에 없음");
                fallback = OwnTable(fr) ?? throw new InvalidOperationException($"chapters.json {flowId}: eliteFallback 지역 {fr.NameKo} 에 정예 표가 없음");
            }
            foreach (var r in _regions.Values)
            {
                bool useFallback = fb != null && fb.Regions.Contains(r.Id);
                var table = useFallback ? fallback : OwnTable(r);
                if (table != null) _elites[r.Id] = table;
            }
        }

        RegionDef Region(string regionId) =>
            _regions.TryGetValue(regionId, out var r) ? r : throw new KeyNotFoundException($"cheonha.json 에 없는 지역입니다: {regionId}");

        /// <summary>그 지역 고유 적 id(enemies.json 순서). 고유 적이 없는 땅은 빈 목록.</summary>
        public IReadOnlyList<string> LocalEnemies(string regionId)
        {
            Region(regionId);
            return _local[regionId];
        }

        /// <summary>그 지역에서 정예가 나오는지(봉마곡처럼 표가 없는 곳은 아니다).</summary>
        public bool HasElites(string regionId)
        {
            Region(regionId);
            return _elites.ContainsKey(regionId);
        }

        /// <summary>그 지역에서 쓰는 정예 표(첫째·둘째·셋째). 대신 쓰는 땅이면 숭산 표.</summary>
        public IReadOnlyList<string> EliteTable(string regionId)
        {
            var r = Region(regionId);
            return _elites.TryGetValue(regionId, out var t) ? t : throw new InvalidOperationException($"{r.NameKo} 에는 정예 표가 없습니다");
        }

        /// <summary>이 생의 n번째(1부터) 정예. 표가 다하면 마지막 것이 다시 나온다.</summary>
        public string Elite(string regionId, int nth)
        {
            if (nth < 1) throw new ArgumentOutOfRangeException(nameof(nth), nth, "정예 순번은 1부터");
            var t = EliteTable(regionId);
            return t[Math.Min(nth, t.Count) - 1];
        }

        /// <summary>사건에서 실제로 나올 적. n번째 정예는 지역 표에서, 지역 무리는 그 지역 고유 적 가운데 하나(여럿이면 같은 비율로 난수 한 번),
        /// 고유 적이 없는 땅이면 데이터에 적힌 대신할 적. 나머지는 데이터 그대로.</summary>
        public string EnemyFor(ChapterEventDef e, string regionId, Random rng)
        {
            if (e.Type == WaveEventKind.Elite && e.Nth > 0) return Elite(regionId, e.Nth);
            if (e.Local)
            {
                var local = LocalEnemies(regionId);
                if (local.Count == 1) return local[0];
                if (local.Count > 1) return local[rng.Next(local.Count)];
            }
            return e.Enemy;
        }

        public string EnemyFor(WaveEvent e, string regionId, Random rng) => EnemyFor(e.Def, regionId, rng);
    }
}
