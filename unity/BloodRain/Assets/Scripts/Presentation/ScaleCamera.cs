using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation
{
    /// <summary>한 생의 첫머리 연출(2026-11-17 사람: "중원 전체 맵 적용이 하나도 안 돼 있다").
    /// 화면을 천하 지도가 보일 만큼 넓혔다가(대각선 fromDiag m) 태어난 자리로 내려온다. 바닥·소품은 MapLayer 가 화면 크기를 보고 알아서 걷고 덮는다.
    /// 게임 시간으로 간다 — 컷·레벨업이 세상을 멈추면 같이 멈춘다. 카메라 크기에만 곱하고 규칙(WorldScale)은 건드리지 않는다.</summary>
    public static class WorldIntro
    {
        static float _t = -1f, _hold, _dur, _from;

        public static bool Playing => _t >= 0f;
        /// <summary>0(막 시작) ~ 1(다 내려옴). 안 도는 중이면 1.</summary>
        public static float Progress => _t < 0f ? 1f : Mathf.Clamp01(_t / Mathf.Max(0.01f, _hold + _dur));
        /// <summary>머무는 구간의 몫(0~1). 땅 이름을 띄우는 쪽이 쓴다.</summary>
        public static float HoldShare => _hold / Mathf.Max(0.01f, _hold + _dur);

        public static void Begin(float fromDiag = 1900f, float hold = 0.9f, float dur = 2.4f)
        {
            _from = fromDiag; _hold = hold; _dur = dur; _t = 0f;
        }

        public static void Skip() => _t = -1f;

        /// <summary>카메라 직교 크기에 곱할 값(1 이상). ScaleCamera 가 프레임마다 한 번 부른다.</summary>
        public static float Step(float viewDiag, float dt)
        {
            if (_t < 0f) return 1f;
            float top = Mathf.Max(1f, _from / Mathf.Max(1f, viewDiag));
            float k = Mathf.Clamp01((_t - _hold) / Mathf.Max(0.01f, _dur));
            _t += dt;
            if (_t >= _hold + _dur) { _t = -1f; return 1f; }
            // 로그로 내려와야 속도가 고르게 느껴진다. 끝은 살짝 감속
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(top), 0f, e));
        }
    }

    // 직교 카메라 크기를 WorldScale 의 화면 대각선(세계 m)에 맞춘다(기획서 7장).
    // 따라갈 대상(target)이 있으면 그 위치를 가운데에 둔다.
    [RequireComponent(typeof(Camera))]
    public sealed class ScaleCamera : MonoBehaviour
    {
        public Transform target;

        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        void LateUpdate()
        {
            var s = WorldScale.Current;
            if (s == null) return;
            s.Tick(Time.unscaledDeltaTime);
            // 큰 타격에 화면이 한 번 눌린다(HitFeel.Jolt, 5%까지). 값은 아래 Tick 이 프레임마다 가라앉힌다
            // 한 생의 첫머리: 천하 한 장에서 태어난 자리로 내려온다(WorldIntro). 끝나면 1
            _cam.orthographicSize = WorldScale.OrthoSize(s.ViewDiagonal, _cam.aspect) * BloodRain.Presentation.Fx.HitFeel.Zoom
                * WorldIntro.Step(s.ViewDiagonal, Time.deltaTime);
            if (target != null)
            {
                var p = target.position;
                transform.position = new Vector3(p.x, p.y, transform.position.z);
            }
            // HF-02U 히트스톱 풀기와 화면 흔들림(미터 → 지금 경지의 월드 길이). 대상이 없어도 시계는 돌린다
            var shake = BloodRain.Presentation.Fx.HitFeel.Tick(Time.unscaledDeltaTime) * s.Length(1f);
            if (target != null) transform.position += new Vector3(shake.x, shake.y, 0f);
        }
    }
}
