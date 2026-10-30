using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation.Fx
{
    // 혈우 장막(TU-05, 기획서 7장 '혈우 장막'·'돌파 연출'). 그림은 셰이더 BloodRain/BloodCurtain.
    // 반경은 Core Scale.BoundaryRadius(그 경지 첫 화면 대각선 × 2.4)에서 읽고 여기서 숫자를 정하지 않는다.
    //
    // 돌파 때(BreakthroughFx 가 돌 때): 반경을 줌과 같은 진행값(ZoomProgress)으로 새 경지 반경까지 밀어내고,
    // 진행값 가운데쯤 가장 크게 빗줄기 벽이 부채꼴로 갈라졌다가 다시 닫힌다(sin(π × 진행값)).
    // 새 경지에 장막이 없으면(현경) 밀려나면서 옅어져 걷힌다. 이 값들은 BreakthroughFx.Curtain* 가 셈한다.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BloodCurtain : MonoBehaviour
    {
        static readonly int IdCenter = Shader.PropertyToID("_Center");
        static readonly int IdRadius = Shader.PropertyToID("_Radius");
        static readonly int IdSplit = Shader.PropertyToID("_Split");
        static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
        static readonly int IdClock = Shader.PropertyToID("_Clock");
        static readonly int IdThick = Shader.PropertyToID("_Thick");
        static readonly int IdOuterSoft = Shader.PropertyToID("_OuterSoft");

        [Tooltip("태어난 자리(세계 m). 판을 여는 쪽이 넣는다.")]
        public Vector2 birthplace;
        [Tooltip("돌파 줌을 따라갈 연출. 비어 있으면 같은 오브젝트나 부모에서 찾는다.")]
        public BreakthroughFx breakthrough;
        [Tooltip("갈라짐이 가장 클 때 값(0~1).")]
        [Range(0f, 1f)] public float splitPeak = 1f;

        MeshRenderer _mr;
        MaterialPropertyBlock _mpb;

        /// <summary>지금 그리는 반경(세계 m). 장막이 없으면 0.</summary>
        public float ShownRadius { get; private set; }
        public float ShownSplit { get; private set; }
        public float ShownAlpha { get; private set; }

        void Awake()
        {
            _mr = GetComponent<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null) mf.sharedMesh = MakeQuad();
            _mr.sortingOrder = 20;
            if (breakthrough == null) breakthrough = GetComponentInParent<BreakthroughFx>();
        }

        void LateUpdate()
        {
            var ws = WorldScale.Current;
            if (ws == null || _mr.sharedMaterial == null) { _mr.enabled = false; return; }

            float radius = 0f, split = 0f, alpha = 0f;
            var fx = breakthrough;
            if (fx != null && fx.IsPlaying)
            {
                // 반경·진하기·갈라짐은 돌파 연출이 정한다(이어 받기 때도 튀지 않게 BreakthroughFx 가 맞춘다)
                radius = fx.CurtainRadius ?? 0f;
                alpha = fx.CurtainRadius.HasValue ? fx.CurtainAlpha : 0f;
                split = splitPeak * fx.CurtainSplit;
            }
            else
            {
                var br = ws.Scale.BoundaryRadius(ws.Realm);
                if (br.HasValue) { radius = br.Value; alpha = 1f; }
            }

            ShownRadius = alpha > 0f ? radius : 0f;
            ShownSplit = split;
            ShownAlpha = alpha;
            _mr.enabled = alpha > 0.001f && radius > 0f;
            if (!_mr.enabled) return;

            var mat = _mr.sharedMaterial;
            float reach = 1f + mat.GetFloat(IdThick) + mat.GetFloat(IdOuterSoft);
            transform.position = new Vector3(birthplace.x, birthplace.y, -1.5f);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * (radius * reach * 2.1f);

            _mr.GetPropertyBlock(_mpb);
            _mpb.SetVector(IdCenter, new Vector4(birthplace.x, birthplace.y, 0f, 0f));
            _mpb.SetFloat(IdRadius, radius);
            _mpb.SetFloat(IdSplit, split);
            _mpb.SetFloat(IdAlpha, alpha);
            _mpb.SetFloat(IdClock, Time.unscaledTime);
            _mr.SetPropertyBlock(_mpb);
        }

        static Mesh MakeQuad()
        {
            var m = new Mesh { name = "BloodCurtainQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(0.5f, 0.5f), new Vector3(-0.5f, 0.5f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }
    }
}
