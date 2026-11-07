using System.Collections.Generic;
using System.Linq;
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
                if (k != GameSettings.Muted && k != GameSettings.Lang && k != GameSettings.Voice) Assert.Contains($"store.set('{k}'", html);
            Assert.Contains("settings.shake === 1 ? 0.5 : settings.shake === 0.5 ? 0 : 1", html);
            Assert.Contains("settings.aim === 1 ? 1.6 : settings.aim === 1.6 ? 0 : 1", html);
            Assert.Contains("{ auto: 'long', long: 'short', short: 'auto' }", html);
            Assert.Contains("store.set('muted'", html);
        }

        [Fact]
        public void 언어는_네_언어를_돌고_기기_언어로_시작한다()
        {
            Assert.Equal(new[] { "ko", "en", "zh-Hans", "zh-Hant", "ko" }, Cycle2("Korean", 4));
            Assert.Equal("ko", GameSettings.LanguageFor("ko-KR"));
            Assert.Equal("zh-Hans", GameSettings.LanguageFor("ChineseSimplified"));
            Assert.Equal("zh-Hans", GameSettings.LanguageFor("Chinese"));
            Assert.Equal("zh-Hans", GameSettings.LanguageFor("zh-CN"));
            Assert.Equal("zh-Hant", GameSettings.LanguageFor("ChineseTraditional"));
            Assert.Equal("zh-Hant", GameSettings.LanguageFor("zh-TW"));
            Assert.Equal("zh-Hant", GameSettings.LanguageFor("zh-Hant-HK"));
            Assert.Equal("en", GameSettings.LanguageFor("Japanese"));
            Assert.Equal("en", GameSettings.LanguageFor(null));

            // 저장된 값이 있으면 기기 언어보다 앞서고, 모르는 언어는 버린다
            Assert.Equal("zh-Hant", GameSettings.Load(k => k == GameSettings.Lang ? "zh-Hant" : null, systemLanguage: "Korean").Language);
            Assert.Equal("ko", GameSettings.Load(k => k == GameSettings.Lang ? "ja" : null, systemLanguage: "Korean").Language);

            // 언어 단추 이름은 Strings.Languages 와 하나씩 맞는다
            Assert.Equal(BloodRain.Core.Data.Strings.Languages.OrderBy(x => x), GameSettings.LanguageNames.Keys.OrderBy(x => x));
        }

        [Fact]
        public void 목소리는_한국어_중국어_두_벌이고_화면_언어를_따라_시작한다()
        {
            Assert.Equal("ko", GameSettings.Defaults(systemLanguage: "Korean").VoiceLanguage);
            Assert.Equal("ko", GameSettings.Defaults(systemLanguage: "English").VoiceLanguage);
            Assert.Equal("zh", GameSettings.Defaults(systemLanguage: "ChineseTraditional").VoiceLanguage);
            var s = GameSettings.Defaults();
            Assert.True(s.Turn(GameSettings.Voice)); Assert.Equal("zh", s.VoiceLanguage);
            Assert.True(s.Turn(GameSettings.Voice)); Assert.Equal("ko", s.VoiceLanguage);

            // 저장된 화면 언어를 따르되, 저장된 목소리가 있으면 그것
            Assert.Equal("zh", GameSettings.Load(k => k == GameSettings.Lang ? "zh-Hans" : null, systemLanguage: "Korean").VoiceLanguage);
            Assert.Equal("ko", GameSettings.Load(k => k == GameSettings.Lang ? "zh-Hans" : k == GameSettings.Voice ? "ko" : null).VoiceLanguage);
            Assert.Equal("ko", GameSettings.Load(k => k == GameSettings.Voice ? "ja" : null, systemLanguage: "Korean").VoiceLanguage);

            var gdd = TestData.ReadRepoFile("docs", "design", "gdd.md");
            Assert.Contains("한국어와 중국어 두 벌을 기본으로 녹음하고 설정에서 고르게 합니다", gdd);
        }

        static List<string> Cycle2(string system, int n)
        {
            var s = GameSettings.Defaults(systemLanguage: system);
            var seen = new List<string> { s.Get(GameSettings.Lang)! };
            for (int i = 0; i < n; i++) { Assert.True(s.Turn(GameSettings.Lang)); seen.Add(s.Get(GameSettings.Lang)!); }
            return seen;
        }
    }
}
