using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static BloodRain.Core.Tests.TestData;

namespace BloodRain.Core.Tests
{
    /// <summary>문서가 가리키는 저장소 파일이 실제로 있는지. 아침에 사람이 `docs/unity_shots/...png` 나
    /// `tools/...py` 를 열어 보러 갔는데 이름이 바뀌어 없으면 헛걸음이다. 파일을 옮기거나 지우면 문서도 같이 고친다.
    /// 일부러 없는 경로(틀 이름, .gitignore 된 비밀 설정)는 아래 Allowed 에 둔다.</summary>
    public class DocLinkTests
    {
        static readonly string[] Allowed =
        {
            "docs/daily/YYYY-MM-DD.md",      // 날짜 틀
            "tools/agent_loop/config.json",  // .gitignore (비밀값)
            "docs/session_lock.md",          // 세션이 일하는 동안만 있는 잠금 파일
        };

        static readonly Regex RepoPath = new Regex(
            @"(?<![\w/.-])(?:docs|tools|web|art_queue|unity|core)/[A-Za-z0-9_./-]+\.(?:png|md|json|jsonl|py|cs|html|yml|shader)\b");

        static readonly Regex AssetPath = new Regex(
            @"(?<![\w/.-])Assets/[A-Za-z0-9_./-]+\.(?:cs|json|png|shader|unity|asmdef)\b");

        static IEnumerable<string> Docs()
        {
            var root = RepoRoot;
            yield return Path.Combine(root, "CLAUDE.md");
            foreach (var dir in new[] { "docs", Path.Combine("docs", "store"), Path.Combine("docs", "design"), Path.Combine("docs", "design", "sim"), Path.Combine("docs", "daily") })
            {
                var full = Path.Combine(root, dir);
                if (!Directory.Exists(full)) continue;
                foreach (var f in Directory.GetFiles(full, "*.md")) yield return f;
            }
            var webReadme = Path.Combine(root, "web", "prototype", "README.md");
            if (File.Exists(webReadme)) yield return webReadme;
        }

        static List<string> Missing(Regex re, string prefix)
        {
            var root = RepoRoot;
            var missing = new List<string>();
            foreach (var doc in Docs())
            {
                var text = ReadText(doc);
                foreach (Match m in re.Matches(text))
                {
                    var p = m.Value.TrimEnd('.');
                    if (Allowed.Contains(p)) continue;
                    if (!File.Exists(Path.Combine(root, prefix, p.Replace('/', Path.DirectorySeparatorChar))))
                        missing.Add($"{Path.GetFileName(doc)}: {p}");
                }
            }
            return missing.Distinct().ToList();
        }

        [Fact]
        public void 문서가_가리키는_저장소_파일이_있다()
        {
            var missing = Missing(RepoPath, "");
            Assert.True(missing.Count == 0, "문서에 적혔는데 저장소에 없는 파일:\n" + string.Join("\n", missing));
        }

        [Fact]
        public void 문서가_가리키는_Unity_에셋이_있다()
        {
            var missing = Missing(AssetPath, Path.Combine("unity", "BloodRain"));
            Assert.True(missing.Count == 0, "문서에 적혔는데 Unity 프로젝트에 없는 에셋:\n" + string.Join("\n", missing));
        }

        [Fact]
        public void 검사가_실제로_경로를_잡는다()
        {
            // 정규식이 아무것도 못 잡아 늘 통과하는 일이 없게
            var text = ReadText(Path.Combine(RepoRoot, "CLAUDE.md"));
            Assert.Contains(RepoPath.Matches(text).Cast<Match>(), m => m.Value == "docs/backlog.md");
            Assert.Contains(RepoPath.Matches(text).Cast<Match>(), m => m.Value == "tools/bake_brush.py");
        }
    }
}
