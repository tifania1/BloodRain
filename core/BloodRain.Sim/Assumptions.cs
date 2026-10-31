// 시뮬레이터 가정. 데이터(JSON)에 아직 없거나 화면이 없어서 대신 정한 값은 전부 여기 있다.
// 여기 숫자를 바꾸면 보고서(docs/design/sim/level-curve.md)의 숫자도 바뀐다. 바꿨으면 보고서를 다시 만든다.
// 출처 표시: [프로토] = web/prototype/template.html 상수, [기획] = docs/design/gdd.md, [가정] = 이 시뮬레이터에서 새로 정한 값.
using System.Collections.Generic;

namespace BloodRain.Sim
{
    public static class Assumptions
    {
        // ───────── 시간 ─────────

        /// <summary>계산 간격(초). 섬광지가 초당 9발 넘게 쏘므로 0.1초면 충분하다.</summary>
        public const double TickSeconds = 0.1;
        /// <summary>돌리는 길이(분). 숭산에서 원각에게 덤비기 전 20분까지만 돌린다(땅의 주인 싸움은 없다).</summary>
        public const double RunMinutes = 20;

        // ───────── 무리(공간) 모형 [가정] ─────────
        // 적 하나하나의 좌표는 들고 있지 않는다. 대신 '플레이어를 둘러싼 무리' 하나로 본다.
        //  - 생긴 지 ArrivalSeconds 가 지난 적만 무리에 들어와 맞을 수 있다.
        //  - 무리 안 적은 반경 CrowdRadius 원 안에 고르게 퍼져 있다. 밀도 = 무리 수 / 원 넓이, 단 MaxDensity 를 넘지 않는다.
        //  - 범위 무공은 '밀도 × 무공이 덮는 넓이' 만큼 맞힌다. 앞쪽(먼저 온 적, 정예)부터 맞는다.
        // 화면 크기는 PC 16:9 기준이다. [프로토] PPM 58이면 화면이 약 33m × 18.6m(대각선 38m).
        // 이 셋은 재 볼 방법이 없는 값이라 보고서 '가정 민감도'에서 크게 흔들어 보고, 결론이 바뀌지 않는지 확인한다.

        /// <summary>싸움이 벌어지는 무리 반경(m). 화면 높이의 절반(9.3m)보다 조금 안쪽.</summary>
        public const double CrowdRadius = 8.0;
        /// <summary>플레이어 곁 최대 밀도(마리/㎡). 적 몸 반지름 0.4m면 빽빽이 붙어도 1㎡에 한 마리 남짓.</summary>
        public const double MaxDensity = 1.0;
        /// <summary>생성 고리에서 무리에 닿기까지(초). 고리는 대각선 0.6~0.75배(balance.json ringMin/MaxDiagonal)라 약 25m,
        /// 무공이 닿는 3m를 빼면 22m. 1장 적 평균 걸음이 초속 약 3.3m(속도 60~85 × 4.6m/100)라 약 7초.</summary>
        public const double ArrivalSeconds = 7.0;
        /// <summary>적 몸 반지름(m). 무공 사거리에 더한다. [프로토] KIND_SIZE(1.2~1.6) × 0.32.</summary>
        public const double EnemyRadius = 0.4;
        /// <summary>바라보는 방향으로만 나가는 투사체(장풍, 백보신권 권경 등)가 무리 쪽을 향하는 비율.
        /// 도망치며 싸우는 장르라 절반 남짓만 무리를 향한다고 본다.</summary>
        public const double FacingEfficiency = 0.6;
        /// <summary>섬광지·벽력탄처럼 '가까운 적 중 아무나' 노리는 무공이 고르는 후보 수. [프로토] 12, 20.</summary>
        public const int RandomPickNearest = 20;

        // ───────── 진기(경험치) ─────────

        /// <summary>처치 진기. enemies.json 에 아직 진기 값이 없어서 웹 프로토타입 값(README '처치 진기' 줄)을 그대로 쓴다.</summary>
        public static readonly IReadOnlyDictionary<string, double> KillXp = new Dictionary<string, double>
        {
            ["sigang"] = 1, ["hyeolin"] = 1, ["hyeol_gangsi"] = 2, ["hyeol_musa"] = 3, ["mogin_nahan"] = 3, ["donggap"] = 4,
        };
        /// <summary>표에 없는 적의 처치 진기. [프로토] ?? 1.</summary>
        public const double DefaultKillXp = 1;
        /// <summary>정예 처치 진기. [프로토] xp: 25(적 구슬 하나).</summary>
        public const double EliteXp = 25;
        /// <summary>금두꺼비 진기. [프로토] 녹색 구슬 12개.</summary>
        public const double GoldenToadXp = 60;
        /// <summary>떨어진 구슬 중 실제로 줍는 비율. 400개가 넘으면 합쳐져 곁에 떨어지므로 다 줍는다고 본다(낙관적인 쪽).</summary>
        public const double PickupRate = 1.0;

        // ───────── 옛 1장 적 구성 [프로토] pickType ─────────
        // 천하 구조 전 기획서 7장 '1장 시간대별 흐름'을 웹 프로토타입이 표로 옮긴 것(목인나한이 섞여 있다).
        // 지금 흐름은 chapters.json life(지역 고유 적은 RegionSpawns·localShare)다. T-17(CheonhaSim)은 이 시뮬레이터의 분당 진기·화력을
        // 그대로 빌려 쓰므로 이 옛 표가 천하 보고서에도 들어간다. life 를 읽게 바꾸면 level-curve·cheonha·cluster-xp·meta·yinyang 보고서가 같이 움직인다.

        public static readonly (double FromMinute, (string Id, double Weight)[] Mix)[] EnemyMix =
        {
            (0,  new[] { ("sigang", 1.0) }),
            (3,  new[] { ("sigang", 0.7), ("hyeol_gangsi", 0.3) }),
            (5,  new[] { ("sigang", 0.35), ("hyeol_gangsi", 0.4), ("hyeol_musa", 0.17), ("mogin_nahan", 0.08) }),
            (8,  new[] { ("hyeol_gangsi", 0.45), ("hyeol_musa", 0.3), ("donggap", 0.15), ("mogin_nahan", 0.1) }),
            (10, new[] { ("hyeol_gangsi", 0.45), ("donggap", 0.3), ("hyeol_musa", 0.25) }),
            (12, new[] { ("sigang", 0.2), ("hyeol_gangsi", 0.35), ("hyeol_musa", 0.2), ("donggap", 0.17), ("mogin_nahan", 0.08) }),
        };

        // ───────── 1장 사건 [프로토] makeEvents ─────────

        public enum EventKind { Charge, Elite, Ring, Toad }

        /// <summary>시각(분), 종류, 수. 정예는 수 자리에 0·1·2(동인·계율원 승려·방장 대리). 금두꺼비는 10.3~11.6분 사이 무작위.</summary>
        public static readonly (double Minute, EventKind Kind, int Count)[] Events =
        {
            (3.5, EventKind.Charge, 14), (5, EventKind.Elite, 0),
            (8.5, EventKind.Charge, 20), (9.5, EventKind.Charge, 22), (10, EventKind.Elite, 1),
            (10.3, EventKind.Toad, 1), (13.5, EventKind.Ring, 24),
            (15, EventKind.Elite, 2), (15.5, EventKind.Charge, 28), (16.6, EventKind.Charge, 30), (17.7, EventKind.Charge, 34),
        };
        /// <summary>금두꺼비가 나오는 구간(분). [프로토] rand(10.3, 11.6).</summary>
        public const double ToadFrom = 10.3, ToadTo = 11.6;
        /// <summary>혈인 돌진 벽에 난 빈틈(마리). [프로토] 두 칸을 비운다.</summary>
        public const int ChargeGap = 2;
        /// <summary>정예 기혈(1장 0분 기준). 기획서에 아직 숫자가 없어 [프로토] ELITES 를 쓴다. 시간 배율은 적과 같다.</summary>
        public static readonly double[] EliteHealth = { 700, 1500, 2600 };
        public static readonly string[] EliteNameKo = { "동인", "계율원 강시 승려", "강시 방장 대리" };
        /// <summary>금두꺼비 기혈은 enemies.json(300)에 시간 배율을 곱한다. 달아나지만 평균 플레이어는 잡는다고 본다.</summary>
        public static readonly bool ToadCaught = true;
        /// <summary>목인나한이 쓰러질 때 나뭇조각 피해와 반경(m). [프로토] 30, 1.6.</summary>
        public const double MoginBurstDamage = 30, MoginBurstRadius = 1.6;

        // ───────── 보갑 [프로토] openChest ─────────

        /// <summary>보갑 강화 개수 확률. 3개 15%, 2개 34%(0.85 × 0.4), 나머지 1개. 기획서는 '1~5개'라 프로토 쪽이 짜다.</summary>
        public const double ChestThree = 0.15, ChestTwo = 0.34;

        // ───────── 절기 [기획] 8장, [프로토] weaponDamage ─────────

        /// <summary>절기가 된 무공 피해 배율. [프로토] evolved → ×2(천지인 삼검 '피해 2배').</summary>
        public const double EvolvedDamage = 2.0;
        /// <summary>각성 파동이 화면 안 잡몹을 쓸어 내는지. [기획] 8장 '화면 안 잡목은 쓸려 나가고'.</summary>
        public static readonly bool WaveClearsTrash = true;
        /// <summary>각성 파동이 정예에게서 깎는 최대 기혈 비율. [기획] 15%.</summary>
        public const double WaveEliteRatio = 0.15;
        /// <summary>각성 뒤 적이 새로 몰려오지 않는 시간(초). [기획] 2.5초.</summary>
        public const double WaveSpawnPause = 2.5;
        /// <summary>각성 여운: 그 절기 피해 배율과 시간. [기획] 8초 1.5배.</summary>
        public const double GlowSeconds = 8, GlowMultiplier = 1.5;

        // ───────── 오의 ─────────

        /// <summary>일검이 지나가는 띠의 폭(m). [프로토] 가운데서 0.9 + 적 반지름, 양쪽.</summary>
        public const double UltLineWidth = 2 * (0.9 + EnemyRadius);
        /// <summary>절정 돌파 보상 오의 위력 +25%. [기획] 3장 경지표.</summary>
        public const double UltPowerAtJeoljeong = 1.25;

        // ───────── 일부러 뺀 것 ─────────
        // - 플레이어는 죽지 않는다(피격·회복·호신·기혈역류 피해 없음). 피격 오의 충전(+3)도 없다.
        // - 깨달음(B-06 전이라 데이터 없음), 연환·진전(synergies.json), 연무장, 영약, 다시 고르기·파기·정진.
        // - 넉백·둔화·기절, 범종·등롱·부적, 보스전(20분부터).
    }
}
