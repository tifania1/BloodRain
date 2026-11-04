// 서장 안내 문구와 판 안 대사를 언제 띄우나(기획서 4장 '서장 상세 흐름', docs/story/prologue.md '쓰는 법', backlog M1-03).
// 사건이 처음 일어나는 순간에 dialogue.json prologue 의 hints·lines 가운데 when 이 맞는 것을 한 번씩 고른다.
//   같은 사건이면 안내 문구가 먼저(지연 0), 대사는 hintFirstDelaySeconds 늦게. 대사가 여럿이면 앞 줄이 lineSeconds 떠 있다 지운 뒤 다음 줄.
//   start 는 start·start_reply 두 줄, 장삼 사건은 boss_warning·boss_spawn, 각성 시작은 awaken, 각성 끝은 awaken_after.
// 사건은 두 갈래로 들어온다. Watch(life) 를 매 프레임 부르면 Life 상태에서 처음을 알아채고(첫 레벨업·오의 게이지 참·경지·음양 처음 기욺·
// 장삼 등장·각성·처치), OnSecond(second) 는 Life.Tick 이 준 1초의 사건(돌진·정예)을, OnEliteKilled 는 정예 처치를 받는다.
// 화면에 그리는 일은 Unity(DL-03b)·웹이 하고, 여기는 무엇을 몇 초 뒤에 띄울지만 정한다. 서장을 처음부터 다시 하면 새로 만든다(같은 줄을 다시 쓴다).
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;

namespace BloodRain.Core.Run
{
    /// <summary>서장 사건 id. dialogue.json prologue lines·hints 의 when 과 같다.</summary>
    public static class PrologueCue
    {
        public const string Start = "start";
        public const string StartReply = "start_reply";
        public const string FirstLevelUp = "first_levelup";
        public const string UltimateReady = "ultimate_ready";
        public const string YinyangTilt = "yinyang_tilt";
        public const string FirstCharge = "first_charge";
        public const string BossWarning = "boss_warning";
        public const string BossSpawn = "boss_spawn";
        public const string Awaken = "awaken";
        public const string AwakenAfter = "awaken_after";
        public const string BossKill = "boss_kill";
        public static string Realm(string realmId) => "realm:" + realmId;
        public static string EliteSpawn(string enemyId) => "elite_spawn:" + enemyId;
        public static string EliteKill(string enemyId) => "elite_kill:" + enemyId;
    }

    /// <summary>띄울 것 하나. Hint 가 있으면 안내 문구, 아니면 Line(대사). Delay 초 뒤에 띄운다.</summary>
    public readonly struct PrologueCueItem
    {
        public readonly string When;
        public readonly PrologueHintDef? Hint;
        public readonly DialogueLineDef? Line;
        public readonly float Delay;

        public PrologueCueItem(string when, PrologueHintDef? hint, DialogueLineDef? line, float delay)
        {
            When = when; Hint = hint; Line = line; Delay = delay;
        }

        public bool IsHint => Hint != null;
        public string TextKo => Hint?.TextKo ?? Line!.TextKo;
    }

    public sealed class PrologueCues
    {
        readonly PrologueDialogueDef _def;
        readonly HashSet<string> _fired = new HashSet<string>();
        readonly HashSet<string> _seenRealms = new HashSet<string>();
        readonly List<RealmDef> _realms;
        bool _sawLevelUp, _sawFull, _sawTilt, _sawBoss, _sawAwaken, _sawFinish, _sawKill;

        public PrologueCues(GameData data)
        {
            _def = data.Dialogue.Prologue;
            // 처음 경지(삼류)는 돌파가 아니라 시작이다.
            _realms = data.Balance.Realms.OrderBy(r => r.Level).ToList();
            _seenRealms.Add(_realms[0].Id);
        }

        /// <summary>이 사건을 이미 띄웠나.</summary>
        public bool Fired(string when) => _fired.Contains(when);

        /// <summary>판 시작(0:00). 운낭 줄과 진소운 대답.</summary>
        public List<PrologueCueItem> Start() => Fire(PrologueCue.Start, PrologueCue.StartReply);

        /// <summary>이 사건(들)을 처음이면 띄운다. 이미 띄운 사건은 건너뛴다. 같이 들어온 사건은 하나로 이어 붙인다.</summary>
        public List<PrologueCueItem> Fire(params string[] whens)
        {
            var hints = new List<PrologueCueItem>();
            var lines = new List<DialogueLineDef>();
            var lineWhen = new List<string>();
            foreach (var w in whens)
            {
                if (!_fired.Add(w)) continue;
                foreach (var h in _def.Hints.Where(h => h.When == w)) hints.Add(new PrologueCueItem(w, h, null, 0f));
                foreach (var l in _def.Lines.Where(l => l.When == w)) { lines.Add(l); lineWhen.Add(w); }
            }
            var r = new List<PrologueCueItem>(hints);
            float t = hints.Count > 0 ? _def.HintFirstDelaySeconds : 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                r.Add(new PrologueCueItem(lineWhen[i], null, lines[i], t));
                t += _def.LineSeconds;
            }
            return r;
        }

        /// <summary>Life 상태를 보고 처음 일어난 일을 띄운다. 매 프레임(또는 상태가 바뀔 만한 때) 부른다.
        /// 첫 레벨업은 레벨이 처음 2 이상이 된 때다. 레벨업 카드를 고른 뒤에 부르면 prologue.md 의 '고른 뒤'가 된다.</summary>
        public List<PrologueCueItem> Watch(Life life)
        {
            var p = life.Prologue ?? throw new ArgumentException("서장 Life 가 아닙니다");
            var whens = new List<string>();
            if (!_sawLevelUp && life.Level >= 2) { _sawLevelUp = true; whens.Add(PrologueCue.FirstLevelUp); }
            if (!_sawFull && life.Charge.IsFull) { _sawFull = true; whens.Add(PrologueCue.UltimateReady); }
            foreach (var realm in _realms)
                if (realm.Level <= life.Level && _seenRealms.Add(realm.Id)) whens.Add(PrologueCue.Realm(realm.Id)); // 한 번에 둘을 넘으면 둘 다
            if (!_sawTilt && life.Loadout.Gauge.Band != YinYangBand.Harmony) { _sawTilt = true; whens.Add(PrologueCue.YinyangTilt); }
            if (!_sawBoss && p.Step >= PrologueStep.Boss) { _sawBoss = true; whens.Add(PrologueCue.BossWarning); whens.Add(PrologueCue.BossSpawn); }
            if (!_sawAwaken && p.Step >= PrologueStep.Awakening) { _sawAwaken = true; whens.Add(PrologueCue.Awaken); }
            if (!_sawFinish && p.Step >= PrologueStep.Finish) { _sawFinish = true; whens.Add(PrologueCue.AwakenAfter); }
            if (!_sawKill && p.Step >= PrologueStep.Cutscene) { _sawKill = true; whens.Add(PrologueCue.BossKill); }
            return whens.Count == 0 ? new List<PrologueCueItem>() : Fire(whens.ToArray());
        }

        /// <summary>Life.Tick 이 준 1초의 사건: 돌진이면 first_charge, 정예면 elite_spawn:&lt;id&gt;. 장삼은 Watch 가 Step 으로 알아챈다.</summary>
        public List<PrologueCueItem> OnSecond(LifeSecond? second)
        {
            if (second == null || second.Events.Count == 0) return new List<PrologueCueItem>();
            var whens = new List<string>();
            foreach (var e in second.Events)
            {
                if (e.Wave.Def.Type == WaveEventKind.Charge) whens.Add(PrologueCue.FirstCharge);
                else if (e.Wave.Def.Type == WaveEventKind.Elite) whens.Add(PrologueCue.EliteSpawn(e.Enemy));
            }
            return Fire(whens.ToArray());
        }

        /// <summary>정예를 쓰러뜨렸다.</summary>
        public List<PrologueCueItem> OnEliteKilled(string enemyId) => Fire(PrologueCue.EliteKill(enemyId));
    }
}
