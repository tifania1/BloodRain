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
        static readonly int HitBlendId = Shader.PropertyToID("_HitEffectBlend");
        bool _allIn1;
        float _height, _foot, _phase, _walk;
        // HF-02U 공격 방향으로 몸이 살짝 튀어나감(월드 벡터, 빠르게 돌아옴)과 경공 잔상
        Vector2 _lunge;
        struct Ghost { public SpriteRenderer R; public float Age; }
        readonly System.Collections.Generic.List<Ghost> _ghosts = new System.Collections.Generic.List<Ghost>();
        float _ghostClock;
        const float GhostLife = 0.28f, GhostEvery = 0.035f;
        static readonly Color GhostInk = new Color(0.16f, 0.17f, 0.2f, 0.5f);

        /// <summary>공격하는 쪽으로 몸을 worldDistance 만큼 내밀었다가 돌아온다(그림만, 판정 자리는 그대로).</summary>
        public void Lunge(Vector2 dir, float worldDistance)
        {
            if (dir.sqrMagnitude < 1e-8f) return;
            _lunge = dir.normalized * worldDistance;
        }

        void TickGhosts(float dt)
        {
            if (Renderer == null) return;
            bool dashing = _move != null && _move.Dash != null && _move.Dash.IsDashing;
            _ghostClock -= dt;
            if (dashing && _ghostClock <= 0f && !BloodRain.Presentation.Fx.FxSettings.Simplified)
            {
                _ghostClock = GhostEvery;
                SpriteRenderer g = null;
                for (int i = 0; i < _ghosts.Count; i++) if (_ghosts[i].Age >= GhostLife) { g = _ghosts[i].R; _ghosts.RemoveAt(i); break; }
                if (g == null && _ghosts.Count < 10)
                {
                    var go = new GameObject("DashGhost");
                    g = go.AddComponent<SpriteRenderer>();
                    g.sortingLayerID = Renderer.sortingLayerID;
                    g.sortingOrder = Renderer.sortingOrder - 1;
                }
                if (g != null)
                {
                    g.sprite = Renderer.sprite;
                    g.flipX = Renderer.flipX;
                    g.transform.SetPositionAndRotation(_body.position, _body.rotation);
                    g.transform.localScale = _body.lossyScale;
                    g.enabled = true;
                    _ghosts.Add(new Ghost { R = g, Age = 0f });
                }
            }
            for (int i = 0; i < _ghosts.Count; i++)
            {
                var gh = _ghosts[i];
                if (gh.Age >= GhostLife) continue;
                gh.Age += dt;
                float k = Mathf.Clamp01(gh.Age / GhostLife);
                var c = GhostInk; c.a *= (1f - k) * (1f - k);
                gh.R.color = c;
                if (gh.Age >= GhostLife) gh.R.enabled = false;
                _ghosts[i] = gh;
            }
        }

        // VF-04U 팔선과해(취선봉 절기): 몸 둘레에 호박빛 분신이 서서 바깥으로 휩쓴다(시안 eightImm — 0.7초, 0.08초에 나타나 끝 40% 에 옅어짐, 비틀비틀)
        struct CloneFx { public SpriteRenderer R; public float Age, Seed; }
        readonly System.Collections.Generic.List<CloneFx> _clones = new System.Collections.Generic.List<CloneFx>();
        const float CloneLife = 0.7f;
        static readonly Color CloneAmber = new Color(0.62f, 0.4f, 0.16f, 0.55f);

        /// <summary>분신 하나를 world 자리에 세운다(그림만). delay 초 뒤에 나타난다.</summary>
        public void Clone(Vector2 world, float delay)
        {
            if (Renderer == null || BloodRain.Presentation.Fx.FxSettings.Simplified) return;
            SpriteRenderer g = null;
            for (int i = 0; i < _clones.Count; i++) if (_clones[i].Age >= CloneLife) { g = _clones[i].R; _clones.RemoveAt(i); break; }
            if (g == null && _clones.Count < 24)
            {
                var go = new GameObject("Clone");
                g = go.AddComponent<SpriteRenderer>();
                g.sortingLayerID = Renderer.sortingLayerID;
                g.sortingOrder = Renderer.sortingOrder - 1;
            }
            if (g == null) return;
            g.sprite = Renderer.sprite;
            g.flipX = world.x < transform.position.x;
            var off = _body.position - transform.position;
            g.transform.SetPositionAndRotation(new Vector3(world.x, world.y, transform.position.z) + off, Quaternion.identity);
            g.transform.localScale = _body.lossyScale;
            g.color = new Color(CloneAmber.r, CloneAmber.g, CloneAmber.b, 0f);
            g.enabled = true;
            _clones.Add(new CloneFx { R = g, Age = -delay, Seed = _clones.Count * 1.7f });
        }

        void TickClones(float dt)
        {
            for (int i = 0; i < _clones.Count; i++)
            {
                var c = _clones[i];
                if (c.Age >= CloneLife) continue;
                c.Age += dt;
                if (c.Age >= 0f)
                {
                    float k = Mathf.Clamp01(c.Age / CloneLife);
                    float al = Mathf.Min(1f, c.Age / 0.08f) * (1f - Mathf.Max(0f, (k - 0.6f) / 0.4f));
                    var col = CloneAmber; col.a *= al;
                    c.R.color = col;
                    c.R.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(c.Age * 14f + c.Seed) * 8.6f);
                }
                if (c.Age >= CloneLife) c.R.enabled = false;
                _clones[i] = c;
            }
        }

        void OnDestroy()
        {
            foreach (var g in _ghosts) if (g.R != null) Destroy(g.R.gameObject);
            foreach (var c in _clones) if (c.R != null) Destroy(c.R.gameObject);
        }
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
            // 산 에셋 All In 1 Sprite Shader 재질(Resources/Fx/PlayerAllIn1 — 셰이더는 PC 에만, docs/design/store_assets.md).
            // 옅은 빛 테두리로 적 무리 속에서 내 몸이 묻히지 않게 하고, 피격 번쩍임은 그 셰이더의 Hit Effect 로 낸다. 셰이더가 없으면 옛 재질 그대로
            var allIn1 = Resources.Load<Material>("Fx/PlayerAllIn1");
            if (allIn1 != null && allIn1.shader != null && allIn1.shader.name.StartsWith("AllIn1SpriteShader")) { Renderer.sharedMaterial = allIn1; _allIn1 = true; }
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

        bool _tinted;

        /// <summary>대장간 몸 색(SM-01, cheonha.json inn.smithy.tints)을 몸 그림에 곱한다. 생이 생긴 뒤 한 번.</summary>
        void ApplyTint()
        {
            if (_tinted || Renderer == null) return;
            var life = LifeRunner.Instance != null ? LifeRunner.Instance.Life : null;
            if (life == null || life.Soul == null) return;
            _tinted = true;
            var def = life.Soul.Data.Cheonha.Inn.Smithy.Tints.Find(t => t.Id == life.Soul.BodyTint);
            if (def != null && def.Rgb.Length >= 3) Renderer.color = new Color(def.Rgb[0], def.Rgb[1], def.Rgb[2], Renderer.color.a);
        }

        void LateUpdate()
        {
            ApplyTint();
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
            if (_lunge.sqrMagnitude > 1e-10f)
            {
                _body.position += new Vector3(_lunge.x, _lunge.y, 0f);
                _lunge *= Mathf.Pow(0.0005f, dt);
            }
            TickGhosts(dt);
            TickClones(dt);

            Renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashId, Mathf.Clamp01(Flash));
            if (_allIn1) _mpb.SetFloat(HitBlendId, Mathf.Clamp01(Flash));
            Renderer.SetPropertyBlock(_mpb);
        }
    }
}
