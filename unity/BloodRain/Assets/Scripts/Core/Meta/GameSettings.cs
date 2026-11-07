#nullable enable
// 설정(기획서 12장 화면 목록 '설정'·'화면 편의 규칙', 타이틀과 일시정지 두 곳에서). 웹 프로토타입 settings·SET_TURN 과 같은 값·같은 차례.
//   값마다 저장 낱말(Key)이 웹 store 이름과 같다. Unity 는 PlayerPrefs 에 이 낱말로 문자열을 넣고 Load/Save 로 읽고 쓴다.
//   단추를 누르면 Turn(key) 로 다음 값으로 돈다(웹 SET_TURN 그대로).
//   '움직임 줄이기'(OS 설정)가 켜져 있으면 흔들림 0·깜빡임 끔·혈우 50% 로 시작한다(웹 REDUCED). 한 번 정한 값은 그대로.
//   언어(lang)는 Strings.Languages 차례로 돈다. 저장 낱말 'lang' 은 Unity Presentation/Loc 이 읽는 PlayerPrefs 낱말과 같다.
//   정한 적 없으면 기기 언어(LanguageFor: 한국어·간체·번체, 그 밖은 영어 — Loc.FromSystem 과 같음). 웹은 한국어뿐이라 언어 단추가 없다.
//   목소리(voice)는 14장 '목소리': 오의 기합을 한국어·중국어 두 벌 가운데 고른다(ko·zh). 정한 적 없으면 화면 언어가 중국어(간체·번체)일 때 zh, 그 밖은 ko.
//   12장 목록 가운데 색각 보정·조작 재설정·자동 고르기는 아직 값이 없다(자동 고르기는 UI-01b, 색각 보정은 UI-03 사람 결정 대기).
using System;
using System.Collections.Generic;
using System.Globalization;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;

namespace BloodRain.Core.Meta
{
    public enum AwakenScene { Auto, Long, Short }

    public sealed class GameSettings
    {
        public const string Shake = "shake", Awaken = "awaken", Flash = "flash", BossBar = "bossbar", Rain = "rain",
            AimMarker = "aim", AimModeKey = "aimMode", Numbers = "nums", Volume = "vol", Effects = "fx", Muted = "muted", Lang = "lang", Voice = "voice";

        /// <summary>저장 낱말 전부(단추 차례, 웹 SET_HTML 과 같음 + 소리 끄기 + 언어 + 목소리).</summary>
        public static readonly IReadOnlyList<string> Keys = new[] { Shake, Awaken, Flash, BossBar, Rain, Volume, Effects, Numbers, AimModeKey, AimMarker, Muted, Lang, Voice };

        /// <summary>언어 단추에 그 언어 글자로 적는 이름(어느 화면 언어에서나 같다).</summary>
        public static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            ["ko"] = "한국어", ["en"] = "English", ["zh-Hans"] = "简体中文", ["zh-Hant"] = "繁體中文",
        };

        /// <summary>기기 언어 → 화면 언어. Unity SystemLanguage 이름(Korean·ChineseSimplified·Chinese·ChineseTraditional)이나
        /// 언어 태그(ko-KR·zh-CN·zh-TW·zh-Hant…)를 받는다. 그 밖은 영어.</summary>
        public static string LanguageFor(string? system)
        {
            var s = (system ?? "").Trim();
            var low = s.ToLowerInvariant();
            if (low == "korean" || low == "ko" || low.StartsWith("ko-")) return "ko";
            if (low == "chinesetraditional" || low.StartsWith("zh-hant") || low == "zh-tw" || low == "zh-hk" || low == "zh-mo") return "zh-Hant";
            if (low == "chinesesimplified" || low == "chinese" || low == "zh" || low.StartsWith("zh-")) return "zh-Hans";
            return "en";
        }

        /// <summary>화면 흔들림 1 / 0.5 / 0.</summary>
        public float ShakeScale = 1f;
        /// <summary>각성 연출: 자동(그 절기 처음만 길게) / 길게 / 짧게.</summary>
        public AwakenScene AwakenMode = AwakenScene.Auto;
        /// <summary>피격 번쩍임.</summary>
        public bool FlashOn = true;
        /// <summary>땅의 주인 체력바 보이기.</summary>
        public bool BossBarOn = true;
        /// <summary>혈우 연출 세기 1 / 0.5 / 0(장막 붉은 번짐·빗줄기·붉은 달. 장막 점선은 늘 그림).</summary>
        public float RainScale = 1f;
        /// <summary>바라보는 방향 표시 크기 1 / 1.6 / 0.</summary>
        public float AimMarkerScale = 1f;
        public AimMode Aim = AimMode.Nearest;
        /// <summary>피해 숫자 표시.</summary>
        public bool NumbersOn = true;
        /// <summary>음량 1 / 0.75 / 0.5 / 0.25. 끄기는 MutedOn.</summary>
        public float VolumeScale = 1f;
        /// <summary>내 이펙트 투명도 1 / 0.75 / 0.5 / 0.25 / 0(적 탄·적 예고는 그대로).</summary>
        public float EffectsAlpha = 1f;
        public bool MutedOn;
        /// <summary>화면 언어(Strings.Languages 가운데 하나).</summary>
        public string Language = "en";
        /// <summary>오의 기합 목소리 "ko" / "zh"(VoiceLanguages).</summary>
        public string VoiceLanguage = "ko";

        /// <summary>목소리 두 벌(14장).</summary>
        public static readonly string[] VoiceLanguages = { "ko", "zh" };

        /// <summary>목소리를 정한 적 없을 때: 화면 언어가 중국어면 zh, 그 밖은 ko.</summary>
        public static string VoiceFor(string language) => language.StartsWith("zh", StringComparison.Ordinal) ? "zh" : "ko";

        public static GameSettings Defaults(bool reducedMotion = false, string? systemLanguage = null) => new GameSettings
        {
            Language = LanguageFor(systemLanguage),
            VoiceLanguage = VoiceFor(LanguageFor(systemLanguage)),
            ShakeScale = reducedMotion ? 0f : 1f,
            FlashOn = !reducedMotion,
            RainScale = reducedMotion ? 0.5f : 1f,
        };

        /// <summary>단추 한 번: 그 값을 다음 차례로. 모르는 낱말이면 false.</summary>
        public bool Turn(string key)
        {
            switch (key)
            {
                case Shake: ShakeScale = ShakeScale == 1f ? 0.5f : ShakeScale == 0.5f ? 0f : 1f; return true;
                case Awaken: AwakenMode = AwakenMode == AwakenScene.Auto ? AwakenScene.Long : AwakenMode == AwakenScene.Long ? AwakenScene.Short : AwakenScene.Auto; return true;
                case Flash: FlashOn = !FlashOn; return true;
                case BossBar: BossBarOn = !BossBarOn; return true;
                case Rain: RainScale = RainScale == 1f ? 0.5f : RainScale == 0.5f ? 0f : 1f; return true;
                case AimMarker: AimMarkerScale = AimMarkerScale == 1f ? 1.6f : AimMarkerScale == 1.6f ? 0f : 1f; return true;
                case AimModeKey: Aim = Aim == AimMode.Move ? AimMode.Nearest : AimMode.Move; return true;
                case Numbers: NumbersOn = !NumbersOn; return true;
                case Volume: VolumeScale = VolumeScale > 0.25f ? Round2(VolumeScale - 0.25f) : 1f; return true;
                case Effects: EffectsAlpha = EffectsAlpha > 0f ? Math.Max(0f, Round2(EffectsAlpha - 0.25f)) : 1f; return true;
                case Muted: MutedOn = !MutedOn; return true;
                case Lang: Language = Strings.Languages[(Math.Max(0, Array.IndexOf(Strings.Languages, Language)) + 1) % Strings.Languages.Length]; return true;
                case Voice: VoiceLanguage = VoiceLanguage == "ko" ? "zh" : "ko"; return true;
                default: return false;
            }
        }

        /// <summary>저장할 문자열(PlayerPrefs). 숫자는 점 소수, 참거짓은 true/false, 나머지는 웹 낱말.</summary>
        public string? Get(string key) => key switch
        {
            Shake => Num(ShakeScale),
            Awaken => AwakenMode == AwakenScene.Long ? "long" : AwakenMode == AwakenScene.Short ? "short" : "auto",
            Flash => Bool(FlashOn),
            BossBar => Bool(BossBarOn),
            Rain => Num(RainScale),
            AimMarker => Num(AimMarkerScale),
            AimModeKey => Combat.Aim.ToKey(Aim),
            Numbers => Bool(NumbersOn),
            Volume => Num(VolumeScale),
            Effects => Num(EffectsAlpha),
            Muted => Bool(MutedOn),
            Lang => Language,
            Voice => VoiceLanguage,
            _ => null,
        };

        /// <summary>저장된 문자열을 읽는다. 없는 값(null)·못 읽는 값·차례에 없는 숫자는 지금 값 그대로 둔다.</summary>
        public void Set(string key, string? value)
        {
            if (value == null) return;
            switch (key)
            {
                case Shake: ShakeScale = Pick(value, ShakeScale, 1f, 0.5f, 0f); break;
                case Awaken: AwakenMode = value == "long" ? AwakenScene.Long : value == "short" ? AwakenScene.Short : value == "auto" ? AwakenScene.Auto : AwakenMode; break;
                case Flash: FlashOn = ReadBool(value, FlashOn); break;
                case BossBar: BossBarOn = ReadBool(value, BossBarOn); break;
                case Rain: RainScale = Pick(value, RainScale, 1f, 0.5f, 0f); break;
                case AimMarker: AimMarkerScale = Pick(value, AimMarkerScale, 1f, 1.6f, 0f); break;
                case AimModeKey: if (value == "move" || value == "near") Aim = Combat.Aim.Parse(value); break;
                case Numbers: NumbersOn = ReadBool(value, NumbersOn); break;
                case Volume: VolumeScale = Pick(value, VolumeScale, 1f, 0.75f, 0.5f, 0.25f); break;
                case Effects: EffectsAlpha = Pick(value, EffectsAlpha, 1f, 0.75f, 0.5f, 0.25f, 0f); break;
                case Muted: MutedOn = ReadBool(value, MutedOn); break;
                case Lang: if (Array.IndexOf(Strings.Languages, value) >= 0) Language = value; break;
                case Voice: if (Array.IndexOf(VoiceLanguages, value) >= 0) VoiceLanguage = value; break;
            }
        }

        /// <summary>읽기: 낱말마다 read(key) 가 준 문자열(없으면 null)을 기본값 위에 얹는다.</summary>
        public static GameSettings Load(Func<string, string?> read, bool reducedMotion = false, string? systemLanguage = null)
        {
            var s = Defaults(reducedMotion, systemLanguage);
            foreach (var k in Keys) s.Set(k, read(k));
            if (read(Voice) == null) s.VoiceLanguage = VoiceFor(s.Language);   // 고른 적 없으면 저장된 화면 언어를 따른다
            return s;
        }

        /// <summary>쓰기: 낱말마다 write(key, 문자열).</summary>
        public void Save(Action<string, string> write)
        {
            foreach (var k in Keys) write(k, Get(k)!);
        }

        static float Round2(float v) => (float)Math.Round(v * 100f) / 100f;
        static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Bool(bool v) => v ? "true" : "false";
        static bool ReadBool(string v, bool fallback) => v == "true" ? true : v == "false" ? false : fallback;

        static float Pick(string value, float fallback, params float[] allowed)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) return fallback;
            foreach (var a in allowed) if (Math.Abs(a - f) < 0.001f) return a;
            return fallback;
        }
    }
}
