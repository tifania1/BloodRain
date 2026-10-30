#nullable enable
// 중원 지도의 지역(기획서 7장 '지역과 혈운 농도', 6장 지역 고유 적·정예). 숫자는 cheonha.json regions·world, 적은 enemies.json.
//
// 좌표는 World.Scale 과 같은 세계 m(삼류 축척, 왼쪽 위 0,0, 아래로 y 증가). cheonha.json pos 는 지도 그림 위 비율이고
// 세계 m = pos × (가로 4,000m, 세로 2,600m).
//
// 봉마곡처럼 봉인석이 모여야 보이는 지역(hiddenUntilSealStones)은 보이기 전엔 '서 있는 지역'에도 혈운 농도에도 끼지 않는다.
// 그래서 지역을 묻는 함수는 sealStones 를 받는다. null 을 주면 숨은 지역까지 모두 센다(도구·테스트용).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public sealed class Regions
    {
        /// <summary>지역 정예가 없는 땅(청운진·개방 총타·남궁세가)은 이 지역의 정예 표를 쓴다(6장 '정예').</summary>
        public const string EliteFallbackRegion = "songshan";
        /// <summary>서장 전용 장 이름. 청운진의 정예(강시 도부수)는 서장 생에서만 나온다.</summary>
        public const string PrologueChapter = "prologue";

        readonly WorldDef _world;
        readonly List<RegionDef> _regions;
        readonly IReadOnlyList<EnemyDef> _enemies;
        readonly IReadOnlyDictionary<string, List<string>> _elites;

        public Regions(GameData data) : this(data.Cheonha, data.Enemies, data.Elites) { }

        public Regions(CheonhaDef cheonha, IReadOnlyList<EnemyDef>? enemies = null, IReadOnlyDictionary<string, List<string>>? elites = null)
        {
            _world = cheonha.World;
            _regions = cheonha.Regions.ToList();
            if (_regions.Count == 0) throw new ArgumentException("지역이 하나도 없습니다", nameof(cheonha));
            _enemies = enemies ?? Array.Empty<EnemyDef>();
            _elites = elites ?? new Dictionary<string, List<string>>();
        }

        public IReadOnlyList<RegionDef> All => _regions;
        public float WidthMeters => _world.WidthMeters;
        public float HeightMeters => _world.HeightMeters;

        public RegionDef Region(string id)
        {
            var r = _regions.FirstOrDefault(x => x.Id == id);
            return r ?? throw new ArgumentException($"모르는 지역: {id}", nameof(id));
        }

        // ---- 좌표 ----

        /// <summary>지도 비율(0~1) → 세계 m.</summary>
        public Vector2 ToWorld(float mapX, float mapY) => new Vector2(mapX * _world.WidthMeters, mapY * _world.HeightMeters);
        public Vector2 ToWorld(Vector2 map) => ToWorld(map.X, map.Y);

        /// <summary>세계 m → 지도 비율(0~1).</summary>
        public Vector2 ToMap(Vector2 world) => new Vector2(world.X / _world.WidthMeters, world.Y / _world.HeightMeters);

        /// <summary>지역 한가운데(세계 m).</summary>
        public Vector2 Center(RegionDef region) => ToWorld(region.Pos[0], region.Pos[1]);
        public Vector2 Center(string regionId) => Center(Region(regionId));

        public float Distance(string fromRegion, string toRegion) => Vector2.Distance(Center(fromRegion), Center(toRegion));

        /// <summary>지도 안인가(가장자리 포함).</summary>
        public bool InsideWorld(Vector2 world) =>
            world.X >= 0f && world.Y >= 0f && world.X <= _world.WidthMeters && world.Y <= _world.HeightMeters;

        /// <summary>지도 끝 밖으로 나간 위치를 가장자리로 되돌린다. 장막이 걷힌 현경에서도 지도 밖으로는 못 나간다.</summary>
        public Vector2 ClampToWorld(Vector2 world) => new Vector2(
            Math.Max(0f, Math.Min(_world.WidthMeters, world.X)),
            Math.Max(0f, Math.Min(_world.HeightMeters, world.Y)));

        // ---- 서 있는 지역과 혈운 농도 ----

        /// <summary>그 지역이 지금 보이는가. sealStones 가 null 이면 모두 보인다고 본다.</summary>
        public static bool IsVisible(RegionDef region, int? sealStones) =>
            sealStones == null || region.HiddenUntilSealStones <= sealStones.Value;

        public IEnumerable<RegionDef> Visible(int? sealStones) => _regions.Where(r => IsVisible(r, sealStones));

        /// <summary>지금 서 있는 지역: 한가운데가 가장 가까운 지역. 거리가 같으면 데이터 순서가 앞선 쪽.</summary>
        public RegionDef Nearest(Vector2 world, int? sealStones = null)
        {
            RegionDef? best = null;
            float bestD = float.MaxValue;
            foreach (var r in _regions)
            {
                if (!IsVisible(r, sealStones)) continue;
                float d = Vector2.Distance(world, Center(r));
                if (d < bestD) { best = r; bestD = d; }
            }
            return best ?? throw new InvalidOperationException("보이는 지역이 없습니다");
        }

        /// <summary>혈운 농도: 가장 가까운 두 지역 값을 거리로 섞는다. v = (v1 × d2 + v2 × d1) / (d1 + d2).
        /// 지역 한가운데면 그 지역 값 그대로, 두 지역 한가운데 점이면 둘의 평균.
        /// 숨은 지역(봉마곡)은 봉인석이 다 모이기 전엔 섞지 않는다(7장): 게임에서는 늘 혼의 봉인석 수를 넘긴다(null 은 모두 보이는 시험용).</summary>
        public float Density(Vector2 world, int? sealStones = null)
        {
            RegionDef? a = null, b = null;
            float da = float.MaxValue, db = float.MaxValue;
            foreach (var r in _regions)
            {
                if (!IsVisible(r, sealStones)) continue;
                float d = Vector2.Distance(world, Center(r));
                if (d < da) { b = a; db = da; a = r; da = d; }
                else if (d < db) { b = r; db = d; }
            }
            if (a == null) throw new InvalidOperationException("보이는 지역이 없습니다");
            if (b == null || da + db <= 0f) return a.Density;
            return (a.Density * db + b.Density * da) / (da + db);
        }

        // ---- 지역에 딸린 것 ----

        /// <summary>그 지역의 지형 기믹 id(cheonha.json gimmicks). 기믹은 그 지역 안에서만, 지금 경지 축척으로 생긴다.</summary>
        public IReadOnlyList<string> Gimmicks(string regionId) => Region(regionId).Gimmicks;

        /// <summary>그 자리(서 있는 지역)의 지형 기믹.</summary>
        public IReadOnlyList<string> GimmicksAt(Vector2 world, int? sealStones = null) => Nearest(world, sealStones).Gimmicks;

        /// <summary>그 지역에서만 나오는 고유 적(enemies.json chapter 가 지역의 장 이름과 같은 적). 없으면 빈 목록.</summary>
        public IReadOnlyList<EnemyDef> LocalEnemies(string regionId)
        {
            var chapter = Region(regionId).Chapter;
            if (chapter == null) return Array.Empty<EnemyDef>();
            return _enemies.Where(e => e.Chapter == chapter).ToList();
        }

        /// <summary>그 지역 정예 표(첫째·둘째·셋째 순서, enemies.json elites). 지역 정예가 없는 땅은 숭산 표.
        /// 청운진의 표(서장)는 서장 생에서만 쓰고, 다른 생에서 청운진에 서 있으면 숭산 표를 쓴다.</summary>
        public IReadOnlyList<string> Elites(string regionId, bool prologueLife = false)
        {
            var r = Region(regionId);
            if (!(r.Chapter == PrologueChapter && !prologueLife) && OwnElites(regionId) is { } own) return own;
            return OwnElites(EliteFallbackRegion) ?? (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>그 지역 장(chapter)에 적힌 정예 표 그대로(대신 쓰는 표 없이). 없거나 비었으면 null.
        /// 정예 표를 찾는 곳은 여기 하나다. Spawning.RegionSpawns 도 이걸 부른다.</summary>
        public IReadOnlyList<string>? OwnElites(string regionId)
        {
            var chapter = Region(regionId).Chapter;
            return chapter != null && _elites.TryGetValue(chapter, out var t) && t.Count > 0 ? t : null;
        }

        /// <summary>그 생에서 nth 번째(0부터: 5분 0, 10분 1, 15분 2 …)로 나오는 정예 id. 셋째 뒤로는 마지막 정예가 다시 나온다.</summary>
        public string? Elite(string regionId, int nth, bool prologueLife = false)
        {
            var list = Elites(regionId, prologueLife);
            if (list.Count == 0 || nth < 0) return null;
            return list[Math.Min(nth, list.Count - 1)];
        }

    }
}
