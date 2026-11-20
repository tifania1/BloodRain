using System.Collections.Generic;
using BloodRain.Core.Build;
using BloodRain.Sim;
using Unity.Mathematics;
using UnityEngine;
using NVec = System.Numerics.Vector2;

namespace BloodRain.Presentation.Combat
{
    // SC-06a 어강술 내 편 강시(2026-11-20 사람이 추천대로 — decisions #50). 셈은 Core ThrallBand, 여기는 들이기·그리기·피해 넣기만.
    //   WeaponRunner 쇄겸술이 정신부에 멈춘 강시(family jiangshi)를 끌어오면 TryTake → 그 덩이를 적 무리에서 빼고(구슬 없음) 내 편 그림으로 세운다.
    //   내 편은 가장 가까운 적을 제 걸음으로 쫓고 닿으면 제 접촉 피해를 0.5초마다(EnemyField.Damage, 넉백 0). 적은 내 편을 노리지 않고 서로 통과한다.
    //   30초가 지나면 옅어지며 사라진다. LifeRunner 오브젝트에 저절로 붙는다(Instance).
    public sealed class ThrallRunner : MonoBehaviour
    {
        public static ThrallRunner Instance { get; private set; }
        public ThrallBand Band { get; } = new ThrallBand();

        EnemyField _enemies;
        readonly Dictionary<int, SpriteRenderer> _look = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<Texture2D, Sprite> _sprites = new Dictionary<Texture2D, Sprite>();
        readonly List<int> _near = new List<int>();
        readonly List<int> _one = new List<int>(1);
        readonly List<int> _gone = new List<int>();
        static Sprite _dot;

        public static ThrallRunner Ensure(EnemyField enemies)
        {
            if (Instance == null)
            {
                var host = LifeRunner.Instance != null ? LifeRunner.Instance.gameObject : new GameObject("내 편 강시");
                Instance = host.AddComponent<ThrallRunner>();
            }
            Instance._enemies = enemies;
            return Instance;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        float Interval => DataBoot.Data.Synergies.Secret("eogang") is { } s ? s.Param("hitIntervalSeconds") : 0.5f;

        /// <summary>index 자리 적이 정신부에 멈춘 강시면 내 편으로 들이고 적 무리에서 뺀다. 들였으면 true(그 index 는 더 쓰지 말 것).</summary>
        public bool TryTake(SecretEffects fx, int index)
        {
            if (_enemies == null || _enemies.Sim == null || fx == null || !fx.CanTakeThrall(Band.Count)) return false;
            var a = _enemies.Sim.Get(index);
            var def = _enemies.DefOf(a.Kind);
            if (def == null || def.Family != "jiangshi" || a.Stun <= 0f || a.Dead != 0) return false;
            var t = Band.TryTake(fx, def.Id, new NVec(a.Pos.x, a.Pos.y), a.Speed, a.ContactDamage, a.Radius, Interval);
            if (t == null) return false;
            _enemies.LookOf(a, out var art, out var size, out var tint);
            _enemies.Sim.RemoveAt(index);
            var go = new GameObject($"내 편 강시 {t.Id}");
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = art != null ? SpriteOf(art) : Dot();
            // 부적을 고쳐 붙인 내 편: 금빛이 도는 색 + 발밑 옅은 금빛 원
            r.color = new Color(tint.r * 0.75f + 0.25f, tint.g * 0.7f + 0.22f, tint.b * 0.45f + 0.05f, 1f);
            r.sortingOrder = 6;
            float s = size > 0f ? size : a.Radius * 2f;
            go.transform.localScale = Vector3.one * (s / Mathf.Max(0.01f, r.sprite.bounds.size.x));
            var ring = new GameObject("금빛 원").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(go.transform, false);
            ring.sprite = Dot();
            ring.color = new Color(0.95f, 0.78f, 0.3f, 0.2f);
            ring.sortingOrder = 5;
            ring.transform.localScale = new Vector3(0.95f, 0.45f, 1f);
            ring.transform.localPosition = new Vector3(0f, -0.35f, 0f);   // 발밑 납작한 금빛 그림자
            go.transform.position = new Vector3(a.Pos.x, a.Pos.y, -0.05f);
            _look[t.Id] = r;
            Debug.Log($"[어강술] 내 편 강시 {def.Id} · {Band.Count}/{fx.Thrall?.max} · 피해 {a.ContactDamage:0.#}");
            if (LifeRunner.Instance != null) LifeRunner.Instance.ShowPopup(Loc.T("ui.thrall.taken"), new Vector2(a.Pos.x, a.Pos.y));
            return true;
        }

        void Update()
        {
            if (Band.Count == 0 && _look.Count == 0) return;
            if (Time.timeScale <= 0f || _enemies == null || _enemies.Sim == null) return;
            float dt = Time.deltaTime;
            var sim = _enemies.Sim;
            _gone.Clear();
            var hits = Band.Tick(dt, p => Nearest(sim, p), Interval, _gone);
            foreach (var h in hits)
            {
                int idx = IndexOf(sim, h.TargetId);
                if (idx < 0) continue;
                var at = sim.Get(idx);
                _one.Clear(); _one.Add(idx);
                var from = Band.All.Count > 0 ? FindPos(h.ThrallId, new Vector2(at.Pos.x, at.Pos.y)) : new Vector2(at.Pos.x, at.Pos.y);
                _enemies.Damage(_one, h.Damage, from, 0f);
            }
            foreach (var t in Band.All)
            {
                if (!_look.TryGetValue(t.Id, out var r) || r == null) continue;
                r.transform.position = new Vector3(t.Pos.X, t.Pos.Y, -0.05f);
                var c = r.color; c.a = Mathf.Clamp01(t.Left / 1.5f); r.color = c;   // 마지막 1.5초에 옅어짐
            }
            foreach (var id in _gone)
                if (_look.TryGetValue(id, out var r)) { if (r != null) Destroy(r.gameObject); _look.Remove(id); }
        }

        Vector2 FindPos(int thrallId, Vector2 fallback)
        {
            foreach (var t in Band.All) if (t.Id == thrallId) return new Vector2(t.Pos.X, t.Pos.Y);
            return fallback;
        }

        ThrallTarget Nearest(EnemySim sim, NVec p)
        {
            float R = 30f * (WorldScale.Current != null ? WorldScale.Current.Length(1f) : 1f);
            sim.QueryCircle(new float2(p.X, p.Y), R, _near);
            int best = -1; float bd = float.MaxValue;
            var agents = sim.Agents;
            foreach (int i in _near)
            {
                var a = agents[i];
                if (a.Dead != 0) continue;
                float dx = a.Pos.x - p.X, dy = a.Pos.y - p.Y, d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) return default;
            var b = agents[best];
            return new ThrallTarget(b.Id, new NVec(b.Pos.x, b.Pos.y), b.Radius);
        }

        static int IndexOf(EnemySim sim, int id)
        {
            var agents = sim.Agents;
            for (int i = 0; i < sim.Count; i++) if (agents[i].Id == id) return i;
            return -1;
        }

        Sprite SpriteOf(Texture2D tex)
        {
            if (!_sprites.TryGetValue(tex, out var s))
            {
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height));
                _sprites[tex] = s;
            }
            return s;
        }

        static Sprite Dot()
        {
            if (_dot != null) return _dot;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    byte al = (byte)(dx * dx + dy * dy <= 1f ? 255 : 0);
                    px[y * n + x] = new Color32(255, 255, 255, al);
                }
            tex.SetPixels32(px); tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _dot;
        }
    }
}
