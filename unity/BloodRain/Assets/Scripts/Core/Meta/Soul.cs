#nullable enable
// 혼(계정 진행, 기획서 10·21장). 몸이 바뀌어도 혼에 남는 것을 여기 모은다.
//   윤회의 기 잔고, 연무장(산 레벨과 쓴 양, 전부 환불), 환혼 강화(넷째 몸, 다시 고르기),
//   해금(땅의 주인 첫 처치 = 'boss:<지역>' 토큰, 도전 과제 토큰), 봉인석(이번 회차), 밟은 땅,
//   한 번 깨친 길(오른 경지, 깨운 절기), 생 횟수와 문파별 생, 문파별로 깬 종장(진엔딩 '서로 다른 문파 4곳'), 의서, 회차.
//   회귀는 cycle.resets 에 적힌 것(봉인석)만 지운다.
// 한 생 동안 일어난 일은 LifeLog 가 받아 적는다. 처음인지 아닌지는 생을 시작할 때의 혼과 이번 생 기록을 보고 가린다.
// 생이 끝나면 EndLife 가 한꺼번에 혼에 옮긴다(도중에 끄면 그 생은 없던 일). 저장은 JSON 문자열 하나(ToJson / FromJson).
//
// 땅의 주인 처치의 '처음'은 둘이다.
//   이번 회차 첫 처치(봉인석이 아직 없는 땅): 봉인석 조각 + 윤회의 기 300(bosses.firstKill). 그 뒤로는 혈영(100, 혈정 5).
//   계정 첫 처치: 그 땅 문파의 환생 해금, 대표 인물 합류. 회귀해도 다시 오지 않는다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace BloodRain.Core.Meta
{
    /// <summary>돌파 한 번이 혼에게 어떤 돌파인지(21장 '한 번 깨친 길').</summary>
    public readonly struct BreakthroughInfo
    {
        public readonly string Realm;
        /// <summary>계정 전체에서 처음 오른 경지.</summary>
        public readonly bool FirstTime;
        /// <summary>긴 돌파 연출(처음이면 긴 것, 올라 본 경지면 짧은 것).</summary>
        public readonly bool LongCinematic;
        /// <summary>깨달음 선택지에 더할 수(올라 본 경지면 +1). Insights.ChoiceCount 에 더한다.</summary>
        public readonly int ExtraInsightChoices;
        /// <summary>이 돌파로 장부에 적힌 윤회의 기(처음이면 100).</summary>
        public readonly int RebirthQi;

        public BreakthroughInfo(string realm, bool firstTime, bool longCinematic, int extraInsightChoices, int rebirthQi)
        {
            Realm = realm; FirstTime = firstTime; LongCinematic = longCinematic; ExtraInsightChoices = extraInsightChoices; RebirthQi = rebirthQi;
        }
    }

    /// <summary>절기 각성 한 번이 혼에게 어떤 각성인지.</summary>
    public readonly struct AwakenInfo
    {
        public readonly string Evolution;
        /// <summary>계정 전체에서 처음 깨운 절기(장경각에 등록).</summary>
        public readonly bool FirstTime;
        /// <summary>설정이 '자동'일 때 긴 각성 연출을 쓸지. Combat.Awakening.ChooseLong(설정, 이 값).</summary>
        public readonly bool LongByDefault;

        public AwakenInfo(string evolution, bool firstTime, bool longByDefault)
        {
            Evolution = evolution; FirstTime = firstTime; LongByDefault = longByDefault;
        }
    }

    /// <summary>땅의 주인을 쓰러뜨렸을 때 받는 것.</summary>
    public readonly struct BossKillInfo
    {
        public readonly string Region;
        /// <summary>이번 회차 첫 처치(봉인석이 아직 없는 땅). 아니면 혈영.</summary>
        public readonly bool FirstInCycle;
        /// <summary>계정 첫 처치(문파 환생 해금, 대표 인물 합류, 컷신).</summary>
        public readonly bool FirstOnAccount;
        /// <summary>얻은 봉인석 조각 이름(천추석 …). 없으면 null.</summary>
        public readonly string? SealStone;
        /// <summary>이 처치로 환생이 열린 문파 id. 없으면 null.</summary>
        public readonly string? UnlockedSect;
        public readonly int RebirthQi;
        public readonly int BloodCrystal;

        public BossKillInfo(string region, bool firstInCycle, bool firstOnAccount, string? sealStone, string? unlockedSect, int rebirthQi, int bloodCrystal)
        {
            Region = region; FirstInCycle = firstInCycle; FirstOnAccount = firstOnAccount; SealStone = sealStone;
            UnlockedSect = unlockedSect; RebirthQi = rebirthQi; BloodCrystal = bloodCrystal;
        }
    }

    /// <summary>한 생의 기록. LifeLog.Finish 가 만들고, 직접 채워서 Soul.EndLife 에 넘겨도 된다.</summary>
    public sealed class LifeRecord
    {
        /// <summary>태어난 문파. 서장(진소운 제 몸)이면 null.</summary>
        public string? Sect { get; }
        /// <summary>생이 끝난 방식(cheonha.json lifeEnd.ways: fallen 쓰러짐, zuohwa 좌화, sealed 봉인).</summary>
        public string EndWay { get; }
        public RebirthQiResult RebirthQi { get; }
        /// <summary>쓰러뜨린 땅의 주인의 지역 id(잡은 순서, 혈영 포함).</summary>
        public IReadOnlyList<string> BossesKilled { get; }
        /// <summary>이번 생에 밟은 지역 id(태어난 곳 포함).</summary>
        public IReadOnlyList<string> VisitedRegions { get; }
        /// <summary>이번 생에 오른 가장 높은 경지 id. 돌파 없이 끝났으면 첫 경지(삼류).</summary>
        public string HighestRealm { get; }
        /// <summary>이번 생에 깨운 절기 id.</summary>
        public IReadOnlyList<string> AwakenedEvolutions { get; }
        /// <summary>이번 생에 찾은 의서 권 수.</summary>
        public int Manuscripts { get; }

        public LifeRecord(string? sect, string endWay, RebirthQiResult rebirthQi, IEnumerable<string> bossesKilled,
            IEnumerable<string> visitedRegions, string highestRealm, IEnumerable<string> awakenedEvolutions, int manuscripts = 0)
        {
            Sect = sect;
            EndWay = endWay;
            RebirthQi = rebirthQi;
            BossesKilled = bossesKilled.ToList();
            VisitedRegions = visitedRegions.ToList();
            HighestRealm = highestRealm;
            AwakenedEvolutions = awakenedEvolutions.ToList();
            Manuscripts = manuscripts;
        }
    }

    /// <summary>생 하나를 혼에 옮기고 새로 생긴 것(정산 화면·객잔 대화용).</summary>
    public sealed class LifeSummary
    {
        public int RebirthQi { get; }
        /// <summary>새로 얻은 봉인석의 지역 id.</summary>
        public IReadOnlyList<string> NewSealStones { get; }
        public IReadOnlyList<string> NewlyUnlockedSects { get; }
        public IReadOnlyList<string> NewRegions { get; }
        public IReadOnlyList<string> NewRealms { get; }
        public IReadOnlyList<string> NewEvolutions { get; }
        /// <summary>종장을 깼다(봉인).</summary>
        public bool FinalCleared { get; }

        public LifeSummary(int rebirthQi, IReadOnlyList<string> newSealStones, IReadOnlyList<string> newlyUnlockedSects,
            IReadOnlyList<string> newRegions, IReadOnlyList<string> newRealms, IReadOnlyList<string> newEvolutions, bool finalCleared)
        {
            RebirthQi = rebirthQi; NewSealStones = newSealStones; NewlyUnlockedSects = newlyUnlockedSects;
            NewRegions = newRegions; NewRealms = newRealms; NewEvolutions = newEvolutions; FinalCleared = finalCleared;
        }
    }

    public sealed class Soul
    {
        public const int SaveVersion = 1;
        /// <summary>환혼 강화 effect 에 쓸 수 있는 이름(candidates+1, rerolls+1).</summary>
        public static readonly string[] UpgradeEffectKeys = { "candidates", "rerolls" };

        readonly GameData _data;
        int _qi;
        int _trainingSpent;
        readonly Dictionary<string, int> _training = new Dictionary<string, int>();
        readonly Dictionary<string, int> _upgrades = new Dictionary<string, int>();
        readonly HashSet<string> _unlocks = new HashSet<string>();
        readonly HashSet<string> _bosses = new HashSet<string>();
        readonly HashSet<string> _sealStones = new HashSet<string>();
        readonly HashSet<string> _visited = new HashSet<string>();
        readonly HashSet<string> _realms = new HashSet<string>();
        readonly HashSet<string> _evolutions = new HashSet<string>();
        readonly Dictionary<string, int> _livesBySect = new Dictionary<string, int>();
        readonly Dictionary<string, int> _finalClears = new Dictionary<string, int>();

        public Soul(GameData data) { _data = data; }

        public GameData Data => _data;
        CheonhaDef C => _data.Cheonha;

        // ---- 윤회의 기 ----

        public int RebirthQi => _qi;

        /// <summary>정산 밖에서 받는 윤회의 기(시험, 이벤트). 생의 정산은 EndLife 가 더한다.</summary>
        public void AddRebirthQi(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            _qi += amount;
        }

        // ---- 연무장 ----

        public IReadOnlyDictionary<string, int> Training => _training;
        /// <summary>연무장에 쓴 윤회의 기 합. 전부 환불하면 이만큼 돌아온다.</summary>
        public int TrainingSpent => _trainingSpent;
        public int TrainingLevel(string stat) => _training.TryGetValue(stat, out var v) ? v : 0;
        /// <summary>다음 레벨 값(Stats.TrainingCost). 최대면 null, 없는 항목이면 InvalidOperationException.</summary>
        public int? TrainingCost(string stat) => Stats.TrainingCost(_data.Balance.Training, stat, _training);

        /// <summary>한 레벨 산다. 최대 레벨이거나 윤회의 기가 모자라면 false 이고 아무것도 안 바뀐다.</summary>
        public bool TryBuyTraining(string stat)
        {
            var cost = TrainingCost(stat);
            if (cost == null || cost.Value > _qi) return false;
            _qi -= cost.Value;
            _trainingSpent += cost.Value;
            _training[stat] = TrainingLevel(stat) + 1;
            return true;
        }

        /// <summary>전부 환불: 쓴 만큼 돌려받고 레벨을 모두 지운다. 돌려받은 양.</summary>
        public int RefundTraining()
        {
            int refund = _trainingSpent;
            _qi += refund;
            _trainingSpent = 0;
            _training.Clear();
            return refund;
        }

        /// <summary>판 시작에 얹을 연무장 덧셈.</summary>
        public StatBonus TrainingBonus() => Stats.FromTraining(_data.Balance.Training, _training);

        // ---- 환혼 강화(침방) ----

        RebirthUpgradeDef Upgrade(string id) =>
            C.Rebirth.Upgrades.FirstOrDefault(u => u.Id == id) ?? throw new ArgumentException($"없는 환혼 강화입니다: {id}");

        public IReadOnlyDictionary<string, int> RebirthUpgrades => _upgrades;

        public int UpgradeLevel(string id)
        {
            Upgrade(id);
            return _upgrades.TryGetValue(id, out var v) ? v : 0;
        }

        /// <summary>다음 레벨 값: 기본가(costPerLevel 이면 × 다음 레벨). 최대면 null.</summary>
        public int? UpgradeCost(string id)
        {
            var u = Upgrade(id);
            int lv = UpgradeLevel(id);
            if (lv >= u.MaxLevel) return null;
            return u.CostPerLevel ? u.BaseCost * (lv + 1) : u.BaseCost;
        }

        public bool TryBuyUpgrade(string id)
        {
            var cost = UpgradeCost(id);
            if (cost == null || cost.Value > _qi) return false;
            _qi -= cost.Value;
            _upgrades[id] = UpgradeLevel(id) + 1;
            return true;
        }

        /// <summary>환생 후보 수(기본 3, 넷째 몸이면 4).</summary>
        public int CandidateCount => C.Rebirth.Candidates + UpgradeEffect("candidates");
        /// <summary>한 번 환혼에 쓸 수 있는 다시 고르기 수.</summary>
        public int RerollsPerRebirth => UpgradeEffect("rerolls");

        int UpgradeEffect(string key)
        {
            int sum = 0;
            foreach (var u in C.Rebirth.Upgrades)
            {
                var (k, amount) = ParseEffect(u.Effect);
                if (!UpgradeEffectKeys.Contains(k)) throw new InvalidOperationException($"모르는 환혼 강화 효과입니다: {u.Effect}");
                if (k == key) sum += amount * UpgradeLevel(u.Id);
            }
            return sum;
        }

        /// <summary>"candidates+1" → ("candidates", 1).</summary>
        public static (string key, int amount) ParseEffect(string effect)
        {
            int plus = effect.IndexOf('+');
            if (plus <= 0 || !int.TryParse(effect.Substring(plus + 1), out var n))
                throw new FormatException($"환혼 강화 효과 모양이 '이름+수'가 아닙니다: {effect}");
            return (effect.Substring(0, plus), n);
        }

        // ---- 해금 ----

        /// <summary>한 번이라도 쓰러뜨린 땅의 주인의 지역(계정 전체, 회귀해도 남는다).</summary>
        public IReadOnlyCollection<string> BossesKilled => _bosses;
        /// <summary>도전 과제 같은 데서 얻은 해금 토큰.</summary>
        public IReadOnlyCollection<string> Unlocks => _unlocks;

        /// <summary>해금 토큰: "start" 는 늘 열림, "boss:&lt;지역&gt;" 은 그 땅의 주인 첫 처치, 나머지는 Unlock 으로 받은 것.</summary>
        public bool IsUnlocked(string token)
        {
            if (token == UnlockToken.Start) return true;
            var bossRegion = UnlockToken.BossRegion(token);
            if (bossRegion != null) return _bosses.Contains(bossRegion);
            return _unlocks.Contains(token);
        }

        /// <summary>도전 과제처럼 땅의 주인 말고 다른 데서 오는 해금.</summary>
        public void Unlock(string token) => _unlocks.Add(token);

        /// <summary>환생할 수 있는 문파(데이터 순서).</summary>
        public IReadOnlyList<string> UnlockedSects => C.Sects.Where(s => IsUnlocked(s.Unlock)).Select(s => s.Id).ToList();

        /// <summary>레벨업 카드에 나올 수 있는 무공(Loadout.Candidates 의 unlocked).</summary>
        public HashSet<string> UnlockedWeapons() => new HashSet<string>(_data.Weapons.Where(w => IsUnlocked(w.Unlock)).Select(w => w.Id));

        /// <summary>깬 장(cheonha.json chapter 값). 서장을 마쳤으면 prologue, 땅의 주인을 쓰러뜨린 땅의 장, 종장을 깼으면 final. Build.RunRecord 에 넘긴다.</summary>
        public IReadOnlyList<string> ClearedChapters
        {
            get
            {
                var list = new List<string>();
                foreach (var r in C.Regions)
                {
                    if (r.Chapter == null) continue;
                    bool cleared = r.Final ? _finalClears.Values.Any(v => v > 0)
                        : r.Boss != null ? _bosses.Contains(r.Id)
                        : r.Id == C.PrologueBody.Region && PrologueDone;
                    if (cleared) list.Add(r.Chapter);
                }
                return list;
            }
        }

        // ---- 봉인석 ----

        /// <summary>이번 회차에 봉인석을 가져온 지역 id.</summary>
        public IReadOnlyCollection<string> SealStones => _sealStones;
        public int SealStoneCount => _sealStones.Count;
        public bool HasAllSealStones => SealStoneCount >= C.Bosses.FinalRequires.SealStones;

        // ---- 밟은 땅, 한 번 깨친 길 ----

        public IReadOnlyCollection<string> VisitedRegions => _visited;
        /// <summary>한 번이라도 돌파해 오른 경지(첫 경지 삼류는 넣지 않는다).</summary>
        public IReadOnlyCollection<string> ReachedRealms => _realms;
        public IReadOnlyCollection<string> AwakenedEvolutions => _evolutions;
        public bool HasVisited(string region) => _visited.Contains(region);
        public bool HasReached(string realm) => _realms.Contains(realm);
        public bool HasAwakened(string evolution) => _evolutions.Contains(evolution);

        // ---- 생과 회차 ----

        /// <summary>마친 생 수(서장 포함).</summary>
        public int Lives { get; private set; }
        public bool PrologueDone { get; private set; }
        /// <summary>문파별로 마친 생 수.</summary>
        public IReadOnlyDictionary<string, int> LivesBySect => _livesBySect;
        /// <summary>다음 환혼이 첫 환혼(서장을 마치고 아직 문파로 산 적 없음)인가.</summary>
        public bool IsFirstRebirth => PrologueDone && _livesBySect.Count == 0;
        /// <summary>문파별로 종장을 깬 횟수(회차 합산).</summary>
        public IReadOnlyDictionary<string, int> FinalClearsBySect => _finalClears;
        public int DistinctFinalSects => _finalClears.Count(kv => kv.Value > 0);
        /// <summary>진엔딩 조건 하나: 종장을 서로 다른 문파 trueEndingDistinctSects 곳 이상으로 깼다.</summary>
        public bool TrueEndingSectsMet => DistinctFinalSects >= C.Cycle.TrueEndingDistinctSects;
        /// <summary>모은 의서 권 수(혈월 천하 은빛 등롱). Build.RunRecord 의 medicalBooks.</summary>
        public int Manuscripts { get; private set; }
        /// <summary>회차. 1회차에서 시작하고 회귀할 때마다 하나씩 는다.</summary>
        public int Cycle { get; private set; } = 1;
        public bool IsBloodMoon => Cycle >= 2;
        /// <summary>이번 회차 종장에서 고를 수 있는 엔딩.</summary>
        public IReadOnlyList<string> Endings => Cycle == 1 ? C.Cycle.EndingsFirst : C.Cycle.EndingsSecond;

        // ---- 환혼과 한 생 ----

        /// <summary>침방을 연다. 첫 환혼이면 고정 셋, 아니면 해금한 문파에서 후보 수·다시 고르기만큼.</summary>
        public RebirthDraw OpenRebirth(Random rng)
        {
            if (!PrologueDone) throw new InvalidOperationException("서장을 마치기 전에는 환혼이 없습니다. 첫 생은 Rebirth.Prologue 로 시작합니다");
            return new RebirthDraw(C, UnlockedSects, CandidateCount, RerollsPerRebirth, IsFirstRebirth, rng);
        }

        /// <summary>이 몸으로 한 생을 시작한다. 서장이 안 끝났으면 Rebirth.Prologue 몸이어야 한다.</summary>
        public LifeLog BeginLife(RebirthBody body)
        {
            if (!PrologueDone && !body.IsPrologue) throw new InvalidOperationException("첫 생은 서장(진소운 제 몸)입니다");
            return new LifeLog(this, body);
        }

        /// <summary>한 생을 혼에 옮긴다: 윤회의 기, 잡은 땅의 주인(해금·봉인석), 밟은 땅, 오른 경지, 깨운 절기, 의서, 생 횟수, 종장.</summary>
        public LifeSummary EndLife(LifeRecord record)
        {
            Validate(record);
            var sectsBefore = new HashSet<string>(UnlockedSects);

            _qi += record.RebirthQi.Total;

            var newStones = new List<string>();
            foreach (var region in record.BossesKilled)
            {
                _bosses.Add(region);
                if (C.Region(region).SealStone != null && _sealStones.Add(region)) newStones.Add(region);
            }

            var newRegions = record.VisitedRegions.Where(r => _visited.Add(r)).ToList();

            var realms = _data.Balance.Realms.OrderBy(r => r.Level).ToList();
            int top = realms.Single(r => r.Id == record.HighestRealm).Level;
            var newRealms = realms.Where(r => r.Level > realms[0].Level && r.Level <= top && _realms.Add(r.Id)).Select(r => r.Id).ToList();

            var newEvolutions = record.AwakenedEvolutions.Where(e => _evolutions.Add(e)).ToList();
            Manuscripts += record.Manuscripts;

            Lives++;
            if (record.Sect == null) PrologueDone = true;
            else _livesBySect[record.Sect] = (_livesBySect.TryGetValue(record.Sect, out var n) ? n : 0) + 1;

            bool sealedEnd = record.EndWay == "sealed";
            if (sealedEnd) _finalClears[record.Sect!] = (_finalClears.TryGetValue(record.Sect!, out var f) ? f : 0) + 1;

            var newSects = UnlockedSects.Where(s => !sectsBefore.Contains(s)).ToList();
            return new LifeSummary(record.RebirthQi.Total, newStones, newSects, newRegions, newRealms, newEvolutions, sealedEnd);
        }

        void Validate(LifeRecord r)
        {
            if (!C.LifeEnd.Ways.Contains(r.EndWay)) throw new ArgumentException($"모르는 생의 끝입니다: {r.EndWay}");
            if (r.Sect != null && C.Sects.All(s => s.Id != r.Sect)) throw new ArgumentException($"없는 문파입니다: {r.Sect}");
            if (r.EndWay == "sealed" && r.Sect == null) throw new ArgumentException("서장 몸으로는 종장을 깰 수 없습니다");
            var bossRegions = new HashSet<string>(C.BossRegions.Select(x => x.Id));
            foreach (var b in r.BossesKilled)
                if (!bossRegions.Contains(b)) throw new ArgumentException($"땅의 주인이 있는 지역이 아닙니다: {b}");
            foreach (var v in r.VisitedRegions)
                if (C.Regions.All(x => x.Id != v)) throw new ArgumentException($"없는 지역입니다: {v}");
            if (_data.Balance.Realms.All(x => x.Id != r.HighestRealm)) throw new ArgumentException($"없는 경지입니다: {r.HighestRealm}");
            foreach (var e in r.AwakenedEvolutions)
                if (_data.Evolutions.All(x => x.Id != e)) throw new ArgumentException($"없는 절기입니다: {e}");
            if (r.Manuscripts < 0) throw new ArgumentException("의서 권 수가 음수입니다");
        }

        /// <summary>회귀(봉인 엔딩 뒤 백중의 밤으로): cycle.resets 에 적힌 것만 지우고 회차를 하나 올린다.</summary>
        public void Regress()
        {
            foreach (var key in C.Cycle.Resets)
            {
                switch (key)
                {
                    case "sealStones": _sealStones.Clear(); break;
                    default: throw new InvalidOperationException($"회귀가 지울 줄 모르는 항목입니다: {key}");
                }
            }
            Cycle++;
        }

        // ---- 저장 ----

        sealed class SaveFile
        {
            public int Version = SaveVersion;
            public int RebirthQi;
            public SortedDictionary<string, int> Training = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public int TrainingSpent;
            public SortedDictionary<string, int> RebirthUpgrades = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public List<string> Unlocks = new List<string>();
            public List<string> BossesKilled = new List<string>();
            public List<string> SealStones = new List<string>();
            public List<string> VisitedRegions = new List<string>();
            public List<string> ReachedRealms = new List<string>();
            public List<string> AwakenedEvolutions = new List<string>();
            public int Lives;
            public bool PrologueDone;
            public SortedDictionary<string, int> LivesBySect = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public SortedDictionary<string, int> FinalClearsBySect = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public int Manuscripts;
            public int Cycle = 1;
        }

        static readonly JsonSerializerSettings SaveSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Ignore,
            // 손으로 고친 저장에 null 이 섞여 있으면 그 칸은 기본값(빈 목록)으로 둔다
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented,
        };

        static List<string> Sorted(IEnumerable<string> items) => items.OrderBy(x => x, StringComparer.Ordinal).ToList();
        static SortedDictionary<string, int> Sorted(IReadOnlyDictionary<string, int> d) =>
            new SortedDictionary<string, int>(d.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal);

        /// <summary>저장 문자열. 목록은 이름순이라 같은 혼이면 같은 문자열이 나온다.</summary>
        public string ToJson()
        {
            var f = new SaveFile
            {
                RebirthQi = _qi,
                Training = Sorted(_training),
                TrainingSpent = _trainingSpent,
                RebirthUpgrades = Sorted(_upgrades),
                Unlocks = Sorted(_unlocks),
                BossesKilled = Sorted(_bosses),
                SealStones = Sorted(_sealStones),
                VisitedRegions = Sorted(_visited),
                ReachedRealms = Sorted(_realms),
                AwakenedEvolutions = Sorted(_evolutions),
                Lives = Lives,
                PrologueDone = PrologueDone,
                LivesBySect = Sorted(_livesBySect),
                FinalClearsBySect = Sorted(_finalClears),
                Manuscripts = Manuscripts,
                Cycle = Cycle,
            };
            return JsonConvert.SerializeObject(f, SaveSettings);
        }

        /// <summary>저장 문자열을 읽는다. 더 새 형식(version 이 큼)이면 NotSupportedException.</summary>
        public static Soul FromJson(GameData data, string json)
        {
            var f = JsonConvert.DeserializeObject<SaveFile>(json, SaveSettings) ?? throw new FormatException("저장 문자열이 비었습니다");
            if (f.Version > SaveVersion) throw new NotSupportedException($"이 판보다 새 저장 형식입니다: {f.Version}");
            var s = new Soul(data)
            {
                _qi = f.RebirthQi,
                _trainingSpent = f.TrainingSpent,
                Lives = f.Lives,
                PrologueDone = f.PrologueDone,
                Manuscripts = f.Manuscripts,
                Cycle = Math.Max(1, f.Cycle),
            };
            foreach (var kv in f.Training) s._training[kv.Key] = kv.Value;
            foreach (var kv in f.RebirthUpgrades) s._upgrades[kv.Key] = kv.Value;
            foreach (var kv in f.LivesBySect) s._livesBySect[kv.Key] = kv.Value;
            foreach (var kv in f.FinalClearsBySect) s._finalClears[kv.Key] = kv.Value;
            s._unlocks.UnionWith(f.Unlocks);
            s._bosses.UnionWith(f.BossesKilled);
            s._sealStones.UnionWith(f.SealStones);
            s._visited.UnionWith(f.VisitedRegions);
            s._realms.UnionWith(f.ReachedRealms);
            s._evolutions.UnionWith(f.AwakenedEvolutions);
            return s;
        }
    }

    /// <summary>한 생 동안의 일지. 게임이 일이 생길 때마다 부르고, 생이 끝나면 Finish → Soul.EndLife.
    /// 혼은 생이 끝날 때까지 바뀌지 않는다. '처음'은 생을 시작할 때의 혼 + 이번 생에 이미 한 일로 가린다.</summary>
    public sealed class LifeLog
    {
        readonly CheonhaDef _c;
        readonly List<RealmDef> _realmOrder;
        readonly HashSet<string> _visitedKnown;
        readonly HashSet<string> _realmsKnown;
        readonly HashSet<string> _evolutionsKnown;
        readonly HashSet<string> _bossesKnown;
        readonly HashSet<string> _stonesKnown;
        readonly List<string> _visited = new List<string>();
        readonly List<string> _bosses = new List<string>();
        readonly List<string> _evolutions = new List<string>();
        RealmDef _highest;

        public RebirthBody Body { get; }
        /// <summary>이번 생의 윤회의 기 장부(성질 배율은 몸에서).</summary>
        public RebirthQiLedger Qi { get; }
        public int Manuscripts { get; private set; }
        /// <summary>혈월 천하(회귀 뒤)면 1.5: 윤회의 기·혈정·은자에 곱한다(은자는 줍는 쪽이 이 값을 곱한다).</summary>
        public float RewardMultiplier { get; }

        public LifeLog(Soul soul, RebirthBody body)
        {
            _c = soul.Data.Cheonha;
            _realmOrder = soul.Data.Balance.Realms.OrderBy(r => r.Level).ToList();
            _highest = _realmOrder[0];
            _visitedKnown = new HashSet<string>(soul.VisitedRegions);
            _realmsKnown = new HashSet<string>(soul.ReachedRealms);
            _evolutionsKnown = new HashSet<string>(soul.AwakenedEvolutions);
            _bossesKnown = new HashSet<string>(soul.BossesKilled);
            _stonesKnown = new HashSet<string>(soul.SealStones);
            Body = body;
            RewardMultiplier = soul.IsBloodMoon ? _c.Cycle.Bloodmoon.Rewards : 1f;
            Qi = new RebirthQiLedger(_c.RebirthQi, body.RebirthQiBonus, RewardMultiplier);
            Enter(body.Region.Id);
        }

        /// <summary>이번 생에 밟은 지역(처음 밟은 순서, 태어난 곳이 맨 앞).</summary>
        public IReadOnlyList<string> VisitedRegions => _visited;
        public IReadOnlyList<string> BossesKilled => _bosses;
        public IReadOnlyList<string> AwakenedEvolutions => _evolutions;
        public string HighestRealm => _highest.Id;

        /// <summary>지역에 들어섰다(태어난 곳은 생을 시작할 때 저절로). 계정 전체에서 처음 밟은 땅이면 윤회의 기를 적고 true.</summary>
        public bool Enter(string region)
        {
            if (_c.Regions.All(r => r.Id != region)) throw new ArgumentException($"없는 지역입니다: {region}");
            if (!_visited.Contains(region)) _visited.Add(region);
            if (!_visitedKnown.Add(region)) return false;
            Qi.AddBonus(RebirthQiBonusKind.FirstVisitRegion, region);
            return true;
        }

        /// <summary>진기 구슬을 먹었다. 값은 심득 배율 전(RunXp.Absorb 에 넘기는 값), 농도는 그 자리 혈운 농도.</summary>
        public void AbsorbOrb(double orbValue, float density, int orbs = 1) => Qi.AddOrb(orbValue, density, orbs);

        /// <summary>경지를 돌파했다. 처음 오른 경지면 긴 연출과 윤회의 기, 올라 본 경지면 짧은 연출과 깨달음 선택지 +1.</summary>
        public BreakthroughInfo Breakthrough(string realm)
        {
            var def = _realmOrder.FirstOrDefault(r => r.Id == realm) ?? throw new ArgumentException($"없는 경지입니다: {realm}");
            if (def.Level > _highest.Level) _highest = def;
            bool first = _realmsKnown.Add(realm);
            var m = _c.Mastery;
            int qi = first ? Qi.AddBonus(RebirthQiBonusKind.FirstReachRealm, realm) : 0;
            bool longCinematic = first || !m.ShortBreakthroughWhenRevisited;
            return new BreakthroughInfo(realm, first, longCinematic, first ? 0 : m.RevisitInsightChoicesBonus, qi);
        }

        /// <summary>절기가 깨어났다. 서장 장삼전의 고정 각성은 이 값과 상관없이 늘 길게(21장).</summary>
        public AwakenInfo Awaken(string evolution)
        {
            if (!_evolutions.Contains(evolution)) _evolutions.Add(evolution);
            bool first = _evolutionsKnown.Add(evolution);
            return new AwakenInfo(evolution, first, first && _c.Mastery.LongAwakeningFirstTime);
        }

        /// <summary>땅의 주인을 쓰러뜨렸다. 이번 회차 첫 처치면 봉인석과 300, 아니면 혈영 100과 혈정. 계정 첫 처치면 문파가 열린다.</summary>
        public BossKillInfo KillBoss(string region)
        {
            var r = _c.BossRegions.FirstOrDefault(x => x.Id == region) ?? throw new ArgumentException($"땅의 주인이 있는 지역이 아닙니다: {region}");
            _bosses.Add(region);
            bool firstInCycle = _stonesKnown.Add(region);
            bool firstOnAccount = _bossesKnown.Add(region);
            string? stone = firstInCycle && _c.Bosses.FirstKill.SealStone ? r.SealStone : null;
            string? sect = firstOnAccount && _c.Bosses.FirstKill.UnlockSect ? _c.Sects.FirstOrDefault(s => s.UnlockBossRegion == region)?.Id : null;
            int qi = Qi.AddBonus(firstInCycle ? RebirthQiBonusKind.BossFirstKill : RebirthQiBonusKind.BossRepeatKill, region);
            int crystalBase = firstInCycle ? _c.Bosses.FirstKill.BloodCrystal : _c.Bosses.RepeatKill.BloodCrystal;
            int crystal = (int)Math.Round(crystalBase * RewardMultiplier, MidpointRounding.AwayFromZero);
            return new BossKillInfo(region, firstInCycle, firstOnAccount, stone, sect, qi, crystal);
        }

        /// <summary>은빛 등롱에서 의서가 나왔다.</summary>
        public void FindManuscript() => Manuscripts++;

        /// <summary>생이 끝났다. 남은 은자로 정산해 기록을 만든다(혼에는 Soul.EndLife 로 옮긴다).</summary>
        public LifeRecord Finish(string endWay, int leftoverSilver) =>
            new LifeRecord(Body.Sect?.Id, endWay, Qi.Settle(leftoverSilver), _bosses, _visited, _highest.Id, _evolutions, Manuscripts);
    }
}
