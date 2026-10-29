#nullable enable
// 장 데이터(chapters.json)로 '지금 어떤 적을 뽑을지'와 '이번 프레임에 일어날 사건'을 알려 준다.
// 몇 마리를 만들지는 SpawnCurve(balance.json spawnTarget)가 맡는다. 여기서는 그 곡선을 장마다
// 생성 중지·보스 시각만 바꿔 끼워 주고, 곡선 계산은 다시 하지 않는다.
// 난수는 밖에서 받는다. 같은 시드면 같은 적 순서와 같은 사건 시각이 나온다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Data;

namespace BloodRain.Core.Spawning
{
    /// <summary>한 판 일정 위에 시각이 정해진 사건 하나.</summary>
    public readonly struct WaveEvent
    {
        public readonly float At;
        public readonly ChapterEventDef Def;

        public WaveEvent(float at, ChapterEventDef def)
        {
            At = at;
            Def = def;
        }

        public WaveEventKind Kind => Def.Type;
        public string Enemy => Def.Enemy;
        public int Count => Def.Count;
        public override string ToString() => $"{At:0.##}분 {Def.Kind} {Def.Enemy}";
    }

    /// <summary>한 판짜리 사건 일정. 무작위 창과 확률은 만들 때 이미 정해져 있다.</summary>
    public sealed class WaveSchedule
    {
        public IReadOnlyList<WaveEvent> Events { get; }

        public WaveSchedule(IReadOnlyList<WaveEvent> events) { Events = events; }

        /// <summary>afterMinute &lt; At ≤ untilMinute 인 사건. 지난 프레임 분과 이번 프레임 분을 넣으면 각 사건이 딱 한 번씩 나온다.</summary>
        public IReadOnlyList<WaveEvent> Between(float afterMinute, float untilMinute)
        {
            var due = new List<WaveEvent>();
            foreach (var e in Events)
            {
                if (e.At > untilMinute) break;
                if (e.At > afterMinute) due.Add(e);
            }
            return due;
        }
    }

    public sealed class WaveTable
    {
        readonly Dictionary<string, ChapterDef> _chapters = new Dictionary<string, ChapterDef>();

        public WaveTable(IEnumerable<ChapterDef> chapters)
        {
            foreach (var c in chapters)
            {
                Check(c);
                if (_chapters.ContainsKey(c.Id)) throw new ArgumentException($"chapters.json 에 같은 장 id 가 두 번 있습니다: {c.Id}");
                _chapters[c.Id] = c;
            }
        }

        /// <summary>장 데이터를 읽고 적·정예·보스 id 가 enemies.json 에 있는지까지 확인한다.</summary>
        public static WaveTable From(GameData data)
        {
            var table = new WaveTable(data.Chapters);
            foreach (var c in data.Chapters) CheckIds(c, data);
            return table;
        }

        public IEnumerable<string> ChapterIds => _chapters.Keys;

        public ChapterDef Chapter(string id) =>
            _chapters.TryGetValue(id, out var c) ? c : throw new KeyNotFoundException($"chapters.json 에 없는 장입니다: {id}");

        /// <summary>그 분에 쓰는 가중치표. 0분 전은 첫 표, 마지막 표가 끝난 뒤(보스전이 길어질 때)는 마지막 표.</summary>
        public SpawnBandDef BandAt(string chapterId, float minute)
        {
            var bands = Chapter(chapterId).Bands;
            foreach (var b in bands)
                if (minute < b.ToMinute) return b;
            return bands[bands.Count - 1];
        }

        /// <summary>가중치대로 적 종류 하나를 뽑는다.</summary>
        public string PickEnemy(string chapterId, float minute, Random rng)
        {
            var band = BandAt(chapterId, minute);
            double r = rng.NextDouble() * band.TotalWeight;
            double acc = 0;
            foreach (var s in band.Spawns)
            {
                acc += s.Weight;
                if (r < acc) return s.Enemy;
            }
            return band.Spawns[band.Spawns.Count - 1].Enemy;
        }

        /// <summary>한 판 일정을 만든다. 창(window) 사건은 창 안 무작위 시각, chance 가 1보다 작은 사건은 그 확률로만 들어간다.
        /// 난수는 데이터 순서대로 확률 → 시각 순으로 쓴다.</summary>
        public WaveSchedule Schedule(string chapterId, Random rng)
        {
            var list = new List<WaveEvent>();
            foreach (var e in Chapter(chapterId).Events)
            {
                if (e.Chance < 1 && rng.NextDouble() >= e.Chance) continue;
                float at = e.Window != null
                    ? e.Window[0] + (float)rng.NextDouble() * (e.Window[1] - e.Window[0])
                    : e.At;
                list.Add(new WaveEvent(at, e));
            }
            // 같은 시각이면 데이터 순서를 지킨다(OrderBy는 안정 정렬)
            return new WaveSchedule(list.OrderBy(x => x.At).ToList());
        }

        /// <summary>balance.json 의 화면 위 적 수 곡선을 그대로 쓰고, 생성 중지와 보스 시각만 이 장 값으로 바꾼 SpawnCurve.</summary>
        public SpawnCurve Curve(string chapterId, SpawnTargetDef shared)
        {
            var c = Chapter(chapterId);
            return new SpawnCurve(new SpawnTargetDef
            {
                Points = shared.Points,
                SpawnFractionPerSecond = shared.SpawnFractionPerSecond,
                StopSpawningAtMinute = c.StopSpawningAtMinute,
                BossAtMinute = c.BossAtMinute,
            });
        }

        /// <summary>이 장에서 그 적이 가장 먼저 나올 수 있는 분(가중치표나 사건 중 이른 쪽). 안 나오면 null.</summary>
        public float? FirstAppearance(string chapterId, string enemyId)
        {
            var c = Chapter(chapterId);
            float? first = null;
            foreach (var b in c.Bands)
                if (b.WeightOf(enemyId) > 0) { first = b.FromMinute; break; }
            foreach (var e in c.Events)
                if (e.Enemy == enemyId && (first == null || e.EarliestMinute < first)) first = e.EarliestMinute;
            return first;
        }

        static void Check(ChapterDef c)
        {
            string at = $"chapters.json {c.Id}";
            if (c.DurationMinutes <= 0) throw new InvalidOperationException($"{at}: durationMinutes 가 0 이하");
            if (c.Bands.Count == 0) throw new InvalidOperationException($"{at}: 시간대 표(bands)가 없음");
            if (c.Bands[0].FromMinute != 0) throw new InvalidOperationException($"{at}: 첫 시간대가 0분에서 시작하지 않음");
            for (int i = 0; i < c.Bands.Count; i++)
            {
                var b = c.Bands[i];
                if (b.ToMinute <= b.FromMinute) throw new InvalidOperationException($"{at}: {b.FromMinute}분 시간대 길이가 0 이하");
                if (i > 0 && b.FromMinute != c.Bands[i - 1].ToMinute) throw new InvalidOperationException($"{at}: {b.FromMinute}분 시간대가 앞 시간대와 이어지지 않음");
                if (b.Spawns.Count == 0 || b.Spawns.Any(s => s.Weight <= 0)) throw new InvalidOperationException($"{at}: {b.FromMinute}분 시간대에 가중치가 비었거나 0 이하");
            }
            if (c.Bands[c.Bands.Count - 1].ToMinute < c.DurationMinutes) throw new InvalidOperationException($"{at}: 시간대 표가 판 길이까지 닿지 않음");

            foreach (var e in c.Events)
            {
                if (e.Type == WaveEventKind.Unknown) throw new InvalidOperationException($"{at}: 모르는 사건 종류 '{e.Kind}'");
                if (e.Window != null && (e.Window.Length != 2 || e.Window[1] < e.Window[0])) throw new InvalidOperationException($"{at}: {e.Kind} 창(window)은 [시작, 끝] 두 값");
                if (e.Chance <= 0 || e.Chance > 1) throw new InvalidOperationException($"{at}: {e.Kind} chance 는 0 초과 1 이하");
                if (e.Count < 1) throw new InvalidOperationException($"{at}: {e.Kind} count 는 1 이상");
                if (WaveEventKinds.SpawnsEnemy(e.Type) && string.IsNullOrEmpty(e.Enemy)) throw new InvalidOperationException($"{at}: {e.Kind} 에 enemy 가 없음");
            }
            var bosses = c.Events.Where(e => e.Type == WaveEventKind.Boss).ToList();
            if (bosses.Count != 1 || bosses[0].Window != null) throw new InvalidOperationException($"{at}: 보스 사건은 정해진 시각(at)으로 정확히 하나");
            if (c.StopSpawningAtMinute > c.BossAtMinute) throw new InvalidOperationException($"{at}: 생성 중지가 보스보다 늦음");
        }

        static void CheckIds(ChapterDef c, GameData data)
        {
            string at = $"chapters.json {c.Id}";
            foreach (var b in c.Bands)
                foreach (var s in b.Spawns)
                    if (!data.Enemies.Any(x => x.Id == s.Enemy)) throw new InvalidOperationException($"{at}: enemies.json 에 없는 적 {s.Enemy}");
            data.Elites.TryGetValue(c.Id, out var elites);
            foreach (var e in c.Events)
            {
                bool ok;
                switch (e.Type)
                {
                    case WaveEventKind.Elite: ok = elites != null && elites.Contains(e.Enemy); break;
                    case WaveEventKind.Boss: ok = data.Bosses.Any(x => x.Id == e.Enemy && x.Chapter == c.Id); break;
                    case WaveEventKind.Merchant:
                    case WaveEventKind.SilverLantern: ok = string.IsNullOrEmpty(e.Enemy); break;
                    default: ok = data.Enemies.Any(x => x.Id == e.Enemy); break;
                }
                if (!ok) throw new InvalidOperationException($"{at}: {e.Kind} 사건의 '{e.Enemy}' 가 enemies.json 과 맞지 않음");
            }
        }
    }
}
