#nullable enable
// 효과음을 지금 울려도 되나(기획서 14장 '동시 재생 제한', balance.json sfx, backlog SFX-14).
//
// 적 수백이 한꺼번에 맞고 쓰러지면 같은 소리가 한 프레임에 수십 번 불린다. SfxGate 는 '울려도 되나'만 정하고
// 무엇을 어떻게 틀지는 Unity 재생기(sfx.json variants 중 하나를 고름)가 한다.
// - 같은 효과음 id 는 sameSoundWindowSeconds(0.05초) 안에 sameSoundMaxInWindow(4)개까지.
// - 진기 흡수음은 한 프레임에 하나. 구슬 열 개를 한 프레임에 먹어도 소리는 한 번.
// - 연속 흡수 음계(14장 '보상' 줄 '연속 흡수 때 음계가 올라감'): 앞 흡수음에서 absorbRunGapSeconds 안이면 한 음 올리고
//   absorbNotes 째 음에서 멈춘다. 틈이 벌어지면 첫 음부터. 웹 프로토타입 sfx('orb') 와 같은 셈.
// 시간은 실제 초(멈춤·돌파 연출로 게임 시간이 느려져도 소리 창은 실제 시간)로 넘긴다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    public sealed class SfxGate
    {
        readonly SfxRulesDef _r;
        readonly Dictionary<string, Queue<double>> _recent = new Dictionary<string, Queue<double>>();
        long _absorbFrame = long.MinValue;
        double _lastAbsorb = double.NegativeInfinity;
        int _absorbStep;

        public SfxGate(SfxRulesDef rules) { _r = rules; }

        /// <summary>같은 효과음 id 를 지금(now 초) 울려도 되면 참이고 그 한 번을 센다. 거짓이면 이번 것은 버린다.</summary>
        public bool TryPlay(string id, double now)
        {
            if (!_recent.TryGetValue(id, out var q)) _recent[id] = q = new Queue<double>();
            // 창 밖으로 나간 것을 지운다. 창 끝(정확히 window 초 전)은 이미 지난 것으로 본다.
            while (q.Count > 0 && now - q.Peek() >= _r.SameSoundWindowSeconds) q.Dequeue();
            if (q.Count >= Math.Max(1, _r.SameSoundMaxInWindow)) return false;
            q.Enqueue(now);
            return true;
        }

        /// <summary>진기 흡수음. 이 프레임(frame)에 이미 울렸으면 거짓. 울리면 note 에 0부터 센 음(0 = n1 … absorbNotes-1).</summary>
        public bool TryAbsorb(long frame, double now, out int note)
        {
            note = _absorbStep;
            if (frame == _absorbFrame) return false;
            _absorbFrame = frame;
            _absorbStep = now - _lastAbsorb < _r.AbsorbRunGapSeconds
                ? Math.Min(Math.Max(1, _r.AbsorbNotes) - 1, _absorbStep + 1)
                : 0;
            _lastAbsorb = now;
            note = _absorbStep;
            return true;
        }

        /// <summary>sfx.json 파일 이름 꼴의 흡수음 id(n1~n8). 변형(_1~_5)은 재생기가 고른다.</summary>
        public static string AbsorbSoundId(int note) => $"sfx_xp_absorb_n{note + 1}";

        /// <summary>한 생이 끝나거나 화면이 바뀔 때 비운다.</summary>
        public void Reset()
        {
            _recent.Clear();
            _absorbFrame = long.MinValue;
            _lastAbsorb = double.NegativeInfinity;
            _absorbStep = 0;
        }
    }
}
