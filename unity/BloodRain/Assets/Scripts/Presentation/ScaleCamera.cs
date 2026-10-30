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
        }
    }
}
