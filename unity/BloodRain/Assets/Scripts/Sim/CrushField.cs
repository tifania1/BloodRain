using System.Collections.Generic;
using BloodRain.Core.World;
using UnityEngine;

namespace BloodRain.Sim
{
    public enum CrushKind { Foe, Orb, Food, Silver, Lantern }

    /// <summary>밟힘 목록 한 칸. 위치는 세계 m, 크기는 생긴 경지 m(반지름).</summary>
    public struct Crushable
    {
        public int Id;
        public CrushKind Kind;
        public Vector2 Position;
        public int BornRealm;
        public float RadiusRealmMeters;
        public bool Elite;
        public bool Boss;
    }

    // 밟힘과 쓸림(기획서 7장, 15장 '밟힘', TU-03).
    // 아래 경지에서 생긴 적·구슬·음식·은자·등롱은 이동 계산을 멈추고 이 가벼운 목록으로 옮긴다.
    // 여기서는 '닿으면 쓰러짐'만 본다. 돌파 쓸림 판정도 이 목록만 훑는다.
    // 규칙(누가 밟히나, 쓸림 반경 안 누구인가)은 Core.World.Scale 이 정한다. 여기는 목록 관리와 거리 재기뿐.
    public sealed class CrushField
    {
        readonly List<Crushable> _items = new List<Crushable>();
        readonly List<ScaledFoe> _scratch = new List<ScaledFoe>();
        int _nextId = 1;

        public int Count => _items.Count;
        public Crushable this[int i] => _items[i];

        /// <summary>목록에 넣고 Id 를 돌려준다(Id 가 0 이면 새로 붙인다).</summary>
        public int Add(Crushable c)
        {
            if (c.Id == 0) c.Id = _nextId++;
            _items.Add(c);
            return c.Id;
        }

        /// <summary>플레이어가 닿은 것을 목록에서 빼 hits 에 담는다. 땅의 주인과 같은·위 경지 것은 남긴다.</summary>
        public int Touch(WorldScale ws, Vector2 player, float playerRadiusWorld, List<Crushable> hits)
        {
            int realm = ws.Realm, n = 0;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var c = _items[i];
                if (!ws.Scale.Crushes(c.BornRealm, realm, c.Boss)) continue;
                float r = playerRadiusWorld + ws.Length(c.RadiusRealmMeters, c.BornRealm);
                if ((c.Position - player).sqrMagnitude > r * r) continue;
                hits.Add(c);
                RemoveAt(i);
                n++;
            }
            return n;
        }

        /// <summary>돌파 쓸림. center 에서 radius(세계 m) 안의 newRealm 아래 적만 빼서 hits 에 담는다(구슬·등롱은 그대로).</summary>
        public int Sweep(Scale scale, Vector2 center, float radius, int newRealm, List<Crushable> hits)
        {
            _scratch.Clear();
            var index = new List<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Kind != CrushKind.Foe) continue;
                _scratch.Add(new ScaledFoe(_items[i].BornRealm, Vector2.Distance(_items[i].Position, center), _items[i].Boss));
                index.Add(i);
            }
            var picked = scale.SelectSweep(_scratch, newRealm, radius);
            var remove = new List<int>(picked.Count);
            foreach (int k in picked) { hits.Add(_items[index[k]]); remove.Add(index[k]); }
            remove.Sort();
            for (int j = remove.Count - 1; j >= 0; j--) RemoveAt(remove[j]);
            return picked.Count;
        }

        void RemoveAt(int i)
        {
            int last = _items.Count - 1;
            _items[i] = _items[last];
            _items.RemoveAt(last);
        }
    }
}
