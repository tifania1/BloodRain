#nullable enable
// 스토어 스크린샷 시작 상태(ST-03). 파일은 unity/BloodRain/Assets/Editor/StoreShots/store_shots.json 이고
// Editor 폴더라 게임 빌드에는 안 들어간다. 게임 데이터(GameData.FileNames)와 따로 읽는다.
// 장면 목록은 docs/store/trailer_screens.md '스크린샷 10장'. 이 값으로 한 생을 세우는 일은 Run/StoreShotStage.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BloodRain.Core.Data
{
    public static class StoreShotScreen
    {
        /// <summary>싸움 화면(한 생을 세운다).</summary>
        public const string Battle = "battle";
        /// <summary>침방 환생 후보 화면(생 없이 후보 카드만).</summary>
        public const string Rebirth = "rebirth";
    }

    public static class StoreShotYinyang
    {
        public const string YangExtreme = "yang_extreme";
        public const string YinExtreme = "yin_extreme";
    }

    /// <summary>땅의 주인과 싸우는 장면: 남은 기혈 비율과, 그 비율로 시계를 밀어 판정을 기다릴 패턴.</summary>
    public sealed class StoreShotBossDef
    {
        /// <summary>남은 기혈 / 최대 기혈. 패턴 2막 문턱(bosspatterns.json phase2At) 이하면 2막.</summary>
        public float Health = 1f;
        /// <summary>이 패턴이 판정될 때까지 패턴 시계를 민다(bosspatterns.json 패턴 id).</summary>
        public string Pattern = "";
    }

    /// <summary>침방 후보 카드 한 장(문파 + 타고난 성질 하나).</summary>
    public sealed class StoreShotCandidateDef
    {
        public string Sect = "";
        public string Trait = "";
    }

    public sealed class StoreShotDef
    {
        /// <summary>trailer_screens.md 표의 번호(올리는 순서).</summary>
        public int No;
        public string Id = "";
        public string Screen = StoreShotScreen.Battle;

        public string Region = "";
        /// <summary>문파 id 또는 서장 진소운(jin_sowoon).</summary>
        public string Body = "";
        /// <summary>경지 id(balance.json realms). level 이 이 경지 안이어야 한다.</summary>
        public string Realm = "";
        public int Level = 1;
        /// <summary>세상 시간(분).</summary>
        public float Minutes;
        /// <summary>무공 id → 레벨. 몸의 시작 무공도 적는다.</summary>
        public Dictionary<string, int> Weapons = new Dictionary<string, int>();
        /// <summary>심법 id → 레벨.</summary>
        public Dictionary<string, int> Passives = new Dictionary<string, int>();
        /// <summary>깨어나 있는 절기(evolutions.json id).</summary>
        public string? Evolution;
        /// <summary>적으면 오의 게이지가 가득 찬 채로 시작한다. 몸의 오의와 같아야 한다.</summary>
        public string? Ultimate;
        /// <summary>yang_extreme·yin_extreme 이면 음양 게이지를 극성 문턱에 놓는다. 비우면 무공·심법 레벨대로 기운 그대로.</summary>
        public string? Yinyang;
        public StoreShotBossDef? Boss;
        /// <summary>참이면 마지막 경지 돌파 연출이 막 시작된 상태.</summary>
        public bool Breakthrough;
        /// <summary>레벨업 화면에 뜬 카드(무공·심법 id). 가진 것이면 단계 올리기, 아니면 새로 배우기.</summary>
        public List<string> LevelUpCards = new List<string>();
        /// <summary>화면 위 적 수. Unity 가 이 수를 채워 찍는다.</summary>
        public int Enemies;
        /// <summary>그 장면의 주된 적(enemies.json id). 없으면 지역 표대로.</summary>
        public string? Enemy;

        public List<StoreShotCandidateDef> Candidates = new List<StoreShotCandidateDef>();
    }

    public sealed class StoreShotDefs
    {
        /// <summary>적 생성·후보 성별처럼 난수가 드는 곳의 시드. 장면마다 seed + no.</summary>
        public int Seed;
        public List<StoreShotDef> Shots = new List<StoreShotDef>();

        /// <summary>파일 이름(확장자 없이). 저장소 경로는 unity/BloodRain/Assets/Editor/StoreShots/.</summary>
        public const string FileName = "store_shots";

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        public static StoreShotDefs FromJson(string json) =>
            JsonConvert.DeserializeObject<StoreShotDefs>(json, Settings) ?? new StoreShotDefs();

        public StoreShotDef Shot(int no) => Shots.FirstOrDefault(s => s.No == no)
            ?? throw new KeyNotFoundException($"스크린샷 {no}번이 없습니다");

        public StoreShotDef Shot(string id) => Shots.FirstOrDefault(s => s.Id == id)
            ?? throw new KeyNotFoundException($"스크린샷 '{id}' 가 없습니다");
    }
}
