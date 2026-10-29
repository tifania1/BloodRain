#nullable enable
// gimmicks.json: 지역 기믹 수치(기획서 7장 '지형 기믹 수치'). 판정은 World/Gimmicks.
// 길이는 화면 기준 m(축척 배율 1). 세상에 놓을 때 그 기믹이 생긴 경지의 축척 배율을 곱한다.
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    /// <summary>기믹 하나의 공통 칸. Id 는 cheonha.json regions[].gimmicks 에 쓰는 이름.</summary>
    public abstract class GimmickDef
    {
        public string Id = "";
        public string NameKo = "";
    }

    public sealed class BellGimmickDef : GimmickDef
    {
        /// <summary>플레이어가 이만큼 다가가면 울린다(m).</summary>
        public float TouchRadius;
        /// <summary>기절시키는 반경(m).</summary>
        public float StunRadius;
        /// <summary>잡몹·정예 기절(초).</summary>
        public float StunSeconds;
        /// <summary>보스 기절(초).</summary>
        public float BossStunSeconds;
        /// <summary>한 번 울리고 다시 울릴 때까지(초).</summary>
        public float CooldownSeconds;
    }

    public sealed class PagodaForestGimmickDef : GimmickDef
    {
        /// <summary>돌탑 무더기 하나의 반경(m). 탑은 이 원 안에만 선다.</summary>
        public float PatchRadius;
        /// <summary>격자 간격(m).</summary>
        public float Spacing;
        /// <summary>돌탑 하나의 반경(m).</summary>
        public float PagodaRadius;
        /// <summary>격자 자리에서 가로·세로로 흔드는 폭(±m).</summary>
        public float Jitter;
        /// <summary>탑과 탑 가장자리 사이가 이보다 좁아지면 그 탑은 뺀다(m).</summary>
        public float MinGap;
        /// <summary>격자 자리를 비워 두는 비율.</summary>
        public float SkipChance;
        public bool BlocksFlying;
        public bool BlocksProjectiles;
    }

    public sealed class WellGimmickDef : GimmickDef
    {
        public float TouchRadius;
        /// <summary>회복량 = 최대 기혈 × 이 값.</summary>
        public float HealMaxHealthRatio;
        /// <summary>우물 하나를 쓸 수 있는 횟수.</summary>
        public int Uses = 1;
    }

    public sealed class PoisonFogGimmickDef : GimmickDef
    {
        /// <summary>한 생 시계로 처음 퍼지는 시각(초).</summary>
        public float FirstSpreadSeconds;
        /// <summary>퍼지는 주기(초).</summary>
        public float PeriodSeconds;
        public float SpreadSeconds;
        public float HoldSeconds;
        public float FadeSeconds;
        /// <summary>다 퍼졌을 때 반경(m).</summary>
        public float Radius;
        /// <summary>안에 있는 동안 초당 잃는 기혈 = 최대 기혈 × 이 값.</summary>
        public float HealthLossPerSecond;
        /// <summary>플레이어가 안에 있는 동안 얻는 진기 배율.</summary>
        public float QiMultiplier = 1;
        public bool BossImmune;

        /// <summary>한 번 퍼져서 다 걷힐 때까지(초).</summary>
        public float EpisodeSeconds => SpreadSeconds + HoldSeconds + FadeSeconds;
    }

    public sealed class WaterGimmickDef : GimmickDef
    {
        public float MoveSpeedMultiplier = 1;
        /// <summary>경공 뒤 수상비(초).</summary>
        public float WaterWalkSeconds;
        public bool FlyingIgnores;
    }

    public sealed class TaijiFloorGimmickDef : GimmickDef
    {
        public float TouchRadius;
        public float DurationSeconds;
        /// <summary>반대 속성 무공 피해에 더하는 비율(0.3 = +30%).</summary>
        public float OppositeAspectDamageBonus;
        public float CooldownSeconds;
    }

    public sealed class GimmicksDef
    {
        public BellGimmickDef Bell = new BellGimmickDef();
        public PagodaForestGimmickDef PagodaForest = new PagodaForestGimmickDef();
        public WellGimmickDef Well = new WellGimmickDef();
        public PoisonFogGimmickDef PoisonFog = new PoisonFogGimmickDef();
        public WaterGimmickDef Water = new WaterGimmickDef();
        public TaijiFloorGimmickDef TaijiFloor = new TaijiFloorGimmickDef();

        public IEnumerable<GimmickDef> All => new GimmickDef[] { Bell, PagodaForest, Well, PoisonFog, Water, TaijiFloor };

        /// <summary>cheonha.json 의 기믹 이름으로 찾는다. 아직 수치가 없는 기믹이면 null.</summary>
        public GimmickDef? Find(string id) => All.FirstOrDefault(g => g.Id == id);
    }
}
