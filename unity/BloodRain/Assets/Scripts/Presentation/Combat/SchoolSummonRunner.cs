using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;

namespace BloodRain.Presentation.Combat
{
    // R-07U 진전 소림 4(나한 허상)·개방 4(개방 제자). 셈은 전부 Core SchoolSummons, 여기는 그리기와 밀기·쏘기만.
    //   허상: 몸 둘레 radius(축척) 원에 images 개가 seconds 동안 돈다. 둘레 안 잡몹·정예를 ArhatPush 만큼 밀고 피해는 없다(땅의 주인 제외).
    //   제자: allies 명이 seconds 동안 몸 곁에 선다(적이 노리지 않음). AllyVolleys 만큼 제자마다 가장 가까운 적에게 장풍 Lv1 탄 한 대.
    //         탄 모양은 weapons.json palm_wind shot, 피해·넉백은 SchoolSummons.AllyShot(공력은 탄이 맞을 때 EnemyField 가 곱하는 길 그대로라 1).
    // LifeRunner 가 붙이고 프레임마다 Step 을 부른다.
    public sealed class SchoolSummonRunner : MonoBehaviour
    {
        public LifeRunner life;
        public WeaponRunner weapons;
        public EnemyField enemies;
        public Transform player;

        /// <summary>시험용: 넣으면 LifeRunner.Effects 대신 이것을 쓴다(진전 채우기).</summary>
        [System.NonSerialized] public SynergyEffects testEffects;

        public SchoolSummons Summons { get; } = new SchoolSummons();
        public int ArhatPushes { get; private set; }
        public int AllyShots { get; private set; }

        readonly List<SpriteRenderer> _images = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _allies = new List<SpriteRenderer>();
        readonly List<int> _near = new List<int>();
        float _pushClock, _spin;
        static Sprite _dot;

        public void Step(float healthRatio, float dt)
        {
            if (player == null) return;
            var fx = testEffects ?? (life != null ? life.Effects : SynergyEffects.None);
            var ev = Summons.Tick(fx, healthRatio, dt);
            var ws = WorldScale.Current;
            float unit = ws != null ? ws.Length(1f) : 1f;
            Vector2 p = player.position;

            if (ev.ArhatStarted) Debug.Log($"[진전] 나한 허상 {Summons.ArhatUsed}번째 · {Summons.ArhatSecondsLeft:0.#}초");
            if (ev.AlliesSpawned > 0) Debug.Log($"[진전] 개방 제자 {ev.AlliesSpawned}명 · {Summons.AlliesSecondsLeft:0.#}초");

            // 나한 허상
            var ring = fx.Tier("arhat_ring");
            int images = Summons.ArhatActive && ring != null ? (int)ring.Param("images") : 0;
            float radius = ring != null ? ring.Param("radius") * unit : 0f;
            Ensure(_images, images, new Color(1f, 0.82f, 0.35f, 0.75f), "나한 허상");
            _spin += dt * 1.2f;
            for (int i = 0; i < images; i++)
            {
                float a = _spin + i * Mathf.PI * 2f / images;
                var t = _images[i].transform;
                t.position = new Vector3(p.x + Mathf.Cos(a) * radius, p.y + Mathf.Sin(a) * radius, -0.1f);
                t.localScale = Vector3.one * unit * 0.45f;
                var c = _images[i].color; c.a = 0.55f + 0.25f * Mathf.Sin(_spin * 3f + i); _images[i].color = c;
            }
            if (images > 0 && enemies != null && enemies.Sim != null)
            {
                _pushClock += dt;
                if (_pushClock >= 0.1f)
                {
                    _pushClock = 0f;
                    var sim = enemies.Sim;
                    sim.QueryCircle(new float2(p.x, p.y), radius, _near);
                    int last = -1;
                    _near.Sort();
                    foreach (int i in _near)
                    {
                        if (i == last) continue;
                        last = i;
                        var ag = sim.Get(i);
                        if (ag.Health <= 0f) continue;
                        var def = enemies.DefOf(ag.Kind);
                        bool boss = def != null && def.Family == "boss";
                        var d = new Vector2(ag.Pos.x - p.x, ag.Pos.y - p.y);
                        float m = d.magnitude;
                        float push = Summons.ArhatPush(fx, m, boss, unit);   // 거리를 세계 m 로 넘기니 둘레도 축척을 곱해 견준다
                        if (push <= 0f || m < 1e-5f) continue;
                        float kk = (ws != null ? ws.Speed(push) : push) * (1f - Mathf.Clamp01(def != null ? def.KnockbackResist : 0f));
                        ag.Knock += new float2(d.x / m, d.y / m) * kk;
                        sim.Set(i, ag);
                        ArhatPushes++;
                    }
                }
            }

            // 개방 제자
            int allies = Summons.AlliesActive ? Summons.AlliesPresent : 0;
            Ensure(_allies, allies, new Color(0.55f, 0.42f, 0.28f, 0.95f), "개방 제자");
            for (int i = 0; i < allies; i++)
            {
                float a = i * Mathf.PI * 2f / Mathf.Max(1, allies) + Mathf.PI / 2f;
                var t = _allies[i].transform;
                var want = new Vector3(p.x + Mathf.Cos(a) * unit * 1.2f, p.y + Mathf.Sin(a) * unit * 1.2f, -0.1f);
                t.position = Vector3.Lerp(t.position == Vector3.zero ? want : t.position, want, 1f - Mathf.Exp(-6f * dt));
                t.localScale = Vector3.one * unit * 0.7f;
            }
            if (ev.AllyVolleys > 0 && allies > 0 && weapons != null)
            {
                var (dmg, knock) = SchoolSummons.AllyShot(fx, 1f);
                var palm = System.Linq.Enumerable.FirstOrDefault(DataBoot.Data.Weapons, w => w.Id == "palm_wind");
                var shot = palm?.Shot;
                float speed = shot != null ? shot.Speed : 5f, rad = shot != null ? shot.Radius : 0.55f, lifeS = shot != null ? shot.LifeSeconds : 2.6f;
                for (int v = 0; v < ev.AllyVolleys; v++)
                    for (int i = 0; i < allies; i++)
                    {
                        Vector2 from = _allies[i].transform.position;
                        float ang = NearestAngle(from, unit * speed * lifeS, out bool found);
                        if (!found) continue;
                        weapons.ShootFreeFrom(from, ang, ws != null ? ws.Speed(speed) : speed, rad * unit, dmg, lifeS, ws != null ? ws.Speed(knock) : knock);
                        AllyShots++;
                    }
            }
        }

        float NearestAngle(Vector2 from, float reach, out bool found)
        {
            found = false;
            if (enemies == null || enemies.Sim == null) return 0f;
            var sim = enemies.Sim;
            sim.QueryCircle(new float2(from.x, from.y), reach, _near);
            float best = float.MaxValue; Vector2 bd = Vector2.right;
            foreach (int i in _near)
            {
                var ag = sim.Get(i);
                if (ag.Health <= 0f) continue;
                var d = new Vector2(ag.Pos.x - from.x, ag.Pos.y - from.y);
                float m = d.sqrMagnitude;
                if (m < best) { best = m; bd = d; found = true; }
            }
            return Mathf.Atan2(bd.y, bd.x);
        }

        void Ensure(List<SpriteRenderer> list, int n, Color c, string name)
        {
            while (list.Count < n)
            {
                var go = new GameObject($"{name} {list.Count + 1}", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                var sr = go.GetComponent<SpriteRenderer>();
                sr.sprite = Dot(); sr.color = c; sr.sortingOrder = 20;
                list.Add(sr);
            }
            for (int i = 0; i < list.Count; i++)
            {
                bool on = i < n;
                if (list[i].gameObject.activeSelf != on)
                {
                    if (on) list[i].transform.position = Vector3.zero;
                    list[i].gameObject.SetActive(on);
                }
            }
        }

        static Sprite Dot()
        {
            if (_dot != null) return _dot;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01((1f - r) * 6f) * 255);
                    px[y * s + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply();
            return _dot = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
