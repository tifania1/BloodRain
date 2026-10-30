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

        Breakthrough _bt;
        WorldScale _ws;
        Transform _center;

        public bool IsPlaying => _bt != null && _bt.IsActive;
        /// <summary>적 생성 디렉터가 본다.</summary>
        public bool SpawnHeld => _bt != null && _bt.SpawnHeld;
        public Breakthrough Core => _bt;

        /// <summary>돌파 연출 시작. fromLevel·toLevel 은 흡수 전후 레벨(소수면 진기 비율까지).</summary>
        public bool Play(WorldScale ws, float fromLevel, float toLevel, Transform center, bool revisit = false)
        {
            if (IsPlaying) return false;
            var data = DataBoot.Data;
            _ws = ws;
            _center = center;
            _bt = new Breakthrough(ws.Scale, data.Cheonha.Breakthrough, data.Cheonha.Crush);
            _bt.Start(fromLevel, toLevel, revisit);
            ws.DiagonalOverride = _bt.ViewDiagonal;
            ws.RealmFactorOverride = ws.Scale.Factor(_bt.FromRealm);
            ws.SetLevel(toLevel);
            Shader.SetGlobalFloat(IdSoft, 0.05f);
            Shader.SetGlobalFloat(IdVignette, 0.45f);
            Shader.SetGlobalFloat(IdInvert, 0f);
            Step(0f);
            return true;
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
                float from = _bt.FromRealm, to = _bt.ToRealm;
                _ws.RealmFactorOverride = (float)Math.Pow(_ws.Scale.World.ScalePerRealm,
                    from + (to - from) * _bt.ZoomProgress);
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
