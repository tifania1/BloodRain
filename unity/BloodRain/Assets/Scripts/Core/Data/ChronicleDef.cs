#nullable enable
// 천하 연표(chronicle.json, 설계안 docs/design/cheonha_expansion_full.md '천하 연표', backlog GU-11d).
// 판정(장막이 덮는지, 제때 닿는지)은 World.Chronicle 이 한다.
// 한국어 칸(~Ko)은 strings 의 chronicle. 키로 옮긴다(chronicle.events.<id>.name·place, chronicle.kinds.<id>.name).
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public sealed class ChronicleDef
    {
        /// <summary>사건 종류 넷(문파의 위기·강호의 움직임·기연·인물의 운명).</summary>
        public List<ChronicleKindDef> Kinds = new List<ChronicleKindDef>();
        public List<ChronicleEventDef> Events = new List<ChronicleEventDef>();

        public static string EventNameKey(ChronicleEventDef e) => $"chronicle.events.{e.Id}.name";
        public static string EventPlaceKey(ChronicleEventDef e) => $"chronicle.events.{e.Id}.place";
        public static string KindNameKey(ChronicleKindDef k) => $"chronicle.kinds.{k.Id}.name";
    }

    public sealed class ChronicleKindDef
    {
        public string Id = "";
        public string NameKo = "";
    }

    public sealed class ChronicleEventDef
    {
        public string Id = "";
        /// <summary>ChronicleDef.Kinds 의 id.</summary>
        public string Kind = "";
        /// <summary>생 시작부터 몇 초에 터지나. 출생지와 상관없이 늘 같다.</summary>
        public float AtSeconds;
        /// <summary>자리: 이 지역 한가운데 + Offset(세계 m). Pos 와 둘 중 하나.</summary>
        public string? Region;
        /// <summary>Region 한가운데에서 비켜 선 거리(세계 m, 지도처럼 y 아래쪽이 +).</summary>
        public float[]? Offset;
        /// <summary>자리: 지도 비율(0~1). Region 이 없을 때.</summary>
        public float[]? Pos;
        /// <summary>자리 없이 천하 전체(혈월). 거리를 보지 않고 MinRealm 만 본다.</summary>
        public bool Everywhere;
        /// <summary>거리와 상관없이 이 경지는 되어야 닿는다(balance.json realms id). 없으면 거리만 본다.</summary>
        public string? MinRealm;
        public string PlaceKo = "";
        public string NameKo = "";
        /// <summary>진영에 따라 갈리는 갈래(천하 확장 '정·사·마 진영'). 없는 진영은 갈래 없이 본 사건 그대로.</summary>
        public List<ChronicleBranchDef> Branches = new List<ChronicleBranchDef>();
    }

    /// <summary>같은 사건의 진영별 갈래 한 줄. 내용(적·보상)은 아직 없고 이름만.</summary>
    public sealed class ChronicleBranchDef
    {
        public string Id = "";
        /// <summary>cheonha.json camps.list 의 id.</summary>
        public string Camp = "";
        public string NameKo = "";
    }
}
