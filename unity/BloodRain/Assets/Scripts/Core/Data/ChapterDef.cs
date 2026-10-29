#nullable enable
// chapters.json 구조. 장마다 판 길이, 맵, 시간대별 적 가중치표, 정해진 시각의 사건을 담는다.
// 몇 마리를 만들지는 balance.json spawnTarget(SpawnCurve)이 정하고, 여기서는 어떤 적과 어떤 사건인지만 정한다.
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
        /// <summary>한 종류가 한 방향에서 무리로 몰려옴(1장 목인나한 무리).</summary>
        Group,
        /// <summary>플레이어를 원형으로 둘러싸는 포위 웨이브.</summary>
        Ring,
        /// <summary>금두꺼비. 보통 창(window) 안 무작위 시점에 나온다.</summary>
        Toad,
        /// <summary>보부상. 적이 아니라 상점.</summary>
        Merchant,
        /// <summary>은빛 등롱(의서 조각이 낮은 확률로 나옴).</summary>
        SilverLantern,
        /// <summary>장 보스. 장마다 정확히 하나.</summary>
        Boss,
    }

    public sealed class MapDef
    {
        /// <summary>bounded(벽이 있는 제한 맵) 또는 endless(사방이 이어지는 무한 반복 맵).</summary>
        public string Type = "endless";
        /// <summary>제한 맵일 때만 쓴다.</summary>
        public float? WidthMeters;
        public float? HeightMeters;
        /// <summary>지형 기믹 id. 수치는 기믹을 만들 때 따로 뺀다.</summary>
        public List<string> Gimmicks = new List<string>();
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
        /// <summary>나오는 적 id. 일반 적은 enemies, 정예는 elites, 보스는 bosses 쪽 id. 상점·등롱은 빈 값.</summary>
        public string Enemy = "";
        /// <summary>돌진·무리·포위의 마리 수. 한 마리짜리 사건은 1.</summary>
        public int Count = 1;
        /// <summary>한 판에 이 사건이 일어날 확률. 1이면 반드시.</summary>
        public float Chance = 1;

        public WaveEventKind Type => WaveEventKinds.Parse(Kind);
        /// <summary>가장 이른 시각(창이 있으면 창 시작).</summary>
        public float EarliestMinute => Window != null && Window.Length > 0 ? Window[0] : At;
        public float LatestMinute => Window != null && Window.Length > 1 ? Window[1] : At;
    }

    public sealed class ChapterDef
    {
        public string Id = "";
        /// <summary>cheonha.json 지역 id.</summary>
        public string Region = "";
        public string NameKo = "";
        public string TitleKo = "";
        /// <summary>기획서가 말하는 판 길이. 보스는 이 안에 나오고, 보스전이 길어지면 마지막 시간대 표를 계속 쓴다.</summary>
        public float DurationMinutes;
        public float StopSpawningAtMinute;
        public MapDef Map = new MapDef();
        public List<SpawnBandDef> Bands = new List<SpawnBandDef>();
        public List<ChapterEventDef> Events = new List<ChapterEventDef>();

        public ChapterEventDef BossEvent => Events.Single(e => e.Type == WaveEventKind.Boss);
        public float BossAtMinute => BossEvent.At;
        public string Boss => BossEvent.Enemy;
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
