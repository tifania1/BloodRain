#nullable enable
// 생 도중 저장 ①(backlog GU-11h1, 천하 확장 설계안 '긴 생을 위한 두 가지'). 한 생 상한이 60분으로 늘어 중간에 끄고 이어 할 수 있어야 한다.
// 이 판(①)이 담는 것: 시각·레벨·진기·위치·밟은 땅·무공/심법 레벨과 칸 수·음양·오의 게이지·은자.
// 담지 않는 것: 적·탄·사건 자리(이어 할 때 비우고 생성 규칙대로 다시 채운다 — 2026-11-18 예약 세션이 정한 시작 방식),
//   처치 때 혼에 바로 여는 것(극성 처치·의상)은 같은 저장 파일의 혼(soul 칸)이 싣는다 — 되돌리며 다시 열지 않는다.
//   (② 첫 조각으로 절기를 깨운 무공·겪은 연표 사건·이번 생에 쓰러뜨린 땅의 주인·켠 연환과 대성은 담는다. 땅의 주인 깨어남은 레벨로 다시 셈한다.
//    셋째 조각(2026-11-18 20시)으로 일지의 절기 각성, 세던 수·찢긴 비급·밀서·속삭임, 구슬로 모은 윤회의 기 장부(LifeLogTally)도 담는다.
//    넷째 조각(같은 세션)으로 깨달음·기연·영약·재기(LifeBoonsSnapshot)도 담는다.)
// 되돌리기는 새로 만든 같은 몸의 Life 에 Life.Restore 로 한다. 저장 파일에는 SaveGame.Ongoing('life' 칸)으로 들어간다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BloodRain.Core.Run
{
    public sealed class LifeSnapshot
    {
        public const int Version = 1;
        public int SnapshotVersion = Version;
        /// <summary>태어난 문파 id(서장은 null). 되돌릴 때 같은 몸인지 본다.</summary>
        public string? Sect;
        /// <summary>몸을 다시 만들 재료(태어난 지역·타고난 성질 id·여자 몸·덕). 옛 저장엔 없어 null — 그때는 문파만 본다.</summary>
        public string? Region;
        public List<string>? Traits;
        public bool Female;
        public string? Boon;
        public float Minutes;
        public int Level;
        public double Xp;
        public double XpTotal;
        public float X, Y;
        public List<string> Visited = new List<string>();
        public Dictionary<string, int> Weapons = new Dictionary<string, int>();
        public Dictionary<string, int> Passives = new Dictionary<string, int>();
        public int WeaponSlots, PassiveSlots;
        public int Gauge;
        public float Charge;
        public int Silver;
        /// <summary>② 첫 조각: 절기를 깨운 무공 id, 이번 생에 겪은 연표 사건 id.</summary>
        public List<string> Evolved = new List<string>();
        public List<string> Chronicle = new List<string>();
        /// <summary>② 둘째 조각: 이번 생에 쓰러뜨린 땅의 주인 지역(잡은 순서). 되돌릴 때 일지·봉인석·윤회의 기를 다시 센다.</summary>
        public List<string> BossesKilled = new List<string>();
        /// <summary>켠 연환과 대성으로 올린 연환 id.</summary>
        public List<string> Links = new List<string>();
        public List<string> GrandLinks = new List<string>();
        /// <summary>② 셋째 조각: 일지의 절기 각성(절기 id, 깨운 차례 — 처음 깨운 절기는 혼 장부로 다시 가린다).</summary>
        public List<string> Awakened = new List<string>();
        /// <summary>일지의 세던 수(처치·경공·정신부·금두꺼비·음식·정예·영물·의서)와 찢긴 비급·밀서·속삭임, 구슬로 모은 윤회의 기 장부. 옛 저장엔 없어 null.</summary>
        public Meta.LifeLogTally? Tally;
        /// <summary>② 넷째 조각: 고른 깨달음(고른 차례)·살기충천/심마가 세기 시작한 값·금종조 대기, 기연, 영약, 재기. 옛 저장엔 없어 null.</summary>
        public LifeBoonsSnapshot? Boons;
        /// <summary>고른 차례(구결 걸음). 옛 저장엔 없어 null — 그때는 되돌린 무공·심법으로 채운 차례가 남는다.</summary>
        public List<string>? PickOrder;
        /// <summary>문파의 위기(GU-11m): 막은 것·버티다 떠난 것·버티는 중인 초. 옛 저장엔 없어 null.</summary>
        public List<string>? CrisesBlocked;
        public List<string>? CrisisBroken;
        public Dictionary<string, float>? CrisisHeld;
        /// <summary>2막(GU-11h4): 이미 모여든 땅의 주인 지역. 옛 저장엔 없어 null.</summary>
        public List<string>? Gathered;

        public static LifeSnapshot Capture(Life life) => new LifeSnapshot
        {
            Sect = life.Body.Sect?.Id,
            Region = life.Body.Region.Id,
            Traits = life.Body.Traits.Select(t => t.Id).ToList(),
            Female = life.Body.Female,
            Boon = life.Body.Boon,
            Minutes = life.Minutes,
            Level = life.Xp.Level,
            Xp = life.Xp.Xp,
            XpTotal = life.Xp.Total,
            X = life.Position.X,
            Y = life.Position.Y,
            Visited = life.Log.VisitedRegions.ToList(),
            Weapons = life.Loadout.Weapons.ToDictionary(kv => kv.Key, kv => kv.Value),
            Passives = life.Loadout.Passives.ToDictionary(kv => kv.Key, kv => kv.Value),
            WeaponSlots = life.Loadout.WeaponSlots,
            PassiveSlots = life.Loadout.PassiveSlots,
            Gauge = life.Loadout.Gauge.Value,
            Charge = life.Charge.Value,
            Silver = life.Silver,
            Evolved = life.Loadout.Evolved.ToList(),
            Chronicle = life.Log.Chronicle.ToList(),
            CrisesBlocked = life.Log.CrisesBlocked.ToList(),
            CrisisBroken = life.CrisisBroken.ToList(),
            CrisisHeld = life.CrisisHeld.ToDictionary(kv => kv.Key, kv => kv.Value),
            Gathered = life.GatheredLords.ToList(),
            PickOrder = life.Loadout.PickOrder.ToList(),
            BossesKilled = life.Log.BossesKilled.ToList(),
            Links = life.Loadout.AwakenedLinks.ToList(),
            GrandLinks = life.Loadout.GrandLinks.ToList(),
            Awakened = life.Log.AwakenedEvolutions.ToList(),
            Tally = life.Log.Tally(),
            Boons = LifeBoonsSnapshot.Capture(life),
        };

        [Newtonsoft.Json.JsonIgnore] public Vector2 Position => new Vector2(X, Y);

        /// <summary>이어 할 때 같은 몸을 만들 후보(Rebirth.Body 에 넘긴다). 서장 저장이거나 몸 재료가 없는 옛 저장이면 null.</summary>
        public Meta.RebirthCandidate? Candidate(Data.CheonhaDef c)
        {
            if (Sect == null || Region == null || Traits == null) return null;
            return new Meta.RebirthCandidate(c.Sect(Sect), c.Region(Region), Traits.Select(c.Trait).ToList(), Female, boon: Boon);
        }

        /// <summary>이 몸이 저장한 몸과 같은가(문파, 그리고 재료가 있으면 지역·성질·성별·덕까지).</summary>
        public bool SameBody(Meta.RebirthBody body)
        {
            if (Sect != body.Sect?.Id) return false;
            if (Region == null || Traits == null) return true;
            return Region == body.Region.Id && Traits.SequenceEqual(body.Traits.Select(t => t.Id)) && Female == body.Female && Boon == body.Boon;
        }
    }

    /// <summary>생 도중 저장 ② 넷째 조각: 깨달음·기연·영약·재기.</summary>
    public sealed class LifeBoonsSnapshot
    {
        public List<string> Insights = new List<string>();
        public int KillsAtSalgi, SimmaBaseline;
        public float BlockCooldown;
        public List<string> Encounters = new List<string>();
        public Dictionary<string, int> EncounterSchools = new Dictionary<string, int>();
        public float SwordTombMight;
        public bool WhisperTaken;
        public List<string> Elixirs = new List<string>();
        public bool ElixirReviveUsed;
        public float YinyangFreezeLeft;
        public int RevivalsUsed;

        public static LifeBoonsSnapshot Capture(Life life) => new LifeBoonsSnapshot
        {
            Insights = life.InsightEffects.Taken.Select(i => i.Id).ToList(),
            KillsAtSalgi = life.InsightEffects.KillsAtSalgi,
            SimmaBaseline = life.InsightEffects.SimmaBaseline,
            BlockCooldown = life.InsightEffects.BlockCooldown,
            Encounters = life.Encounters.Met.ToList(),
            EncounterSchools = life.Encounters.SchoolExtra.ToDictionary(kv => kv.Key, kv => kv.Value),
            SwordTombMight = life.Encounters.WeaponMightBonus,
            WhisperTaken = life.Encounters.WhisperTaken,
            Elixirs = life.Elixirs.Select(e => e.Id).ToList(),
            ElixirReviveUsed = life.ElixirReviveUsed,
            YinyangFreezeLeft = life.YinyangFreezeLeft,
            RevivalsUsed = life.RevivalsUsed,
        };
    }
}
