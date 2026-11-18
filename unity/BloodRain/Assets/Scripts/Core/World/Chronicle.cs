#nullable enable
// 천하 연표 판정(설계안 docs/design/cheonha_expansion_full.md '천하 연표', backlog GU-11d). 자료는 chronicle.json.
//
// 규칙(설계안 '규칙' 1~3):
//   - 사건의 시각과 자리는 출생지와 상관없이 늘 같다.
//   - 그 자리에 닿으려면 혈우 장막(Scale.BoundaryRadius)이 출생지에서 그 자리까지 덮어야 한다.
//     그래서 '필요한 경지' = 장막 반경이 그 거리 이상인 가장 낮은 경지(MinRealm 이 있으면 그보다 낮지 않게). 현경은 장막이 없다.
//   - 그 경지에 사건 시각 전에(같은 시각 포함) 오르면 '닿음', 늦으면 '늦음'(몇 초), 끝내 못 오르면 '못 닿음'.
//   - 같은 시각에 먼 두 곳에서 터지는 사건은 SameTime 으로 찾는다(어느 쪽을 갈지가 그 생의 계획).
// 아직 Life 에 물리지 않았다. 시뮬레이터(GU-11e)와 연표 화면이 이 판정을 쓴다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public enum ChronicleReach { OnTime, Late, Never }

    /// <summary>한 사건을 한 생의 경지 도달 시각으로 판정한 결과.</summary>
    public readonly struct ChronicleJudgement
    {
        public readonly ChronicleEventDef Event;
        public readonly ChronicleReach Reach;
        /// <summary>필요한 경지 순번(0 = 삼류).</summary>
        public readonly int RequiredRealm;
        /// <summary>필요한 경지에 오른 시각(초). 못 올랐으면 null.</summary>
        public readonly float? ReachedAt;
        /// <summary>늦은 초. 닿음·못 닿음은 0.</summary>
        public readonly float LateBy;

        public ChronicleJudgement(ChronicleEventDef ev, ChronicleReach reach, int requiredRealm, float? reachedAt, float lateBy)
        {
            Event = ev;
            Reach = reach;
            RequiredRealm = requiredRealm;
            ReachedAt = reachedAt;
            LateBy = lateBy;
        }
    }

    public sealed class Chronicle
    {
        readonly ChronicleDef _def;
        readonly Regions _regions;
        readonly Scale _scale;
        readonly List<ChronicleEventDef> _events;

        public Chronicle(GameData data) : this(data.Chronicle, new Regions(data), new Scale(data)) { }

        public Chronicle(ChronicleDef def, Regions regions, Scale scale)
        {
            _def = def;
            _regions = regions;
            _scale = scale;
            Validate();
            _events = def.Events.OrderBy(e => e.AtSeconds).ToList();
        }

        void Validate()
        {
            var kinds = new HashSet<string>(_def.Kinds.Select(k => k.Id));
            var ids = new HashSet<string>();
            foreach (var e in _def.Events)
            {
                if (string.IsNullOrEmpty(e.Id) || !ids.Add(e.Id))
                    throw new InvalidOperationException($"chronicle.json 사건 id 가 비었거나 겹칩니다: '{e.Id}'");
                if (!kinds.Contains(e.Kind))
                    throw new InvalidOperationException($"chronicle.json {e.Id}: 모르는 종류 {e.Kind}");
                if (e.AtSeconds < 0)
                    throw new InvalidOperationException($"chronicle.json {e.Id}: 시각이 0보다 작습니다");
                int places = (e.Region != null ? 1 : 0) + (e.Pos != null ? 1 : 0) + (e.Everywhere ? 1 : 0);
                if (places != 1)
                    throw new InvalidOperationException($"chronicle.json {e.Id}: 자리는 region·pos·everywhere 중 하나만 둔다");
                if (e.Region != null && _regions.All.All(r => r.Id != e.Region))
                    throw new InvalidOperationException($"chronicle.json {e.Id}: 모르는 지역 {e.Region}");
                if (e.Offset != null && (e.Region == null || e.Offset.Length != 2))
                    throw new InvalidOperationException($"chronicle.json {e.Id}: offset 은 region 과 같이, 두 칸으로");
                if (e.Pos != null && e.Pos.Length != 2)
                    throw new InvalidOperationException($"chronicle.json {e.Id}: pos 는 두 칸");
                if (e.MinRealm != null && _scale.Realms.All(r => r.Id != e.MinRealm))
                    throw new InvalidOperationException($"chronicle.json {e.Id}: 모르는 경지 {e.MinRealm}");
                if (e.Everywhere && e.MinRealm == null)
                    throw new InvalidOperationException($"chronicle.json {e.Id}: 천하 전체 사건은 minRealm 이 있어야 한다");
            }
        }

        /// <summary>시각 순서.</summary>
        public IReadOnlyList<ChronicleEventDef> Events => _events;
        public IReadOnlyList<ChronicleKindDef> Kinds => _def.Kinds;

        public ChronicleEventDef Event(string id) =>
            _events.FirstOrDefault(e => e.Id == id) ?? throw new ArgumentException($"모르는 사건: {id}", nameof(id));

        /// <summary>사건 자리(세계 m). 천하 전체 사건은 null.</summary>
        public Vector2? Position(ChronicleEventDef e)
        {
            if (e.Everywhere) return null;
            if (e.Pos != null) return _regions.ToWorld(e.Pos[0], e.Pos[1]);
            var c = _regions.Center(e.Region!);
            return e.Offset == null ? c : c + new Vector2(e.Offset[0], e.Offset[1]);
        }

        /// <summary>출생지에서 사건 자리까지(세계 m). 천하 전체 사건은 0.</summary>
        public float Distance(ChronicleEventDef e, Vector2 birthplace)
        {
            var p = Position(e);
            return p == null ? 0f : Vector2.Distance(p.Value, birthplace);
        }

        public float Distance(ChronicleEventDef e, string birthRegion) => Distance(e, _regions.Center(birthRegion));

        /// <summary>그 자리를 장막이 덮는 가장 낮은 경지 순번(MinRealm 보다 낮지 않게).</summary>
        public int RequiredRealm(ChronicleEventDef e, Vector2 birthplace)
        {
            int min = e.MinRealm == null ? 0 : _scale.RealmIndex(e.MinRealm);
            float d = Distance(e, birthplace);
            for (int r = min; r <= _scale.TopRealm; r++)
            {
                var radius = _scale.BoundaryRadius(r);
                if (radius == null || radius.Value >= d) return r;
            }
            return _scale.TopRealm;
        }

        public int RequiredRealm(ChronicleEventDef e, string birthRegion) => RequiredRealm(e, _regions.Center(birthRegion));

        /// <summary>그 경지의 장막 안에 사건 자리가 드나.</summary>
        public bool InReach(ChronicleEventDef e, Vector2 birthplace, int realm) => realm >= RequiredRealm(e, birthplace);

        /// <summary>그 시각까지 터진 사건(시각 포함), 시각 순서.</summary>
        public IEnumerable<ChronicleEventDef> HappenedBy(float seconds) => _events.Where(e => e.AtSeconds <= seconds);

        /// <summary>그 시각 뒤에 터질 사건, 시각 순서.</summary>
        public IEnumerable<ChronicleEventDef> Upcoming(float seconds) => _events.Where(e => e.AtSeconds > seconds);

        /// <summary>같은 시각에 터지는 다른 사건.</summary>
        public IEnumerable<ChronicleEventDef> SameTime(ChronicleEventDef e) =>
            _events.Where(o => o != e && Math.Abs(o.AtSeconds - e.AtSeconds) < 0.001f);

        /// <summary>
        /// 한 생의 경지 도달 시각으로 사건 하나를 판정한다.
        /// realmReachedAt(경지 순번) = 그 경지에 오른 시각(초), 못 올랐으면 null. 삼류(0)는 0초로 본다.
        /// </summary>
        public ChronicleJudgement Judge(ChronicleEventDef e, Vector2 birthplace, Func<int, float?> realmReachedAt)
        {
            int need = RequiredRealm(e, birthplace);
            float? at = need == 0 ? 0f : realmReachedAt(need);
            if (at == null) return new ChronicleJudgement(e, ChronicleReach.Never, need, null, 0f);
            if (at.Value <= e.AtSeconds) return new ChronicleJudgement(e, ChronicleReach.OnTime, need, at, 0f);
            return new ChronicleJudgement(e, ChronicleReach.Late, need, at, at.Value - e.AtSeconds);
        }

        /// <summary>모든 사건을 시각 순서로 판정한다.</summary>
        public IReadOnlyList<ChronicleJudgement> JudgeAll(string birthRegion, Func<int, float?> realmReachedAt)
        {
            var birth = _regions.Center(birthRegion);
            return _events.Select(e => Judge(e, birth, realmReachedAt)).ToList();
        }
    }
}
