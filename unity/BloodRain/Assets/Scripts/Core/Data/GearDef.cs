#nullable enable
// 문파 병기(gear.json, 기획서 8장 '문파 병기와 6단', docs/design/sinbyeong.md). 판정은 Build.Gear.
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public sealed class GearFileDef
    {
        public GearDropDef Drop = new GearDropDef();
        /// <summary>범병·양병·명병·신병 차례. 앞이 낮은 등급.</summary>
        public List<GearGradeDef> Grades = new List<GearGradeDef>();
        public List<GearDef> Gear = new List<GearDef>();
    }

    public sealed class GearDropDef
    {
        /// <summary>droppers 적 한 마리를 쓰러뜨렸을 때 떨어질 확률(0~1).</summary>
        public double Chance;
        /// <summary>기연 1마다 더하는 확률.</summary>
        public double LuckBonus;
    }

    public sealed class GearGradeDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        /// <summary>그 문파 무공 피해에 더하는 몫(0.05 = +5%).</summary>
        public double Damage;
        /// <summary>그 문파 무공 범위에 더하는 몫.</summary>
        public double Area;
        /// <summary>레벨업 때 그 문파 무공·심법 카드 뽑기 무게에 곱하는 값(1 이면 그대로).</summary>
        public double OfferWeight = 1;
        /// <summary>이 등급이면 대표 무공이 절기로 깨어 있을 때 6단(신병합일).</summary>
        public bool Sixth;
    }

    public sealed class GearDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Hanja = "";
        /// <summary>문파 id(cheonha sects, synergies schools). 문파가 없는 병기(진소운 무명 철검)는 null.</summary>
        public string? Sect;
        /// <summary>대표 무공 — 6단이 붙는 무공.</summary>
        public string Weapon = "";
        /// <summary>그 문파 땅에서 쓰러뜨리면 drop.chance 로 이 병기를 떨어뜨리는 적.</summary>
        public List<string> Droppers = new List<string>();
        /// <summary>그 문파 땅에서 쓰러뜨리면 확정으로 떨어뜨리는 정예.</summary>
        public List<string> Guaranteed = new List<string>();
        /// <summary>떨어지지 않고 따로 받는 병기의 출처("relic:&lt;지역&gt;" = 그 땅의 주인 첫 처치 때 유품으로). 보통 병기는 null.</summary>
        public string? Source;
    }
}
