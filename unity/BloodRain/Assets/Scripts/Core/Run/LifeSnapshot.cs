#nullable enable
// 생 도중 저장 ①(backlog GU-11h1, 천하 확장 설계안 '긴 생을 위한 두 가지'). 한 생 상한이 60분으로 늘어 중간에 끄고 이어 할 수 있어야 한다.
// 이 판(①)이 담는 것: 시각·레벨·진기·위치·밟은 땅·무공/심법 레벨과 칸 수·음양·오의 게이지·은자.
// 담지 않는 것: 적·탄·사건 자리(이어 할 때 비우고 생성 규칙대로 다시 채운다 — 2026-11-18 예약 세션이 정한 시작 방식),
//   그리고 ②(GU-11h2)로 미룬 것: 땅의 주인 깨어남·처치, 각성·절기, 연환·돈오, 깨달음, 기연, 윤회의 기 장부, 연표 겪음, 영약, 부활.
// 되돌리기는 새로 만든 같은 몸의 Life 에 Life.Restore 로 한다. SaveGame 에 넣는 일은 ②.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BloodRain.Core.Run
{
    public sealed class LifeSnapshot
    {
        public const int Version = 1;
        public int SnapshotVersion = Version;
        /// <summary>태어난 문파 id(서장은 null). 되돌릴 때 같은 몸인지 본다.</summary>
        public string? Sect;
        public float Minutes;
        public int Level;
        public double Xp;
        public double XpTotal;
        public float X, Y;
        public List<string> Visited = new List<string>();
        public Dictionary<string, int> Weapons = new Dictionary<string, int>();
        public Dictionary<string, int> Passives = new Dictionary<string, int>();
        public int WeaponSlots, PassiveSlots;
        public int Gauge;
        public float Charge;
        public int Silver;

        public static LifeSnapshot Capture(Life life) => new LifeSnapshot
        {
            Sect = life.Body.Sect?.Id,
            Minutes = life.Minutes,
            Level = life.Xp.Level,
            Xp = life.Xp.Xp,
            XpTotal = life.Xp.Total,
            X = life.Position.X,
            Y = life.Position.Y,
            Visited = life.Log.VisitedRegions.ToList(),
            Weapons = life.Loadout.Weapons.ToDictionary(kv => kv.Key, kv => kv.Value),
            Passives = life.Loadout.Passives.ToDictionary(kv => kv.Key, kv => kv.Value),
            WeaponSlots = life.Loadout.WeaponSlots,
            PassiveSlots = life.Loadout.PassiveSlots,
            Gauge = life.Loadout.Gauge.Value,
            Charge = life.Charge.Value,
            Silver = life.Silver,
        };

        public Vector2 Position => new Vector2(X, Y);
    }
}
