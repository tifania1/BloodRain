using System.Collections.Generic;
using BloodRain.Core.World;
using UnityEngine;

namespace BloodRain.Presentation
{
    // SFX-14U 효과음 재생기. BgmPlayer 옆에 붙고 씬을 넘어가도 하나만 산다(DontDestroyOnLoad).
    // '울려도 되나'는 Core SfxGate(같은 id 0.05초에 4번, 흡수음 한 프레임에 하나·연속 흡수 음계)가 정하고,
    // 여기서는 sfx.json 의 변형 가운데 하나를 무작위로 골라 AudioSource 풀에서 튼다.
    // 목록은 Bootstrap 메뉴(SfxBootstrap)가 Assets/Audio/SFX/sfx.json 대로 채운다. 흡수음은 id 가 sfx_xp_absorb_n1~n8.
    public sealed class SfxPlayer : MonoBehaviour
    {
        [System.Serializable]
        public struct Clip { public string id; public AudioClip clip; public float gainDb; }

        public List<Clip> clips = new List<Clip>();
        [Range(0f, 1f)] public float volume = 0.8f;   // 설정 음량(WP-57U GameSettings audio.master)이 생기면 곱한다
        public int poolSize = 16;
        public bool showDebug = true;

        public static SfxPlayer Instance { get; private set; }

        /// <summary>SfxGate 가 막아 버린 수(디버그).</summary>
        public int Dropped { get; private set; }
        /// <summary>실제로 튼 수(디버그).</summary>
        public int Played { get; private set; }
        public string LastId { get; private set; }

        SfxGate _gate;
        readonly List<AudioSource> _pool = new List<AudioSource>();
        int _next;
        Dictionary<string, List<Clip>> _byId;
        readonly HashSet<string> _missingLogged = new HashSet<string>();
        readonly Queue<string> _recent = new Queue<string>();   // 디버그: 최근 튼 소리(짧게)

        /// <summary>최근 튼 소리 열둘(흡수음은 n1~n8 만). 디버그 줄용.</summary>
        public string Recent => string.Join(" ", _recent);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.clips.Count == 0) { Instance.clips = clips; Instance._byId = null; }
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < Mathf.Max(1, poolSize); i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _pool.Add(s);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        SfxGate Gate()
        {
            if (_gate == null)
            {
                var data = DataBoot.Data;
                if (data == null) return null;
                _gate = new SfxGate(data.Balance.Sfx);
            }
            return _gate;
        }

        /// <summary>효과음 하나. 같은 id 가 몰리면 SfxGate 가 버린다.</summary>
        public void Play(string id)
        {
            var g = Gate();
            if (g == null) return;
            if (!g.TryPlay(id, Time.unscaledTimeAsDouble)) { Dropped++; return; }
            PlayClip(id);
        }

        /// <summary>진기 흡수음. 한 프레임에 하나, 연달아 먹으면 음이 오른다.</summary>
        public void PlayAbsorb()
        {
            var g = Gate();
            if (g == null) return;
            if (!g.TryAbsorb(Time.frameCount, Time.unscaledTimeAsDouble, out int note)) { Dropped++; return; }
            PlayClip(SfxGate.AbsorbSoundId(note));
        }

        /// <summary>BT-01U 경지 돌파 연출이 시작됐다: seconds(실제 초) 동안 효과음을 끊고 붓 긋는 소리를 한 번(14장).
        /// 연출 도중 또 불리면 끊는 끝만 늦추고 붓 소리는 다시 안 튼다.</summary>
        public void Hush(float seconds)
        {
            var g = Gate();
            if (g == null) return;
            double now = Time.unscaledTimeAsDouble;
            bool already = g.Hushed(now);
            g.Hush(now, Mathf.Max(0f, seconds));
            if (!already) Play(SfxGate.BreakthroughBrush);
        }

        /// <summary>배경음에 곱할 배: 돌파 연출 동안 0, 끝나고 returnFadeSeconds 에 걸쳐 1(Core SfxGate.BgmVolume).</summary>
        public float BgmVolume(float returnFadeSeconds)
            => _gate == null ? 1f : _gate.BgmVolume(Time.unscaledTimeAsDouble, returnFadeSeconds);

        /// <summary>한 생이 끝나면 비운다.</summary>
        public void Reset()
        {
            _gate?.Reset();
            Dropped = 0;
            Played = 0;
            _recent.Clear();
        }

        void PlayClip(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, List<Clip>>();
                foreach (var c in clips)
                {
                    if (c.clip == null || string.IsNullOrEmpty(c.id)) continue;
                    if (!_byId.TryGetValue(c.id, out var l)) _byId[c.id] = l = new List<Clip>();
                    l.Add(c);
                }
            }
            if (!_byId.TryGetValue(id, out var list) || list.Count == 0)
            {
                if (_missingLogged.Add(id)) Debug.Log($"[효과음] 소리 파일이 아직 없음: {id}");
                return;
            }
            var pick = list[Random.Range(0, list.Count)];
            var src = NextSource();
            src.clip = pick.clip;
            src.volume = volume * Mathf.Pow(10f, pick.gainDb / 20f);
            src.Play();
            Played++;
            LastId = id;
            _recent.Enqueue(id.StartsWith("sfx_xp_absorb_") ? id.Substring(14) : id.Replace("sfx_", ""));
            while (_recent.Count > 12) _recent.Dequeue();
        }

        AudioSource NextSource()
        {
            // 쉬는 것을 먼저, 다 울리는 중이면 돌아가며 가장 오래된 자리를 뺏는다
            for (int i = 0; i < _pool.Count; i++)
            {
                var s = _pool[(_next + i) % _pool.Count];
                if (!s.isPlaying) { _next = (_next + i + 1) % _pool.Count; return s; }
            }
            var t = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            t.Stop();
            return t;
        }

        void OnGUI()
        {
            ImguiFont.Use(); // 웹 빌드에서 한글이 빠지지 않게(WG-01Ub)
            if (!showDebug) return;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(12, 184, 900, 22), $"효과음  튼 수 {Played} · 버린 수 {Dropped} · 최근 {Recent}");
        }
    }
}
