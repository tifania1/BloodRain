#nullable enable
// chapters.json 구조. 흐름(서장, 한 생)마다 표가 다루는 길이, 시간대별 적 가중치표, 정해진 시각의 사건을 담는다.
// 몇 마리를 만들지는 balance.json spawnTarget(SpawnCurve)이 정하고, 여기서는 어떤 적과 어떤 사건인지만 정한다.
// 맵은 따로 없다. 서장도 한 생도 천하 한 장(cheonha.json)을 쓰고, 기믹은 서 있는 지역 것을 쓴다.
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    public enum WaveEventKind
    {
        Unknown,
        /// <summary>혈인 떼가 한 줄로 화면을 가로질러 돌진.</summary>
        Charge,
        /// <summary>정예 한 마리. 처치하면 보갑.</summary>
        Elite,
        /// <summary>한 종류가 한 방향에서 무리로 몰려옴(5~8분, 서 있는 지역의 고유 적).</summary>
        Group,
        /// <summary>플레이어를 원형으로 둘러싸는 포위 웨이브.</summary>
        Ring,
        /// <summary>금두꺼비. 보통 창(window) 안 무작위 시점에 나온다.</summary>
        Toad,
        /// <summary>보부상. 적이 아니라 상점.</summary>
        Merchant,
        /// <summary>은빛 등롱(혈월 천하에서 의서가 낮은 확률로 나옴).</summary>
        SilverLantern,
        /// <summary>정해진 시각의 보스(서장 장삼). 한 생의 땅의 주인은 사건이 아니라 지역에서 싸움을 건다(7장).</summary>
        Boss,
    }

    public sealed class SpawnWeightDef
    {
        public string Enemy = "";
        public float Weight;
    }

    /// <summary>[FromMinute, ToMinute) 동안 쓰는 가중치표. 순서가 곧 추첨 순서라 목록으로 둔다.</summary>
    public sealed class SpawnBandDef
    {
        public float FromMinute;
        public float ToMinute;
        public List<SpawnWeightDef> Spawns = new List<SpawnWeightDef>();

        public float TotalWeight => Spawns.Sum(s => s.Weight);
        public float WeightOf(string enemy) => Spawns.Where(s => s.Enemy == enemy).Sum(s => s.Weight);
    }

    public sealed class ChapterEventDef
    {
        /// <summary>일어나는 게임 분. Window가 있으면 쓰지 않는다.</summary>
        public float At;
        /// <summary>[시작, 끝] 분. 있으면 한 판마다 이 안에서 무작위로 시각을 정한다.</summary>
        public float[]? Window;
        public string Kind = "";
        /// <summary>나오는 적 id. 일반 적은 enemies, 정예는 elites, 보스는 bosses 쪽 id. 상점·등롱은 빈 값.
        /// Local 이면 지역에 고유 적이 없을 때 대신 나오는 적. Nth 가 있는 정예는 빈 값.</summary>
        public string Enemy = "";
        /// <summary>돌진·무리·포위의 마리 수. 한 마리짜리 사건은 1.</summary>
        public int Count = 1;
        /// <summary>한 판에 이 사건이 일어날 확률. 1이면 반드시.</summary>
        public float Chance = 1;
        /// <summary>정예가 이 생에서 몇 번째인지(1부터). 0이 아니면 Enemy 대신 서 있는 지역 정예 표에서 고른다(Spawning.RegionSpawns).</summary>
        public int Nth;
        /// <summary>참이면 서 있는 지역의 고유 적이 나온다. 고유 적이 없는 땅이면 Enemy.</summary>
        public bool Local;

        public WaveEventKind Type => WaveEventKinds.Parse(Kind);
        /// <summary>가장 이른 시각(창이 있으면 창 시작).</summary>
        public float EarliestMinute => Window != null && Window.Length > 0 ? Window[0] : At;
        public float LatestMinute => Window != null && Window.Length > 1 ? Window[1] : At;
    }

    /// <summary>[FromMinute, 다음 줄) 동안 생성 가운데 지역 고유 적이 차지하는 몫.</summary>
    public sealed class LocalShareDef
    {
        public float FromMinute;
        public float Share;
    }

    /// <summary>정예 표가 없는 땅에서 대신 쓰는 지역(6장: 청운진·개방 총타·남궁세가는 숭산 표).</summary>
    public sealed class EliteFallbackDef
    {
        public string Region = "";
        public List<string> Regions = new List<string>();
    }

    public sealed class ChapterDef
    {
        public string Id = "";
        /// <summary>cheonha.json 지역 id. 정해진 자리에서만 도는 흐름(서장 청운진)만 있고, 한 생처럼 서 있는 지역을 따르는 흐름은 null.</summary>
        public string? Region;
        public string NameKo = "";
        public string TitleKo = "";
        /// <summary>표가 다루는 길이(분). 가중치표는 여기까지 이어져야 하고 사건도 이 안에 있다.
        /// OpenEnded 가 아니면 이 시각에 판이 끝난다(서장 10분 컷신).</summary>
        public float DurationMinutes;
        /// <summary>참이면 DurationMinutes 뒤에도 이어진다(한 생: 쓰러짐·좌화·봉인으로만 끝남). 그 뒤로는 마지막 가중치표를 계속 쓴다.</summary>
        public bool OpenEnded;
        /// <summary>이 시각부터 보스 사건까지 새 적을 만들지 않는다. 없으면 멈추지 않는다.</summary>
        public float? StopSpawningAtMinute;
        public List<SpawnBandDef> Bands = new List<SpawnBandDef>();
        public List<ChapterEventDef> Events = new List<ChapterEventDef>();
        /// <summary>시각 순서. 비어 있으면 지역 고유 적을 뽑지 않는다.</summary>
        public List<LocalShareDef> LocalShare = new List<LocalShareDef>();
        public EliteFallbackDef? EliteFallback;

        public ChapterEventDef? BossEvent => Events.SingleOrDefault(e => e.Type == WaveEventKind.Boss);
        public float? BossAtMinute => BossEvent?.At;
        public string? Boss => BossEvent?.Enemy;

        /// <summary>그 분에 생성 가운데 지역 고유 적이 차지하는 몫(0~1).</summary>
        public float LocalShareAt(float minute)
        {
            float share = 0;
            foreach (var s in LocalShare)
                if (minute >= s.FromMinute) share = s.Share;
            return share;
        }
    }

    public static class WaveEventKinds
    {
        public static WaveEventKind Parse(string s)
        {
            switch (s)
            {
                case "charge": return WaveEventKind.Charge;
                case "elite": return WaveEventKind.Elite;
                case "group": return WaveEventKind.Group;
                case "ring": return WaveEventKind.Ring;
                case "toad": return WaveEventKind.Toad;
                case "merchant": return WaveEventKind.Merchant;
                case "silver_lantern": return WaveEventKind.SilverLantern;
                case "boss": return WaveEventKind.Boss;
                default: return WaveEventKind.Unknown;
            }
        }

        /// <summary>적을 만드는 사건인지(상점·등롱은 아님).</summary>
        public static bool SpawnsEnemy(WaveEventKind k) => k != WaveEventKind.Merchant && k != WaveEventKind.SilverLantern && k != WaveEventKind.Unknown;
    }
}
