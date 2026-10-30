#nullable enable
// 비전 판정(기획서 20장). 조건 글(condition)은 사람이 읽는 것이고, 판정은 synergies.json secrets[].require 로 한다.
//   게임은 판 하나에 RunRecord 하나를 만들어 일이 생길 때마다 알려 주고(정신부, 음식, 보갑, 역류 끊기 …),
//   SecretChecker.Check 를 자주(매 프레임이나 1초마다, 그리고 사건 바로 뒤에) 불러 새로 열린 비전을 받는다.
//   비전은 판마다 새로 조건을 채워야 켜진다. 도감에 '찾음'으로 남기는 일은 게임 쪽(메타)이 한다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Build
{
    public enum RunEventKind { Talisman, Chest, SilverLantern }

    /// <summary>한 판 동안 쌓이는 수. 값 복사라 사건이 난 순간의 사진으로도 쓴다.</summary>
    public struct RunCounters
    {
        /// <summary>정신부를 쓴 수.</summary>
        public int TalismansUsed;
        /// <summary>음식을 먹은 수.</summary>
        public int FoodEaten;
        /// <summary>지금까지 모은 의서(판 밖에서 모은 것 포함, 진엔딩 수집품).</summary>
        public int MedicalBooks;
        /// <summary>지금 이어지고 있는 기혈역류 끊기 연속 횟수. 한 번 못 끊으면 0.</summary>
        public int BacklashCutStreak;
        /// <summary>이번 판에서 가장 길게 이어진 역류 끊기 연속 횟수.</summary>
        public int BestBacklashCutStreak;
        /// <summary>게임 시간(초).</summary>
        public float ElapsedSeconds;
        /// <summary>음양 게이지가 0에서 한 칸도 안 움직이고 머문 연속 시간(초).</summary>
        public float GaugeZeroSeconds;

        /// <summary>synergies.json 에서 쓰는 이름.</summary>
        public static readonly string[] Names =
            { "talismansUsed", "foodEaten", "medicalBooks", "backlashCutsInRow", "elapsedSeconds", "gaugeZeroSeconds" };

        public double Get(string name) => name switch
        {
            "talismansUsed" => TalismansUsed,
            "foodEaten" => FoodEaten,
            "medicalBooks" => MedicalBooks,
            "backlashCutsInRow" => BestBacklashCutStreak,
            "elapsedSeconds" => ElapsedSeconds,
            "gaugeZeroSeconds" => GaugeZeroSeconds,
            _ => throw new ArgumentException($"모르는 판 기록 이름: {name}"),
        };

        public static RunEventKind ParseEvent(string kind) => kind switch
        {
            "talisman" => RunEventKind.Talisman,
            "chest" => RunEventKind.Chest,
            "silverLantern" => RunEventKind.SilverLantern,
            _ => throw new ArgumentException($"모르는 사건 이름: {kind}"),
        };
    }

    public readonly struct RunEvent
    {
        public readonly RunEventKind Kind;
        /// <summary>그 순간 기혈 비율(보갑만). 모르면 null.</summary>
        public readonly float? HpRatio;
        /// <summary>그 순간의 판 기록.</summary>
        public readonly RunCounters Counters;
        public RunEvent(RunEventKind kind, float? hpRatio, RunCounters counters) { Kind = kind; HpRatio = hpRatio; Counters = counters; }
    }

    /// <summary>판 기록. 게임이 일이 생길 때마다 불러 준다.</summary>
    public sealed class RunRecord
    {
        readonly List<RunEvent> _events = new List<RunEvent>();
        readonly HashSet<string> _cleared;
        RunCounters _c;
        int? _lastGauge;

        public string CharacterId { get; }
        /// <summary>판 밖 기록: 이 판 전까지 깬 장(cheonha.json chapter 값).</summary>
        public IReadOnlyCollection<string> ClearedChapters => _cleared;
        public RunCounters Counters => _c;
        public IReadOnlyList<RunEvent> Events => _events;

        public RunRecord(string characterId, IEnumerable<string>? clearedChapters = null, int medicalBooks = 0)
        {
            CharacterId = characterId;
            _cleared = new HashSet<string>(clearedChapters ?? Array.Empty<string>());
            _c.MedicalBooks = medicalBooks;
        }

        /// <summary>정신부를 썼다.</summary>
        public void UseTalisman() { _c.TalismansUsed++; _events.Add(new RunEvent(RunEventKind.Talisman, null, _c)); }

        public void EatFood() => _c.FoodEaten++;

        /// <summary>은빛 등롱에서 의서가 나왔다.</summary>
        public void CollectMedicalBook() => _c.MedicalBooks++;

        /// <summary>기혈역류를 시작 1초 안에 경공으로 끊었다(창은 balance.json yinyang.extreme.dashCancelWindowSeconds).</summary>
        public void CutBacklash()
        {
            _c.BacklashCutStreak++;
            if (_c.BacklashCutStreak > _c.BestBacklashCutStreak) _c.BestBacklashCutStreak = _c.BacklashCutStreak;
        }

        /// <summary>기혈역류를 끊지 못하고 끝까지 맞았다. 연속이 끊긴다.</summary>
        public void MissBacklash() => _c.BacklashCutStreak = 0;

        /// <summary>시간이 dt초 흘렀고 지금 음양 게이지가 gaugeValue 다.</summary>
        public void Tick(float dt, int gaugeValue)
        {
            _c.ElapsedSeconds += dt;
            if (gaugeValue != 0) _c.GaugeZeroSeconds = 0;
            else if (_lastGauge == null || _lastGauge == 0) _c.GaugeZeroSeconds += dt;
            else _c.GaugeZeroSeconds = 0; // 방금 0으로 돌아왔다. 여기서부터 다시 센다
            _lastGauge = gaugeValue;
        }

        /// <summary>보갑을 열었다. hpRatio = 지금 기혈 / 최대 기혈.</summary>
        public void OpenChest(float hpRatio) => _events.Add(new RunEvent(RunEventKind.Chest, hpRatio, _c));

        public void BreakSilverLantern() => _events.Add(new RunEvent(RunEventKind.SilverLantern, null, _c));
    }

    /// <summary>판 하나의 비전 판정기. 한 번 열린 비전은 다시 돌려주지 않는다.</summary>
    public sealed class SecretChecker
    {
        readonly GameData _data;
        readonly RunRecord _record;
        readonly HashSet<string> _unlocked = new HashSet<string>();
        readonly Dictionary<string, int> _eventHits = new Dictionary<string, int>();
        readonly Dictionary<string, float> _holdBase = new Dictionary<string, float>();
        int _seenEvents;

        public SecretChecker(GameData data, RunRecord record)
        {
            _data = data;
            _record = record;
        }

        public RunRecord Record => _record;
        public IReadOnlyCollection<string> Unlocked => _unlocked;

        public List<SecretDef> Check(Loadout loadout) => Check(BuildLevels.Of(_data, loadout));

        /// <summary>이번에 새로 열린 비전(데이터 순서).
        /// 사건 조건은 지난 Check 뒤에 난 사건만 지금 로드아웃으로 본다. 그래서 사건 바로 뒤에 불러야 한다.</summary>
        public List<SecretDef> Check(BuildLevels levels)
        {
            var fresh = new List<RunEvent>();
            for (int i = _seenEvents; i < _record.Events.Count; i++) fresh.Add(_record.Events[i]);
            _seenEvents = _record.Events.Count;

            var result = new List<SecretDef>();
            foreach (var s in _data.Synergies.Secrets)
            {
                if (_unlocked.Contains(s.Id)) continue;
                if (!Passes(s, levels, fresh)) continue;
                _unlocked.Add(s.Id);
                result.Add(s);
            }
            return result;
        }

        bool Passes(SecretDef s, BuildLevels levels, List<RunEvent> fresh)
        {
            var req = s.Require;
            bool holding = Holds(req, levels);

            bool held = true;
            if (req.Hold != null)
            {
                float streak = (float)_record.Counters.Get(req.Hold.Counter);
                if (!holding) _holdBase.Remove(s.Id);
                else
                {
                    // 조건을 갖추기 전에 흐른 시간은 세지 않는다. 연속이 끊겼다 다시 시작했으면 처음부터.
                    if (!_holdBase.TryGetValue(s.Id, out var b)) _holdBase[s.Id] = b = streak;
                    else if (streak < b) _holdBase[s.Id] = b = 0;
                    held = streak - b >= req.Hold.Seconds;
                }
            }
            if (!holding || !held) return false;

            if (req.Event != null)
            {
                var kind = RunCounters.ParseEvent(req.Event.Kind);
                int hits = _eventHits.TryGetValue(s.Id, out var h) ? h : 0;
                foreach (var ev in fresh)
                {
                    if (ev.Kind != kind) continue;
                    if (req.Event.MaxHpRatio is float max && !(ev.HpRatio is float hp && hp <= max)) continue;
                    if (!CountsMet(req.Counts, ev.Counters)) continue;
                    hits++;
                }
                _eventHits[s.Id] = hits;
                return hits >= Math.Max(1, req.Event.Times);
            }
            return CountsMet(req.Counts, _record.Counters);
        }

        /// <summary>캐릭터·깬 장·무공 구성·레벨처럼 그 순간 로드아웃으로 바로 볼 수 있는 조건.</summary>
        bool Holds(SecretRequireDef req, BuildLevels levels)
        {
            if (req.Character != null && req.Character != _record.CharacterId) return false;
            if (req.Sect != null && req.Sect != _record.CharacterId) return false;
            foreach (var ch in req.Cleared)
                if (!_record.ClearedChapters.Contains(ch)) return false;
            if (req.OnlyWeapon != null && (levels.Weapons.Count != 1 || !levels.Weapons.ContainsKey(req.OnlyWeapon))) return false;
            foreach (var kv in req.Levels)
                if (levels.Level(kv.Key) < Math.Max(1, kv.Value)) return false;
            return true;
        }

        static bool CountsMet(Dictionary<string, double> counts, RunCounters c)
        {
            foreach (var kv in counts)
                if (c.Get(kv.Key) < kv.Value) return false;
            return true;
        }
    }
}
