#nullable enable
// 객잔 대사 고르기(backlog DL-03). 문장과 조건은 dialogue.json, 규칙은 docs/story 각 파일 '쓰는 법'.
//
// 귀환(OnReturn): 생이 끝나 Soul.EndLife 를 한 바로 뒤에 부른다.
//   목 노인 많아야 두 마디, 운낭 한 마디, 대표 인물 한 사람 한 마디(returns.*Max).
//   이번 귀환에 조건이 맞은 줄은 '밀린 줄'에 넣고, 갈래 순서(returns.mokOrder·unnangOrder) → 파일 순서로 앞에서부터 꺼낸다.
//   못 꺼낸 줄은 다음 귀환으로 밀린다. 한 번 한 줄은 다시 하지 않는다(repeat 줄만 빼고, repeat 줄은 밀리지 않는다).
//   서장이 끝난 귀환에는 귀환 줄이 없다(침방 첫 환혼 줄이 이어진다).
// 마당(YardLine): 그 문파로 태어나 삼류로 첫걸음을 뗄 때 대표 인물 한 줄. 여자 몸이면 사매판.
// 연무장(Training): 조건마다 한 번.
// 침방(Bedroom → BedroomTalk, backlog DL-04): 환혼 한 번 단위. 열 때 → 후보 → 다시 고르기 → 고름 → 보냄 순서로 부른다.
//   첫 환혼(first:*)·문파 첫 환생(sect:<문파>) 줄은 늘 한다. 나머지는 한 번 환혼에 한 마디이고,
//   그 침방에 첫 환혼·문파 첫 환생 줄이 나왔으면 하지 않는다(inn_screens.md '그 줄이 없는 때에만').
//   보여 줄 때는 넷째 몸·다시 고르기·같은 문파 줄만, 무작위 show 줄은 고른 뒤 다른 줄이 없을 때(DL-04 추천안).
//   '처음 ~했을 때' 줄은 그 일이 있고 아직 안 한 줄이면 한다. 자리가 없어 못 했으면 다음에 같은 일이 있을 때 한다.
// 대표 인물 합류는 이번 회차 기준이다. 자기 땅의 봉인석을 이번 회차에 가져왔으면 합류하고(혜공·당연화·하연·운화·백야),
// 땅의 주인이 없는 문파(개방·남궁)는 그 문파 몸으로 땅의 주인을 쓰러뜨린 생 뒤에 합류한다.
// 혈월 천하로 회귀하면 기억을 잃으니 합류와 객잔 줄 차례가 처음으로 돌아간다(leaders.md '아직 정하지 않은 것'의 가정).
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Run;
using Newtonsoft.Json;

namespace BloodRain.Core.Meta
{
    /// <summary>귀환 때 Soul·LifeRecord 에 없는 사실. 게임이 채운다.</summary>
    public sealed class ReturnFacts
    {
        /// <summary>이번 생 몸이 여자 제자였나(RebirthBody.Female).</summary>
        public bool FemaleBody;
        /// <summary>이번 생에 혈마의 그림자가 쫓아왔나(현경 뒤 30분, 봉인석이 모자랄 때).</summary>
        public bool ShadowChased;
        /// <summary>이번 생에 하오문 밀서(기연)를 주웠나.</summary>
        public bool PickedHaomunLetter;
        /// <summary>이번 귀환에 새로 열린 객잔 시설(dialogue.json facility:&lt;id&gt;).</summary>
        public List<string> NewFacilities = new List<string>();
    }

    /// <summary>대표 인물 한 줄.</summary>
    public sealed class LeaderLine
    {
        public string Leader { get; }
        /// <summary>yardUnknown.0, join, inn.2 처럼 칸 이름(.번호). 현지화 키 끝에 쓴다.</summary>
        public string Slot { get; }
        public string Text { get; }
        /// <summary>사매판으로 바꿔 쓴 줄인가.</summary>
        public bool Sister { get; }

        public LeaderLine(string leader, string slot, string text, bool sister)
        { Leader = leader; Slot = slot; Text = text; Sister = sister; }
    }

    /// <summary>귀환 한 번에 나오는 대사.</summary>
    public sealed class InnReturn
    {
        public IReadOnlyList<DialogueLineDef> Mok { get; }
        public IReadOnlyList<DialogueLineDef> Unnang { get; }
        public LeaderLine? Leader { get; }
        /// <summary>이 대사로 열린 것(운낭 고백 3 → haomun_board).</summary>
        public IReadOnlyList<string> Opens { get; }

        public InnReturn(IReadOnlyList<DialogueLineDef> mok, IReadOnlyList<DialogueLineDef> unnang, LeaderLine? leader, IReadOnlyList<string> opens)
        { Mok = mok; Unnang = unnang; Leader = leader; Opens = opens; }
    }

    /// <summary>대사 진행(저장 대상). 본 줄, 밀린 줄, 이번 회차 합류, 객잔 줄 차례.</summary>
    public sealed class DialogueProgress
    {
        public const int SaveVersion = 1;

        readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _pending = new List<string>();
        readonly List<string> _joined = new List<string>();
        readonly Dictionary<string, int> _innNext = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly List<string> _pendingJoins = new List<string>();

        /// <summary>합류·객잔 줄 차례가 어느 회차 것인지. 회차가 바뀌면 처음으로.</summary>
        public int Cycle { get; private set; } = 1;
        /// <summary>마지막 운낭 잡담(같은 줄을 연달아 쓰지 않는다).</summary>
        public string? LastChat { get; internal set; }
        /// <summary>마지막 침방 무작위 줄(show). 고를 줄이 둘 이상이면 연달아 쓰지 않는다.</summary>
        public string? LastBedroomChat { get; internal set; }

        /// <summary>"mok.lives_1", "unnang.return_1", "training.open", "yard.hyegong.yardUnknown" 처럼 묶음.id.</summary>
        public bool HasSeen(string key) => _seen.Contains(key);
        public IReadOnlyCollection<string> Seen => _seen;
        public IReadOnlyList<string> Pending => _pending;
        /// <summary>이번 회차에 합류한 대표 인물(합류한 차례).</summary>
        public IReadOnlyList<string> Joined => _joined;
        public bool HasJoined(string leader) => _joined.Contains(leader);
        public int InnLinesSaid(string leader) => _innNext.TryGetValue(leader, out var n) ? n : 0;

        internal void See(string key) => _seen.Add(key);
        internal void Queue(string key) { if (!_seen.Contains(key) && !_pending.Contains(key)) _pending.Add(key); }
        internal void Unqueue(string key) => _pending.Remove(key);
        internal IReadOnlyList<string> PendingJoins => _pendingJoins;
        internal void QueueJoin(string leader) { if (!_pendingJoins.Contains(leader)) _pendingJoins.Add(leader); }
        internal void Join(string leader) { _pendingJoins.Remove(leader); if (!_joined.Contains(leader)) _joined.Add(leader); }
        internal void SaidInn(string leader) => _innNext[leader] = InnLinesSaid(leader) + 1;

        /// <summary>회귀하면 대표 인물이 기억을 잃는다: 합류·객잔 줄 차례를 비운다. 본 줄과 밀린 줄은 혼의 기억이라 둔다.</summary>
        internal void SyncCycle(int cycle)
        {
            if (cycle == Cycle) return;
            Cycle = cycle;
            _joined.Clear();
            _pendingJoins.Clear();
            _innNext.Clear();
        }

        sealed class SaveFile
        {
            public int Version = SaveVersion;
            public List<string> Seen = new List<string>();
            public List<string> Pending = new List<string>();
            public List<string> Joined = new List<string>();
            public List<string> PendingJoins = new List<string>();
            public SortedDictionary<string, int> InnNext = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public int Cycle = 1;
            public string? LastChat;
            public string? LastBedroomChat;
        }

        public string ToJson() => JsonConvert.SerializeObject(new SaveFile
        {
            Seen = _seen.OrderBy(s => s, StringComparer.Ordinal).ToList(),
            Pending = _pending.ToList(),
            Joined = _joined.ToList(),
            PendingJoins = _pendingJoins.ToList(),
            InnNext = new SortedDictionary<string, int>(_innNext, StringComparer.Ordinal),
            Cycle = Cycle,
            LastChat = LastChat,
            LastBedroomChat = LastBedroomChat,
        });

        public static DialogueProgress FromJson(string json)
        {
            var f = JsonConvert.DeserializeObject<SaveFile>(json) ?? throw new FormatException("대사 진행 저장이 비었습니다");
            if (f.Version > SaveVersion) throw new NotSupportedException($"이 판보다 새 저장 형식입니다: {f.Version}");
            var p = new DialogueProgress { Cycle = Math.Max(1, f.Cycle), LastChat = f.LastChat, LastBedroomChat = f.LastBedroomChat };
            p._seen.UnionWith(f.Seen);
            p._pending.AddRange(f.Pending.Distinct());
            p._joined.AddRange(f.Joined.Distinct());
            p._pendingJoins.AddRange(f.PendingJoins.Distinct());
            foreach (var kv in f.InnNext) p._innNext[kv.Key] = kv.Value;
            return p;
        }
    }

    public static class InnDialogue
    {
        const string MaCheonpae = "ma_cheonpae";

        /// <summary>서장을 뺀 생 횟수(mok.md '생 횟수').</summary>
        public static int LivesAfterPrologue(Soul soul) => soul.PrologueDone ? Math.Max(0, soul.Lives - 1) : 0;

        static string? RegionOfLord(CheonhaDef c, string lord) => c.Regions.FirstOrDefault(r => r.Boss == lord)?.Id;

        /// <summary>그 땅의 주인을 (계정에서) 한 번이라도 쓰러뜨렸나.</summary>
        static bool LordKilled(Soul soul, string lord)
        {
            var region = RegionOfLord(soul.Data.Cheonha, lord);
            return region != null && soul.BossesKilled.Contains(region);
        }

        /// <summary>생이 끝나고 객잔에 돌아왔을 때. Soul.EndLife(record) 바로 뒤에 부른다.</summary>
        public static InnReturn OnReturn(GameData data, Soul soul, LifeRecord record, LifeSummary summary,
            DialogueProgress p, ReturnFacts facts, Random rng)
        {
            var d = data.Dialogue;
            p.SyncCycle(soul.Cycle);
            if (record.Sect == null)   // 서장이 끝난 귀환: 침방 첫 환혼 줄로 넘어간다
                return new InnReturn(Array.Empty<DialogueLineDef>(), Array.Empty<DialogueLineDef>(), null, Array.Empty<string>());

            int lives = LivesAfterPrologue(soul);

            // ---- 목 노인 ----
            var mokRepeat = new List<DialogueLineDef>();
            foreach (var l in d.Mok.Lines)
            {
                var a = l.Args;
                bool hit = l.Kind switch
                {
                    "lives" => lives >= int.Parse(a[0]),
                    "stones" => soul.SealStoneCount >= int.Parse(a[0]),
                    "realm" when a.Length == 1 => soul.HasReached(a[0]),
                    "realm" when a[1] == "short" => summary.NewRealms.Contains(a[0]) && !soul.HasAllSealStones,
                    "realm" when a[1] == "sealed" => record.HighestRealm == a[0] && soul.HasAllSealStones,
                    "lord" => LordKilled(soul, a[0]),
                    _ => false,
                };
                if (hit) p.Queue("mok." + l.Id);
            }
            if (record.EndWay == LifeEndWay.Zuohwa)
            {
                var z = facts.ShadowChased && !p.HasSeen("mok.zuohwa_shadow") ? "zuohwa_shadow"
                      : !p.HasSeen("mok.zuohwa_first") ? "zuohwa_first" : "zuohwa_repeat";
                var line = d.Mok.Lines.FirstOrDefault(l => l.Id == z);
                if (line != null)
                {
                    if (line.Repeat) mokRepeat.Add(line);
                    else p.Queue("mok." + line.Id);
                }
            }
            var mok = Take(p, "mok", d.Mok.Lines, d.Returns.MokOrder, d.Returns.MokMax, mokRepeat);

            // ---- 운낭 ----
            bool afterMa = LordKilled(soul, MaCheonpae);
            foreach (var l in d.Unnang.Lines)
            {
                var a = l.Args;
                bool hit = l.Kind switch
                {
                    "return" => lives >= int.Parse(a[0]),
                    "lives" => lives >= int.Parse(a[0]),
                    "confess" => afterMa,
                    "facility" => facts.NewFacilities.Contains(a[0]),
                    "letter" => facts.PickedHaomunLetter && (a[0] == "after") == afterMa,
                    _ => false,
                };
                if (hit) p.Queue("unnang." + l.Id);
            }
            var unnang = Take(p, "unnang", d.Unnang.Lines, d.Returns.UnnangOrder, d.Returns.UnnangMax, Array.Empty<DialogueLineDef>());
            if (unnang.Count == 0 && d.Returns.UnnangMax > 0)
            {
                var side = afterMa ? "after" : "before";
                var chats = d.Unnang.Lines.Where(l => l.Kind == "chat" && l.Args[0] == side && l.Id != p.LastChat).ToList();
                if (chats.Count > 0)
                {
                    var c = chats[rng.Next(chats.Count)];
                    p.LastChat = c.Id;
                    unnang = new List<DialogueLineDef> { c };
                }
            }
            var opens = mok.Concat(unnang).Where(l => l.Opens != null).Select(l => l.Opens!).ToList();

            // ---- 대표 인물 ----
            foreach (var leader in d.Leaders.List)
            {
                if (p.HasJoined(leader.Id)) continue;
                var home = soul.Data.Cheonha.Regions.FirstOrDefault(r => r.Sect == leader.Sect && r.Boss != null && !r.Final);
                bool join = home != null
                    ? soul.SealStones.Contains(home.Id)
                    : record.Sect == leader.Sect && record.BossesKilled.Count > 0;
                if (join) p.QueueJoin(leader.Id);
            }
            LeaderLine? said = null;
            if (d.Returns.LeaderMax > 0)
            {
                if (p.PendingJoins.Count > 0)
                {
                    var id = p.PendingJoins[0];
                    var leader = d.Leader(id);
                    p.Join(id);
                    // 합류는 이번 생 몸 앞에서 하는 말이라 몸이 그 문파 여자 제자면 사매판.
                    bool sister = facts.FemaleBody && record.Sect == leader.Sect && leader.Sister.ContainsKey("join");
                    said = new LeaderLine(id, "join", sister ? leader.Sister["join"] : leader.Join, sister);
                }
                else
                {
                    var next = p.Joined.Select(id => d.Leader(id)).Where(l => p.InnLinesSaid(l.Id) < l.Inn.Count)
                        .OrderBy(l => p.InnLinesSaid(l.Id)).ThenBy(l => p.Joined.ToList().IndexOf(l.Id)).FirstOrDefault();
                    if (next != null)
                    {
                        int i = p.InnLinesSaid(next.Id);
                        p.SaidInn(next.Id);
                        said = new LeaderLine(next.Id, "inn." + i, next.Inn[i], false);   // 객잔 줄은 혼에게 하는 말이라 사매판이 없다
                    }
                }
            }

            return new InnReturn(mok, unnang, said, opens);
        }

        /// <summary>밀린 줄에서 갈래 순서 → 파일 순서로 max 개를 꺼낸다. repeat 줄은 남는 자리에만.</summary>
        static List<DialogueLineDef> Take(DialogueProgress p, string group, List<DialogueLineDef> lines, List<string> order,
            int max, IReadOnlyList<DialogueLineDef> repeatThisTime)
        {
            int Rank(DialogueLineDef l) { int i = order.IndexOf(l.Kind); return i < 0 ? int.MaxValue : i; }
            var pending = lines.Select((l, i) => (l, i)).Where(x => p.Pending.Contains(group + "." + x.l.Id))
                .OrderBy(x => Rank(x.l)).ThenBy(x => x.i).Select(x => x.l).ToList();
            var taken = pending.Take(max).ToList();
            foreach (var l in taken)
            {
                p.Unqueue(group + "." + l.Id);
                p.See(group + "." + l.Id);
            }
            foreach (var l in repeatThisTime)
                if (taken.Count < max) taken.Add(l);
            // 같은 귀환 안에서도 갈래 순서를 지킨다(repeat 줄이 앞 갈래면 앞으로).
            return taken.Select((l, i) => (l, i)).OrderBy(x => Rank(x.l)).ThenBy(x => x.i).Select(x => x.l).ToList();
        }

        /// <summary>그 문파로 태어나 마당에서 대표 인물이 하는 한 줄. 대표 인물이 없는 문파(화산·곤륜)나 서장이면 null.
        /// 상태(모를 때·안 뒤)마다 처음 한 번은 0번, 그 뒤로는 무작위. 혈월 회차에 그 문파로 처음 태어나면 혈월 줄.</summary>
        public static LeaderLine? YardLine(GameData data, Soul soul, RebirthBody body, DialogueProgress p, Random rng)
        {
            if (body.Sect?.Leader == null) return null;
            p.SyncCycle(soul.Cycle);
            var leader = data.Dialogue.Leader(body.Sect.Leader);

            string slot;
            string bloodKey = $"yard.{leader.Id}.yardBloodMoon.{soul.Cycle}";
            if (p.HasJoined(leader.Id))
                slot = Pick("yardKnown", leader.YardKnown.Count);
            else if (soul.IsBloodMoon && !p.HasSeen(bloodKey))
            {
                p.See(bloodKey);
                slot = "yardBloodMoon";
            }
            else if (leader.YardUnknown.Count > 0)
                slot = Pick("yardUnknown", leader.YardUnknown.Count);
            else if (soul.IsBloodMoon)
                slot = "yardBloodMoon";   // 모를 때 줄이 혈월 하나뿐인 문파(당가·수로채·무당·마교)는 그 줄을 되풀이
            else
                slot = Pick("yardKnown", leader.YardKnown.Count);   // 1회차엔 합류 전에 태어날 수 없다. 저장이 어긋났을 때만

            string text = Slot(leader, slot);
            bool sister = false;
            if (body.Female && leader.Sister.TryGetValue(slot, out var swapped)) { text = swapped; sister = true; }
            return new LeaderLine(leader.Id, slot, text, sister);

            string Pick(string kind, int count)
            {
                var key = $"yard.{leader.Id}.{kind}";
                if (!p.HasSeen(key)) { p.See(key); return kind + ".0"; }
                return kind + "." + rng.Next(count);
            }
        }

        static string Slot(LeaderDialogueDef l, string slot)
        {
            var parts = slot.Split('.');
            int i = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            return parts[0] switch
            {
                "yardUnknown" => l.YardUnknown[i],
                "yardKnown" => l.YardKnown[i],
                "inn" => l.Inn[i],
                "yardBloodMoon" => l.YardBloodMoon,
                "join" => l.Join,
                _ => throw new ArgumentException($"모르는 칸입니다: {slot}"),
            };
        }

        /// <summary>침방을 열 때 부른다. 후보는 draw 가 뽑고, 다시 고르기·고르기는 돌려받은 BedroomTalk 로 한다.</summary>
        public static BedroomTalk Bedroom(GameData data, RebirthDraw draw, DialogueProgress p, Random rng) =>
            new BedroomTalk(data, draw, p, rng);

        /// <summary>연무장 한마디. when 은 training:open / first_buy / first_refund / first_max. 이미 했으면 null.</summary>
        public static DialogueLineDef? Training(GameData data, DialogueProgress p, string when)
        {
            var line = data.Dialogue.Training.Lines.FirstOrDefault(l => l.When == "training:" + when);
            if (line == null || p.HasSeen("training." + line.Id)) return null;
            p.See("training." + line.Id);
            return line;
        }
    }

    /// <summary>후보 카드 한 장의 글. Key 는 현지화 키(Loc.T 에 그대로).</summary>
    public sealed class CandidateCardText
    {
        public string Sect { get; }
        public string Intro { get; }
        public string IntroKey { get; }
        public string Body { get; }
        public string BodyKey { get; }
        /// <summary>묘사 번호(0~2). 같은 문파 두 장은 번호가 다르다.</summary>
        public int BodyIndex { get; }

        public CandidateCardText(string sect, string intro, string introKey, string body, string bodyKey, int bodyIndex)
        { Sect = sect; Intro = intro; IntroKey = introKey; Body = body; BodyKey = bodyKey; BodyIndex = bodyIndex; }
    }

    /// <summary>침방 한 번(환혼 한 번)의 목 노인 대사와 후보 카드 글. InnDialogue.Bedroom 으로 연다.
    /// Open → Show → (Reroll …) → Pick → Send 순서로 부르고, 각각 돌려준 줄을 차례로 띄운다. 할 말이 없으면 빈 목록.</summary>
    public sealed class BedroomTalk
    {
        const string Group = "bedroom";

        readonly GameData _data;
        readonly RebirthDraw _draw;
        readonly DialogueProgress _p;
        readonly Random _rng;
        readonly List<DialogueLineDef> _lines;
        int _step;          // 0 열기 전, 1 열림, 2 보여 줌, 3 고름, 4 보냄
        bool _keySaid;      // 이번 침방에 첫 환혼·문파 첫 환생 줄이 나왔나
        bool _extraSaid;    // 나머지 줄(한 번 환혼에 한 마디)을 했나

        /// <summary>이번 침방에 쓴 다시 고르기 수.</summary>
        public int Rerolls { get; private set; }
        /// <summary>지금 후보 카드 글(후보와 같은 순서). 다시 고르면 새로 뽑는다.</summary>
        public IReadOnlyList<CandidateCardText> Cards { get; private set; }
        /// <summary>고른 몸(Pick 뒤).</summary>
        public RebirthBody? Chosen { get; private set; }
        public RebirthDraw Draw => _draw;

        internal BedroomTalk(GameData data, RebirthDraw draw, DialogueProgress p, Random rng)
        {
            _data = data;
            _draw = draw;
            _p = p;
            _rng = rng;
            _lines = data.Dialogue.Bedroom.Lines;
            Cards = CardTexts(data.Dialogue, draw.Candidates, rng);
        }

        DialogueLineDef? Line(string when) => _lines.FirstOrDefault(l => l.When == when);
        bool Seen(DialogueLineDef l) => _p.HasSeen(Group + "." + l.Id);

        void Step(int from, string what)
        {
            if (_step != from) throw new InvalidOperationException($"침방 순서가 어긋났습니다: {what}(열기 → 보여 주기 → 다시 고르기 → 고르기 → 보내기)");
            _step = from + 1;
        }

        /// <summary>첫 환혼·문파 첫 환생 줄. 늘 한다.</summary>
        void Key(List<DialogueLineDef> into, DialogueLineDef? l)
        {
            if (l == null) return;
            _p.See(Group + "." + l.Id);
            _keySaid = true;
            into.Add(l);
        }

        /// <summary>나머지 줄. 이번 침방에 아직 아무 줄도 안 했을 때 한 마디만. 했으면 참.</summary>
        bool Extra(List<DialogueLineDef> into, DialogueLineDef? l)
        {
            if (l == null || _keySaid || _extraSaid) return false;
            if (!l.Repeat && Seen(l)) return false;
            _p.See(Group + "." + l.Id);
            _extraSaid = true;
            into.Add(l);
            return true;
        }

        /// <summary>침방을 열 때. 첫 환혼이면 '네 몸은 다했다'.</summary>
        public IReadOnlyList<DialogueLineDef> Open()
        {
            Step(0, "열기");
            var said = new List<DialogueLineDef>();
            if (_draw.IsFirst) Key(said, Line("first:open"));
            return said;
        }

        /// <summary>후보를 처음 보여 줄 때. 첫 환혼 줄, 넷째 몸을 산 뒤 첫 침방, 같은 문파 두 장.</summary>
        public IReadOnlyList<DialogueLineDef> Show()
        {
            Step(1, "보여 주기");
            var said = new List<DialogueLineDef>();
            if (_draw.IsFirst) { Key(said, Line("first:candidates")); return said; }
            if (_draw.Count >= 4 && Extra(said, Line("fourth:first"))) return said;
            if (SameSect()) Extra(said, Line("same_sect"));
            return said;
        }

        /// <summary>다시 고르기. draw 를 다시 뽑고 카드 글도 새로 뽑는다. 남은 횟수가 없으면 InvalidOperationException.</summary>
        public IReadOnlyList<DialogueLineDef> Reroll()
        {
            if (_step != 2) throw new InvalidOperationException("침방 순서가 어긋났습니다: 다시 고르기는 후보를 보여 준 뒤, 고르기 전에만");
            _draw.Reroll();
            Rerolls++;
            Cards = CardTexts(_data.Dialogue, _draw.Candidates, _rng);
            var said = new List<DialogueLineDef>();
            if (Extra(said, Line("reroll:first"))) return said;
            if (Rerolls > 2 && Extra(said, Line("reroll:many"))) return said;   // 한 침방에 둘 넘게
            if (SameSect()) Extra(said, Line("same_sect"));
            return said;
        }

        /// <summary>후보 하나를 고른다. 문파 첫 환생 줄(첫 환혼이면 그 뒤에 침 줄), 아니면 여자 몸·성질 줄, 그것도 없으면 무작위 한 마디.</summary>
        public IReadOnlyList<DialogueLineDef> Pick(int index)
        {
            if (_step != 2) throw new InvalidOperationException("침방 순서가 어긋났습니다: 고르기는 후보를 보여 준 뒤 한 번");
            var body = _draw.Choose(index);
            _step = 3;
            Chosen = body;
            var cand = _draw.Candidates[index];
            var said = new List<DialogueLineDef>();

            var sectLine = Line("sect:" + cand.Sect.Id);
            if (sectLine != null && !Seen(sectLine)) Key(said, sectLine);
            if (_draw.IsFirst) Key(said, Line("first:needle"));

            if (cand.Female && Extra(said, Line("pick:female"))) return said;
            if (cand.Traits.Count > 0)
            {
                if (Extra(said, Line("pick:trait"))) return said;
                foreach (var t in cand.Traits)
                    if (Extra(said, Line("pick:trait:" + t.Id))) return said;
            }

            if (!_keySaid && !_extraSaid)
            {
                var chats = _lines.Where(l => l.Kind == "show" && (l.Args.Length == 0 || l.Args[0] == _draw.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();
                if (chats.Count > 1) chats.RemoveAll(l => l.Id == _p.LastBedroomChat);
                if (chats.Count > 0)
                {
                    var c = chats[_rng.Next(chats.Count)];
                    _p.LastBedroomChat = c.Id;
                    Extra(said, c);
                }
            }
            return said;
        }

        /// <summary>그 몸으로 보낼 때. 첫 환혼이면 '이 아이는 강시로 일어나지 않겠구나'.</summary>
        public IReadOnlyList<DialogueLineDef> Send()
        {
            Step(3, "보내기");
            var said = new List<DialogueLineDef>();
            if (_draw.IsFirst) Key(said, Line("first:send"));
            return said;
        }

        bool SameSect() => _draw.Candidates.GroupBy(c => c.Sect.Id).Any(g => g.Count() > 1);

        /// <summary>후보마다 묘사 하나를 무작위로. 같은 문파 카드는 서로 다른 번호, 여자 몸이면 여자 몸 칸.</summary>
        public static List<CandidateCardText> CardTexts(DialogueDef d, IReadOnlyList<RebirthCandidate> candidates, Random rng)
        {
            var used = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var result = new List<CandidateCardText>(candidates.Count);
            foreach (var cand in candidates)
            {
                var card = d.Card(cand.Sect.Id);
                if (!used.TryGetValue(card.Id, out var taken)) used[card.Id] = taken = new List<int>();
                var free = Enumerable.Range(0, card.BodiesKo.Count).Where(i => !taken.Contains(i)).ToList();
                if (free.Count == 0) free = Enumerable.Range(0, card.BodiesKo.Count).ToList();   // 묘사보다 많이 겹치면(지금 데이터로는 없음) 다시 쓴다
                int idx = free[rng.Next(free.Count)];
                taken.Add(idx);
                bool female = cand.Female && card.FemaleBodiesKo.Count > idx;
                string field = female ? "femaleBodies" : "bodies";
                string prefix = "dialogue.cards.list." + card.Id;
                result.Add(new CandidateCardText(card.Id, card.IntroKo, prefix + ".intro",
                    female ? card.FemaleBodiesKo[idx] : card.BodiesKo[idx], prefix + "." + field + "." + idx, idx));
            }
            return result;
        }
    }
}
