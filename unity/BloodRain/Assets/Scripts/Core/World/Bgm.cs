#nullable enable
// 지금 틀 배경음 고르기(기획서 14장 '곡 목록', docs/design/audio_pipeline.md). 곡 id 는 audio_queue/queue.jsonl 과 같다.
//
// Bgm.TrackId 는 '지금 어떤 곡이어야 하나'만 정한다. 언제 바꾸고 얼마나 섞어 넘길지는 BgmDirector(balance.json bgm,
// 14장 '곡 바꾸기', backlog BGM-06): 지역 곡끼리는 새 지역에 4초 넘게 서 있으면 3초로, 나머지는 바로 1초로, 돌파 연출 동안은 그대로.
// Unity 재생기는 매 프레임 Tick 을 불러 돌아온 BgmChange 대로 섞어 넘기기만 한다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.World
{
    /// <summary>배경음을 고를 때 보는 화면.</summary>
    public enum BgmScreen
    {
        Title,
        Inn,
        /// <summary>침방(다음 몸 고르기·환혼 강화).</summary>
        Chimbang,
        /// <summary>연무장(영구 강화).</summary>
        Yeonmu,
        /// <summary>생이 끝난 뒤 정산.</summary>
        Settle,
        /// <summary>한 생을 사는 중(서장 포함).</summary>
        Life,
        Ending,
    }

    /// <summary>곡을 고를 때 필요한 지금 상태. 한 생 밖 화면은 Screen 만 보면 된다.</summary>
    public readonly struct BgmState
    {
        public readonly BgmScreen Screen;
        /// <summary>서장(첫 생)인가.</summary>
        public readonly bool Prologue;
        /// <summary>서 있는 지역 id(Life.RegionId).</summary>
        public readonly string? RegionId;
        /// <summary>땅의 주인과 싸우는 중이면 그 보스 id(enemies.json bosses). 아니면 null.</summary>
        public readonly string? BossId;
        /// <summary>종장 혈마 단계(1부터). 혈영을 넘기는 중이거나 종장이 아니면 0.</summary>
        public readonly int FinalStage;
        /// <summary>엔딩 id(cheonha.json cycle endings: seal·inherit·true).</summary>
        public readonly string? EndingId;
        /// <summary>그 지역이 종장 싸움터(봉마곡)인가. RegionDef.Final.</summary>
        public readonly bool FinalRegion;

        public BgmState(BgmScreen screen, bool prologue = false, string? regionId = null, string? bossId = null,
                        int finalStage = 0, string? endingId = null, bool finalRegion = false)
        {
            Screen = screen; Prologue = prologue; RegionId = regionId; BossId = bossId;
            FinalStage = finalStage; EndingId = endingId; FinalRegion = finalRegion;
        }
    }

    public static class Bgm
    {
        /// <summary>절기 각성 뒤 금빛 기운에 한 번 트는 짧은 곡(8장). 배경음 위에 겹친다.</summary>
        public const string AwakenSting = "sting_awaken";

        static readonly Dictionary<BgmScreen, string> ScreenTracks = new Dictionary<BgmScreen, string>
        {
            { BgmScreen.Title, "bgm_title" },
            { BgmScreen.Inn, "bgm_inn" },
            { BgmScreen.Chimbang, "bgm_chimbang" },
            { BgmScreen.Yeonmu, "bgm_yeonmu" },
            { BgmScreen.Settle, "bgm_settle" },
        };

        /// <summary>지금 틀 곡 id. 우선순위: 종장 단계 → 봉마곡(혈영 행렬) → 땅의 주인 싸움 → 서장 → 서 있는 지역.</summary>
        public static string TrackId(in BgmState s)
        {
            if (ScreenTracks.TryGetValue(s.Screen, out var screenTrack)) return screenTrack;
            if (s.Screen == BgmScreen.Ending)
                return "bgm_ending_" + (s.EndingId ?? throw new ArgumentException("엔딩 화면인데 EndingId 가 없습니다"));
            if (s.FinalStage > 0) return "bgm_final_" + s.FinalStage;
            // 봉마곡에서는 혈영 일곱을 넘기는 동안 땅의 주인 곡 대신 봉마곡 곡 하나로 이어 간다
            if (s.FinalRegion && s.RegionId != null) return "bgm_region_" + s.RegionId;
            if (s.BossId != null) return "bgm_boss_" + s.BossId;
            if (s.Prologue) return "bgm_prologue";
            return "bgm_region_" + (s.RegionId ?? throw new ArgumentException("한 생인데 RegionId 가 없습니다"));
        }
    }

    /// <summary>곡을 바꾸라는 신호. FadeSeconds 동안 지금 곡을 줄이며 새 곡을 올린다.</summary>
    public readonly struct BgmChange
    {
        public readonly string TrackId;
        public readonly float FadeSeconds;
        public BgmChange(string trackId, float fadeSeconds) { TrackId = trackId; FadeSeconds = fadeSeconds; }
    }

    /// <summary>언제 곡을 바꿀지(balance.json bgm). 매 프레임 Tick 을 부른다.</summary>
    public sealed class BgmDirector
    {
        readonly BgmRulesDef _rule;
        string? _pending;
        float _held;

        public BgmDirector(BgmRulesDef rule)
        {
            if (rule.RegionHoldSeconds < 0 || rule.RegionFadeSeconds < 0 || rule.QuickFadeSeconds < 0)
                throw new InvalidOperationException("bgm 초는 0 이상입니다");
            _rule = rule;
        }

        /// <summary>지금 트는 곡. 처음 Tick 전에는 null.</summary>
        public string? Current { get; private set; }

        /// <summary>지역 곡(서장 곡 포함)끼리 바뀌는 것만 기다렸다 바꾼다. 봉마곡도 지역 곡이다.</summary>
        public static bool IsRegionTrack(string trackId) => trackId == "bgm_prologue" || trackId.StartsWith("bgm_region_", StringComparison.Ordinal);

        /// <summary>dt 초가 지났다. 곡을 바꿀 때만 BgmChange 를 돌려준다. breakthrough 는 돌파 연출(줌) 중인가 — 그동안은 바꾸지 않고 기다린 시간도 세지 않는다.</summary>
        public BgmChange? Tick(float dt, in BgmState state, bool breakthrough = false)
        {
            if (dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            string want = Bgm.TrackId(state);
            if (Current == null) return Switch(want, _rule.QuickFadeSeconds);
            if (want == Current) { _pending = null; return null; }
            if (breakthrough) return null;
            if (!IsRegionTrack(Current) || !IsRegionTrack(want)) return Switch(want, _rule.QuickFadeSeconds);
            if (_pending != want) { _pending = want; _held = 0; }
            _held += dt;
            return _held > _rule.RegionHoldSeconds ? Switch(want, _rule.RegionFadeSeconds) : (BgmChange?)null;
        }

        BgmChange Switch(string track, float fade)
        {
            Current = track;
            _pending = null;
            _held = 0;
            return new BgmChange(track, fade);
        }
    }
}
