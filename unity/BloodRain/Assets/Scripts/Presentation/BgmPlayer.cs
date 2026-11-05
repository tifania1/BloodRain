using System.Collections.Generic;
using BloodRain.Core.World;
using BloodRain.Presentation.Fx;
using BloodRain.Presentation.Meta;
using UnityEngine;

namespace BloodRain.Presentation
{
    // BGM-06U 배경음 재생기. AudioSource 둘을 번갈아 쓰며 BgmDirector 가 곡을 바꾸라 하면 FadeSeconds 동안 등가 출력으로 섞는다.
    // 씬을 넘어가도 이어지게 DontDestroyOnLoad. Run·Inn 씬에 하나씩 붙여 두고, 먼저 살아 있는 것이 있으면 새로 뜬 쪽은 스스로 사라진다.
    // 곡 파일은 Assets/Audio/BGM/bgm.json(tools/postprocess_audio.py) 목록대로 Bootstrap 메뉴가 clips 에 채운다. 없는 곡이면 로그 한 줄만 남기고 조용히.
    public sealed class BgmPlayer : MonoBehaviour
    {
        [System.Serializable]
        public struct Track { public string id; public AudioClip clip; public bool loop; }

        public List<Track> clips = new List<Track>();
        [Range(0f, 1f)] public float volume = 0.7f;
        public bool showDebug = true;

        public static BgmPlayer Instance { get; private set; }

        /// <summary>지금 트는 곡 id(파일이 없어도 디렉터가 고른 id).</summary>
        public string CurrentId => _director?.Current;

        AudioSource _a, _b;   // _a 가 지금 곡, _b 가 넘어가는 앞 곡
        BgmDirector _director;
        float _fade, _fadeLeft;
        readonly HashSet<string> _missingLogged = new HashSet<string>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 앞 씬에서 넘어온 재생기가 있으면 그쪽이 곡을 잇는다. 곡 목록은 비어 있으면 이쪽 것으로 채워 준다
                if (Instance.clips.Count == 0) Instance.clips = clips;
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _a = MakeSource();
            _b = MakeSource();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            return s;
        }

        void Update()
        {
            var data = DataBoot.Data;
            if (data == null) return;
            if (_director == null) _director = new BgmDirector(data.Balance.Bgm);

            float dt = Time.unscaledDeltaTime; // 레벨업·정산 화면은 시간을 멈추지만 곡은 흘러야 한다
            var change = _director.Tick(dt, CurrentState(out bool breakthrough), breakthrough);
            if (change.HasValue) Switch(change.Value);

            if (_fadeLeft > 0f)
            {
                _fadeLeft = Mathf.Max(0f, _fadeLeft - dt);
                float t = _fade > 0f ? 1f - _fadeLeft / _fade : 1f;
                // 등가 출력: 들어오는 곡 sin, 나가는 곡 cos
                _a.volume = volume * Mathf.Sin(t * Mathf.PI * 0.5f);
                _b.volume = volume * Mathf.Cos(t * Mathf.PI * 0.5f);
                if (_fadeLeft <= 0f) { _b.Stop(); _b.clip = null; }
            }
            else
            {
                _a.volume = volume;
            }
        }

        void Switch(BgmChange c)
        {
            // 지금 곡을 뒤로 돌리고 새 곡을 앞에
            (_a, _b) = (_b, _a);
            _a.Stop();
            _a.clip = Find(c.TrackId, out bool loop);
            _a.loop = loop;
            _a.volume = 0f;
            if (_a.clip != null) _a.Play();
            _fade = Mathf.Max(0f, c.FadeSeconds);
            _fadeLeft = _fade;
            if (_fade <= 0f)
            {
                _a.volume = volume;
                _b.Stop(); _b.clip = null;
            }
            Debug.Log($"[배경음] {c.TrackId}{(_a.clip == null ? " (곡 파일 없음)" : "")} · 넘김 {c.FadeSeconds:0.##}초");
        }

        AudioClip Find(string id, out bool loop)
        {
            loop = true;
            // 격해진 벌 곡이 없으면 기본 곡으로
            foreach (var key in new[] { id, Bgm.BaseTrack(id) })
                foreach (var t in clips)
                    if (t.id == key && t.clip != null) { loop = t.loop; return t.clip; }
            if (_missingLogged.Add(id)) Debug.Log($"[배경음] 곡 파일이 아직 없음: {id}");
            return null;
        }

        /// <summary>지금 화면에서 BgmState 를 만든다.</summary>
        BgmState CurrentState(out bool breakthrough)
        {
            breakthrough = false;
            var lr = LifeRunner.Instance;
            if (lr != null)
            {
                var life = lr.Life;
                if (life.Record != null) return new BgmState(BgmScreen.Settle);
                if (lr.breakthrough != null) breakthrough = lr.breakthrough.IsPlaying;
                var region = life.Regions.Region(life.RegionId);
                // 땅의 주인 싸움(bossId)·종장 단계는 BR-01 뒤에 넣는다
                return new BgmState(BgmScreen.Life, life.IsPrologue, life.RegionId, null, 0, null,
                    region != null && region.Final, (float)(life.Minutes * 60.0));
            }
            var chim = FindFirstObjectByType<ChimbangScreen>();
            if (chim != null && chim.Draw != null) return new BgmState(BgmScreen.Chimbang);
            return new BgmState(BgmScreen.Inn);
        }

        void OnGUI()
        {
            if (!showDebug) return;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(12, 162, 600, 22), $"배경음  {CurrentId ?? "-"}{(_a != null && _a.clip == null && CurrentId != null ? " (파일 없음)" : "")}");
        }
    }
}
