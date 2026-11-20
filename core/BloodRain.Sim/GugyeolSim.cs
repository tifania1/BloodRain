// 구결(口訣) 시제품 확인(backlog GU-11o). 보고서: docs/design/sim/gugyeol.md
//
// 같은 문파 몸으로 두 생을 잇달아 산다고 치고, 앞 생의 고른 차례를 뒤 생이 구결(Core Build.GugyeolPlayer)로 몇 걸음이나 따르는지 센다.
// 한 판 무리 모형 없이 로드아웃만 돌린다: 레벨업마다 Loadout.RollLevelUp 으로 카드를 뽑고 평균 정책(Policies.AveragePolicy)이 고른다.
// 가정(이 파일에서 새로 정함):
//   - 처음부터 열린 무공(unlock start)과 심법 전부(심법엔 해금 조건이 없음)만 카드에 나온다. 행운 0, 돈오·연환은 보지 않는다.
//   - 레벨업 횟수는 숭산 첫 생 10분쯤(레벨 30)에 맞춰 29번. 초절정 돌파 7칸째는 넣지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BloodRain.Core.Build;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;

namespace BloodRain.Sim
{
    public static class GugyeolSim
    {
        public const int LevelUps = 29;

        public sealed class Pair
        {
            public int Steps;     // 앞 생 걸음 수
            public int Followed;  // 뒤 생이 따른 걸음 수
        }

        static List<Offer> Play(GameData d, Loadout l, Random rng, HashSet<string> unlocked, GugyeolPlayer? g)
        {
            var policy = new AveragePolicy();
            var ctx = new PolicyContext { Data = d, Loadout = l, Rng = rng };
            var taken = new List<Offer>();
            for (int i = 0; i < LevelUps; i++)
            {
                var offers = l.RollLevelUp(rng, 0, unlocked, null, g?.Next);
                if (offers.Count == 0) break;
                var pick = g?.Pick(offers) ?? policy.Choose(offers, ctx);
                l.Apply(pick);
                taken.Add(pick);
            }
            return taken;
        }

        public static Pair RunPair(GameData d, string sectId, int seed)
        {
            var c = d.Cheonha;
            var sect = c.Sect(sectId);
            var body = Rebirth.Body(c, new RebirthCandidate(sect, c.Region(sect.Region), Array.Empty<BirthTraitDef>()));
            var unlocked = d.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id)
                .Concat(d.Passives.Select(p => p.Id)).ToHashSet();
            var a = body.NewLoadout(d);
            Play(d, a, new Random(seed), unlocked, null);
            var g = new GugyeolPlayer(a.PickOrder);
            var b = body.NewLoadout(d);
            Play(d, b, new Random(seed + 10007), unlocked, g);
            return new Pair { Steps = a.PickOrder.Count, Followed = g.Done };
        }

        public static string Report(SimData data, int seeds = 30)
        {
            var d = data.Game;
            var sb = new StringBuilder();
            void L(string s = "") => sb.Append(s).Append('\n');
            string F(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
            L("# 구결이 몇 걸음이나 따르나(GU-11o)");
            L();
            L("`dotnet run --project core/BloodRain.Sim -- --gugyeol docs/design/sim/gugyeol.md` 로 다시 만든다.");
            L();
            L($"같은 문파 몸으로 두 생. 앞 생에 레벨업 {LevelUps}번(숭산 첫 생 10분쯤, 레벨 30)을 평균 정책으로 고르고, 뒤 생은 카드가 다르게 뜨는(난수가 다른) 판에서 구결로 앞부분을 따른다. " +
              "구결이 켜진 동안 카드 한 장이 다음 걸음 카드로 바뀌어 뜨고 그걸 고른다. 그 걸음을 지금 고를 수 없으면(칸이 참·최고 단계) 멈춘다(기획서 10장 '천하 확장: 전생의 공력', GU-11o1). 한 판 무리 모형 없이 로드아웃만 돌렸고, 처음부터 열린 무공·심법만, 행운 0.");
            L();
            L("| 몸 | 판 | 따른 걸음 가운데값 | 평균 | 0걸음 판 | 5걸음 이상 판 | 끝까지 |");
            L("|---|---|---|---|---|---|---|");
            var all = new List<int>();
            foreach (var sect in ChronicleSim.Bodies)
            {
                var pairs = Enumerable.Range(1, seeds).Select(s => RunPair(d, sect, s)).ToList();
                var f = pairs.Select(p => p.Followed).OrderBy(x => x).ToList();
                all.AddRange(f);
                L($"| {d.Cheonha.Sect(sect).NameKo} | {seeds} | {f[f.Count / 2]} | {F(f.Average())} | {f.Count(x => x == 0)} | {f.Count(x => x >= 5)} | {pairs.Count(p => p.Followed == p.Steps)} |");
            }
            all.Sort();
            L();
            L("## 읽기");
            L();
            int full = all.Count(x => x >= LevelUps);
            L($"- 모든 판 가운데값 {all[all.Count / 2]}걸음, 평균 {F(all.Average())}걸음, 끝까지 따른 판 {full}/{all.Count}. 구결 카드가 늘 한 장 끼어 뜨니(GU-11o1, decisions #66) 앞 생 차례를 거의 그대로 따른다.");
            L("- 끝까지 못 간 판은 어느 걸음에서 그 카드를 고를 수 없게 된 판이다(칸이 찼거나 이미 최고 단계 — 대오 카드는 한 번에 두 단계를 올리니 앞 생과 단계가 어긋날 수 있다). 거기서 멈추고 나머지는 사람이 고른다.");
            return sb.ToString();
        }
    }
}
