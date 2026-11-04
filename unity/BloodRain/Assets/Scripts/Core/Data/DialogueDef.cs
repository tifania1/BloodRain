#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    /// <summary>dialogue.json(backlog DL-02). 문장 원문과 쓰는 법은 docs/story/*.md 이고, 이 파일은 게임이 읽는 사본이다.
    /// 조건(When)의 뜻은 묶음마다 JSON _note 에 있고 Core/Meta/InnDialogue 가 읽는다.</summary>
    public sealed class DialogueDef
    {
        public List<SpeakerDef> Speakers = new List<SpeakerDef>();
        public DialogueReturnsDef Returns = new DialogueReturnsDef();
        public PrologueDialogueDef Prologue = new PrologueDialogueDef();
        public DialogueLinesDef Mok = new DialogueLinesDef();
        public DialogueLinesDef Bedroom = new DialogueLinesDef();
        /// <summary>침방 후보 카드 글(docs/story/sects.md).</summary>
        public SectCardsDef Cards = new SectCardsDef();
        public DialogueLinesDef Unnang = new DialogueLinesDef();
        public DialogueLinesDef Training = new DialogueLinesDef();
        public LeaderDialoguesDef Leaders = new LeaderDialoguesDef();
        public LordDialoguesDef Lords = new LordDialoguesDef();
        public DialogueLinesDef Settlement = new DialogueLinesDef();

        public SpeakerDef Speaker(string id) => Speakers.First(s => s.Id == id);
        public LeaderDialogueDef Leader(string id) => Leaders.List.First(l => l.Id == id);
        public LordDialogueDef Lord(string id) => Lords.List.First(l => l.Id == id);
        public SectCardDef Card(string sect) => Cards.List.First(c => c.Id == sect);
    }

    public sealed class SpeakerDef
    {
        public string Id = "";
        public string NameKo = "";
    }

    /// <summary>객잔 귀환 한 번에 나오는 마디 수와 갈래 순서.</summary>
    public sealed class DialogueReturnsDef
    {
        public int MokMax = 2;
        public int UnnangMax = 1;
        public int LeaderMax = 1;
        /// <summary>When 의 첫 칸(lord, stones …). 앞에 있는 갈래가 먼저 나온다.</summary>
        public List<string> MokOrder = new List<string>();
        public List<string> UnnangOrder = new List<string>();
    }

    public sealed class DialogueLinesDef
    {
        public List<DialogueLineDef> Lines = new List<DialogueLineDef>();
    }

    /// <summary>조건 하나에 붙은 한 줄.</summary>
    public sealed class DialogueLineDef
    {
        public string Id = "";
        /// <summary>"갈래:값:값" 꼴. 갈래 = 첫 칸.</summary>
        public string When = "";
        public string TextKo = "";
        /// <summary>본 뒤에도 다시 나올 수 있는 줄(무작위 잡담 등).</summary>
        public bool Repeat;
        /// <summary>서장 판 안 줄의 말하는 사람(speakers id).</summary>
        public string? Speaker;
        /// <summary>화면 밖 목소리(화면 아래 가운데).</summary>
        public bool Offscreen;
        /// <summary>혈월 회차에 바꿔 쓰는 문장(정산).</summary>
        public string? BloodMoonKo;
        /// <summary>이 줄을 하면 열리는 것(운낭 고백 3 → haomun_board).</summary>
        public string? Opens;

        public string Kind => When.Split(':')[0];
        public string[] Args => When.Split(':').Skip(1).ToArray();
    }

    public sealed class PrologueDialogueDef
    {
        public List<DialogueLineDef> Lines = new List<DialogueLineDef>();
        public List<CutDef> Cuts = new List<CutDef>();
    }

    /// <summary>컷 한 장의 자막. Lines 가 비면 자막 없음.</summary>
    public sealed class CutDef
    {
        public string Id = "";
        /// <summary>art_queue id(있을 때만).</summary>
        public string? Art;
        public List<CutLineDef> Lines = new List<CutLineDef>();
        /// <summary>조건이 맞으면 Lines 대신 쓰는 자막(무당·화산 한 쌍).</summary>
        public CutAltDef? Alt;
    }

    public sealed class CutAltDef
    {
        /// <summary>"stone:&lt;지역&gt;" — 그 지역 봉인석을 지니고 있으면.</summary>
        public string When = "";
        public List<CutLineDef> Lines = new List<CutLineDef>();
    }

    public sealed class CutLineDef
    {
        /// <summary>speakers id. null 이면 서술 자막.</summary>
        public string? Speaker;
        public string TextKo = "";
    }

    public sealed class LeaderDialoguesDef
    {
        public List<LeaderDialogueDef> List = new List<LeaderDialogueDef>();
    }

    /// <summary>대표 인물 한 사람(docs/story/leaders.md).</summary>
    public sealed class LeaderDialogueDef
    {
        public string Id = "";
        public string Sect = "";
        public List<string> YardUnknown = new List<string>();
        public List<string> YardKnown = new List<string>();
        public string YardBloodMoon = "";
        public string Join = "";
        public List<string> Inn = new List<string>();
        /// <summary>여자 몸일 때 바꿔 쓰는 줄. 키는 "yardUnknown.0", "yardBloodMoon", "join" 처럼 칸 이름(.번호).</summary>
        public Dictionary<string, string> Sister = new Dictionary<string, string>();
    }

    public sealed class LordDialoguesDef
    {
        public List<LordDialogueDef> List = new List<LordDialogueDef>();
    }

    /// <summary>땅의 주인 한 명(docs/story/boss_cuts.md).</summary>
    public sealed class LordDialogueDef
    {
        public string Id = "";
        /// <summary>meet1·meet2·kill1~kill4.</summary>
        public List<CutDef> Cuts = new List<CutDef>();
        public string Phase2Ko = "";
        public string EchoKo = "";
        public string BloodMoonKo = "";
    }

    public sealed class SectCardsDef
    {
        public List<SectCardDef> List = new List<SectCardDef>();
    }

    /// <summary>문파 하나의 후보 카드 글. Id 는 문파 id.</summary>
    public sealed class SectCardDef
    {
        public string Id = "";
        /// <summary>한 줄 소개(문파의 내력).</summary>
        public string IntroKo = "";
        /// <summary>'그날 밤 숨진 몸' 묘사 셋.</summary>
        public List<string> BodiesKo = new List<string>();
        /// <summary>여자 몸일 때 같은 번호 칸. 남자만인 문파(소림)는 비어 있다.</summary>
        public List<string> FemaleBodiesKo = new List<string>();
    }
}
