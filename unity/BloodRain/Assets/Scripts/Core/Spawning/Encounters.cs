#nullable enable
// 기연 사건 추첨(기획서 20장). 확률은 '한 판에서 그 사건이 한 번이라도 나올 확률'이라 사건마다 판에 한 번만 굴린다.
//   확률 = 기본 확률 + 기연 능력치 × luck.chancePerLuck (+ 진전 같은 데서 오는 덧셈), 0~1로 자른다.
//   어느 장에서 나오는지·몇 분부터인지는 synergies.json encounters[] 의 chapters·minMinute·needsChapterSchool.
//   조건이 아직 안 맞으면(예: 15분 전의 혈마의 속삭임) 굴리지 않고 남겨 두었다가 조건이 맞을 때 굴린다.
//   언제 굴릴지는 게임이 정한다: 판 시작에 RollAll 한 번, 그 뒤 1분마다 RollAll 처럼.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Spawning
{
    /// <summary>지금 판의 형편. 굴릴 때마다 새로 채워 넘긴다.</summary>
    public sealed class EncounterContext
    {
        /// <summary>지금 장(cheonha.json chapter 값: prologue, chapter1 … final). 외전처럼 장이 아니면 null.</summary>
        public string? Chapter;
        /// <summary>그 땅의 문파(cheonha.json regions[].sect). 없으면 null. 고수의 유해가 이 문파 진전을 하나 더 준다.</summary>
        public string? ChapterSchool;
        /// <summary>게임 시간(분).</summary>
        public float Minute;
        /// <summary>기연 능력치.</summary>
        public float Luck;
        /// <summary>사건 id → 더할 확률(마도 진전 2의 혈마의 속삭임처럼).</summary>
        public IReadOnlyDictionary<string, double>? BonusChance;
    }

    /// <summary>판 하나의 기연 사건 추첨기. 같은 시드의 Random 이면 같은 결과가 나온다.</summary>
    public sealed class EncounterRoller
    {
        readonly SynergyDefs _syn;
        readonly Random _rng;
        readonly HashSet<string> _rolled = new HashSet<string>();
        readonly List<string> _appeared = new List<string>();

        public EncounterRoller(GameData data, Random rng)
        {
            _syn = data.Synergies;
            _rng = rng;
        }

        /// <summary>이번 판에 나오기로 정해진 사건(정해진 순서).</summary>
        public IReadOnlyList<string> Appeared => _appeared;
        public bool HasRolled(string id) => _rolled.Contains(id);
        public bool HasAppeared(string id) => _appeared.Contains(id);

        /// <summary>지금 장·시간에 나올 수 있는 사건인가.</summary>
        public static bool CanAppear(EncounterDef e, EncounterContext ctx)
        {
            if (e.Chapters != null && (ctx.Chapter == null || !e.Chapters.Contains(ctx.Chapter))) return false;
            if (ctx.Minute < e.MinMinute) return false;
            if (e.NeedsChapterSchool && ctx.ChapterSchool == null) return false;
            return true;
        }

        /// <summary>지금 굴리면 나올 확률. 나올 수 없는 형편이면 0.</summary>
        public double Chance(EncounterDef e, EncounterContext ctx)
        {
            if (!CanAppear(e, ctx)) return 0;
            double p = e.Chance + ctx.Luck * _syn.Luck.ChancePerLuck;
            if (ctx.BonusChance != null && ctx.BonusChance.TryGetValue(e.Id, out var bonus)) p += bonus;
            return Math.Max(0, Math.Min(1, p));
        }

        /// <summary>사건 하나를 굴린다. 판마다 한 번이라 이미 굴렸으면 false.
        /// 지금 나올 수 없는 형편이면 굴리지 않고 false(나중에 다시 굴릴 수 있다).</summary>
        public bool Roll(string id, EncounterContext ctx)
        {
            var e = _syn.Encounter(id);
            if (_rolled.Contains(id) || !CanAppear(e, ctx)) return false;
            _rolled.Add(id);
            // 확률과 상관없이 늘 한 번 뽑는다. 그래야 같은 시드에서 기연이 높을 때 나오는 사건이 줄지 않는다.
            bool hit = _rng.NextDouble() < Chance(e, ctx);
            if (hit && !_appeared.Contains(id)) _appeared.Add(id);
            return hit;
        }

        /// <summary>아직 안 굴린 사건 중 지금 나올 수 있는 것을 모두 굴려, 이번에 나오기로 된 것을 돌려준다(데이터 순서).</summary>
        public List<EncounterDef> RollAll(EncounterContext ctx)
        {
            var result = new List<EncounterDef>();
            foreach (var e in _syn.Encounters)
                if (Roll(e.Id, ctx)) result.Add(e);
            return result;
        }

        /// <summary>반드시 나오게 한다(비전 '혈해마공 잔편' → 혈마의 속삭임). 앞서 굴려 안 나왔어도 나온다.
        /// 새로 나오게 됐으면 true, 이미 나오기로 되어 있었으면 false. 언제 놓을지(minMinute)는 게임이 지킨다.</summary>
        public bool Force(string id)
        {
            _syn.Encounter(id);
            _rolled.Add(id);
            if (_appeared.Contains(id)) return false;
            _appeared.Add(id);
            return true;
        }

        /// <summary>판 하나를 통째로 굴려 본다(밸런스 시뮬레이션용). 장과 기연이 판 내내 같다고 보고, 마지막 분까지 조건이 맞는 것을 다 굴린다.</summary>
        public static List<string> SimulateRun(GameData data, Random rng, string? chapter, string? chapterSchool, float luck, float runMinutes = 20)
        {
            var roller = new EncounterRoller(data, rng);
            var ctx = new EncounterContext { Chapter = chapter, ChapterSchool = chapterSchool, Luck = luck };
            var mins = data.Synergies.Encounters.Select(e => e.MinMinute).Where(m => m <= runMinutes).Append(0f).Distinct().OrderBy(m => m);
            foreach (var m in mins)
            {
                ctx.Minute = m;
                roller.RollAll(ctx);
            }
            return roller.Appeared.ToList();
        }
    }
}
