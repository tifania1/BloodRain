// 지역 기믹의 기혈 수지(밸런스 시뮬레이션). 보고서: docs/design/sim/gimmicks.md
//
// contact.md 는 적에게 맞는 쪽만 셌다. 여기서는 땅에 깔린 기믹이 기혈을 얼마나 채우고 깎는지를
// '몸 기혈의 몇 %'와 '그 땅에서 맨몸으로 맞는 한 대가 몇 번'으로 바꿔 본다. 수치와 판정은 gimmicks.json 과 Core World 그대로다.
//   회복: 우물(WellGimmick), 술독(BreakableGimmick.RollDrop), 매화 더미(PlumHeapGimmick), 모닥불(CampfireGimmick).
//   피해: 독안개(PoisonFogGimmick 시계·반경), 낙석(FallingRocksGimmick 자리·판정), 혈제단(BloodAltarGimmick 켜진 수·피해 배율).
// 움직임은 넣지 않는다. 회복은 '쓸 수 있을 때마다 쓴다', 피해는 '그 자리에 가만히 있는다'로 센다(둘 다 가장 큰 쪽).
// 축척 배율은 1(기믹이 생긴 경지 화면)로 본다. 반경이 같이 커지므로 비율은 배율과 상관없다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using BloodRain.Core.Data;
using BloodRain.Core.Spawning;
using BloodRain.Core.World;

namespace BloodRain.Sim
{
    public static class GimmickSim
    {
        /// <summary>견주는 몸 기혈. contact.md 의 '몸 기혈 100' 표와 같다.</summary>
        public const float BodyHealth = 100f;
        public static readonly float[] Minutes = { 10, 30 };

        /// <summary>술독 하나를 깼을 때 기대 회복(몸 기혈 비율). RollDrop 을 n번 굴린 평균.</summary>
        public static double WineJarExpected(GameData g, int n = 20000, int seed = 1)
        {
            var rng = new Random(seed);
            double sum = 0;
            for (int i = 0; i < n; i++) sum += new BreakableGimmick(g.Gimmicks.WineJars).RollDrop(rng).heal;
            return sum / n;
        }

        /// <summary>매화 더미 하나를 다시 필 때마다 먹으면 1분에 회복(몸 기혈 비율).</summary>
        public static double PlumPerMinute(GameData g)
        {
            var heap = new PlumHeapGimmick(g.Gimmicks.PlumHeaps);
            double healed = 0;
            const float dt = 0.1f;
            for (float t = 0; t < 600f - 1e-3f; t += dt)
            {
                healed += heap.TryUse(0f, 1f);
                heap.Tick(dt);
            }
            return healed / 10.0;
        }

        /// <summary>모닥불 안에 1분 서 있으면 회복(몸 기혈 비율).</summary>
        public static double CampfirePerMinute(GameData g) => CampfireGimmick.Heal(g.Gimmicks.Campfire, 0f, 1f, 60f);

        /// <summary>독안개 한 번 퍼질 때 샘에서 distance 떨어진 자리에 가만히 있으면 잃는 기혈(비율)과 안개 안에 있던 초.</summary>
        public static (double Loss, double Seconds) FogEpisode(GameData g, float distance)
        {
            var def = g.Gimmicks.PoisonFog;
            var fog = new PoisonFogGimmick(def);
            double loss = 0, inside = 0;
            const float dt = 0.01f;
            float start = def.FirstSpreadSeconds;
            for (float t = start; t < start + def.PeriodSeconds; t += dt)
            {
                if (!fog.Contains(distance, t)) continue;
                loss += fog.HealthLoss(GimmickTargetKind.Player, 1f, dt);
                inside += dt;
            }
            return (loss, inside);
        }

        /// <summary>낙석: 가만히 있는 플레이어가 1분에 맞는 기대 횟수(자리 n번 굴림)와 식으로 셈한 값.</summary>
        public static (double Simulated, double Formula) RockHitsPerMinute(GameData g, int falls = 20000, int seed = 1)
        {
            var def = g.Gimmicks.FallingRocks;
            var rng = new Random(seed);
            int hits = 0;
            for (int i = 0; i < falls; i++)
                foreach (var p in FallingRocksGimmick.Places(def, Vector2.Zero, rng))
                    if (FallingRocksGimmick.Hits(def, p, Vector2.Zero)) hits++;
            double perFall = (double)hits / falls;
            double fallsPerMin = 60.0 / def.PeriodSeconds;
            double side = 2.0 * def.Scatter;
            double formula = def.Count * Math.PI * def.Radius * def.Radius / (side * side) * fallsPerMin;
            return (perFall * fallsPerMin, formula);
        }

        /// <summary>그 땅·그 분에 맨몸으로 닿는 적 평균 한 번 피해(contact.md 와 같은 셈).</summary>
        public static double HitAt(GameData g, WaveTable wt, RegionSpawns rs, string regionId, float minute, float damageMul = 1f)
        {
            var region = g.Cheonha.Regions.First(r => r.Id == regionId);
            var (avg, _, _) = ContactSim.BaseContact(g, wt, rs, regionId, minute);
            return ContactSim.PerHit(g, avg * damageMul, minute, region.Density, 0f, 0f);
        }

        /// <summary>주기 날씨가 켜져 있는 시간 몫(30분 동안).</summary>
        public static double WeatherShare(WeatherGimmickDef def, float minutes = 30f)
        {
            int on = 0, n = 0;
            for (float t = 0; t < minutes * 60f; t += 0.5f, n++) if (WeatherGimmick.ActiveAt(def, t)) on++;
            return (double)on / n;
        }

        /// <summary>범종을 울릴 수 있을 때마다 울리면 30분 동안 울린 횟수와 잡몹이 기절해 있는 시간 몫.</summary>
        public static (int Rings, double StunShare) BellUse(GameData g, float minutes = 30f)
        {
            var bell = new BellGimmick(g.Gimmicks.Bell);
            int rings = 0; double stunned = 0, left = 0;
            const float dt = 0.1f;
            for (float t = 0; t < minutes * 60f - 1e-3f; t += dt)
            {
                if (bell.TryRing(0f)) { rings++; left = bell.StunSeconds(GimmickTargetKind.Normal, 0f); }
                if (left > 0) { stunned += dt; left -= dt; }
                bell.Tick(dt);
            }
            return (rings, stunned / (minutes * 60.0));
        }

        /// <summary>태극 바닥을 밟을 수 있을 때마다 밟으면(게이지는 0이 아니라고 본다) 반대 속성 기운이 걸려 있는 시간 몫.</summary>
        public static double TaijiUptime(GameData g, float minutes = 30f)
        {
            var floor = new TaijiFloorGimmick(g.Gimmicks.TaijiFloor);
            var bless = new TaijiBlessing(g.Gimmicks.TaijiFloor);
            int on = 0, n = 0;
            const float dt = 0.1f;
            for (float t = 0; t < minutes * 60f - 1e-3f; t += dt, n++)
            {
                floor.TryStep(0f, 1, bless);
                if (bless.Active) on++;
                floor.Tick(dt); bless.Tick(dt);
            }
            return (double)on / n;
        }

        public static string Report(SimData data)
        {
            var g = data.Game;
            var gm = g.Gimmicks;
            var wt = WaveTable.From(g);
            var rs = new RegionSpawns(g);
            string P(double x) => (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
            string F1(double x) => x.ToString("0.0", CultureInfo.InvariantCulture);
            string F2(double x) => x.ToString("0.00", CultureInfo.InvariantCulture);
            string RegionKo(string id) => g.Cheonha.Regions.First(r => r.Id == id).NameKo;
            float Dens(string id) => g.Cheonha.Regions.First(r => r.Id == id).Density;
            string Hits(string id, double hp) =>
                string.Join(" / ", Minutes.Select(m => F1(hp / HitAt(g, wt, rs, id, m))));
            string HitList(string id) =>
                string.Join(" / ", Minutes.Select(m => F1(HitAt(g, wt, rs, id, m))));
            double recMaxPerMin = ContactSim.RecoveryMax(g) * 60.0;

            double well = gm.Well.HealMaxHealthRatio * gm.Well.Uses;
            double jar = WineJarExpected(g);
            double plum = PlumPerMinute(g);
            double fire = CampfirePerMinute(g);
            var fog0 = FogEpisode(g, 0f);
            var fogHalf = FogEpisode(g, gm.PoisonFog.Radius * 0.5f);
            double fogPerMin = fog0.Loss * 60.0 / gm.PoisonFog.PeriodSeconds;
            var rocks = RockHitsPerMinute(g);
            double rockPerMin = rocks.Simulated * gm.FallingRocks.PlayerDamageMaxHealthRatio;
            var alt = gm.BloodAltars;

            var sb = new StringBuilder();
            sb.AppendLine("# 지역 기믹의 기혈 수지");
            sb.AppendLine();
            sb.AppendLine("`dotnet run --project core/BloodRain.Sim -- --gimmicks docs/design/sim/gimmicks.md` 로 다시 만든다(`GimmickSim.cs`).");
            sb.AppendLine();
            sb.AppendLine("[contact.md](contact.md) 는 적에게 맞는 쪽만 셌다. 여기서는 땅에 깔린 기믹이 기혈을 얼마나 채우고 깎는지 본다. 수치는 gimmicks.json, 판정은 Core `World/Gimmicks`·`Gimmicks2` 그대로다. 움직임은 넣지 않는다. 회복은 '쓸 수 있을 때마다 쓴다', 피해는 '그 자리에 가만히 있는다'로 셌다. 둘 다 가장 큰 쪽이다.");
            sb.AppendLine();
            sb.AppendLine($"몸 기혈은 {BodyHealth:0}(contact.md 표와 같다). 기믹 회복·피해는 모두 최대 기혈 비율이라 몸이 달라도 % 는 같고, '한 대'로 바꾼 값만 몸 기혈에 비례한다. '한 대'는 그 땅 {string.Join("·", Minutes.Select(m => m + "분"))}에 닿는 적 평균에게 맨몸(호신 0)으로 한 번 맞는 피해다. contact.md 첫 표 맨몸 칸과 같은 셈인데 화경 받는 피해 −10%는 넣지 않았다(그래서 30분 칸이 contact.md 보다 10%쯤 크다). 축척 배율은 1로 봤다(반경이 같이 커지니 비율은 그대로다).");
            sb.AppendLine();

            sb.AppendLine("## 회복 기믹");
            sb.AppendLine();
            sb.AppendLine($"칸 '한 대로'는 회복량이 그 땅 {string.Join(" / ", Minutes.Select(m => m + "분"))} 맨몸 몇 대인지.");
            sb.AppendLine();
            sb.AppendLine("| 기믹(지역, 농도) | 셈 | 회복 | 한 대로 | 그 땅 한 대 |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            sb.AppendLine($"| 우물({RegionKo("cheongun")}, {F1(Dens("cheongun"))}) | 한 번({gm.Well.Uses}회) | {P(well)} | {Hits("cheongun", well * BodyHealth)} | {HitList("cheongun")} |");
            sb.AppendLine($"| 술독({RegionKo("gaebang_hq")}, {F1(Dens("gaebang_hq"))}) | 하나 깰 때 기대값({P(gm.WineJars.DropChance)} × {P(gm.WineJars.HealMaxHealthRatio)}) | {P(jar)} | {Hits("gaebang_hq", jar * BodyHealth)} | {HitList("gaebang_hq")} |");
            sb.AppendLine($"| 매화 더미({RegionKo("huashan")}, {F1(Dens("huashan"))}) | 더미 하나, 필 때마다({gm.PlumHeaps.RegrowSeconds:0}초) 1분 | {P(plum)} | {Hits("huashan", plum * BodyHealth)} | {HitList("huashan")} |");
            sb.AppendLine($"| 모닥불({RegionKo("kunlun")}, {F1(Dens("kunlun"))}) | 반경 {gm.Campfire.Radius:0}m 안 1분 | {P(fire)} | {Hits("kunlun", fire * BodyHealth)} | {HitList("kunlun")} |");
            sb.AppendLine();
            sb.AppendLine($"견줄 것: 회복 능력치 최대(연무장 회복 다 사고 매화심법 Lv5, 초당 {F2(ContactSim.RecoveryMax(g))})는 1분에 {recMaxPerMin:0}, 몸 기혈 {BodyHealth:0}의 {P(recMaxPerMin / BodyHealth)}다.");
            sb.AppendLine();

            sb.AppendLine("## 피해 기믹");
            sb.AppendLine();
            sb.AppendLine("| 기믹(지역, 농도) | 셈 | 잃는 기혈 | 한 대로 |");
            sb.AppendLine("| --- | --- | --- | --- |");
            sb.AppendLine($"| 독안개({RegionKo("danggata")}, {F1(Dens("danggata"))}) | 샘 바로 위, 한 번 퍼질 때(안개 안 {fog0.Seconds:0}초) | {P(fog0.Loss)} | {Hits("danggata", fog0.Loss * BodyHealth)} |");
            sb.AppendLine($"| 독안개 | 샘에서 반경 절반({gm.PoisonFog.Radius * 0.5f:0}m), 한 번 퍼질 때(안개 안 {fogHalf.Seconds:0}초) | {P(fogHalf.Loss)} | {Hits("danggata", fogHalf.Loss * BodyHealth)} |");
            sb.AppendLine($"| 독안개 | 샘 바로 위에 머물면 1분 평균({gm.PoisonFog.PeriodSeconds:0}초마다) | {P(fogPerMin)} | {Hits("danggata", fogPerMin * BodyHealth)} |");
            sb.AppendLine($"| 낙석({RegionKo("huashan")}, {F1(Dens("huashan"))}) | 가만히 서서 1분(맞는 횟수 {F2(rocks.Simulated)}, 식 {F2(rocks.Formula)}) | {P(rockPerMin)} | {Hits("huashan", rockPerMin * BodyHealth)} |");
            sb.AppendLine();
            sb.AppendLine($"독안개 안에선 얻는 진기가 {gm.PoisonFog.QiMultiplier:0}배다. 샘 바로 위에 있으면 {gm.PoisonFog.PeriodSeconds:0}초 가운데 {fog0.Seconds:0}초({P(fog0.Seconds / gm.PoisonFog.PeriodSeconds)}) 동안 진기 {gm.PoisonFog.QiMultiplier:0}배를 받는 값으로 한 번에 {P(fog0.Loss)}를 낸다.");
            sb.AppendLine($"낙석은 {gm.FallingRocks.TelegraphSeconds:0}초 그림자 예고가 있어 피할 수 있고, 맞은 잡몹은 쓰러진다. 위 값은 하나도 안 피할 때다.");
            sb.AppendLine();

            sb.AppendLine($"## 혈제단({RegionKo("sipmandaesan")}, {F1(Dens("sipmandaesan"))})");
            sb.AppendLine();
            sb.AppendLine($"한 생 {alt.FirstLightSeconds / 60:0}분에 첫 제단이 켜지고 {alt.LightEverySeconds / 60:0}분마다 하나씩, {alt.Count}개까지. 켜진 제단 {alt.BuffRadius:0}m 안의 적은 피해 {alt.EnemyDamageMultiplier:0.00}배·이동 {alt.EnemyMoveSpeedMultiplier:0.00}배. 제단 기혈 {alt.Health:0}.");
            sb.AppendLine();
            sb.AppendLine("| 분 | 켜진 제단 | 맨몸 한 대(제단 밖) | 제단 곁 | 몸 기혈 100으로 몇 대(밖 / 곁) |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (float m in new float[] { 2, 4, 8, 10, 20, 30 })
            {
                int lit = BloodAltarGimmick.LitCount(alt, m * 60f);
                double outHit = HitAt(g, wt, rs, "sipmandaesan", m);
                double inHit = HitAt(g, wt, rs, "sipmandaesan", m, BloodAltarGimmick.EnemyDamage(alt, true));
                sb.AppendLine($"| {m:0} | {lit} | {F1(outHit)} | {F1(inHit)} | {F1(BodyHealth / outHit)} / {F1(BodyHealth / inHit)} |");
            }
            sb.AppendLine();

            var bell = BellUse(g);
            double taiji = TaijiUptime(g);
            double bellHits = ContactSim.SurroundCount / g.Balance.Contact.SameEnemyIntervalSeconds * gm.Bell.StunSeconds;
            double surv = BodyHealth / ContactSim.SurroundedDps(g, HitAt(g, wt, rs, "songshan", 30));
            sb.AppendLine("## 숨 돌리는 기믹");
            sb.AppendLine();
            sb.AppendLine($"- 범종({RegionKo("songshan")}): 반경 {gm.Bell.StunRadius:0}m 잡몹·정예 {gm.Bell.StunSeconds:0}초(보스 {gm.Bell.BossStunSeconds:0.0}초) 기절, {gm.Bell.CooldownSeconds:0}초마다. 울릴 수 있을 때마다 울리면 30분에 {bell.Rings}번, 잡몹이 멈춰 있는 시간은 {P(bell.StunShare)}다. 둘러싸인 채 울리면 {gm.Bell.StunSeconds:0}초 동안 받을 {bellHits:0}대(6마리 × 0.35초마다)를 안 받는다. 숭산 30분 맨몸으로 둘러싸여 버티는 {F1(surv)}초(몸 기혈 {BodyHealth:0})의 {gm.Bell.StunSeconds / surv:0}배이니, 포위를 풀고 빠져나갈 틈으로 넉넉하다.");
            sb.AppendLine($"- 태극 바닥({RegionKo("wudangsan")}): 밟으면 {gm.TaijiFloor.DurationSeconds:0}초 동안 게이지와 반대 속성 무공 피해 +{P(gm.TaijiFloor.OppositeAspectDamageBonus)}, {gm.TaijiFloor.CooldownSeconds:0}초마다. 밟을 수 있을 때마다 밟으면 걸려 있는 몫은 {P(taiji)}다. [yinyang.md](yinyang.md) 대로 게이지가 대개 양으로 기우니 오르는 쪽은 주로 음 무공이다.");
            sb.AppendLine();

            double redShare = WeatherShare(gm.RedMoon), snowShare = WeatherShare(gm.Blizzard);
            sb.AppendLine("## 주기 날씨");
            sb.AppendLine();
            sb.AppendLine($"30분 가운데 켜져 있는 몫: 붉은 달({RegionKo("sipmandaesan")}, 적 이동 {gm.RedMoon.EnemyMoveSpeedMultiplier:0.0}배) {P(redShare)}, 눈보라({RegionKo("kunlun")}, 시야 {gm.Blizzard.ViewScale:0.0}배·양쪽 이동 {gm.Blizzard.PlayerMoveSpeedMultiplier:0.0}배) {P(snowShare)}.");
            sb.AppendLine();

            // 읽을 것: 숫자는 위에서 셈한 값으로 넣는다.
            double fire30 = fire * BodyHealth / HitAt(g, wt, rs, "kunlun", 30);
            double plum30 = plum * BodyHealth / HitAt(g, wt, rs, "huashan", 30);
            double rock30 = rockPerMin * BodyHealth / HitAt(g, wt, rs, "huashan", 30);
            sb.AppendLine("## 읽을 것");
            sb.AppendLine();
            sb.AppendLine($"- 기믹 회복은 최대 기혈 비율이라 contact.md 가 짚은 '고농도 30분엔 회복이 의미 없다'를 조금 덜어 준다. 모닥불 안 1분({P(fire)})은 곤륜 30분 맨몸 {F1(fire30)}대, 매화 더미 하나는 1분에 화산 30분 {F1(plum30)}대분이다. 회복 능력치 최대(1분 {recMaxPerMin:0})와 견주면 몸 기혈 {BodyHealth:0}에선 모닥불({fire * BodyHealth:0})이 {(fire * BodyHealth < recMaxPerMin ? "조금 작다" : "같거나 크다")}. 다만 기믹은 그 자리에 서 있어야 하고 능력치는 걸으면서 받는다.");
            sb.AppendLine($"- 술독 하나는 기대값 {P(jar)}다. 개방 총타 10분이면 맨몸 {F1(jar * BodyHealth / HitAt(g, wt, rs, "gaebang_hq", 10))}대분이라 깨고 다닐 값이 있다. 지역에 술독이 몇 개 놓이는지는 데이터에 없다(배치는 Unity 몫).");
            sb.AppendLine($"- 우물은 한 생 한 번 {P(well)}로 한 번에 받는 양이 가장 크다. {RegionKo("cheongun")} 0분 맨몸 {F1(well * BodyHealth / HitAt(g, wt, rs, "cheongun", 0))}대분이다.");
            sb.AppendLine($"- 독안개 샘 바로 위에 머물면 1분 평균 {P(fogPerMin)}를 잃고 그 몫으로 진기 {gm.PoisonFog.QiMultiplier:0}배를 받는다. 반경 절반 자리여도 한 번에 {P(fogHalf.Loss)}로 크게 줄지 않는다. 안개가 {gm.PoisonFog.SpreadSeconds:0}초 만에 다 퍼지니 덜 맞으려면 안개 밖으로 나가야 한다.");
            sb.AppendLine($"- 낙석은 하나도 안 피하면 1분 {P(rockPerMin)}(화산 30분 맨몸 {F1(rock30)}대분)다. {gm.FallingRocks.TelegraphSeconds:0}초 예고가 있으니 실제로는 이보다 적다.");
            sb.AppendLine($"- 혈제단 곁에선 십만대산 30분 맨몸 {F1(BodyHealth / HitAt(g, wt, rs, "sipmandaesan", 30, alt.EnemyDamageMultiplier))}대에 쓰러진다(밖은 {F1(BodyHealth / HitAt(g, wt, rs, "sipmandaesan", 30))}대). 마교 몸(90)이면 그보다 적다. 제단을 부수러 가는 길이 가장 위험하다는 뜻이다.");
            sb.AppendLine("- 기믹 수치는 2026-10-30 예약 세션이 정한 값(T-16)이다. 지금은 고치지 않고 플레이 결과를 본다.");
            return sb.ToString();
        }
    }
}
