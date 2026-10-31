// 사용법(저장소 루트에서):
//   dotnet run --project core/BloodRain.Sim -- --policy average --seed 1        한 판을 돌려 분당 표를 찍는다
//   dotnet run --project core/BloodRain.Sim -- --policy greedy --seeds 30       여러 판 요약(5·10·15·20분 레벨)
//   dotnet run --project core/BloodRain.Sim -- --report docs/design/sim/level-curve.md   보고서 전체를 다시 쓴다
//   dotnet run --project core/BloodRain.Sim -- --synergy docs/design/sim/synergy.md     연환·진전 화력 상한(S-05)
//   dotnet run --project core/BloodRain.Sim -- --cheonha docs/design/sim/cheonha.md     천하 한 생 30분(T-17)
//   dotnet run --project core/BloodRain.Sim -- --meta docs/design/sim/meta.md           생을 이어 메타 곡선(T-18)
//   dotnet run --project core/BloodRain.Sim -- --cluster-xp docs/design/sim/cluster-xp.md  천하에서 조정 시험별 레벨 속도(T-19)
//   dotnet run --project core/BloodRain.Sim -- --encounters docs/design/sim/encounters.md  천하 한 생의 기연 사건 출현율과 도감 채우기
//   dotnet run --project core/BloodRain.Sim -- --contact docs/design/sim/contact.md        맞는 쪽 셈(한 번 피해, 둘러싸여 버티는 초, 회복)
//   dotnet run --project core/BloodRain.Sim -c Release -- --yinyang docs/design/sim/yinyang.md  음양 게이지가 머무는 구간, 기혈역류 횟수
//   dotnet run --project core/BloodRain.Sim -c Release -- --insights docs/design/sim/insights.md  돌파 깨달음이 보일 확률과 값어치
//   dotnet run --project core/BloodRain.Sim -c Release -- --decisions docs/design/sim/decisions.md  사람 결정 묶음: P-02 에 따라 T-40·T-41 이 어떻게 움직이나
//   dotnet run --project core/BloodRain.Sim -c Release -- --blood-art docs/design/sim/blood-art.md  마교 몸의 혈공: 경지 도달 시각, 심마와 천마해체
//   dotnet run --project core/BloodRain.Sim -c Release -- --rebirth docs/design/sim/rebirth.md      침방 환생 후보 분포, 구파윤회까지 환혼 수
//   dotnet run --project core/BloodRain.Sim -- --gimmicks docs/design/sim/gimmicks.md                지역 기믹의 기혈 수지(회복·독안개·낙석·혈제단)
// 그 밖에: --data <데이터 폴더>, --minutes <분>, 무리 가정 바꿔 보기 --crowd-radius <m> --max-density <마리/㎡> --arrival <초>
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BloodRain.Sim
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            string policy = "average", report = "", scenario = "baseline", synergy = "", cheonha = "", meta = "", clusterXp = "", encounters = "", contact = "", yinyang = "", bloodArt = "", rebirth = "", gimmicks = "", insights = "", decisions = "";
            string? dataDir = null;
            int seed = 1, seeds = 0;
            double minutes = Assumptions.RunMinutes;
            double? crowdRadius = null, maxDensity = null, arrival = null;
            for (int i = 0; i < args.Length; i++)
            {
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} 뒤에 값이 없습니다");
                switch (args[i])
                {
                    case "--policy": policy = Next(); break;
                    case "--seed": seed = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--seeds": seeds = int.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--minutes": minutes = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--data": dataDir = Next(); break;
                    case "--report": report = Next(); break;
                    case "--scenario": scenario = Next(); break;
                    case "--synergy": synergy = Next(); break;
                    case "--cheonha": cheonha = Next(); break;
                    case "--meta": meta = Next(); break;
                    case "--cluster-xp": clusterXp = Next(); break;
                    case "--encounters": encounters = Next(); break;
                    case "--contact": contact = Next(); break;
                    case "--yinyang": yinyang = Next(); break;
                    case "--insights": insights = Next(); break;
                    case "--blood-art": bloodArt = Next(); break;
                    case "--decisions": decisions = Next(); break;
                    case "--rebirth": rebirth = Next(); break;
                    case "--gimmicks": gimmicks = Next(); break;
                    case "--crowd-radius": crowdRadius = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--max-density": maxDensity = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "--arrival": arrival = double.Parse(Next(), CultureInfo.InvariantCulture); break;
                    case "-h":
                    case "--help":
                        Console.WriteLine("dotnet run --project core/BloodRain.Sim -- [--policy average|greedy|slow] [--seed N] [--seeds N] [--report 경로] [--data 폴더] [--minutes 분]");
                        return 0;
                    default:
                        Console.Error.WriteLine($"모르는 인자입니다: {args[i]} (--help)");
                        return 2;
                }
            }

            var sc = Scenarios.Find(scenario);
            var data = sc.LoadData(dataDir);
            if (gimmicks.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(gimmicks))!);
                File.WriteAllText(gimmicks, GimmickSim.Report(data), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {gimmicks}");
                return 0;
            }
            if (rebirth.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rebirth))!);
                File.WriteAllText(rebirth, RebirthSim.Report(data), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {rebirth}");
                return 0;
            }
            if (decisions.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(decisions))!);
                File.WriteAllText(decisions, DecisionSim.Report(dataDir, seeds > 0 ? seeds : 3), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {decisions}");
                return 0;
            }
            if (bloodArt.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(bloodArt))!);
                File.WriteAllText(bloodArt, BloodArtSim.Report(data, seeds > 0 ? seeds : 5), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {bloodArt}");
                return 0;
            }
            if (insights.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(insights))!);
                File.WriteAllText(insights, InsightSim.Report(data, seeds > 0 ? seeds : 5), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {insights}");
                return 0;
            }
            if (yinyang.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(yinyang))!);
                File.WriteAllText(yinyang, YinYangSim.Report(data, seeds > 0 ? seeds : 10), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {yinyang}");
                return 0;
            }
            if (contact.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(contact))!);
                File.WriteAllText(contact, ContactSim.Report(data), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {contact}");
                return 0;
            }
            if (encounters.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(encounters))!);
                File.WriteAllText(encounters, EncounterSim.Report(data, seeds > 0 ? seeds : 3), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {encounters}");
                return 0;
            }
            if (clusterXp.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(clusterXp))!);
                File.WriteAllText(clusterXp, CheonhaSim.ClusterXpReport(dataDir, seeds > 0 ? seeds : 5), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {clusterXp}");
                return 0;
            }
            if (meta.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(meta))!);
                File.WriteAllText(meta, MetaSim.Report(data), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {meta}");
                return 0;
            }
            if (cheonha.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cheonha))!);
                File.WriteAllText(cheonha, CheonhaSim.Report(data, seeds > 0 ? seeds : 5), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {cheonha}");
                return 0;
            }
            if (synergy.Length > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(synergy))!);
                File.WriteAllText(synergy, SynergyCheck.Report(data), new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {synergy}");
                return 0;
            }
            if (report.Length > 0)
            {
                string text = Report.Build(dataDir);
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
                File.WriteAllText(report, text, new UTF8Encoding(false));
                Console.WriteLine($"보고서를 썼습니다: {report}");
                return 0;
            }

            var opt = sc.ApplyOptions(new SimOptions
            {
                Policy = policy, Seed = seed, Minutes = minutes,
                CrowdRadius = crowdRadius, MaxDensity = maxDensity, ArrivalSeconds = arrival,
            });
            if (seeds > 0)
            {
                var runs = Enumerable.Range(1, seeds).Select(s => RunSimulator.Run(data, opt.With(o => o.Seed = s))).ToList();
                Console.WriteLine(Report.Summary(runs));
                return 0;
            }

            var result = RunSimulator.Run(data, opt);
            Console.WriteLine(Report.SingleRun(result));
            return 0;
        }
    }
}
