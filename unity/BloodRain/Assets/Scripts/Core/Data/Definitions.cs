#nullable enable
// 데이터 정의. Assets/Resources/Data/*.json 을 그대로 담는다.
// 이 폴더(Core)는 UnityEngine을 쓰지 않는다. dotnet 테스트(core/)가 같은 파일을 컴파일한다.
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public enum YinYang { Neutral, Yang, Yin }

    public sealed class WeaponDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Category = "";
        public string Yinyang = "neutral";
        public string Behavior = "";
        public float Range;
        public float DamageLv1;
        public float DamageLv8;
        public float Cooldown;
        public string PairPassive = "";
        /// <summary>레벨업 풀에 들어오는 조건. "start", "boss:&lt;지역 id&gt;"(그 땅의 주인 첫 처치), 그 밖은 도전 과제 토큰.</summary>
        public string Unlock = "start";

        // Lv1 → Lv8 로 자라는 값(기획서 9장 'Lv8까지 성장' 칸). [Lv1 값, Lv8 값]. 그 무공에 없으면 null.
        // 읽는 쪽은 Combat.WeaponGrowth. 목록 기본값을 두지 않는다(Newtonsoft 가 기본값 뒤에 덧붙인다).
        /// <summary>한 번에 치는 횟수(나한권 연타, 취선봉 휘두르기, 이화창 찌르기).</summary>
        public List<float>? Hits;
        /// <summary>한 번에 나가는 개수(비도·폭탄·검·사슬·갈래·낙뢰·꽃잎).</summary>
        public List<float>? Projectiles;
        public List<float>? Pierce;
        /// <summary>섬광지 초당 발 수. 있으면 쿨다운 대신 1 / 이 값.</summary>
        public List<float>? ShotsPerSecond;
        public List<float>? BurnPerSecond;
        /// <summary>둔화 비율(0.15 = 15%).</summary>
        public List<float>? Slow;
        /// <summary>지속 초(태극검 공전).</summary>
        public List<float>? Duration;
        public List<float>? Lifesteal;
        public List<float>? StunChance;
        /// <summary>Lv8 반경(m). 독무처럼 반경이 '2→3.5m' 로 자라는 무공만.</summary>
        public float? RangeLv8;
        /// <summary>Lv8 범위·크기 배율(창천검기 범위 +40% = 1.4, 장풍은 장풍 크기). 없으면 1.</summary>
        public float? AreaLv8;
        /// <summary>그 레벨에 붙는 새 동작(레벨 → 이름). 기획서 '앞뒤 베기(Lv4)'.</summary>
        public Dictionary<string, string>? Milestones;

        // 기획서 9장 '날아가는 것과 넉백'. 그 무공에 없으면 null.
        /// <summary>날아가는 것(비도·장풍·지풍). 탄속은 연무장 탄속을 곱하기 전.</summary>
        public WeaponShot? Shot;
        /// <summary>맞은 적을 미는 세기. 적 넉백 저항·넉백 배율을 곱하기 전. 보스는 안 밀린다.</summary>
        public float? Knockback;
        /// <summary>벽력탄이 자리에 떨어지기까지 초.</summary>
        public float? FuseSeconds;
        /// <summary>벽력탄이 떨어질 자리를 고르는 거리(m). 이 안의 적이 없으면 몸 둘레에 떨어진다.</summary>
        public float? SeekRange;
        /// <summary>한 번 쏠 때 연달아 나가는 획·발 사이 초(삼재검법 3연 베기, 나한권 연타, 장풍 2연발, 취선봉 휘두르기).</summary>
        public float? GapSeconds;
        /// <summary>창천검기 검기가 한 바퀴 도는 초. 2회 회전이면 두 배 동안 돈다.</summary>
        public float? SweepSeconds;
        /// <summary>독무가 닿는 간격 초. 쿨다운 0(상시)인 무공이 한 번 닿을 때 초당 피해 x 이 값.</summary>
        public float? TickSeconds;
        /// <summary>벽력탄에 맞은 적이 타는 초. 연무장 '지속'을 곱하기 전. 초당 피해는 BurnPerSecond.</summary>
        public float? BurnSeconds;
        /// <summary>비도 중독(Lv6·절기)이 가는 초.</summary>
        public float? PoisonSeconds;
        /// <summary>중독 초당 피해 = 비도 한 방 x 이 값(0.25 = 25%).</summary>
        public float? PoisonDamage;

        public YinYang Aspect => YinYangParse.From(Yinyang);
        /// <summary>땅의 주인 첫 처치로 열리면 그 지역 id, 아니면 null.</summary>
        public string? UnlockBossRegion => UnlockToken.BossRegion(Unlock);
    }

    public sealed class WeaponShot
    {
        /// <summary>초속(m).</summary>
        public float Speed;
        /// <summary>맞는 반경(m). 장풍은 Lv8까지 areaLv8 배로 커진다.</summary>
        public float Radius;
        public float LifeSeconds;
        /// <summary>과녁 쪽으로 꺾는 세기(초당). 0이면 곧게 간다.</summary>
        public float Homing;
        /// <summary>날아가는 거리 = 초속 x 수명.</summary>
        public float Reach => Speed * LifeSeconds;
    }

    public sealed class PassiveEffect
    {
        public string Stat = "";
        public float PerLevel;
        public Dictionary<string, float>? AtLevels;
    }

    public sealed class PassiveDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Yinyang = "neutral";
        public int? YinyangPerLevelOverride;
        public List<PassiveEffect> Effects = new List<PassiveEffect>();
        /// <summary>Lv5에 덧붙는 말(넉백 면역, 독 면역, 경공 충전 +1). 기획서 9장 심법 표 '최대' 칸에 그대로 들어 있다.</summary>
        public string? MaxBonus;
        public string PairWeapon = "";

        public YinYang Aspect => YinYangParse.From(Yinyang);
    }

    public sealed class EvolutionDef
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>각성 연출에 크게 쓰는 한자 이름(글자는 이미지에 굽지 않고 Unity에서 얹는다).</summary>
        public string Hanja = "";
        public string Weapon = "";
        public string Passive = "";
        public string Summary = "";
        /// <summary>절기 수치(rangeMultiplier, bounces, chain …). 웹 프로토타입 값을 옮긴 것이고 아직 아홉 절기만 있다. 없으면 null.</summary>
        public Dictionary<string, float>? Params;
        /// <summary>params 값. 없으면 예외(데이터 빠짐을 바로 알게).</summary>
        public float Param(string key) => Params != null && Params.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException($"{Id} params 에 {key} 가 없습니다");
    }

    public sealed class CharacterDef
    {
        public string Id = "";
        public string NameKo = "";
        public string StartWeapon = "";
        public float MaxHealth = 100;
        public float MoveSpeed = 1;
        public int YinyangStart;
        public int? HarmonyRange;
        public string Ultimate = "";
        public string Trait = "";
        /// <summary>이 몸으로 태어날 수 있게 되는 조건. cheonha.json sects[].unlock 과 같다("start" 또는 "boss:&lt;지역 id&gt;").</summary>
        public string Unlock = "start";

        public string? UnlockBossRegion => UnlockToken.BossRegion(Unlock);
    }

    /// <summary>해금 토큰 읽기. "boss:&lt;지역 id&gt;" 는 그 지역 땅의 주인 첫 처치.</summary>
    public static class UnlockToken
    {
        public const string Start = "start";
        public const string BossPrefix = "boss:";

        public static string? BossRegion(string token) =>
            token.StartsWith(BossPrefix, System.StringComparison.Ordinal) ? token.Substring(BossPrefix.Length) : null;
    }

    public sealed class UltimateDef
    {
        public string Id = "";
        public string NameKo = "";
        public float MightMultiplier;
        public string Summary = "";
    }

    public sealed class EnemyDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Family = "";
        public float Health;
        public float Speed;
        public float ContactDamage;
        public string Behavior = "";
        public float KnockbackResist;
        /// <summary>지역 고유 적이면 그 지역의 장 이름(cheonha.json regions[].chapter: chapter1 …). 공통 적은 null.
        /// 공통 적이 처음 나오는 시각은 chapters.json 흐름의 가중치표가 정한다.</summary>
        public string? Chapter;

        // 행동에 딸린 값(기획서 6장 '행동' 칸). 그 적에게 없으면 기본값.
        /// <summary>예고 초(혈무사 창 찌르기 0.6, 혈영자객 표식 뒤 순간이동 1.0).</summary>
        public float Telegraph;
        /// <summary>쏘는 탄 한 발 피해(혈궁수 화살). 접촉 피해와 따로.</summary>
        public float ProjectileDamage;
        /// <summary>발밑 장판 초당 피해(혈술사 피 웅덩이).</summary>
        public float ZoneDamagePerSecond;
        /// <summary>둘레 적 강화 반경(m)과 이동·피해 배율 덧셈(기수 5m, +0.2).</summary>
        public float AuraRadius;
        public float AuraBonus;
        /// <summary>지형을 무시하고 날아다닌다(비강).</summary>
        public bool IgnoresTerrain;
        /// <summary>물 구역에서만 솟아난다(수귀).</summary>
        public bool OnlyInWater;
        /// <summary>짝이 쓰러지면 남은 쪽이 이 초 뒤 되살린다(흑백 쌍강시). 0 이면 없음.</summary>
        public float ReviveSeconds;
        /// <summary>닿으면 이 초 동안 둔화(빙강시). 0 이면 없음.</summary>
        public float OnHitSlowSeconds;
        /// <summary>둔화 동안 이동속도 배율(빙강시 0.65). 다시 닿으면 시간만 처음부터. 0 이면 1로 본다.</summary>
        public float OnHitSlowMultiplier;
        /// <summary>쓰러질 때 남기는 것(splinter_burst 나뭇조각, poison_pool 독 장판).</summary>
        public string? OnDeath;
        /// <summary>잡으면 주는 것(금두꺼비).</summary>
        public EnemyReward? Reward;
        /// <summary>나온 뒤 이 초가 지나면 사라진다(혈인 돌진 14, 금두꺼비 도망 25). 0 이면 끝까지 남는다.</summary>
        public float LifeSeconds;
        /// <summary>예고 뒤 돌진(혈무사 telegraph_dash). 없으면 null.</summary>
        public EnemyDash? Dash;
        /// <summary>거리를 두고 쏜다(혈궁수 kite_shoot). 없으면 null.</summary>
        public EnemyShoot? Shoot;
        /// <summary>표식 뒤 순간이동(혈영자객 mark_teleport). 예고 초는 Telegraph. 없으면 null.</summary>
        public EnemyMark? Mark;
        /// <summary>발밑 장판(혈술사 blood_pool). 초당 피해는 ZoneDamagePerSecond. 없으면 null.</summary>
        public EnemyPool? Pool;
        /// <summary>곡선 비행(비강 fly_curve): 플레이어 쪽으로 가며 옆으로 Amplitude m 만큼 Period 초 주기로 흔들린다. 없으면 null.</summary>
        public EnemyCurve? Curve;
        /// <summary>OnDeath 의 크기·피해(splinter_burst·poison_pool). 없으면 null.</summary>
        public EnemyDeath? Death;
    }

    public sealed class EnemyCurve
    {
        public float Amplitude;
        public float Period;
    }

    /// <summary>쓰러질 때 남기는 것. splinter_burst: 반경 Radius m 안 다른 적에게 EnemyDamage, 플레이어에게 PlayerDamage 한 번.
    /// poison_pool: 그 자리에 반경 Radius m 독 장판을 Seconds 초, 플레이어가 밟으면 초당 PerSecond(독 장판끼리 겹쳐도 한 번).</summary>
    public sealed class EnemyDeath
    {
        public float Radius;
        public float EnemyDamage;
        public float PlayerDamage;
        public float Seconds;
        public float PerSecond;
    }

    /// <summary>kite_shoot: 플레이어와 [KeepMin, KeepMax] m 를 지키며(가까우면 물러나고 멀면 다가옴) Interval 초마다
    /// 플레이어 쪽으로 한 발. 탄은 초속 Speed m, Range m 날아가면 사라지고 반경 Radius m. 피해는 ProjectileDamage.
    /// 2026-11-02 클라우드 세션이 정함(기획서 6장 표 아래).</summary>
    public sealed class EnemyShoot
    {
        public float KeepMin;
        public float KeepMax;
        public float Interval;
        public float Speed;
        public float Range;
        public float Radius;
    }

    /// <summary>mark_teleport: 플레이어 발밑에 반경 Radius m 붉은 표식을 찍고 Telegraph 초 뒤 그 자리로 순간이동해
    /// 표식 안에 있으면 접촉 피해. 표식부터 다음 표식까지 Interval 초. 처음 나타나면 바로 첫 표식.</summary>
    public sealed class EnemyMark
    {
        public float Radius;
        public float Interval;
    }

    /// <summary>blood_pool: Interval 초마다 플레이어 발밑에 반경 Radius m 피 웅덩이를 깔고 Seconds 초 남긴다.
    /// 한 혈술사가 깔아 둔 웅덩이는 Max 개까지(넘으면 오래된 것부터 지움). 겹쳐 밟아도 초당 피해는 한 번.</summary>
    public sealed class EnemyPool
    {
        public float Radius;
        public float Seconds;
        public float Interval;
        public int Max;
    }

    /// <summary>telegraph_dash 의 돌진(기획서 6장 '공통 적' 표 아래). 거리·속도는 경지 m, m/s.</summary>
    public sealed class EnemyDash
    {
        /// <summary>플레이어가 이 거리 안에 들면 예고(Telegraph 초)를 시작한다.</summary>
        public float Range;
        /// <summary>돌진하는 초와 그동안의 속도(적 속도와 따로).</summary>
        public float Seconds;
        public float Speed;
        /// <summary>돌진 뒤 다시 쫓아오며 쉬는 초 [RestMin, RestMax] 사이 무작위.</summary>
        public float RestMin;
        public float RestMax;
        public float Reach => Seconds * Speed;
    }

    public sealed class EnemyReward
    {
        public int Silver;
        /// <summary>진기 구슬을 한꺼번에 쏟는다.</summary>
        public bool OrbBurst;
        /// <summary>쏟는 구슬 수와 한 알 진기(웹 프로토타입 12알 × 5). 둘레 1.5m 안에 흩뿌린다.</summary>
        public int Orbs;
        public float OrbXp;
    }

    public sealed class BossDef
    {
        public string Id = "";
        public string NameKo = "";
        public string Chapter = "";
        public float Health;
        public float TargetKillSeconds;
        public List<string> Patterns = new List<string>();
    }

    /// <summary>돌파 깨달음 한 장(insights.json). 효과 수치는 Params에 이름을 붙여 둔다.</summary>
    public sealed class InsightDef
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>한자 이름. 한글 이름과 글자 수가 같다.</summary>
        public string Hanja = "";
        /// <summary>카드 아이콘에 크게 쓰는 한 글자. Hanja 안의 글자다.</summary>
        public string Glyph = "";
        public string TextKo = "";
        /// <summary>효과 종류. 실행하는 쪽(Unity)이 이 값으로 갈라 처리한다.</summary>
        public string Effect = "";
        public Dictionary<string, float> Params = new Dictionary<string, float>();
        /// <summary>효과가 걸리는 무공 behavior(이기어검처럼 일부 무공에만 걸릴 때). 비어 있으면 해당 없음.</summary>
        public List<string> WeaponBehaviors = new List<string>();

        public float Param(string key)
        {
            if (!Params.TryGetValue(key, out var v))
                throw new KeyNotFoundException($"깨달음 {Id} 에 {key} 값이 없습니다");
            return v;
        }
    }

    /// <summary>돌파 때 보여 주는 깨달음 수. 특성(trait) 이름으로 따로 정한 캐릭터만 다르다.</summary>
    public sealed class InsightChoicesDef
    {
        public int Base = 3;
        public Dictionary<string, int> ByTrait = new Dictionary<string, int>();
        /// <summary>balance.json realms reward 안에서 깨달음 선택을 뜻하는 말.</summary>
        public string RealmReward = "insight_choice";
    }

    public static class YinYangParse
    {
        public static YinYang From(string s)
        {
            switch (s)
            {
                case "yang": return YinYang.Yang;
                case "yin": return YinYang.Yin;
                default: return YinYang.Neutral;
            }
        }
    }
}
