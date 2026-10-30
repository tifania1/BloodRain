using System;
using BloodRain.Core.Data;
using BloodRain.Core.World;

namespace BloodRain.Sim
{
    // 축척 배율을 곱하는 곳은 여기 한 군데다(기획서 15장 '배율 한 곳에서'). 계산 자체는 Core.World.Scale.
    //
    // 단위: Unity 1 = 세계 m(삼류 m). 전투 수치(크기·속도·사거리)는 경지 m 로 적혀 있으니
    // 화면에 놓을 때 Length/Speed 로 세계 m 로 바꾼다. 다른 곳에서 2.25^n 을 직접 곱하지 않는다.
    //
    // 화면 대각선은 경지 순번 + 0.35 × 경지 안 진행(축척 지수)을 바로 쓰지 않고, 지수를 언스케일 시간으로
    // 천천히 따라가게 해서 레벨이 오를 때 튀지 않게 한다. 돌파 연출(TU-02)은 DiagonalOverride 에
    // Breakthrough.ViewDiagonal 을 넣어 이 보간을 잠시 대신한다.
    public sealed class WorldScale
    {
        /// <summary>지금 판의 축척. 판을 여는 쪽(시험 씬은 ScaleTestDriver)이 넣는다.</summary>
        public static WorldScale Current { get; set; }

        public readonly Scale Scale;

        float _level = 1f;
        float _shownExponent;

        /// <summary>화면 대각선 축척 지수가 목표를 따라가는 시간 상수(초, 언스케일).</summary>
        public float FollowTime = 0.35f;

        /// <summary>값이 있으면 화면 대각선(세계 m)을 이 값으로 쓴다. 돌파 연출이 쓴다.</summary>
        public float? DiagonalOverride;

        public WorldScale(GameData data) : this(new Scale(data)) { }

        public WorldScale(Scale scale)
        {
            Scale = scale;
            _shownExponent = TargetExponent;
        }

        /// <summary>레벨(소수면 진기 비율까지 넣은 값).</summary>
        public float Level => _level;
        /// <summary>지금 경지 순번(0 = 삼류).</summary>
        public int Realm => Scale.RealmIndex((int)Math.Floor(_level));
        public string RealmName => Scale.Realms[Realm].NameKo;
        /// <summary>지금 경지 1m 가 세계 몇 m 인가(2.25^n). 돌파 연출 중에는 RealmFactorOverride 가 앞선다.</summary>
        public float RealmFactor => RealmFactorOverride ?? Scale.Factor(Realm);

        /// <summary>값이 있으면 지금 경지 배율을 이 값으로 쓴다. 돌파 연출이 화면과 함께 천천히 넘긴다.</summary>
        public float? RealmFactorOverride;

        public float TargetExponent => Scale.ViewExponent(_level);
        public float ShownExponent => _shownExponent;

        public void SetLevel(float level, bool snap = false)
        {
            _level = Math.Max(1f, level);
            if (snap) _shownExponent = TargetExponent;
        }

        /// <summary>매 프레임 한 번. Time.unscaledDeltaTime 을 넘긴다.</summary>
        public void Tick(float unscaledDt)
        {
            float k = FollowTime <= 0f ? 1f : 1f - (float)Math.Exp(-unscaledDt / FollowTime);
            _shownExponent += (TargetExponent - _shownExponent) * k;
        }

        /// <summary>지금 화면 대각선(세계 m).</summary>
        public float ViewDiagonal => DiagonalOverride ?? Scale.DiagonalAtExponent(_shownExponent);

        /// <summary>직교 카메라 크기(세로 절반) = 대각선 × 세로 ÷ √(가로² + 세로²) ÷ 2. 16:9 면 대각선 × 0.245.</summary>
        public static float OrthoSize(float diagonal, float aspect) =>
            diagonal / (float)Math.Sqrt(1.0 + aspect * aspect) / 2f;

        // ---- 경지 m → 세계 m ----

        /// <summary>지금 경지에서 생긴 것(플레이어·무공 이펙트·사거리)의 길이.</summary>
        public float Length(float realmMeters) => realmMeters * RealmFactor;
        /// <summary>bornRealm 에서 생긴 것(적·소품)의 길이. 아래 경지 적은 작게 남는다.</summary>
        public float Length(float realmMeters, int bornRealm) => realmMeters * Scale.Factor(bornRealm);
        public float Speed(float realmMetersPerSecond) => realmMetersPerSecond * RealmFactor;
        public float Speed(float realmMetersPerSecond, int bornRealm) => realmMetersPerSecond * Scale.Factor(bornRealm);
    }
}
