#nullable enable
// 크레딧(credits.json, 기획서 12장 화면 목록 '크레딧', backlog TT-02). 고지를 왜 이렇게 적었는지와 약관 출처는 docs/design/credits.md.
// 한국어 칸(~Ko)은 strings 의 credits. 키로 옮기고(키는 StringCatalog 규칙 그대로), 고유 이름·저작권 줄·라이선스 원문은 옮기지 않는다.
// 화면은 Roll(...) 이 주는 줄을 위에서부터 흘리기만 한다(TT-02U).
using System;
using System.Collections.Generic;
using System.Linq;

namespace BloodRain.Core.Data
{
    public sealed class CreditsDef
    {
        /// <summary>맨 위 제목(한자, 옮기지 않음).</summary>
        public string Hanja = "";
        /// <summary>만든 사람. 이름(name)이 비어 있으면 화면에서 건너뛴다(이름은 사람이 나중에 정함, 2026-11-09).</summary>
        public List<CreditMakerDef> Makers = new List<CreditMakerDef>();
        /// <summary>고지 묶음(글꼴, 엔진과 라이브러리 …).</summary>
        public List<CreditSectionDef> Sections = new List<CreditSectionDef>();
        public string LicensesTitleKo = "";
        /// <summary>라이선스 원문. 항목의 license 가 id 로 가리킨다. 원문이라 옮기지 않는다.</summary>
        public List<CreditLicenseDef> Licenses = new List<CreditLicenseDef>();
        public string ThanksKo = "";

        public IEnumerable<CreditItemDef> Items => Sections.SelectMany(s => s.Items);
        public IEnumerable<CreditMakerDef> NamedMakers => Makers.Where(m => !string.IsNullOrWhiteSpace(m.Name));
        public CreditLicenseDef? License(string id) => Licenses.FirstOrDefault(l => l.Id == id);
        /// <summary>covers 에 이 이름(글꼴 파일·패키지 id·소리 갈래)이 든 항목이 있나.</summary>
        public bool Covers(string what) => Items.Any(i => i.Covers.Contains(what));

        // strings 키(StringCatalog.FromData 가 credits.json 에서 뽑는 키와 같다).
        public static string MakerRoleKey(CreditMakerDef m) => $"credits.makers.{m.Id}.role";
        public static string SectionTitleKey(CreditSectionDef s) => $"credits.sections.{s.Id}.title";
        public static string ItemUseKey(CreditSectionDef s, CreditItemDef i) => $"credits.sections.{s.Id}.items.{i.Id}.use";
        public const string LicensesTitleKey = "credits.licensesTitle";
        public const string ThanksKey = "credits.thanks";

        /// <summary>
        /// 화면에 흘릴 줄을 차례대로. tr(키, 한국어 원문) 은 Unity 에서 Loc.T 를 넘긴다.
        /// 차례: 제목 → 만든 사람(이름 있는 것만, 없으면 묶음째 건너뜀) → 묶음마다 머리·항목(쓰임 · 이름, 저작권, 라이선스 이름) → 라이선스 원문 → 끝 인사.
        /// </summary>
        public IReadOnlyList<CreditLine> Roll(Func<string, string, string> tr)
        {
            var lines = new List<CreditLine>();
            void Add(CreditLineKind k, string text) { if (!string.IsNullOrWhiteSpace(text)) lines.Add(new CreditLine(k, text)); }

            Add(CreditLineKind.Title, Hanja);
            foreach (var m in NamedMakers)
            {
                Add(CreditLineKind.Heading, tr(MakerRoleKey(m), m.RoleKo));
                Add(CreditLineKind.Name, m.Name);
            }
            foreach (var s in Sections.Where(s => s.Items.Count > 0))
            {
                Add(CreditLineKind.Heading, tr(SectionTitleKey(s), s.TitleKo));
                foreach (var i in s.Items)
                {
                    Add(CreditLineKind.Use, tr(ItemUseKey(s, i), i.UseKo));
                    Add(CreditLineKind.Name, i.Name);
                    Add(CreditLineKind.Notice, i.Copyright);
                    var lic = License(i.License);
                    if (lic != null) Add(CreditLineKind.Notice, lic.Name);
                }
            }
            var used = Licenses.Where(l => Items.Any(i => i.License == l.Id)).ToList();
            if (used.Count > 0)
            {
                Add(CreditLineKind.Heading, tr(LicensesTitleKey, LicensesTitleKo));
                foreach (var l in used)
                {
                    Add(CreditLineKind.Name, l.Name);
                    Add(CreditLineKind.LicenseText, l.Text);
                }
            }
            Add(CreditLineKind.Thanks, tr(ThanksKey, ThanksKo));
            return lines;
        }
    }

    public sealed class CreditMakerDef
    {
        public string Id = "";
        public string RoleKo = "";
        /// <summary>이름(옮기지 않음). 빈 값이면 화면에서 건너뛴다.</summary>
        public string Name = "";
    }

    public sealed class CreditSectionDef
    {
        public string Id = "";
        public string TitleKo = "";
        public List<CreditItemDef> Items = new List<CreditItemDef>();
    }

    public sealed class CreditItemDef
    {
        public string Id = "";
        /// <summary>고유 이름(글꼴·엔진·라이브러리 이름, 옮기지 않음).</summary>
        public string Name = "";
        /// <summary>게임에서 무엇에 쓰나(본문, 데이터 읽기 …).</summary>
        public string UseKo = "";
        /// <summary>저작권 줄. 글꼴은 파일 안 저작권 줄(이름표 0번) 그대로.</summary>
        public string Copyright = "";
        /// <summary>Licenses 의 id. 고지할 라이선스가 없으면 빈 값.</summary>
        public string License = "";
        /// <summary>이 항목이 고지하는 것: 글꼴 파일 이름, manifest 패키지 id, 'unity', 소리 갈래('sfx:ai'·'sfx:layer'·'bgm:gemini').</summary>
        public List<string> Covers = new List<string>();
    }

    public sealed class CreditLicenseDef
    {
        public string Id = "";
        public string Name = "";
        /// <summary>원문. 문단은 빈 줄로 나눈다.</summary>
        public string Text = "";
    }

    public enum CreditLineKind { Title, Heading, Use, Name, Notice, LicenseText, Thanks }

    public readonly struct CreditLine
    {
        public readonly CreditLineKind Kind;
        public readonly string Text;
        public CreditLine(CreditLineKind kind, string text) { Kind = kind; Text = text; }
        public override string ToString() => $"{Kind}: {Text}";
    }
}
