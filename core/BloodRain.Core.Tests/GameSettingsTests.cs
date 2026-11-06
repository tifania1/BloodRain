using System.Collections.Generic;
using BloodRain.Core.Combat;
using BloodRain.Core.Meta;
using Xunit;

namespace BloodRain.Core.Tests
{
    // 기획서 12장 설정, 웹 프로토타입 settings·SET_TURN 과 같은 값·차례인지.
    public class GameSettingsTests
    {
        static List<string> Cycle(string key, int n)
        {
            var s = GameSettings.Defaults();
            var seen = new List<string> { s.Get(key)! };
            for (int i = 0; i < n; i++) { Assert.True(s.Turn(key)); seen.Add(s.Get(key)!); }
            return seen;
        }

        [Fact]
        public void 단추를_누르면_웹_SET_TURN_과_같은_차례로_돈다()
        {
            Assert.Equal(new[] { "1", "0.5", "0", "1" }, Cycle(GameSettings.Shake, 3));
            Assert.Equal(new[] { "auto", "long", "short", "auto" }, Cycle(GameSettings.Awaken, 3));
            Assert.Equal(new[] { "1", "0.5", "0", "1" }, Cycle(GameSettings.Rain, 3));
            Assert.Equal(new[] { "1", "1.6", "0", "1" }, Cycle(GameSettings.AimMarker, 3));
            Assert.Equal(new[] { "near", "move", "near" }, Cycle(GameSettings.AimModeKey, 2));
            Assert.Equal(new[] { "1", "0.75", "0.5", "0.25", "1" }, Cycle(GameSettings.Volume, 4));
            Assert.Equal(new[] { "1", "0.75", "0.5", "0.25", "0", "1" }, Cycle(GameSettings.Effects, 5));
            Assert.Equal(new[] { "true", "false", "true" }, Cycle(GameSettings.Numbers, 2));
            Assert.Equal(new[] { "false", "true" }, Cycle(GameSettings.Muted, 1));
            Assert.False(GameSettings.Defaults().Turn("easy"));
        }

        [Fact]
        public void 움직임_줄이기면_흔들림_0_깜빡임_끔_혈우_절반()
        {
            var s = GameSettings.Defaults(reducedMotion: true);
            Assert.Equal(0f, s.ShakeScale);
            Assert.False(s.FlashOn);
            Assert.Equal(0.5f, s.RainScale);
            var n = GameSettings.Defaults();
            Assert.Equal(1f, n.ShakeScale);
            Assert.True(n.FlashOn && n.BossBarOn && n.NumbersOn);
            Assert.Equal(AimMode.Nearest, n.Aim);
        }

        [Fact]
        public void 저장하고_다시_읽으면_같고_못_읽는_값은_기본값()
        {
            var s = GameSettings.Defaults();
            s.Turn(GameSettings.Shake); s.Turn(GameSettings.AimModeKey); s.Turn(GameSettings.Effects); s.Turn(GameSettings.BossBar); s.Turn(GameSettings.Awaken);
            var box = new Dictionary<string, string>();
            s.Save((k, v) => box[k] = v);
            Assert.Equal(GameSettings.Keys.Count, box.Count);
            var back = GameSettings.Load(k => box.TryGetValue(k, out var v) ? v : null);
            foreach (var k in GameSettings.Keys) Assert.Equal(s.Get(k), back.Get(k));

            var bad = GameSettings.Load(k => k == GameSettings.Volume ? "0.3" : k == GameSettings.Flash ? "yes" : null, reducedMotion: true);
            Assert.Equal(1f, bad.VolumeScale);
            Assert.False(bad.FlashOn);   // 못 읽으면 움직임 줄이기 기본값 그대로
        }

        [Fact]
        public void 저장_낱말과_차례가_웹_프로토타입과_같다()
        {
            var html = TestData.ReadRepoFile("web", "prototype", "template.html");
            foreach (var k in GameSettings.Keys)
                if (k != GameSettings.Muted) Assert.Contains($"store.set('{k}'", html);
            Assert.Contains("settings.shake === 1 ? 0.5 : settings.shake === 0.5 ? 0 : 1", html);
            Assert.Contains("settings.aim === 1 ? 1.6 : settings.aim === 1.6 ? 0 : 1", html);
            Assert.Contains("{ auto: 'long', long: 'short', short: 'auto' }", html);
            Assert.Contains("store.set('muted'", html);
        }
    }
}
