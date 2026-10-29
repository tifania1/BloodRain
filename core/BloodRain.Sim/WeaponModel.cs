// 무공 한 번 발동이 무리에 무엇을 하는지(무리 덮는 넓이, 타수, 피해). 동작(behavior)별 계산은
// 웹 프로토타입 fireWeapon 을 따랐고, 좌표 대신 '넓이 × 밀도'로 맞는 수를 센다(Assumptions 무리 모형).
// 1장 첫 판에서 쓸 수 있는 시작 무공 8종은 하나씩 옮겼고, 잠겨 있는 8종은 대충 잡았다(default).
using System;
using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using static BloodRain.Sim.Assumptions;

namespace BloodRain.Sim
{
    /// <summary>무공 한 번 발동 안의 한 획.</summary>
    public readonly struct Strike
    {
        /// <summary>0보다 크면 넓이형(㎡): 밀도 × 넓이만큼 맞힌다.</summary>
        public readonly double Area;
        /// <summary>넓이형이 아니면 맞히는 적 수.</summary>
        public readonly int Targets;
        /// <summary>앞쪽부터가 아니라 가까운 적 RandomPickNearest 마리 근처 아무 데나.</summary>
        public readonly bool Random;
        /// <summary>맞은 적 하나를 몇 번 때리나.</summary>
        public readonly int Hits;
        /// <summary>한 번 피해(공력·음양·절기·치명 기대값까지 곱한 값).</summary>
        public readonly double Damage;
        /// <summary>맞고 살아남은 적에게 더 들어가는 지속 피해 총량(화상·중독).</summary>
        public readonly double Dot;
        /// <summary>보스 한 마리만 있을 때 이 획이 몇 번 들어가나(화력 표용).</summary>
        public readonly double SingleHits;
        /// <summary>보스 한 마리에게 들어가는 지속 피해(덮어쓰기라 한 번만 센다).</summary>
        public readonly double SingleDot;
        /// <summary>칠보단혼무: 안에서 7초 버틴 잡몹은 즉사.</summary>
        public readonly bool AuraExecute;

        public Strike(double area = 0, int targets = 0, bool random = false, int hits = 1, double damage = 0,
            double dot = 0, double singleHits = 0, double singleDot = 0, bool auraExecute = false)
        {
            Area = area; Targets = targets; Random = random; Hits = hits; Damage = damage;
            Dot = dot; SingleHits = singleHits; SingleDot = singleDot; AuraExecute = auraExecute;
        }
    }

    /// <summary>그 순간의 능력치와 음양. 심법이 바뀌거나 음양 구간이 바뀌면 다시 만든다.</summary>
    public sealed class BuildStats
    {
        public StatSheet Sheet = null!;
        /// <summary>쾌속(태극 -10% 포함, 상한 적용).</summary>
        public double Cdr;
        public YinYangBand Band;
        public YinYang Leaning;
        public double BiasBonus, ExtremeBonus;
        public double Crit, EliteDamage, Burn, Dot, ExplosionArea;
    }

    public static class WeaponModel
    {
        static double Lerp(double a, double b, double t) => a + (b - a) * t;
        /// <summary>자바스크립트 Math.round 와 같게(0.5는 올림).</summary>
        static int RoundJs(double x) => (int)Math.Floor(x + 0.5);
        static double T(int level, int max) => max <= 1 ? 0 : Math.Clamp((level - 1) / (double)(max - 1), 0, 1);
        static double Range(float[]? r, double t, double a, double b) => r == null ? Lerp(a, b, t) : Lerp(r[0], r[1], t);
        static double Disc(double r) => Math.PI * r * r;

        /// <summary>편향이면 기운 쪽 속성 무공 +15%, 극성이면 +40%. 태극·중 속성은 그대로.</summary>
        public static double AspectMultiplier(WeaponDef def, BuildStats b)
        {
            if (b.Band == YinYangBand.Harmony || def.Aspect == YinYang.Neutral || def.Aspect != b.Leaning) return 1;
            return 1 + (b.Band == YinYangBand.Extreme ? b.ExtremeBonus : b.BiasBonus);
        }

        /// <summary>한 번 피해. 치명(피해 2배)은 기대값으로 곱한다.</summary>
        public static double HitDamage(WeaponDef def, int level, bool evolved, bool glow, BuildStats b, int maxLevel) =>
            WeaponMath.DamageAt(def, level, maxLevel) * b.Sheet.Might * AspectMultiplier(def, b)
            * (evolved ? EvolvedDamage : 1) * (glow ? GlowMultiplier : 1) * (1 + b.Crit);

        /// <summary>다음 발동까지(초). 섬광지는 초당 발수, 독무는 0.5초마다 한 번 닿는다([프로토] update).</summary>
        public static double Interval(WeaponDef def, WeaponExtras? ex, int level, bool evolved, double cdr, int maxLevel)
        {
            double t = T(level, maxLevel);
            double cd = def.Cooldown > 0 ? def.Cooldown : 0.5;
            if (def.Behavior == "random_rapid") cd = 1 / (Range(ex?.ShotsPerSecond, t, 4, 9) * (evolved ? 2.2 : 1));
            if (def.Behavior == "aura") cd = 0.5;
            return Math.Max(0.05, cd * (1 - cdr));
        }

        public static List<Strike> Strikes(WeaponDef def, WeaponExtras? ex, int level, bool evolved, bool glow, BuildStats b, int maxLevel)
        {
            double t = T(level, maxLevel);
            double dmg = HitDamage(def, level, evolved, glow, b, maxLevel);
            var s = b.Sheet;
            var list = new List<Strike>();
            switch (def.Behavior)
            {
                case "front_arc":
                {
                    // 삼재검법: 가까운 적 쪽 반원(넓이 0.5), Lv4 앞뒤(1), 절기는 세 방향(1.5, 겹침 포함). Lv7부터 3연 베기.
                    double r = def.Range * s.Area * (evolved ? 1.6 : 1) + EnemyRadius;
                    double cover = evolved ? 1.5 : level >= 4 ? 1.0 : 0.5;
                    int strikes = level >= 7 ? 3 : 1;
                    list.Add(new Strike(area: Disc(r) * cover, hits: strikes, damage: dmg, singleHits: strikes));
                    // 절기: 세 번째 공격마다 바라보는 쪽으로 관통 검기(피해 1.5배, 폭 0.7m). 세 번에 한 번을 넓이 1/3로 나눠 센다.
                    if (evolved)
                        list.Add(new Strike(area: 2 * (0.7 + EnemyRadius) * CrowdRadius * FacingEfficiency / 3, random: true,
                            damage: dmg * 1.5, singleHits: FacingEfficiency / 3));
                    break;
                }
                case "melee_combo":
                {
                    // 나한권: 주변 원 안을 3→5연타.
                    double r = def.Range * s.Area + EnemyRadius;
                    int hits = RoundJs(Range(ex?.Hits, t, 3, 5));
                    list.Add(new Strike(area: Disc(r), hits: hits, damage: dmg, singleHits: hits));
                    // 절기 백보신권: 관통 권경 세 갈래(피해 2배, 폭 0.6m).
                    if (evolved)
                        list.Add(new Strike(area: 3 * 2 * (0.6 + EnemyRadius) * CrowdRadius * FacingEfficiency, random: true,
                            damage: dmg * 2, singleHits: FacingEfficiency));
                    break;
                }
                case "homing_dagger":
                {
                    // 비도술: 가까운 적에게 n자루, 자루마다 관통+1 마리. Lv6부터 중독(2초, 피해의 25%/초). 절기는 +2자루, 5번 튕김.
                    int n = RoundJs(Range(ex?.Projectiles, t, 1, 4)) + s.Amount + (evolved ? 2 : 0);
                    int pierce = RoundJs(Range(ex?.Pierce, t, 1, 3));
                    int per = pierce + 1 + (evolved ? 5 : 0);
                    double dot = level >= 6 || evolved ? dmg * 0.25 * (1 + b.Dot) * 2 : 0;
                    list.Add(new Strike(targets: n * per, damage: dmg, dot: dot, singleHits: n, singleDot: dot));
                    break;
                }
                case "orbit_slash":
                {
                    // 창천검기: 몸 주위 원을 한 바퀴, Lv5부터 두 바퀴. 절기는 반경 1.3배.
                    double r = def.Range * s.Area * (evolved ? 1.3 : 1) + EnemyRadius;
                    int sweeps = level >= 5 || evolved ? 2 : 1;
                    list.Add(new Strike(area: Disc(r), hits: sweeps, damage: dmg, singleHits: sweeps));
                    break;
                }
                case "slow_projectile":
                {
                    // 장풍: 바라보는 쪽으로 느린 관통탄. 무리 안을 지나는 길이(최대 무리 반경) × 폭. Lv6부터 2연발(같은 길).
                    int shots = (level >= 6 ? 2 : 1) + (evolved ? 1 : 0);
                    double pr = 0.55 * Lerp(1, 1.6, t) * s.Area * (evolved ? 1.4 : 1);
                    double length = Math.Min(5 * s.ProjectileSpeed * 2.6 * s.Duration, CrowdRadius);
                    double band = 2 * (pr + EnemyRadius) * length * FacingEfficiency;
                    list.Add(new Strike(area: band, random: true, hits: shots, damage: dmg, singleHits: shots * FacingEfficiency));
                    // 절기 파산장: 맞을 때마다 작은 폭발(피해 절반). 같은 길을 한 번 더 긁는다고 본다.
                    if (evolved) list.Add(new Strike(area: band, random: true, damage: dmg * 0.5));
                    break;
                }
                case "random_rapid":
                {
                    // 섬광지: 가까운 적 중 아무에게나 한 발.
                    list.Add(new Strike(targets: 1, random: true, damage: dmg, singleHits: 1));
                    // 절기 만천성지: 25% 확률로 작은 폭발(반경 1.1m, 피해 절반).
                    if (evolved) list.Add(new Strike(area: Disc(1.1 + EnemyRadius) * 0.25, random: true, damage: dmg * 0.5));
                    break;
                }
                case "random_bomb":
                {
                    // 벽력탄: 가까운 적 근처에 폭탄 n개, 반경 2→3m, 맞은 적은 2초 화상. 절기는 +1개, 3연쇄(0.9배씩 작아짐).
                    int n = RoundJs(Range(ex?.Projectiles, t, 1, 3)) + s.Amount + (evolved ? 1 : 0);
                    double rr = def.Range * Lerp(1, 1.5, t) * s.Area * (1 + b.ExplosionArea);
                    double burn = Range(ex?.BurnPerSecond, t, 4, 10) * (1 + b.Burn) * (1 + b.Dot) * 2 * s.Duration;
                    for (int i = 0; i < n; i++)
                    {
                        list.Add(new Strike(area: Disc(rr + EnemyRadius), random: true, damage: dmg, dot: burn,
                            singleHits: 1, singleDot: i == 0 ? burn : 0));
                        if (!evolved) continue;
                        list.Add(new Strike(area: Disc(rr * 0.9 + EnemyRadius), random: true, damage: dmg * 0.8, dot: burn));
                        list.Add(new Strike(area: Disc(rr * 0.81 + EnemyRadius), random: true, damage: dmg * 0.64, dot: burn));
                    }
                    break;
                }
                case "aura":
                {
                    // 독무: 발밑 원(반경 2→3.5m) 안을 0.5초마다 '초당 피해의 절반'씩. 절기는 반경 2배, 7초 버틴 잡몹 즉사.
                    double baseR = ex?.RangeLv8 is float r8 ? Lerp(def.Range, r8, t) : Lerp(def.Range, 3.5, t);
                    double r = baseR * s.Area * (evolved ? 2 : 1) + EnemyRadius;
                    double tick = dmg * (1 + b.Dot) * 0.5;
                    list.Add(new Strike(area: Disc(r), damage: tick, singleHits: 1, auraExecute: evolved));
                    break;
                }
                default:
                {
                    // 1장 첫 판엔 잠겨 있는 무공. 사거리 반원 한 획 × 투사체 수 × 타수로 대충 잡는다.
                    double r = def.Range * s.Area + EnemyRadius;
                    int n = ex?.Projectiles != null ? RoundJs(Range(ex.Projectiles, t, 1, 1)) + s.Amount : 1;
                    int hits = ex?.Hits != null ? RoundJs(Range(ex.Hits, t, 1, 1)) : 1;
                    for (int i = 0; i < Math.Max(1, n); i++)
                        list.Add(new Strike(area: Disc(r) * 0.5, random: true, hits: hits, damage: dmg, singleHits: hits));
                    break;
                }
            }
            return list;
        }

        /// <summary>보스 한 마리를 칠 때의 초당 피해(기획서 11장 '화력 목표'와 견주는 값).</summary>
        public static double SingleTargetDps(IEnumerable<Strike> strikes, double interval)
        {
            double sum = 0;
            foreach (var st in strikes) sum += st.Damage * st.SingleHits + st.SingleDot;
            return sum / interval;
        }

        /// <summary>지금 무리에 쏟는 초당 피해 기대값(넘친 피해 포함). 화력만 보는 정책이 고를 때 쓴다.</summary>
        public static double ExpectedCrowdDps(IEnumerable<Strike> strikes, double interval, double density, int inCrowd)
        {
            double sum = 0;
            foreach (var st in strikes)
            {
                double k = st.Area > 0 ? density * st.Area : st.Targets;
                k = Math.Min(k, inCrowd);
                sum += k * (st.Hits * st.Damage + st.Dot);
            }
            return sum / interval;
        }
    }
}
