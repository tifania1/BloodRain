#nullable enable
// 천하 한 장의 축척(기획서 7장 '경지와 축척'). 숫자는 cheonha.json world·realmScale·crush, 경지 레벨은 balance.json realms.
// 돌파 순간의 연출 시간표는 World.Breakthrough 가 맡는다.
//
// 길이 단위가 둘이다.
//   세계 m  삼류 축척의 m. 지역 위치(cheonha.json pos × 세계 크기), 화면 대각선, 혈우 장막 반경, 플레이어·적 위치가 이 단위다.
//           지도처럼 왼쪽 위가 (0,0)이고 아래로 갈수록 y가 커진다. Unity 좌표(y 위쪽)로 바꾸는 일은 Unity 쪽에서 한다.
//   경지 m  지금 경지 축척의 m. 전투 수치(무공 사거리, 이동·적 속도, 생성 고리)는 이 단위로 읽고 고치지 않는다(7장).
//           경지 m 하나 = 세계 m × Factor(경지 순번) = 2.25^n.
//
// Unity 쪽 흐름:
//   - 카메라는 화면 대각선이 ViewDiagonal(레벨) 세계 m 가 되게 맞춘다. 돌파 중에는 Breakthrough.ViewDiagonal.
//   - 플레이어 위치는 매 프레임 KeepInside(위치, 출생지 중심, 경지)로 장막 안에 되돌린다. 현경부터는 그대로 둔다.
//   - 적이 닿으면 Crushes(적이 생긴 경지, 지금 경지)를 본다. 참이면 피해 없이 쓰러뜨리고 진기 CrushXp 를 떨군다.
//   - 덩이는 DrawMembers(생긴 경지, 남은 기혈 비율)명만큼 적 한 장을 겹쳐 그리고, 쓰러지면 처치 수를 KillCount(생긴 경지)로 센다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>밟힘·쓸림 판정에 넘기는 적 하나. 위치 대신 플레이어와의 거리(세계 m)만 받는다.</summary>
    public readonly struct ScaledFoe
    {
        /// <summary>그 적이 생긴 경지 순번(0 = 삼류).</summary>
        public readonly int BornRealm;
        /// <summary>플레이어와의 거리(세계 m).</summary>
        public readonly float Distance;
        /// <summary>땅의 주인. 밟히지도 쓸리지도 않는다(싸움을 건 순간의 축척 그대로 싸운다).</summary>
        public readonly bool Boss;

        public ScaledFoe(int bornRealm, float distance, bool boss = false)
        {
            BornRealm = bornRealm;
            Distance = distance;
            Boss = boss;
        }
    }

    public sealed class Scale
    {
        readonly WorldDef _world;
        readonly CrushDef _crush;
        readonly List<RealmDef> _realms;
        readonly int[] _clusterSize;
        readonly int _noBoundaryFrom;

        public Scale(GameData data) : this(data.Cheonha, data.Balance.Realms) { }

        public Scale(CheonhaDef cheonha, IEnumerable<RealmDef> realms)
        {
            _world = cheonha.World;
            _crush = cheonha.Crush;
            _realms = realms.OrderBy(r => r.Level).ToList();
            if (_realms.Count == 0) throw new ArgumentException("경지가 하나도 없습니다", nameof(realms));
            _clusterSize = new int[_realms.Count];
            for (int i = 0; i < _realms.Count; i++)
            {
                var row = cheonha.RealmScale.FirstOrDefault(s => s.Realm == _realms[i].Id);
                if (row == null) throw new ArgumentException($"cheonha.json realmScale 에 {_realms[i].Id} 가 없습니다");
                _clusterSize[i] = row.ClusterSize;
            }
            int nb = _realms.FindIndex(r => r.Id == _world.NoBoundaryFrom);
            _noBoundaryFrom = nb < 0 ? _realms.Count : nb;
        }

        public WorldDef World => _world;
        /// <summary>경지(레벨 순서). 순번이 곧 축척 지수다.</summary>
        public IReadOnlyList<RealmDef> Realms => _realms;
        /// <summary>마지막 경지(현경) 순번.</summary>
        public int TopRealm => _realms.Count - 1;

        // ---- 경지 순번 ----

        /// <summary>레벨이 닿은 경지 순번. 0 = 삼류.</summary>
        public int RealmIndex(int level)
        {
            int n = 0;
            for (int i = 0; i < _realms.Count; i++)
                if (_realms[i].Level <= level) n = i;
            return n;
        }

        public int RealmIndex(string realmId)
        {
            int n = _realms.FindIndex(r => r.Id == realmId);
            if (n < 0) throw new ArgumentException($"모르는 경지: {realmId}", nameof(realmId));
            return n;
        }

        /// <summary>세상의 축척 배율 2.25^n. 지금 경지 1m가 세계 몇 m인가.</summary>
        public float Factor(int realm) => (float)Math.Pow(_world.ScalePerRealm, realm);

        public float ToWorld(float realmMeters, int realm) => realmMeters * Factor(realm);
        public float ToRealm(float worldMeters, int realm) => worldMeters / Factor(realm);

        // ---- 화면 대각선 ----

        /// <summary>경지 안 진행 0~1 = (레벨 - 그 경지 레벨) / (다음 경지 레벨 - 그 경지 레벨). 현경은 다음이 없어 0.
        /// 레벨에 소수를 주면(진기 비율) 레벨 사이도 매끄럽게 물러난다.</summary>
        public float ProgressInRealm(float level)
        {
            int n = RealmIndex((int)Math.Floor(level));
            if (n >= TopRealm) return 0f;
            float from = _realms[n].Level, to = _realms[n + 1].Level;
            return Clamp01((level - from) / (to - from));
        }

        /// <summary>화면 대각선의 축척 지수 = 경지 순번 + 0.35 × 경지 안 진행.</summary>
        public float ViewExponent(float level)
        {
            int n = RealmIndex((int)Math.Floor(level));
            return n + _world.ViewInRealmShare * ProgressInRealm(level);
        }

        /// <summary>화면 대각선(세계 m) = 36 × 2.25^(경지 순번 + 0.35 × 경지 안 진행).</summary>
        public float ViewDiagonal(float level) => DiagonalAtExponent(ViewExponent(level));

        /// <summary>그 경지 첫 화면 대각선(세계 m). 7장 표의 '화면 대각선' 칸.</summary>
        public float ViewDiagonalAtRealm(int realm) => DiagonalAtExponent(realm);

        public float DiagonalAtExponent(float exponent) => _world.View0Meters * (float)Math.Pow(_world.ScalePerRealm, exponent);

        // ---- 혈우 장막 ----

        /// <summary>장막이 있는 경지인가. 현경부터 없다.</summary>
        public bool HasBoundary(int realm) => realm < _noBoundaryFrom;

        /// <summary>혈우 장막 반경(세계 m) = 그 경지 첫 화면 대각선 × 2.4. 장막이 없는 경지는 null.</summary>
        public float? BoundaryRadius(int realm) =>
            HasBoundary(realm) ? ViewDiagonalAtRealm(realm) * _world.BoundaryViewMultiple : (float?)null;

        /// <summary>장막 안인가(경계 포함). 장막이 없으면 늘 참.</summary>
        public bool IsInside(Vector2 position, Vector2 birthplace, int realm)
        {
            var r = BoundaryRadius(realm);
            return r == null || Vector2.Distance(position, birthplace) <= r.Value;
        }

        /// <summary>장막 밖으로 나간 위치를 출생지 쪽으로 장막 위까지 되돌린다. 안이면 그대로.</summary>
        public Vector2 KeepInside(Vector2 position, Vector2 birthplace, int realm)
        {
            var r = BoundaryRadius(realm);
            return r == null ? position : KeepInside(position, birthplace, r.Value);
        }

        public static Vector2 KeepInside(Vector2 position, Vector2 center, float radius)
        {
            var offset = position - center;
            float len = offset.Length();
            if (len <= radius || len <= 0f) return position;
            return center + offset * (radius / len);
        }

        // ---- 밟힘과 쓸림 ----

        /// <summary>밟혀 쓰러진 적이 남기는 진기(0.3). 그 적 원래 값과 상관없이 이 값 하나다.</summary>
        public float CrushXp => _crush.XpPerCrushed;
        /// <summary>돌파 쓸림 반경 = 화면 대각선 × 이 값(0.6).</summary>
        public float SweepViewMultiple => _crush.SweepViewMultiple;

        /// <summary>닿으면 밟히는가: 아래 경지에서 생긴 적은 닿기만 해도 쓰러지고 피해를 주지 못한다. 땅의 주인은 밟히지 않는다.
        /// 진기 구슬·음식·은자·등롱도 생긴 경지가 아래면 같은 규칙(지나가면 주워지고, 등롱은 깨진다).</summary>
        public bool Crushes(int bornRealm, int playerRealm, bool boss = false) => !boss && bornRealm < playerRealm;

        /// <summary>닿았을 때 플레이어가 피해를 받는가.</summary>
        public bool DealsContactDamage(int bornRealm, int playerRealm, bool boss = false) => !Crushes(bornRealm, playerRealm, boss);

        /// <summary>돌파 쓸림 대상: 반경(세계 m, 경계 포함) 안의 새 경지보다 아래에서 생긴 적. 넘긴 목록의 순번을 순서대로.</summary>
        public List<int> SelectSweep(IReadOnlyList<ScaledFoe> foes, int newRealm, float radius)
        {
            var list = new List<int>();
            for (int i = 0; i < foes.Count; i++)
            {
                var f = foes[i];
                if (f.Distance <= radius && Crushes(f.BornRealm, newRealm, f.Boss)) list.Add(i);
            }
            return list;
        }

        // ---- 적 한 덩이 ----

        /// <summary>그 경지에서 생긴 적 한 덩이에 든 사람 수(삼류 1 … 현경 64).</summary>
        public int ClusterSize(int realm) => _clusterSize[Math.Max(0, Math.Min(TopRealm, realm))];

        /// <summary>덩이를 쓰러뜨렸을 때 처치 수(HUD·업적). 진기는 덩이 하나에 그 적 한 마리 값이다.</summary>
        public int KillCount(int bornRealm) => ClusterSize(bornRealm);

        /// <summary>덩이에 그릴 사람 수 = ceil(덩이 크기 × 남은 기혈 비율). 살아 있으면 적어도 1, 쓰러졌으면 0.</summary>
        public int DrawMembers(int bornRealm, float healthRatio) => MembersToDraw(ClusterSize(bornRealm), healthRatio);

        public static int MembersToDraw(int clusterSize, float healthRatio)
        {
            if (healthRatio <= 0f || clusterSize <= 0) return 0;
            float k = Math.Min(1f, healthRatio);
            // 16 × 0.25 같은 딱 떨어지는 값이 부동소수 오차로 하나 더 올라가지 않게 아주 조금 깎고 올린다
            int n = (int)Math.Ceiling(clusterSize * k - 1e-4f);
            return Math.Max(1, Math.Min(clusterSize, n));
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
