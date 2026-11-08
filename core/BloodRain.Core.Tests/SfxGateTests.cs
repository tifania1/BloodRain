using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BloodRain.Core.Data;
using BloodRain.Core.World;
using Xunit;

namespace BloodRain.Core.Tests
{
    /// <summary>효과음 동시 재생 제한(14장 '동시 재생 제한', balance.json sfx, backlog SFX-14).</summary>
    public class SfxGateTests
    {
        readonly SfxRulesDef R = TestData.Load().Balance.Sfx;

        [Fact] public void 값이_14장_문장과_같다()
        {
            var m = Regex.Match(TestData.Gdd, @"같은 효과음은 ([\d.]+)초 안에 최대 (\d+)개만 재생하고, 진기 흡수음은 한 프레임에 하나로 합칩니다");
            Assert.True(m.Success, "14장 '동시 재생 제한' 문장이 없다");
            Assert.Equal(R.SameSoundWindowSeconds, float.Parse(m.Groups[1].Value));
            Assert.Equal(R.SameSoundMaxInWindow, int.Parse(m.Groups[2].Value));
        }

        [Fact] public void 흡수음_음계는_웹과_구운_소리와_같다()
        {
            var html = File.ReadAllText(Path.Combine(TestData.RepoRoot, "web", "prototype", "template.html"));
            // 웹도 같은 값(balance.json sfx)을 읽어 같은 셈을 한다.
            Assert.Contains("const SFXR = B.sfx, SFX_VARIANTS = 5, ORB_NOTES = SFXR.absorbNotes, ORB_RUN_GAP = SFXR.absorbRunGapSeconds;", html);
            Assert.Contains("if (q.length >= SFXR.sameSoundMaxInWindow) return;", html);
            Assert.Contains("if (audio.orbFrame === lastTs) return;", html);
            Assert.Equal(8, R.AbsorbNotes);
            Assert.Equal(0.35f, R.AbsorbRunGapSeconds);
            // 구운 풍경 n1~n8 이 있어야 재생기가 고를 수 있다.
            var dir = Path.Combine(TestData.RepoRoot, "unity", "BloodRain", "Assets", "Audio", "SFX");
            for (int n = 0; n < R.AbsorbNotes; n++)
                Assert.True(Directory.GetFiles(dir, SfxGate.AbsorbSoundId(n) + "_*.ogg").Any(), SfxGate.AbsorbSoundId(n));
        }

        [Fact] public void 같은_소리는_창_안에_넷까지()
        {
            var g = new SfxGate(R);
            int played = Enumerable.Range(0, 30).Count(_ => g.TryPlay("sfx_hit", 1.0));
            Assert.Equal(4, played);
            Assert.False(g.TryPlay("sfx_hit", 1.04));
            // 다른 소리는 따로 센다.
            Assert.True(g.TryPlay("sfx_kill", 1.04));
            // 창이 지나면 다시 넷.
            Assert.Equal(4, Enumerable.Range(0, 10).Count(_ => g.TryPlay("sfx_hit", 1.051)));
        }

        [Fact] public void 창은_미끄러진다()
        {
            var g = new SfxGate(R);
            Assert.True(g.TryPlay("a", 0.00));
            Assert.True(g.TryPlay("a", 0.02));
            Assert.True(g.TryPlay("a", 0.03));
            Assert.True(g.TryPlay("a", 0.04));
            Assert.False(g.TryPlay("a", 0.045));
            Assert.True(g.TryPlay("a", 0.051));   // 0.00 이 빠짐
            Assert.False(g.TryPlay("a", 0.052));
        }

        [Fact] public void 흡수음은_한_프레임에_하나()
        {
            var g = new SfxGate(R);
            Assert.True(g.TryAbsorb(10, 1.0, out var n0));
            Assert.Equal(0, n0);
            Assert.False(g.TryAbsorb(10, 1.0, out _));
            Assert.False(g.TryAbsorb(10, 1.0, out _));
            Assert.True(g.TryAbsorb(11, 1.016, out var n1));
            Assert.Equal(1, n1);
        }

        [Fact] public void 이어_먹으면_음계가_오르고_끝음에서_멈추고_틈이_벌면_처음부터()
        {
            var g = new SfxGate(R);
            var notes = Enumerable.Range(0, 12).Select(i => { g.TryAbsorb(i, i * 0.1, out var n); return n; }).ToArray();
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 7, 7, 7, 7 }, notes);
            g.TryAbsorb(100, 1.1 + R.AbsorbRunGapSeconds + 0.01, out var again);
            Assert.Equal(0, again);
            Assert.Equal("sfx_xp_absorb_n1", SfxGate.AbsorbSoundId(again));
        }

        [Fact] public void 비우면_처음처럼()
        {
            var g = new SfxGate(R);
            for (int i = 0; i < 4; i++) g.TryPlay("a", 0);
            g.TryAbsorb(1, 0, out _); g.TryAbsorb(2, 0.1, out _);
            g.Reset();
            Assert.True(g.TryPlay("a", 0));
            Assert.True(g.TryAbsorb(1, 0.1, out var n));
            Assert.Equal(0, n);
        }

        [Fact] public void 심장_소리_간격은_소리보다_길고_웹이_같은_값을_읽는다()
        {
            Assert.True(R.LowHealthHeartSeconds > 0);
            var line = File.ReadLines(Path.Combine(TestData.RepoRoot, "sfx_queue", "queue.jsonl")).First(l => l.Contains("\"id\": \"sfx_low_health_heart\""));
            var sec = float.Parse(Regex.Match(line, @"""seconds"": ([\d.]+)").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(R.LowHealthHeartSeconds >= sec, $"심장 소리 {sec}초가 간격 {R.LowHealthHeartSeconds}초보다 길어 겹친다");
            Assert.Contains("기혈 30% 이하에선 화면 가장자리에 먹물이 번지고 심장 소리가 납니다", TestData.Gdd);
            var html = File.ReadAllText(Path.Combine(TestData.RepoRoot, "web", "prototype", "template.html"));
            Assert.Contains("heart: 'sfx_low_health_heart'", html);
            Assert.Contains("G.heartT = SFXR.lowHealthHeartSeconds;", html);
        }

        [Fact] public void 돌파_연출_동안은_붓_소리만_울리고_배경음은_0에서_돌아온다()
        {
            Assert.Contains("경지 돌파 때 모든 소리가 끊기고 붓 긋는 소리 한 번", TestData.Gdd);
            var data = TestData.Load();
            var g = new SfxGate(R);
            Assert.Equal(1f, g.BgmVolume(0, data.Balance.Bgm.QuickFadeSeconds));
            float len = data.Cheonha.Breakthrough.ZoomToSeconds;   // 1.4초
            double end = 10.0 + len;
            g.Hush(10, len);
            Assert.False(g.TryPlay("sfx_enemy_kill", 10.5));
            Assert.False(g.TryAbsorb(1, 10.5, out _));
            Assert.True(g.TryPlay(SfxGate.BreakthroughBrush, 10));
            Assert.Equal(0f, g.BgmVolume(11, 1f));
            // 연출 도중 또 넘어도(7장 '처음부터 다시 하지 않고') 끝은 늦출 수만 있다
            g.Hush(10.5, 0.2);
            Assert.True(g.Hushed(11.3));
            Assert.False(g.Hushed(end));
            Assert.True(g.TryPlay("sfx_enemy_kill", end));
            Assert.True(g.TryAbsorb(2, end, out var n));
            Assert.Equal(0, n);
            Assert.Equal(0.5f, g.BgmVolume(end + 0.5, 1f), 3);
            Assert.Equal(1f, g.BgmVolume(end + 2, 1f));
            Assert.Equal(1f, g.BgmVolume(end, 0f));
            g.Hush(20, 1); g.Reset();
            Assert.False(g.Hushed(20.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => g.Hush(0, -1));
            var html = File.ReadAllText(Path.Combine(TestData.RepoRoot, "web", "prototype", "template.html"));
            Assert.Contains("if (typeof G !== 'undefined' && G && G.brk) return;   // 돌파 연출 동안 소리 끊김(SfxGate.Hush)", html);
            Assert.Contains("sfxAwaken('brush')", html);
        }
    }
}
