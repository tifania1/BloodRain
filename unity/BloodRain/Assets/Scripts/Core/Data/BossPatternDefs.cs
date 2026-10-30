#nullable enable
// bosspatterns.json: 땅의 주인 패턴 시간표(기획서 6장 '땅의 주인 패턴 수치'). 시간표 계산은 Combat/BossPatterns.
// 길이는 화면 기준 m(축척 배율 1)이라 어느 경지에서 싸워도 그대로 쓴다.
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    /// <summary>보스 하나의 패턴 묶음.</summary>
    public sealed class BossPatternSetDef
    {
        /// <summary>싸움이 시작되고 첫 패턴까지(초).</summary>
        public float FirstCastSeconds;
        /// <summary>한 패턴이 시작된 시각에서 다음 패턴이 시작되는 시각까지(초).</summary>
        public float IntervalSeconds;
        /// <summary>2막이 된 뒤 시작하는 패턴부터 쓰는 간격(초).</summary>
        public float Phase2IntervalSeconds;
        /// <summary>남은 기혈 비율이 이 값 이하가 되면 2막. enemies.json phase2At 과 같아야 한다.</summary>
        public float Phase2At;
        /// <summary>몸에 닿을 때 피해(0분·혈운 농도 1 값).</summary>
        public float ContactDamage;
        /// <summary>이동속도. 플레이어 기본 이동속도 100 기준.</summary>
        public float MoveSpeed;
        /// <summary>차례 패턴 id. 이 순서로 돌아간다.</summary>
        public List<string> Order = new List<string>();
        /// <summary>차례 패턴(1막부터 쓰는 것).</summary>
        public List<BossPatternDef> Patterns = new List<BossPatternDef>();
        /// <summary>2막이 되면 켜져서 끝까지 이어지는 패턴.</summary>
        public List<BossPatternDef> Phase2 = new List<BossPatternDef>();

        public BossPatternDef Pattern(string id) =>
            Patterns.Concat(Phase2).FirstOrDefault(p => p.Id == id)
            ?? throw new KeyNotFoundException($"패턴이 없습니다: {id}");
    }

    /// <summary>패턴 하나. 모양(Shape)에 따라 쓰는 칸이 다르고, 안 쓰는 칸은 0이다.</summary>
    public sealed class BossPatternDef
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>cone(부채꼴), summon_ring(원 위에 소환), circles(동그라미 여러 곳), rotating_beams(도는 빛줄기).</summary>
        public string Shape = "";
        /// <summary>예고 시작에서 판정까지(초).</summary>
        public float TelegraphSeconds;
        /// <summary>부채꼴·원·동그라미의 반경(m).</summary>
        public float Radius;
        /// <summary>부채꼴 전체 각도(도). 반각은 이 값의 절반.</summary>
        public float AngleDegrees;
        /// <summary>동그라미·소환·빛줄기 수.</summary>
        public int Count;
        /// <summary>동그라미 자리를 고르는 범위. 플레이어 둘레 가로·세로 ±이 값(m).</summary>
        public float Scatter;
        /// <summary>빛줄기 길이(m).</summary>
        public float Length;
        /// <summary>빛줄기 폭(m). 판정은 가운데 줄에서 절반씩.</summary>
        public float Width;
        /// <summary>빛줄기가 도는 빠르기(도/초).</summary>
        public float TurnDegreesPerSecond;
        /// <summary>빛줄기 첫 줄이 2막 순간 플레이어 쪽에서 돌아가 있는 각도(도).</summary>
        public float StartOffsetDegrees;
        /// <summary>계속 닿아 있을 때 같은 대상이 다시 맞는 간격(초).</summary>
        public float HitIntervalSeconds;
        /// <summary>판정 피해(0분·혈운 농도 1 값). 0이면 피해 없는 패턴(소환 등).</summary>
        public float BaseDamage;
        /// <summary>player_at_telegraph: 예고를 시작할 때 플레이어 쪽(위치)을 잡는다.</summary>
        public string Aim = "";
        /// <summary>player_at_resolve: 판정 순간 플레이어 자리를 가운데로 쓴다.</summary>
        public string Center = "";
        /// <summary>예고 동안 보스가 멈춰 서는가.</summary>
        public bool BossHolds;
        /// <summary>소환하는 적 id(enemies.json).</summary>
        public string Summon = "";
        /// <summary>소환한 적의 이동속도(플레이어 100 기준). 0이면 그 적의 기본값.</summary>
        public float SummonSpeed;
        /// <summary>판정 뒤 그 자리에 남기는 기믹 id(gimmicks.json). 비어 있으면 없음.</summary>
        public string LeaveGimmick = "";
        /// <summary>남긴 기믹이 사라질 때까지(초).</summary>
        public float LeaveSeconds;
        /// <summary>남긴 기믹을 쓸 수 있는 횟수.</summary>
        public int LeaveUses;
        /// <summary>2막 패턴처럼 켜지면 끝까지 이어지는가.</summary>
        public bool Continuous;
    }

    sealed class BossPatternFile
    {
        public Dictionary<string, BossPatternSetDef> Bosses = new Dictionary<string, BossPatternSetDef>();
    }
}
