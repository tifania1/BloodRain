#nullable enable
// 절기가 '옮아가는' 셈(기획서 9장 '절기로 바뀐 뒤 날아가는 것' 표). 웹 프로토타입 같은 이름 함수와 같은 규칙.
//   무영비도 튕김(bounceRange): 맞힌 자리에서 bounceRange 안, 아직 안 맞힌 가장 가까운 적.          웹 탄 pr.bounce
//   천뢰검진 낙뢰(lightningChance·Damage·Chains·Range·Falloff): 검기 원에 닿은 적마다 확률로 한 줄기,
//     그다음 가까운 적으로 lightningChains 번, 옮을 때마다 lightningFalloff 배. 줄기마다 맞은 적을 새로 셈.  웹 lightning
//   오뢰정법(lightningChains·Range·Falloff): 낙뢰 한 곳에 맞은 적마다 같은 방식으로 옮아가되,
//     그 낙뢰에 맞은 적과 이미 옮아간 적은 다시 안 맞는다(한 곳이 집합 하나).                    웹 random_lightning 절기
//   벽력천화(chain·chainDelaySeconds·chainShrink·chainDamage·fireSeconds·fireRadiusMultiplier):
//     첫 폭발에서 chainDelaySeconds 뒤 '지금 반경만큼 떨어진 무작위 쪽'에서 반경 × chainShrink, 피해 × chainDamage 로 다시,
//     모두 chain 번. 마지막 폭발 자리에 반경 × fireRadiusMultiplier 불바다 fireSeconds 초.           웹 bomb·fireField
//   만천성지(explodeOnCrit): 1 이면 치명타일 때만 터진다.
// 적은 번호로 받는다(xs·ys 같은 길이, 죽은 적은 넘기기 전에 뺀다). 같은 거리면 앞 번호(웹 nearestEnemies·Aim.Nearest 와 같음).
// 거리는 경지 m. 그리기·피해 넣기는 Unity WeaponRunner(unity_tasks C-09e·C-09c) 몫.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    /// <summary>번개 한 번 옮아감. From 이 -1 이면 하늘에서 내려친 첫 줄기.</summary>
    public readonly struct ChainHop
    {
        public readonly int From, To;
        /// <summary>그 무공 한 방에 곱하는 배율.</summary>
        public readonly float DamageScale;
        public ChainHop(int from, int to, float damageScale) { From = from; To = to; DamageScale = damageScale; }
        public override string ToString() => $"{From}->{To} x{DamageScale:0.###}";
    }

    /// <summary>벽력천화 폭발 한 번. Delay 는 첫 폭발부터 잰 초.</summary>
    public readonly struct ChainBomb
    {
        public readonly float Delay, X, Y, Radius, DamageScale;
        public ChainBomb(float delay, float x, float y, float radius, float damageScale) { Delay = delay; X = x; Y = y; Radius = radius; DamageScale = damageScale; }
    }

    public static class EvolvedHits
    {
        /// <summary>(px, py) 에서 range 안, exclude 에 없는 가장 가까운 적 번호. 없으면 -1.</summary>
        public static int NearestExcept(float px, float py, IReadOnlyList<float> xs, IReadOnlyList<float> ys, float range, ICollection<int>? exclude)
        {
            if (range <= 0f) return -1;
            int n = Math.Min(xs.Count, ys.Count), best = -1;
            float bestD = range * range;
            for (int i = 0; i < n; i++)
            {
                if (exclude != null && exclude.Contains(i)) continue;
                float dx = xs[i] - px, dy = ys[i] - py, d = dx * dx + dy * dy;
                if (d > bestD || (best >= 0 && d >= bestD)) continue;
                best = i; bestD = d;
            }
            return best;
        }

        /// <summary>
        /// start 에서 chains 번 옮아간다. 옮을 때마다 피해 × falloff, 옮아간 적은 hit 에 더한다(start 는 호출자가 넣어 둔다).
        /// 곁에 안 맞은 적이 없으면 거기서 멈춘다.
        /// </summary>
        public static List<ChainHop> Chain(int start, float startScale, IReadOnlyList<float> xs, IReadOnlyList<float> ys,
            int chains, float range, float falloff, ISet<int> hit)
        {
            var hops = new List<ChainHop>();
            int cur = start; float d = startScale;
            for (int k = 0; k < chains; k++)
            {
                int nx = NearestExcept(xs[cur], ys[cur], xs, ys, range, hit);
                if (nx < 0) break;
                d *= falloff; hit.Add(nx);
                hops.Add(new ChainHop(cur, nx, d));
                cur = nx;
            }
            return hops;
        }

        /// <summary>천뢰검진: 검기 원에 닿은 적마다 이 확률로 낙뢰. roll 은 0~1 난수.</summary>
        public static bool SkyLightningRolls(EvolutionDef evo, double roll) => roll < evo.Param("lightningChance");

        /// <summary>천뢰검진 낙뢰 한 줄기: 첫 줄기(From -1, × lightningDamage) 다음 곁의 적으로.</summary>
        public static List<ChainHop> SkyLightning(EvolutionDef evo, int first, IReadOnlyList<float> xs, IReadOnlyList<float> ys)
        {
            float d = evo.Param("lightningDamage");
            var hit = new HashSet<int> { first };
            var hops = new List<ChainHop> { new ChainHop(-1, first, d) };
            hops.AddRange(Chain(first, d, xs, ys, (int)evo.Param("lightningChains"), evo.Param("lightningRange"), evo.Param("lightningFalloff"), hit));
            return hops;
        }

        /// <summary>오뢰정법 낙뢰 한 곳: struck(반경 안에 맞은 적, 1배)마다 곁의 적으로 옮아감. 한 곳 안에서는 같은 적을 두 번 안 맞힌다.</summary>
        public static List<ChainHop> FiveThunders(EvolutionDef evo, IReadOnlyList<int> struck, IReadOnlyList<float> xs, IReadOnlyList<float> ys)
        {
            var hit = new HashSet<int>(struck);
            var hops = new List<ChainHop>();
            int chains = (int)evo.Param("lightningChains");
            float range = evo.Param("lightningRange"), falloff = evo.Param("lightningFalloff");
            foreach (var s in struck) hops.AddRange(Chain(s, 1f, xs, ys, chains, range, falloff, hit));
            return hops;
        }

        /// <summary>삼재검법 베는 방향: 절기(천지인 삼검)면 조준 각과 ±120°, Lv '앞뒤 베기'면 앞뒤, 아니면 조준 각 하나. 웹 front_arc.</summary>
        public static float[] FrontArcDirections(float baseAngle, bool evolved, bool frontBack)
        {
            float third = (float)(2 * Math.PI / 3);
            return evolved ? new[] { baseAngle, baseAngle + third, baseAngle - third }
                 : frontBack ? new[] { baseAngle, baseAngle + (float)Math.PI } : new[] { baseAngle };
        }

        /// <summary>천지인 삼검: attackCount 번째 공격(1부터, 웹 w.n)에 바라보는 쪽 관통 검기가 나가나(waveEvery 마다).</summary>
        public static bool SendsWave(EvolutionDef evo, int attackCount)
        {
            int every = (int)evo.Param("waveEvery");
            return every > 0 && attackCount % every == 0;
        }

        /// <summary>백보신권 등 갈래 탄: projectiles 발을 조준 각 가운데로 projectileSpread 간격. 웹 melee_combo 절기.</summary>
        public static float[] FanAngles(EvolutionDef evo, float baseAngle)
        {
            int n = (int)evo.Param("projectiles");
            float spread = evo.Param("projectileSpread");
            var a = new float[Math.Max(0, n)];
            for (int k = 0; k < a.Length; k++) a[k] = baseAngle + (k - (n - 1) / 2f) * spread;
            return a;
        }

        /// <summary>독무 절기: 안개 안에 머문 누적 초가 executeSeconds 를 넘으면 처형(정예·땅의 주인은 안 됨). 웹 aura 절기.</summary>
        public static bool PoisonExecutes(EvolutionDef evo, float secondsInside, bool eliteOrBoss) =>
            !eliteOrBoss && secondsInside >= evo.Param("executeSeconds");

        /// <summary>무영비도: 맞힌 자리 (x, y) 에서 다음으로 튈 적. hit 은 이 비도가 이미 맞힌 적.</summary>
        public static int NextBounce(EvolutionDef evo, float x, float y, IReadOnlyList<float> xs, IReadOnlyList<float> ys, ICollection<int> hit) =>
            NearestExcept(x, y, xs, ys, evo.Param("bounceRange"), hit);

        /// <summary>만천성지 등 폭발 절기: 이 맞음에서 터지는가(explodeOnCrit 이 1 이면 치명타만).</summary>
        public static bool Explodes(EvolutionDef evo, bool crit) => evo.Param("explodeOnCrit", 0f) < 0.5f || crit;

        /// <summary>
        /// 벽력천화 연쇄 폭발 전부(첫 폭발 포함 chain 개). angle01 은 0~1 난수를 하나씩 준다(다음 폭발 쪽 = 2π × 값).
        /// 다음 폭발은 지금 폭발 반경만큼 떨어진 자리.
        /// </summary>
        public static List<ChainBomb> ChainBombs(EvolutionDef evo, float x, float y, float radius, Func<double> angle01)
        {
            int chain = Math.Max(1, (int)evo.Param("chain"));
            float delay = evo.Param("chainDelaySeconds"), shrink = evo.Param("chainShrink"), dmg = evo.Param("chainDamage");
            var list = new List<ChainBomb> { new ChainBomb(0f, x, y, radius, 1f) };
            float t = 0f, scale = 1f;
            for (int i = 1; i < chain; i++)
            {
                double a = angle01() * 2 * Math.PI;
                x += (float)Math.Cos(a) * radius; y += (float)Math.Sin(a) * radius;
                radius *= shrink; scale *= dmg; t += delay;
                list.Add(new ChainBomb(t, x, y, radius, scale));
            }
            return list;
        }

        /// <summary>벽력천화 불바다(마지막 폭발 자리): 반경과 초. fireSeconds 가 없으면 (0, 0).</summary>
        public static (float radius, float seconds) FireField(EvolutionDef evo, ChainBomb last)
        {
            float s = evo.Param("fireSeconds", 0f);
            return s > 0f ? (last.Radius * evo.Param("fireRadiusMultiplier", 1f), s) : (0f, 0f);
        }
    }
}
