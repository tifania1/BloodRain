using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation
{
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
            _cam.orthographicSize = WorldScale.OrthoSize(s.ViewDiagonal, _cam.aspect);
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
