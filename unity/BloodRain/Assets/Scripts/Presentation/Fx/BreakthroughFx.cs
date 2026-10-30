using System;
using BloodRain.Core.World;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 경지 돌파 줌(기획서 7장 '돌파 연출', TU-02). 시간표와 화면 대각선은 Core.World.Breakthrough 가 정하고,
    // 여기서는 그 값을 화면에 옮긴다. 연출 시계는 언스케일.
    //
    //   0초       Stamp      세상 시간 0.05배, 흑백 심상(각성 연출의 Full Screen Pass BloodRain/AwakenHole 재사용).
    //                        공간 해시 격자를 새 배율로 다시 짓는 자리(RealmFactorChanged).
    //   0.2~1.4초 ZoomStart  카메라 대각선과 세계 배율(WorldScale.RealmFactor)을 같은 진행값으로 함께 넘긴다.
    //   0.8초     Sweep      쓸림 신호(Swept). 밟힘 처리는 TU-03.
    //   1.4초     End        세상 시간 1로. 깨달음 3택1 자리(Ended).
    //   0~1.4초   적 생성 멈춤(SpawnHeld).
    // 세상 시간 0.05배는 Core 주석대로 1.4초 내내 건다(줌 도중 적이 움직이면 쓸림 반경이 흐트러진다).
    //
    // 이어 받기(TU-15): 연출 도중 또 한 경지를 넘으면(큰 구슬·쓸림 진기) Play 가 Core Breakthrough.Extend 로 끝만 옮긴다.
    // 화면 대각선은 Core 가, 세계 배율과 혈우 장막 반경은 여기서 지금 값이 이어지게 출발점을 다시 맞춘다.
    // 새 경지 배율은 RealmFactorChanged 로 다시 알린다(새 경지 글자는 ToRealm 을 읽는 쪽이 바꾼다).
    public sealed class BreakthroughFx : MonoBehaviour
    {
        static readonly int IdCenter = Shader.PropertyToID("_AwakenCenter");
        static readonly int IdRadius = Shader.PropertyToID("_AwakenRadius");
        static readonly int IdAmount = Shader.PropertyToID("_AwakenAmount");
        static readonly int IdSoft = Shader.PropertyToID("_AwakenSoft");
        static readonly int IdVignette = Shader.PropertyToID("_AwakenVignette");
        static readonly int IdInvert = Shader.PropertyToID("_AwakenInvert");

        [Tooltip("흑백 심상이 다 켜진 채로 머무는 시간(초, 연출 시계). 기획서 표의 0~0.2초.")]
        [SerializeField] float monoHoldSeconds = 0.2f;
        [Tooltip("그 뒤 색이 돌아오는 시간(초).")]
        [SerializeField] float monoFadeSeconds = 0.3f;
        [Tooltip("흑백 속에서 색이 남는 플레이어 둘레 반지름(화면 높이 기준).")]
        [SerializeField] float holeRadius = 0.05f;

        /// <summary>시험용: 0 이상이면 연출 시계가 이 시각에서 멈춘다(스크린샷용).</summary>
        public float debugFreezeAt = -1f;

        /// <summary>새 경지 배율로 바뀌는 순간(Stamp). 공간 해시 격자를 2.25배로 다시 짓는 호출 자리.</summary>
        public event Action<float> RealmFactorChanged;
        /// <summary>쓸림 순간(0.8초). 반경은 SweepRadius, 대상 고르기는 SelectSweep(둘 다 Breakthrough).</summary>
        public event Action<Breakthrough> Swept;
        /// <summary>연출이 끝났다. 깨달음 3택1과 각성 판정을 여는 자리.</summary>
        public event Action<Breakthrough> Ended;

        [Tooltip("현경으로 넘어 장막이 걷힐 때 반경이 이 배수까지 밀려나며 옅어진다.")]
        [SerializeField] float curtainLiftScale = 1.6f;

        Breakthrough _bt;
        WorldScale _ws;
        Transform _center;

        // 세계 배율 축척 지수의 출발·끝(이어 받기 때 출발점을 다시 맞춘다)
        float _factorExpFrom, _factorExpTo;
        // 혈우 장막 반경의 출발·끝(세계 m). _curtainTo 가 null 이면 걷히는 중
        float _curtainFrom;
        float? _curtainTo;
        bool _hasCurtain;
        float _liftFromProgress;

        public bool IsPlaying => _bt != null && _bt.IsActive;
        /// <summary>적 생성 디렉터가 본다.</summary>
        public bool SpawnHeld => _bt != null && _bt.SpawnHeld;
        public Breakthrough Core => _bt;

        /// <summary>연출 중 혈우 장막 반경(세계 m). 장막이 처음부터 없었으면 null.</summary>
        public float? CurtainRadius { get; private set; }
        /// <summary>연출 중 장막 진하기(0~1). 현경으로 걷힐 때 줄어든다.</summary>
        public float CurtainAlpha { get; private set; }
        /// <summary>장막 빗줄기 갈라짐(0~1) = sin(π × 줌 진행값).</summary>
        public float CurtainSplit { get; private set; }

        /// <summary>
        /// 돌파 연출 시작. fromLevel·toLevel 은 흡수 전후 레벨(소수면 진기 비율까지).
        /// 이미 연출 중이면 이어 받는다(toLevel 이 지금 끝보다 높을 때만, 시계·속도·장면 신호는 그대로).
        /// </summary>
        public bool Play(WorldScale ws, float fromLevel, float toLevel, Transform center, bool revisit = false)
        {
            if (IsPlaying) return Extend(toLevel);
            var data = DataBoot.Data;
            _ws = ws;
            _center = center;
            _bt = new Breakthrough(ws.Scale, data.Cheonha.Breakthrough, data.Cheonha.Crush);
            _bt.Start(fromLevel, toLevel, revisit);
            _factorExpFrom = _bt.FromRealm;
            _factorExpTo = _bt.ToRealm;
            _hasCurtain = _bt.FromBoundaryRadius.HasValue;
            _curtainFrom = _bt.FromBoundaryRadius ?? 0f;
            _curtainTo = _bt.ToBoundaryRadius;
            _liftFromProgress = 0f;
            ws.DiagonalOverride = _bt.ViewDiagonal;
            ws.RealmFactorOverride = ws.Scale.Factor(_bt.FromRealm);
            ws.SetLevel(toLevel);
            Shader.SetGlobalFloat(IdSoft, 0.05f);
            Shader.SetGlobalFloat(IdVignette, 0.45f);
            Shader.SetGlobalFloat(IdInvert, 0f);
            Step(0f);
            return true;
        }

        /// <summary>연출 도중 또 넘은 경지까지 끝을 옮긴다. 지금 화면·배율·장막 반경은 튀지 않는다.</summary>
        bool Extend(float toLevel)
        {
            if (toLevel <= _bt.ToLevel) return false;
            int oldTo = _bt.ToRealm;
            float p = _bt.ZoomProgress;
            float curFactorExp = _factorExpFrom + (_factorExpTo - _factorExpFrom) * p;
            float? curCurtain = CurtainRadiusAt(p);

            _bt.Extend(toLevel);
            _ws.SetLevel(toLevel);

            // 세계 배율: 지금 지수에서 새 끝까지 남은 진행값으로 가게 출발점을 다시 맞춘다(Core 의 대각선과 같은 식)
            _factorExpTo = _bt.ToRealm;
            if (p < 1f) _factorExpFrom = (curFactorExp - _factorExpTo * p) / (1f - p);

            // 장막 반경
            if (_hasCurtain && curCurtain.HasValue)
            {
                var newTo = _bt.ToBoundaryRadius;
                if (newTo.HasValue)
                {
                    _curtainTo = newTo;
                    if (p < 1f) _curtainFrom = (curCurtain.Value - newTo.Value * p) / (1f - p);
                }
                else if (_curtainTo.HasValue)
                {
                    // 이번에 현경까지 넘었다: 지금 반경에서 밀려나며 걷힌다
                    _curtainTo = null;
                    _liftFromProgress = p;
                    _curtainFrom = curCurtain.Value / Mathf.Lerp(1f, curtainLiftScale, p);
                }
            }

            if (_bt.ToRealm != oldTo) RealmFactorChanged?.Invoke(_ws.Scale.Factor(_bt.ToRealm));
            return true;
        }

        float? CurtainRadiusAt(float p)
        {
            if (!_hasCurtain) return null;
            return _curtainTo.HasValue
                ? Mathf.Lerp(_curtainFrom, _curtainTo.Value, p)
                : _curtainFrom * Mathf.Lerp(1f, curtainLiftScale, p);
        }

        void UpdateCurtain(float p)
        {
            CurtainRadius = CurtainRadiusAt(p);
            CurtainAlpha = !_hasCurtain ? 0f
                : _curtainTo.HasValue ? 1f
                : 1f - Mathf.Clamp01((p - _liftFromProgress) / Mathf.Max(0.0001f, 1f - _liftFromProgress));
            CurtainSplit = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p));
        }

        void Update()
        {
            if (!IsPlaying) return;
            float dt = Time.unscaledDeltaTime;
            if (debugFreezeAt >= 0f) dt = Mathf.Clamp(debugFreezeAt - _bt.Elapsed, 0f, dt);
            Step(dt);
        }

        void Step(float dt)
        {
            var cues = _bt.Tick(dt);
            float t = _bt.Elapsed;

            if ((cues & BreakthroughCue.Stamp) != 0)
                RealmFactorChanged?.Invoke(_ws.Scale.Factor(_bt.ToRealm));
            if ((cues & BreakthroughCue.Sweep) != 0)
                Swept?.Invoke(_bt);

            if (_bt.IsActive)
            {
                Time.timeScale = _bt.WorldTimeScale;
                _ws.DiagonalOverride = _bt.ViewDiagonal;
                // 세계 배율도 축척 지수로 같은 진행값만큼(대각선과 같은 모양으로 물러나게)
                float p = _bt.ZoomProgress;
                _ws.RealmFactorOverride = (float)Math.Pow(_ws.Scale.World.ScalePerRealm,
                    _factorExpFrom + (_factorExpTo - _factorExpFrom) * p);
                UpdateCurtain(p);
                SetMono(t);
            }

            if ((cues & BreakthroughCue.End) != 0) Finish();
        }

        void SetMono(float t)
        {
            float amount = t <= monoHoldSeconds ? 1f
                : 1f - Mathf.Clamp01((t - monoHoldSeconds) / Mathf.Max(0.0001f, monoFadeSeconds));
            var cam = Camera.main;
            if (cam != null && _center != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(_center.position);
                Shader.SetGlobalVector(IdCenter, new Vector4(vp.x, vp.y, 0, 0));
            }
            Shader.SetGlobalFloat(IdRadius, holeRadius);
            Shader.SetGlobalFloat(IdAmount, amount);
        }

        void Finish()
        {
            Time.timeScale = 1f;
            _ws.DiagonalOverride = null;
            _ws.RealmFactorOverride = null;
            _ws.SetLevel(_bt.ToLevel, snap: true);
            Shader.SetGlobalFloat(IdAmount, 0f);
            Ended?.Invoke(_bt);
        }

        void OnDisable()
        {
            if (!IsPlaying) return;
            Time.timeScale = 1f;
            if (_ws != null)
            {
                _ws.DiagonalOverride = null;
                _ws.RealmFactorOverride = null;
            }
            Shader.SetGlobalFloat(IdAmount, 0f);
        }
    }
}
