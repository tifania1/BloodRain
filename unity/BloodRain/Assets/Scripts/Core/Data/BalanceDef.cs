#nullable enable
// balance.json 구조. 값은 기획서 '11. 밸런스 기본값'과 맞춘다.
using System.Collections.Generic;

namespace BloodRain.Core.Data
{
    public sealed class XpSegment
    {
        public int FromLevel;
        public int ToLevel;
        public int Base;
        public int Step;
        public int Anchor;
    }

    public sealed class XpCurveDef
    {
        public List<XpSegment> Segments = new List<XpSegment>();
    }

    public sealed class RealmDef
    {
        public string Id = "";
        public string NameKo = "";
        public int Level;
        public string? Reward;
        /// <summary>이 경지가 열리는 모드. null 이면 모든 생. 천하 구조에선 현경도 모든 생에서 열려 지금은 쓰는 경지가 없다.</summary>
        public List<string>? OnlyIn;
    }

    public sealed class SlotsDef
    {
        public int Weapons = 6;
        public int Passives = 6;
        public string ExtraSlotAtRealm = "chojeoljeong";
    }

    public sealed class LevelsDef
    {
        public int WeaponMax = 8;
        public int PassiveMax = 5;
    }

    public sealed class LevelUpDef
    {
        public int BaseChoices = 3;
        public int MaxChoices = 4;
        public float LuckForFourthChoice = 50;
        /// <summary>뽑힌 선택지가 모두 새 무공이면 은자·음식 카드를 한 장 더 붙인다(무명검처럼 무공 하나로 버티는 길, S-07 · 기획서 8장).</summary>
        public bool FallbackWhenAllNewWeapons;
        /// <summary>그 덧붙는 카드 한 장의 몫: 은자와 기혈 회복을 함께 준다(웹 프로토타입 50·30). 회복은 음식 배율을 곱하지 않는다.</summary>
        public FallbackCardDef FallbackCard = new FallbackCardDef();
        /// <summary>캐릭터 특성 → 한 판 다시 고르기 몫(진소운 reroll_plus2_insight4 는 2). 연무장 몫과 더한다.</summary>
        public Dictionary<string, int> TraitRerolls = new Dictionary<string, int>();
        /// <summary>레벨업 화면이 열린 직후 입력을 받지 않는 초(기획서 12장 '레벨업 입력', 프로토타입 값 0.35). 싸우던 손이 카드를 잘못 누르지 않게.</summary>
        public float InputGuardSeconds = 0.35f;
    }

    public sealed class FallbackCardDef
    {
        public int Silver = 50;
        public float Heal = 30;
    }

    /// <summary>보갑(정예 확정 드롭) 강화 개수. 가중치 i 번째가 강화 i+1 개(기획서 8장 보상 표).</summary>
    public sealed class ChestDef
    {
        /// <summary>기본값은 비워 둔다(Newtonsoft 가 기본 목록 뒤에 덧붙이므로). 비면 늘 1개.</summary>
        public List<float> UpgradeCountWeights = new List<float>();
    }

    /// <summary>절기 규칙. 경지마다 담을 수 있는 절기 수(그릇)가 정해져 있고, 문턱에 선 무공만 깨어난다.</summary>
    public sealed class EvolutionRuleDef
    {
        /// <summary>경지 id → 그 경지까지 담을 수 있는 절기 수(누적). 없는 경지는 0.</summary>
        public Dictionary<string, int> Vessels = new Dictionary<string, int>();
        /// <summary>문턱 조건: 짝 심법 최소 레벨(무공은 최고 단계).</summary>
        public int PairPassiveMinLevel = 3;
        /// <summary>절기가 된 무공의 피해 배율(웹 프로토타입 값, 모든 절기에 같게). 기획서 9장은 천지인 삼검에만 '피해 2배'라고 적어 backlog E-02.</summary>
        public float DamageMultiplier = 2f;
        /// <summary>깨어나는 순간: breakthrough(그릇이 늘어나는 돌파), chest(보갑). 기본값은 비워 둔다. Newtonsoft 는 이미 든 목록 뒤에 JSON 값을 덧붙여 같은 낱말이 두 번 들어간다(DataDefaultsTests).</summary>
        public List<string> Triggers = new List<string>();
    }

    public sealed class YinYangDef
    {
        public int Min = -100;
        public int Max = 100;
        public int WeaponPerLevel = 5;
        public int PassivePerLevel = 8;
        public int HarmonyRange = 25;
        public int ExtremeFrom = 70;
        public YinYangHarmonyDef Harmony = new YinYangHarmonyDef();
        public YinYangBiasDef Bias = new YinYangBiasDef();
        public YinYangExtremeDef Extreme = new YinYangExtremeDef();
    }

    /// <summary>태극(조화) 구간 보너스.</summary>
    public sealed class YinYangHarmonyDef
    {
        public float CooldownReduction = 0.10f;
        public float UltimateChargeBonus = 0.25f;
    }

    /// <summary>편향 구간: 기운 쪽 속성 무공 피해 보너스.</summary>
    public sealed class YinYangBiasDef
    {
        public float AspectDamageBonus = 0.15f;
    }

    /// <summary>극성 구간: 피해 보너스가 커지는 대신 기혈역류가 온다(Combat.Backlash).</summary>
    public sealed class YinYangExtremeDef
    {
        public float AspectDamageBonus = 0.40f;
        /// <summary>극성에 머문 시간이 이만큼 쌓일 때마다 역류가 온다(초).</summary>
        public float BacklashIntervalSeconds = 60;
        /// <summary>역류가 이어지는 시간(초).</summary>
        public float BacklashSeconds = 3;
        /// <summary>역류 동안 이동속도에서 빼는 비율. 0.5면 -50%.</summary>
        public float BacklashSlow = 0.5f;
        /// <summary>끝까지 버텼을 때 받는 피해(최대 기혈 대비).</summary>
        public float BacklashDamageMaxHealthRatio = 0.05f;
        /// <summary>역류가 시작된 뒤 이 시간 안에 경공을 쓰면 끊긴다(초).</summary>
        public float DashCancelWindowSeconds = 1.0f;
        /// <summary>끊었을 때 오의 게이지에 그대로 더하는 양(배율 없음).</summary>
        public float DashCancelUltimateCharge = 50;
    }

    /// <summary>각성 연출 한 벌(긴 것 / 짧은 것)의 장면 시각. 연출 시작부터 실제 시간(초), 그 연출에 없는 장면은 null.</summary>
    public sealed class AwakeningTimelineDef
    {
        public float DurationSeconds;
        /// <summary>문턱: 무공 칸 글자가 날아와 몸에 스민다.</summary>
        public float? GlyphSeconds;
        /// <summary>균열: 금빛 균열이 위아래로 화면을 가른다.</summary>
        public float? CrackSeconds;
        /// <summary>이름: 한자 이름 붓질이 시작된다.</summary>
        public float NameSeconds;
        /// <summary>낙관: 絶 낙관이 찍히고 무공 칸이 절기로 바뀐다.</summary>
        public float StampSeconds;
        /// <summary>파동: 판정(잡몹 처치, 정예·보스 깎기, 적 탄 지우기)이 이 순간 일어난다.</summary>
        public float ShockSeconds;
    }

    /// <summary>절기 각성(Combat.Awakening). 기획서 8장 '절기 그릇과 각성'의 각성 연출 표, docs/design/vfx.md.</summary>
    public sealed class AwakeningDef
    {
        public AwakeningTimelineDef Long = new AwakeningTimelineDef
        {
            DurationSeconds = 3.5f, GlyphSeconds = 0.45f, CrackSeconds = 0.95f, NameSeconds = 1.15f, StampSeconds = 1.8f, ShockSeconds = 2.3f,
        };
        public AwakeningTimelineDef Short = new AwakeningTimelineDef
        {
            DurationSeconds = 1.5f, NameSeconds = 0.1f, StampSeconds = 0.42f, ShockSeconds = 0.64f,
        };
        /// <summary>파동 전까지 세상 시간 배율.</summary>
        public float WorldTimeScale = 0.04f;
        /// <summary>연출 시작부터 이 시간 동안 1 → WorldTimeScale 로 느려진다(실제 시간).</summary>
        public float TimeScaleInSeconds = 0.18f;
        /// <summary>파동 뒤 이 시간 동안 WorldTimeScale → 1 로 돌아온다(실제 시간).</summary>
        public float TimeScaleOutSeconds = 0.25f;
        /// <summary>파동 반경 = 화면 대각선(월드 단위) × 이 값.</summary>
        public float ShockRadiusViewDiagonal = 0.62f;
        public float EliteMaxHealthRatio = 0.15f;
        public float BossMaxHealthRatio = 0.04f;
        /// <summary>정예를 밀어내는 힘(프로토타입 단위). 보스는 밀리지 않는다.</summary>
        public float EliteKnockback = 8f;
        public bool ClearEnemyProjectiles = true;
        /// <summary>파동 때 깨어난 절기의 재사용 대기를 0으로.</summary>
        public bool ResetCooldownOnShock = true;
        /// <summary>파동 뒤 적이 새로 몰려오지 않는 시간(세상 시간).</summary>
        public float SpawnPauseSeconds = 2.5f;
        /// <summary>파동 뒤 금빛 여운 시간(세상 시간).</summary>
        public float AfterglowSeconds = 8f;
        /// <summary>여운 동안 그 절기 피해 배율.</summary>
        public float AfterglowDamageMultiplier = 1.5f;
        /// <summary>무적 = 연출 길이 + 이 값. 시작할 때 걸고 세상 시간으로 줄어든다(프로토타입과 같다).</summary>
        public float InvulnerableExtraSeconds = 0.8f;
    }

    public sealed class EnemyScalingDef
    {
        public float HealthPerMinute = 0.08f;
        public float DamagePerMinute = 0.03f;
        public Dictionary<string, float> ChapterMultiplier = new Dictionary<string, float>();
    }

    /// <summary>화면 위 목표 적 수. 생성을 멈추는 구간은 흐름(chapters.json)마다 따로 정한다.</summary>
    public sealed class SpawnTargetDef
    {
        /// <summary>[분, 마리]. 마지막 점 뒤로는 마지막 값.</summary>
        public List<float[]> Points = new List<float[]>();
        public float SpawnFractionPerSecond = 0.3f;
        /// <summary>생성 고리(화면 대각선 배수). 화면 기준이라 축척이 바뀌어도 그대로다.</summary>
        public float RingMinDiagonal = 0.6f;
        public float RingMaxDiagonal = 0.75f;
        /// <summary>플레이어에게서 이만큼(대각선 배수) 넘게 멀어진 적은 진행 방향 반대편으로 옮긴다.</summary>
        public float RecycleBeyondDiagonal = 1.5f;
        /// <summary>옮긴 적을 두는 거리(대각선 배수). 생성 고리 안쪽 끝에 가깝게 둬서 바로 화면에 들어오게 한다.</summary>
        public float RecycleToDiagonal = 0.65f;
        /// <summary>정예가 나타나는 거리(대각선 배수, 화면 바로 바깥).</summary>
        public float EliteDiagonal = 0.62f;
        /// <summary>금두꺼비는 생성 고리 자리에 이 배율을 곱한 거리(화면 안)에 나타난다. 보여야 쫓아간다.</summary>
        public float ToadRingScale = 0.55f;
    }

    public sealed class ContactDef
    {
        public float SameEnemyIntervalSeconds = 0.35f;
        public float MinDamage = 1;
        /// <summary>기혈이 이 비율 이하면 화면 가장자리 먹물 번짐·심장 소리(8장 '피격과 사망' 30%). 판 위 체력바도 이 아래에서 붉게.</summary>
        public float LowHealthRatio = 0.3f;

        /// <summary>위험 표시를 켤까(기혈 비율이 LowHealthRatio 이하, 0 보다 큼).</summary>
        public bool IsLowHealth(float healthRatio) => healthRatio > 0f && healthRatio <= LowHealthRatio;
    }

    /// <summary>경공 대시(기획서 8장). 움직임은 Unity 쪽이고 Core 는 숫자만 들고 있다.</summary>
    public sealed class DashDef
    {
        public float Distance = 4f;
        public float Duration = 0.25f;
        /// <summary>무적 시간(초).</summary>
        public float Iframes = 0.2f;
        public int Charges = 2;
        public float RechargeSeconds = 4f;
    }

    public sealed class UltimateChargeDef
    {
        public float Max = 100;
        public float PerKill = 0.4f;
        public float PerHit = 3;
        public float PerElite = 15;
        /// <summary>레벨업 화면 '정진(건너뛰기)'으로 얻는 양. 배율을 받지 않는다.</summary>
        public float PerSkip = 20;
    }

    public sealed class StatsDef
    {
        /// <summary>신법 100%일 때 걸음(지금 경지 축척의 m/s). 적 속도(기획서 9장, 플레이어 = 100)도 이 값에 곱한다.</summary>
        public float MoveBase = 4.6f;
        public float MagnetBase = 1.5f;
        public float CooldownReductionCap = 0.6f;
        /// <summary>치명타 피해 배율. 기획서 9장 천리안 '치명 피해 2배'. 시뮬레이터는 (1 + 치명 확률)로 2배를 가정한다.</summary>
        public float CritDamageMultiplier = 2f;
        /// <summary>플레이어 몸 반지름(경지 m). 적 몸과 이만큼 겹치면 접촉 피해, 보스 패턴 판정도 이 원으로 본다.</summary>
        public float PlayerBodyRadius = 0.4f;
    }

    /// <summary>연무장 한 항목. Stat은 Core.Build.StatKind 이름.</summary>
    public sealed class TrainingItemDef
    {
        public string Stat = "";
        public float PerLevel;
        public int MaxLevel = 1;
        public int BaseCost;
        /// <summary>해금으로 늘어나는 최대 레벨(기획서 10장 해금 표 '금두꺼비 누적 10마리 → 탐욕 최대 레벨 +2'). 없으면 null.</summary>
        public TrainingBonusMaxDef? BonusMax;
    }

    /// <summary>모든 생에서 잡은 금두꺼비가 GoldenToadsTotal 마리 이상이면 그 항목 최대 레벨 + Levels.</summary>
    public sealed class TrainingBonusMaxDef
    {
        public int GoldenToadsTotal;
        public int Levels;
    }

    public sealed class TrainingDef
    {
        public float CostGrowthPerBought = 0.1f;
        public List<TrainingItemDef> Items = new List<TrainingItemDef>();
    }

    /// <summary>한 화면에 두는 수의 상한(balance.json limits).</summary>
    public sealed class LimitsDef
    {
        public int EnemiesPc = 600;
        public int EnemiesMobile = 400;
        /// <summary>Steam Deck(15장 '성능 목표' 1280x800 에서 적 400). 리눅스 PC 로 잡히니 따로 둔다(SD-01).</summary>
        public int EnemiesSteamDeck = 400;
        public int Projectiles = 1500;
        /// <summary>바닥 진기 구슬. 넘으면 오래된 것부터 하나로 합친다.</summary>
        public int Orbs = 400;
        public int DamageNumbers = 80;

        /// <summary>동시 적 수 상한(11장 '성능 상한': PC 600, Steam Deck·모바일 400).</summary>
        public int EnemyCap(PlatformTier tier) => tier switch
        {
            PlatformTier.Mobile => EnemiesMobile,
            PlatformTier.SteamDeck => EnemiesSteamDeck,
            _ => EnemiesPc,
        };

        public int EnemyCap(bool mobile) => EnemyCap(mobile ? PlatformTier.Mobile : PlatformTier.Pc);

        /// <summary>
        /// 적 하나를 더 세워도 되나. 상한에 닿으면 생성 고리의 보통 적·사건 무리만 건너뛴다.
        /// 정예·땅의 주인·금두꺼비·보부상처럼 판의 흐름이 걸린 것(important)은 상한을 넘어도 세운다 — 하나뿐이고 놓치면 그 생의 사건이 사라진다.
        /// </summary>
        public bool CanSpawn(int alive, PlatformTier tier, bool important = false) => important || alive < EnemyCap(tier);

        public bool CanSpawn(int alive, bool mobile, bool important = false) => CanSpawn(alive, mobile ? PlatformTier.Mobile : PlatformTier.Pc, important);
    }

    /// <summary>성능 상한을 고르는 기기 갈래(15장 '성능 목표' 표의 세 줄). Unity 가 기기를 보고 고른다(unity_tasks SD-01U).</summary>
    public enum PlatformTier { Pc, SteamDeck, Mobile }

    /// <summary>진기 구슬 크기별 값(balance.json orbs).</summary>
    public sealed class OrbValuesDef
    {
        public float Small = 1;
        public float Medium = 5;
        public float Large = 25;
    }

    /// <summary>피해 숫자 띄우기(balance.json damageNumber, 12장 설정 '피해 숫자 표시'). 길이는 경지 m.</summary>
    public sealed class DamageNumberDef
    {
        /// <summary>치명타가 아니면 이 피해 이상일 때만 띄울 수 있다.</summary>
        public float MinDamage = 8;
        /// <summary>보통 적에 띄울 확률(치명타 아님).</summary>
        public float Chance = 0.18f;
        /// <summary>정예·땅의 주인에 띄울 확률(치명타 아님).</summary>
        public float ChanceEliteBoss = 0.5f;
        /// <summary>떠 있는 시간. 이 동안 떠오르며 옅어진다.</summary>
        public float LifeSeconds = 0.7f;
        /// <summary>가로로 흔드는 폭(±).</summary>
        public float JitterMeters = 0.2f;
        /// <summary>적 몸 반지름의 이 배만큼 위에 띄운다.</summary>
        public float HeadOffsetRadii = 2.4f;
    }

    /// <summary>진기 구슬 끌기·흡수·합치기(balance.json orbPickup, 기획서 8장 '드롭' 아래). 길이는 경지 m, 속도는 경지 m/s.</summary>
    public sealed class OrbPickupDef
    {
        /// <summary>플레이어에게 이만큼 다가오면 흡수.</summary>
        public float AbsorbRadius = 0.5f;
        /// <summary>끌리는 속도 = PullSpeedBase + PullSpeedPerSecond x 구슬 나이(초).</summary>
        public float PullSpeedBase = 9;
        public float PullSpeedPerSecond = 4;
        /// <summary>limits.orbs 를 넘으면 오래된 구슬 이만큼을 하나로 합친다.</summary>
        public int MergeBatch = 100;
        /// <summary>합친 큰 구슬을 두는 플레이어 둘레 거리.</summary>
        public float MergeDistance = 5;
    }

    /// <summary>등롱 한 칸(balance.json drops.lantern).</summary>
    public sealed class LanternDropDef
    {
        public string Kind = "";
        public float Weight;
    }

    /// <summary>음식·은자·혈정·등롱(balance.json drops, 기획서 8장 '드롭').</summary>
    public sealed class DropsDef
    {
        /// <summary>만두 회복(FoodHeal 배율 전).</summary>
        public float BunHeal = 20;
        /// <summary>오리구이 회복(FoodHeal 배율 전).</summary>
        public float DuckHeal = 50;
        /// <summary>은자 주머니 [최소, 최대].</summary>
        public List<int> SilverBag = new List<int>();
        /// <summary>잡몹 하나가 쓰러질 때 은자 주머니를 떨굴 확률.</summary>
        public float EnemySilverChance;
        /// <summary>정예 하나가 주는 혈정(땅의 주인 몫은 cheonha.json bosses).</summary>
        public int EliteBloodCrystal = 1;
        /// <summary>정신부가 적을 멈추는 초(곤륜 진전 '정지 시간 2배' 같은 배율을 곱하기 전). 기획서 7장 부적 드롭.</summary>
        public float JeongsinFreezeSeconds = 5;
        /// <summary>등롱이 서는 간격·자리·깨지는 판정(Drops.LanternDue 등). 없으면 웹 프로토타입 값.</summary>
        public LanternSpawnDef LanternSpawn = new LanternSpawnDef();
        public List<LanternDropDef> Lantern = new List<LanternDropDef>();
    }

    /// <summary>등롱 서는 규칙(balance.json drops.lanternSpawn, 기획서 8장 '드롭'). 길이는 경지 m, 초는 세상 시간. 기본값은 웹 프로토타입 값.</summary>
    public sealed class LanternSpawnDef
    {
        /// <summary>새 등롱이 서는 간격(초).</summary>
        public float IntervalSeconds = 33f;
        /// <summary>서 있는 등롱이 이 수 아래일 때만 새로 선다.</summary>
        public int MaxAlive = 4;
        /// <summary>플레이어 둘레 고리 안쪽 반경(m).</summary>
        public float RingMin = 5f;
        /// <summary>플레이어 둘레 고리 바깥쪽 반경(m).</summary>
        public float RingMax = 9f;
        /// <summary>무공이 친 원 반경에 더하는 등롱 몸 반경(m).</summary>
        public float HitPadding = 0.4f;
        /// <summary>떨어진 것을 줍는 거리(m).</summary>
        public float PickupRadius = 0.8f;
        /// <summary>은빛 등롱은 화면 대각선 × 이 값 거리에 선다.</summary>
        public float SilverViewRatio = 0.28f;
    }

    /// <summary>배경음 바꾸기(balance.json bgm, 기획서 14장 '곡 바꾸기').</summary>
    public sealed class BgmRulesDef
    {
        /// <summary>지역 곡끼리는 새 지역에 이 초를 넘게 서 있어야 바꾼다(경계를 넘나들 때 곡이 왔다 갔다 하지 않게).</summary>
        public float RegionHoldSeconds = 4;
        /// <summary>지역 곡끼리 섞어 넘기는 초.</summary>
        public float RegionFadeSeconds = 3;
        /// <summary>땅의 주인 싸움·종장 단계·화면 전환에 섞어 넘기는 초.</summary>
        public float QuickFadeSeconds = 1;
        /// <summary>한 생 이 초부터 지역 곡을 격해진 벌(_intense)로(BGM-02b 600). 0 이면 안 바꾼다.</summary>
        public float IntenseAfterSeconds;
        /// <summary>기본 → 격해진 벌로 섞어 넘기는 초(4).</summary>
        public float IntenseFadeSeconds = 4;
        /// <summary>격해진 벌이 있는 지역 id(먼저 숭산 하나로 시험).</summary>
        public List<string> IntenseRegions = new List<string>();
    }

    /// <summary>효과음 동시 재생 제한과 연속 흡수 음계(balance.json sfx, 기획서 14장 '동시 재생 제한').</summary>
    public sealed class SfxRulesDef
    {
        /// <summary>같은 효과음을 세는 창(초).</summary>
        public float SameSoundWindowSeconds = 0.05f;
        /// <summary>그 창 안에 같은 효과음을 몇 개까지 울리나.</summary>
        public int SameSoundMaxInWindow = 4;
        /// <summary>앞 진기 흡수음에서 이 초 안이면 음계를 하나 올린다(웹 ORB_RUN_GAP).</summary>
        public float AbsorbRunGapSeconds = 0.35f;
        /// <summary>흡수음 음 수(구운 풍경 sfx_xp_absorb_n1~n8).</summary>
        public int AbsorbNotes = 8;
        /// <summary>기혈 위험(ContactDef.IsLowHealth)인 동안 심장 소리 sfx_low_health_heart 를 되풀이하는 간격(초, 8장 '피격과 사망'). 0 이면 안 튼다.</summary>
        public float LowHealthHeartSeconds = 1f;
    }

    /// <summary>보부상 품목 하나(balance.json merchant.items).</summary>
    public sealed class MerchantItemDef
    {
        public string Id = "";
        public string NameKo = "";
        /// <summary>은자 값.</summary>
        public int Price;
        /// <summary>회복약: 최대 기혈의 이 비율만큼 회복. 0 이면 회복 없음.</summary>
        public float HealFraction;
        /// <summary>다시 고르기 권: 그 판 다시 고르기 횟수에 더하는 수.</summary>
        public int Rerolls;
    }

    /// <summary>보부상(balance.json merchant, 기획서 7장 '보부상').</summary>
    public sealed class MerchantDef
    {
        /// <summary>한 번 들를 때 품목마다 살 수 있는 수.</summary>
        public int MaxPerVisit = 2;
        public List<MerchantItemDef> Items = new List<MerchantItemDef>();
    }

    /// <summary>적 상태(balance.json enemyStatus, 기획서 9장 '날아가는 것과 넉백' 아래).</summary>
    public sealed class EnemyStatusDef
    {
        /// <summary>화상·중독 지속 피해가 들어가는 간격 초. 한 번에 초당 피해 x 이 값.</summary>
        public float BurnTickSeconds = 0.5f;
        /// <summary>둔화가 풀리는 속도(초당 비율). 0.3 둔화는 0.5초에 풀린다.</summary>
        public float SlowFadePerSecond = 0.6f;
        /// <summary>넉백으로 밀린 속도가 1초 뒤 남는 비율. 매 프레임 속도 x 이 값^dt.</summary>
        public float KnockbackKeepPerSecond = 0.02f;
    }

    /// <summary>재기(기획서 8장): 기혈 0 에서 남은 횟수가 있으면 최대 기혈 × HealthRatio 로 일어난다.</summary>
    public sealed class RevivalDef
    {
        public float HealthRatio = 0.5f;
    }

    public sealed class BalanceDef
    {
        public EnemyStatusDef EnemyStatus = new EnemyStatusDef();
        public DropsDef Drops = new DropsDef();
        public RevivalDef Revival = new RevivalDef();
        public MerchantDef Merchant = new MerchantDef();
        public BgmRulesDef Bgm = new BgmRulesDef();
        public SfxRulesDef Sfx = new SfxRulesDef();
        public LimitsDef Limits = new LimitsDef();
        public OrbValuesDef Orbs = new OrbValuesDef();
        public OrbPickupDef OrbPickup = new OrbPickupDef();
        public DamageNumberDef DamageNumber = new DamageNumberDef();
        public XpCurveDef XpCurve = new XpCurveDef();
        public List<RealmDef> Realms = new List<RealmDef>();
        public SlotsDef Slots = new SlotsDef();
        public LevelsDef Levels = new LevelsDef();
        public LevelUpDef LevelUp = new LevelUpDef();
        public EvolutionRuleDef Evolution = new EvolutionRuleDef();
        public ChestDef Chest = new ChestDef();
        public AwakeningDef Awakening = new AwakeningDef();
        public YinYangDef Yinyang = new YinYangDef();
        public UltimateChargeDef UltimateCharge = new UltimateChargeDef();
        public ContactDef Contact = new ContactDef();
        public DashDef Dash = new DashDef();
        public EnemyScalingDef EnemyScaling = new EnemyScalingDef();
        public SpawnTargetDef SpawnTarget = new SpawnTargetDef();
        public StatsDef Stats = new StatsDef();
        public TrainingDef Training = new TrainingDef();
    }
}
