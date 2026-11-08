using BloodRain.Core.Meta;
using BloodRain.Presentation.Meta;
using BloodRain.Sim;
using UnityEngine;

namespace BloodRain.Presentation
{
    // 플레이어 몸 그림(BD-01U, 기획서 13장 '움직임은 이미지 한 장 + 연출로').
    //   한 생 몸으로 Core BodyArt.SpriteIds 를 앞에서부터 찾아 있는 첫 그림을 자식 'Body' SpriteRenderer 에 그린다.
    //   그림이 없으면(아직 안 뽑은 문파) 아무것도 안 하고 PlayerController 의 동그라미가 그대로 남는다.
    //   목록(names·sprites)은 에디터 메뉴 BloodRain/Bootstrap/몸 그림 목록 채우기 가 Assets/Art/Characters 에서 채운다.
    //   크기: 13장 '화면에선 약 72px 높이'(1080 줄 화면, 삼류 시야 기준). 부모(플레이어)가 몸 지름 × WorldScale 로 커지므로
    //         그 배수로 한 번 셈해 두면 경지가 올라도 다른 것처럼 같이 줄어든다. 발밑(footV)이 몸 원 가운데.
    //   걷기: 움직이는 동안 좌우로 기울고 위아래로 튐, 왼쪽으로 가면 뒤집기. 피격 흰 번쩍임은 PlayerVitals 가 Flash 로 넣는다.
    public sealed class PlayerLook : MonoBehaviour
    {
        [Tooltip("그림 이름(확장자 없음). sprites 와 같은 차례")]
        public string[] names = new string[0];
        public Sprite[] sprites = new Sprite[0];
        [Tooltip("그림에서 발밑 높이(아래에서 uv). 메뉴가 알파로 잰다")]
        public float[] footV = new float[0];
        [Tooltip("그림에서 사람 높이(uv). 메뉴가 알파로 잰다")]
        public float[] bodyV = new float[0];
        public Material flashMaterial;

        [Header("크기(13장)")]
        public float screenPixels = 72f;
        public float referenceScreenHeight = 1080f;

        [Header("걷기(눈으로 정함, 기획서에 숫자 없음)")]
        [Tooltip("걸음 한 번(한쪽 발)마다 기우는 각도")]
        public float tiltDegrees = 6f;
        [Tooltip("튀는 높이(그림 높이 배수)")]
        public float bobHeight = 0.06f;
        [Tooltip("초당 걸음 수")]
        public float stepsPerSecond = 4f;
        [Tooltip("멈추면 제자리로 돌아오는 빠르기")]
        public float settleSpeed = 12f;

        public SpriteRenderer Renderer { get; private set; }
        public string SpriteId { get; private set; }
        /// <summary>흰색 번쩍임 0~1(PlayerVitals).</summary>
        public float Flash { get; set; }

        Transform _body;
        PlayerController _move;
        MaterialPropertyBlock _mpb;
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        float _height, _foot, _phase, _walk;
        Vector3 _last;
        bool _flip;

        void Start()
        {
            var data = DataBoot.Data;
            var body = RebirthHandoff.Body ?? Rebirth.Prologue(data);
            int found = -1;
            foreach (var id in BodyArt.SpriteIds(body))
            {
                for (int i = 0; i < names.Length && found < 0; i++)
                    if (names[i] == id && i < sprites.Length && sprites[i] != null) found = i;
                if (found >= 0) break;
            }
            if (found < 0) return; // 자리 표시(동그라미) 그대로
            SpriteId = names[found];
            _move = GetComponent<PlayerController>();
            var rootSr = GetComponent<SpriteRenderer>();

            var go = new GameObject("Body");
            _body = go.transform;
            _body.SetParent(transform, false);
            Renderer = go.AddComponent<SpriteRenderer>();
            Renderer.sprite = sprites[found];
            if (flashMaterial != null) Renderer.sharedMaterial = flashMaterial;
            if (rootSr != null)
            {
                Renderer.sortingLayerID = rootSr.sortingLayerID;
                Renderer.sortingOrder = rootSr.sortingOrder;
                rootSr.enabled = false;
            }
            _mpb = new MaterialPropertyBlock();

            // 부모 한 칸 = 몸 지름(세계). 삼류(Lv1) 시야에서 사람 높이가 screenPixels 가 되게 그 배수를 셈한다.
            var ws = new WorldScale(data);
            ws.SetLevel(1, snap: true);
            float ortho = WorldScale.OrthoSize(ws.ViewDiagonal, 16f / 9f);
            float personWorld = screenPixels / referenceScreenHeight * 2f * ortho;
            float diameterWorld = ws.Length(data.Balance.Stats.PlayerBodyRadius * 2f);
            float person = personWorld / diameterWorld; // 부모 칸 단위 사람 높이
            float bv = found < bodyV.Length && bodyV[found] > 0f ? bodyV[found] : 1f;
            _foot = found < footV.Length ? footV[found] : 0f;
            float spriteUnits = sprites[found].rect.height / sprites[found].pixelsPerUnit;
            _height = person / bv; // 그림 전체 높이(부모 칸)
            _body.localScale = Vector3.one * (_height / spriteUnits);
            _last = transform.position;
            Place(0f, 0f);
        }

        void Place(float bob, float tilt)
        {
            // 가운데 pivot 그림을 발밑이 원점에 오게 올리고, 발밑을 축으로 기운다
            var up = new Vector3(0f, (0.5f - _foot) * _height, 0f);
            var rot = Quaternion.Euler(0f, 0f, tilt);
            _body.localRotation = rot;
            _body.localPosition = rot * up + new Vector3(0f, bob * _height, 0f);
        }

        void LateUpdate()
        {
            if (_body == null) return;
            float dt = Time.deltaTime;
            var p = transform.position;
            bool moving = dt > 0f && (p - _last).sqrMagnitude > 1e-10f;
            _last = p;
            if (_move != null)
            {
                float fx = _move.Facing.x;
                if (fx < -0.1f) _flip = true; else if (fx > 0.1f) _flip = false;
            }
            Renderer.flipX = _flip;

            _walk = Mathf.MoveTowards(_walk, moving ? 1f : 0f, settleSpeed * dt);
            if (moving) _phase += dt * stepsPerSecond * Mathf.PI; // 반 바퀴 = 한 걸음
            else if (_walk <= 0f) _phase = 0f;
            float s = Mathf.Sin(_phase);
            Place(Mathf.Abs(s) * bobHeight * _walk, s * tiltDegrees * _walk);

            Renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashId, Mathf.Clamp01(Flash));
            Renderer.SetPropertyBlock(_mpb);
        }
    }
}
