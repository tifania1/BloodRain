// 숭산 20분(옛 1장 한 판)을 화면 없이 돌린다. 0.1초마다:
//   사건(정예·혈인 돌진·동갑시 포위·금두꺼비) → 1초마다 적 생성(Core SpawnCurve) → 무리 갱신 → 무공 발동 → 오의
//   → 처치 진기 흡수(Core RunXp) → 레벨업 카드(Core Loadout + 정책) → 경지 돌파·절기 각성(Core Loadout 그릇 규칙) → 보갑
// 규칙은 Core 것을 그대로 쓰고, 데이터에 없는 값은 Assumptions 에서 가져온다.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Progression;
using BloodRain.Core.Spawning;
using static BloodRain.Sim.Assumptions;

namespace BloodRain.Sim
{
    public sealed class SimOptions
    {
        public string Policy = "average";
        public int Seed = 1;
        public double Minutes = RunMinutes;
        public string Character = "jin_sowoon";
        /// <summary>적 기혈 배율을 찾는 지역의 장 이름(balance.json enemyScaling.chapterMultiplier, cheonha.json regions[].chapter). chapter1 은 숭산.
        /// chapters.json 의 흐름 id(prologue, life)가 아니다.</summary>
        public string Chapter = "chapter1";
        /// <summary>처치 진기를 바꿔 볼 때(조정 시험). 없으면 Assumptions.KillXp.</summary>
        public IReadOnlyDictionary<string, double>? KillXp;
        /// <summary>정예 진기를 바꿔 볼 때. 없으면 Assumptions.EliteXp.</summary>
        public double? EliteXp;
        /// <summary>민감도 시험용. 없으면 Assumptions 값.</summary>
        public double? CrowdRadius, MaxDensity, ArrivalSeconds;
        /// <summary>연무장 레벨(StatKind 이름 → 레벨). 메타 시뮬레이션(T-18)이 혼의 연무장을 넘긴다. 없으면 연무장 없음.</summary>
        public IReadOnlyDictionary<string, int>? Training;

        public SimOptions With(Action<SimOptions> change)
        {
            var o = (SimOptions)MemberwiseClone();
            change(o);
            return o;
        }
    }

    public sealed class MinuteSnapshot
    {
        public int Minute;
        public int Level;
        public string RealmKo = "";
        public int Kills;
        /// <summary>판 처음부터 생긴 적(사건으로 나온 적 포함).</summary>
        public int Spawned;
        /// <summary>살아 있는 적 전부(생성 디렉터가 세는 수).</summary>
        public int Alive;
        /// <summary>그중 무리에 닿아 맞을 수 있는 적.</summary>
        public int InCrowd;
        /// <summary>보스 한 마리를 칠 때의 초당 피해(기획서 화력 목표와 견주는 값).</summary>
        public double SingleDps;
        /// <summary>지난 1분 동안 실제로 깎은 기혈 / 60(넘친 피해는 빼고).</summary>
        public double CrowdDamagePerSecond;
        public int Evolved;
        public double TotalXp;
        public string Build = "";
    }

    public sealed class AwakenRecord
    {
        public double Minute;
        public int Level;
        public string WeaponId = "";
        public string NameKo = "";
        public string Trigger = "";
    }

    public sealed class SimResult
    {
        public SimOptions Options = null!;
        public List<MinuteSnapshot> Minutes = new List<MinuteSnapshot>();
        public List<AwakenRecord> Awakenings = new List<AwakenRecord>();
        /// <summary>경지 id → 처음 닿은 시각(분).</summary>
        public Dictionary<string, double> RealmMinute = new Dictionary<string, double>();
        public int Ultimates;

        public MinuteSnapshot At(int minute) => Minutes.First(m => m.Minute == minute);
        public int LevelAt(int minute) => At(minute).Level;
    }

    public sealed class RunSimulator
    {
        sealed class Enemy
        {
            public string Id = "";
            public double Hp, MaxHp, Xp, Born, PoisonT;
            public bool Elite, Priority, Dead, Mogin;
        }

        sealed class WeaponState
        {
            public string Id = "";
            public bool Evolved;
            /// <summary>[프로토] 새 무공은 0.2초 뒤 첫 발동.</summary>
            public double Timer = 0.2;
            public double GlowUntil = -1;
        }

        static readonly IReadOnlyDictionary<string, int> NoTraining = new Dictionary<string, int>();

        readonly SimData _data;
        readonly GameData _g;
        readonly SimOptions _opt;
        readonly Random _rng;
        readonly IPolicy _policy;
        readonly CharacterDef _character;
        readonly UltimateDef? _ultDef;
        readonly Loadout _loadout;
        readonly Realms _realms;
        readonly RunXp _xp;
        readonly SpawnCurve _spawn;
        readonly EnemyScaling _scaling;
        readonly UltimateCharge _ult;
        readonly ISet<string> _unlocked;
        readonly PolicyContext _ctx;
        readonly IReadOnlyDictionary<string, double> _killXp;
        readonly double _eliteXp, _crowdRadius, _maxDensity, _arrival, _crowdArea;
        readonly int _weaponMax;
        readonly int _jeoljeongLevel;

        readonly List<Enemy> _enemies = new List<Enemy>();
        readonly List<Enemy> _crowd = new List<Enemy>();
        readonly List<WeaponState> _weapons = new List<WeaponState>();
        readonly Queue<Enemy> _bursts = new Queue<Enemy>();
        readonly SimResult _result;

        BuildStats _b = null!;
        double _t, _pendingXp, _spawnPauseUntil, _dealtThisMinute, _density;
        int _kills, _spawned, _pendingChests;

        public static SimResult Run(SimData data, SimOptions options) => new RunSimulator(data, options).Execute();

        RunSimulator(SimData data, SimOptions opt)
        {
            _data = data;
            _g = data.Game;
            _opt = opt;
            _rng = new Random(opt.Seed);
            _policy = Policies.Create(opt.Policy);
            _character = _g.Character(opt.Character);
            _ultDef = _g.Ultimates.FirstOrDefault(u => u.Id == _character.Ultimate);
            _loadout = new Loadout(_g, _character);
            _realms = new Realms(_g.Balance.Realms);
            _xp = new RunXp(new XpCurve(_g.Balance.XpCurve), _realms);
            _spawn = new SpawnCurve(_g.Balance.SpawnTarget);
            _scaling = new EnemyScaling(_g.Balance.EnemyScaling);
            _ult = new UltimateCharge(_g.Balance.UltimateCharge, _g.Balance.Yinyang.Harmony);
            _unlocked = new HashSet<string>(_g.Weapons.Where(w => w.Unlock == "start").Select(w => w.Id));
            _killXp = opt.KillXp ?? KillXp;
            _eliteXp = opt.EliteXp ?? EliteXp;
            _crowdRadius = opt.CrowdRadius ?? CrowdRadius;
            _maxDensity = opt.MaxDensity ?? MaxDensity;
            _arrival = opt.ArrivalSeconds ?? ArrivalSeconds;
            _crowdArea = Math.PI * _crowdRadius * _crowdRadius;
            _weaponMax = _g.Balance.Levels.WeaponMax;
            _jeoljeongLevel = _g.Balance.Realms.FirstOrDefault(r => r.Id == "jeoljeong")?.Level ?? int.MaxValue;
            _weapons.Add(new WeaponState { Id = _character.StartWeapon });
            _ctx = new PolicyContext { Data = _g, Loadout = _loadout, Rng = _rng, CrowdDpsAfter = CrowdDpsAfter };
            _result = new SimResult { Options = opt };
        }

        SimResult Execute()
        {
            RecomputeStats();
            var events = BuildEvents();
            int perSecond = (int)Math.Round(1 / TickSeconds);
            int perMinute = perSecond * 60;
            int total = (int)Math.Round(_opt.Minutes * 60 * perSecond);
            Snapshot(0);
            int ev = 0;
            for (int i = 1; i <= total; i++)
            {
                _t = i * TickSeconds;
                double m = _t / 60;
                while (ev < events.Count && m >= events[ev].Minute - 1e-9) RunEvent(events[ev++], m);
                // [프로토] 1초마다 생성. 보스(20분)부터는 이 시뮬레이터가 다루지 않는다.
                if (i % perSecond == 0 && _t >= _spawnPauseUntil && m < _opt.Minutes - 1e-9) Spawn(m);
                BuildCrowd();
                for (int w = 0; w < _weapons.Count; w++) TickWeapon(_weapons[w]);
                if (_ult.IsFull && _crowd.Count > 0) FireUltimate();
                Settle();
                _enemies.RemoveAll(e => e.Dead);
                if (i % perMinute == 0) Snapshot(i / perMinute);
            }
            _result.Ultimates = _ultUses;
            return _result;
        }

        int _ultUses;

        // ───────── 사건과 생성 ─────────

        List<(double Minute, EventKind Kind, int Count)> BuildEvents()
        {
            var list = new List<(double Minute, EventKind Kind, int Count)>();
            foreach (var e in Events)
            {
                if (e.Kind == EventKind.Toad)
                {
                    if (!ToadCaught) continue;
                    list.Add((ToadFrom + _rng.NextDouble() * (ToadTo - ToadFrom), e.Kind, e.Count));
                }
                else list.Add(e);
            }
            return list.OrderBy(e => e.Minute).ToList();
        }

        void RunEvent((double Minute, EventKind Kind, int Count) e, double m)
        {
            switch (e.Kind)
            {
                case EventKind.Charge:
                    for (int i = 0; i < e.Count - ChargeGap; i++) AddEnemy("hyeolin", m);
                    break;
                case EventKind.Ring:
                    for (int i = 0; i < e.Count; i++) AddEnemy("donggap", m);
                    break;
                case EventKind.Toad:
                {
                    var t = AddEnemy("golden_toad", m);
                    t.Xp = GoldenToadXp;
                    t.Priority = true;
                    break;
                }
                case EventKind.Elite:
                {
                    double hp = EliteHealth[e.Count] * (1 + _g.Balance.EnemyScaling.HealthPerMinute * m) * _scaling.ChapterMultiplier(_opt.Chapter);
                    _enemies.Add(new Enemy { Id = "elite", Hp = hp, MaxHp = hp, Xp = _eliteXp, Born = _t, Elite = true, Priority = true });
                    _spawned++;
                    break;
                }
            }
        }

        void Spawn(double m)
        {
            int alive = 0;
            foreach (var e in _enemies) if (!e.Dead) alive++;
            int n = _spawn.SpawnThisSecond((float)m, alive);
            // 한 생 곡선에는 생성 중지가 없다(18분 뒤 400 유지). 19~20분에도 그대로 만든다.
            for (int i = 0; i < n; i++) AddEnemy(PickType(m), m);
        }

        string PickType(double m)
        {
            var mix = EnemyMix[0].Mix;
            foreach (var row in EnemyMix) if (m >= row.FromMinute) mix = row.Mix;
            double r = _rng.NextDouble(), acc = 0;
            foreach (var (id, w) in mix)
            {
                acc += w;
                if (r <= acc) return id;
            }
            return mix[0].Id;
        }

        Enemy AddEnemy(string id, double m)
        {
            var def = _g.Enemy(id);
            double hp = _scaling.Health(def, (float)m, _opt.Chapter);
            var e = new Enemy
            {
                Id = id, Hp = hp, MaxHp = hp, Born = _t, Mogin = id == "mogin_nahan",
                Xp = _killXp.TryGetValue(id, out var x) ? x : DefaultKillXp,
            };
            _enemies.Add(e);
            _spawned++;
            return e;
        }

        /// <summary>무리: 생긴 지 도착 시간이 지난 적. 정예·금두꺼비를 맨 앞에, 나머지는 먼저 온 순서.</summary>
        void BuildCrowd()
        {
            _crowd.Clear();
            foreach (var e in _enemies) if (!e.Dead && e.Priority && _t - e.Born >= _arrival) _crowd.Add(e);
            foreach (var e in _enemies) if (!e.Dead && !e.Priority && _t - e.Born >= _arrival) _crowd.Add(e);
            _density = Math.Min(_maxDensity, _crowd.Count / _crowdArea);
        }

        // ───────── 무공 ─────────

        void TickWeapon(WeaponState w)
        {
            w.Timer -= TickSeconds;
            int guard = 0;
            while (w.Timer <= 0 && guard++ < 50)
            {
                var def = _g.Weapon(w.Id);
                int lv = _loadout.Weapons[w.Id];
                var ex = _data.Extras.TryGetValue(w.Id, out var x) ? x : null;
                foreach (var st in WeaponModel.Strikes(def, ex, lv, w.Evolved, _t < w.GlowUntil, _b, _weaponMax)) Apply(st);
                w.Timer += WeaponModel.Interval(def, ex, lv, w.Evolved, _b.Cdr, _weaponMax);
            }
        }

        int StochasticRound(double x)
        {
            if (x <= 0) return 0;
            int f = (int)Math.Floor(x);
            return f + (_rng.NextDouble() < x - f ? 1 : 0);
        }

        void Apply(in Strike st)
        {
            if (_crowd.Count == 0) return;
            int k = StochasticRound(st.Area > 0 ? _density * st.Area : st.Targets);
            if (k <= 0) return;
            int start = st.Random ? _rng.Next(Math.Min(RandomPickNearest, _crowd.Count)) : 0;
            int hit = 0;
            for (int j = 0; j < _crowd.Count && hit < k; j++)
            {
                var e = _crowd[(start + j) % _crowd.Count];
                if (e.Dead) continue;
                hit++;
                for (int h = 0; h < st.Hits && !e.Dead; h++) Hit(e, st.Damage);
                if (!e.Dead && st.Dot > 0) Hit(e, st.Dot);
                if (!e.Dead && st.AuraExecute && !e.Elite)
                {
                    e.PoisonT += 0.5;
                    if (e.PoisonT >= 7) { _dealtThisMinute += e.Hp; e.Hp = 0; Kill(e); }
                }
            }
        }

        void Hit(Enemy e, double dmg)
        {
            if (e.Dead) return;
            double d = e.Elite ? dmg * (1 + _b.EliteDamage) : dmg;
            _dealtThisMinute += Math.Min(d, e.Hp);
            e.Hp -= d;
            if (e.Hp <= 0) Kill(e);
        }

        void Kill(Enemy e)
        {
            if (e.Dead) return;
            e.Dead = true;
            _kills++;
            _pendingXp += e.Xp * PickupRate;
            var band = _loadout.Gauge.Band;
            if (e.Elite) { _ult.OnEliteKill(band); _pendingChests++; }
            else _ult.OnKill(band);
            if (e.Mogin) _bursts.Enqueue(e);
        }

        /// <summary>오의(진소운 일검): 시작 무공 한 번 피해 × 배율, 바라보는 쪽 화면 끝까지 한 줄.</summary>
        void FireUltimate()
        {
            if (!_ult.TryUse()) return;
            _ultUses++;
            var def = _g.Weapon(_character.StartWeapon);
            double baseDmg = _loadout.Weapons.TryGetValue(def.Id, out var lv)
                ? WeaponModel.HitDamage(def, lv, _loadout.Evolved.Contains(def.Id), false, _b, _weaponMax)
                : def.DamageLv1 * _b.Sheet.Might;
            double power = _xp.Level >= _jeoljeongLevel ? UltPowerAtJeoljeong : 1;
            double mult = _ultDef?.MightMultiplier ?? 0;
            if (mult <= 0) return;
            Apply(new Strike(area: UltLineWidth * _crowdRadius, random: true, damage: baseDmg * mult * power));
        }

        // ───────── 진기, 레벨업, 돌파, 보갑 ─────────

        void Settle()
        {
            while (true)
            {
                if (_bursts.Count > 0)
                {
                    _bursts.Dequeue();
                    double r = MoginBurstRadius + EnemyRadius;
                    Apply(new Strike(area: Math.PI * r * r, random: true, damage: MoginBurstDamage));
                    continue;
                }
                if (_pendingXp > 0)
                {
                    double x = _pendingXp;
                    _pendingXp = 0;
                    var r = _xp.Absorb(x);
                    for (int lv = r.FromLevel + 1; lv <= r.ToLevel; lv++) LevelUp(lv);
                    continue;
                }
                if (_pendingChests > 0)
                {
                    _pendingChests--;
                    OpenChest();
                    continue;
                }
                break;
            }
        }

        void LevelUp(int level)
        {
            var offers = _loadout.RollLevelUp(_rng, (float)_b.Sheet.Luck, _unlocked);
            ApplyOffer(_policy.Choose(offers, _ctx));
            foreach (var realm in _realms.Breakthroughs(level - 1, level)) Breakthrough(realm, level);
        }

        void ApplyOffer(Offer o)
        {
            _loadout.Apply(o);
            if (o.Kind == OfferKind.NewWeapon) _weapons.Add(new WeaponState { Id = o.Id });
            RecomputeStats();
        }

        void Breakthrough(RealmDef realm, int level)
        {
            if (!_result.RealmMinute.ContainsKey(realm.Id)) _result.RealmMinute[realm.Id] = _t / 60;
            if (realm.Id == _g.Balance.Slots.ExtraSlotAtRealm) _loadout.AddExtraSlot(_policy.ExtraSlotIsWeapon(_ctx));
            var ready = _loadout.AwakenOnBreakthrough(level - 1, level);
            if (ready.Count > 0) Awaken(_policy.PickAwaken(ready, _ctx), realm.NameKo + " 돌파");
        }

        void OpenChest()
        {
            var evo = _loadout.AwakenOnChest(_xp.Level);
            if (evo != null) { Awaken(evo, "보갑"); return; }
            double r = _rng.NextDouble();
            int n = r < ChestThree ? 3 : r < ChestThree + ChestTwo ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                var pool = _loadout.Candidates(_unlocked)
                    .Where(c => c.Kind == OfferKind.UpgradeWeapon || c.Kind == OfferKind.UpgradePassive).ToList();
                if (pool.Count == 0) break;
                ApplyOffer(pool[_rng.Next(pool.Count)]);
            }
        }

        void Awaken(EvolutionDef evo, string trigger)
        {
            _loadout.Evolve(evo);
            var ws = _weapons.First(w => w.Id == evo.Weapon);
            ws.Evolved = true;
            ws.GlowUntil = _t + GlowSeconds;
            _result.Awakenings.Add(new AwakenRecord { Minute = _t / 60, Level = _xp.Level, WeaponId = evo.Weapon, NameKo = evo.NameKo, Trigger = trigger });
            if (WaveClearsTrash)
            {
                // 각성 파동: 화면 안(무리에 닿은) 잡몹은 쓸려 나가고 정예는 최대 기혈 15%를 잃는다.
                foreach (var e in _enemies)
                {
                    if (e.Dead || _t - e.Born < _arrival) continue;
                    if (e.Elite)
                    {
                        double cut = Math.Min(e.Hp, e.MaxHp * WaveEliteRatio);
                        _dealtThisMinute += cut;
                        e.Hp -= cut;
                        if (e.Hp <= 0) Kill(e);
                    }
                    else { _dealtThisMinute += e.Hp; e.Hp = 0; Kill(e); }
                }
            }
            _spawnPauseUntil = Math.Max(_spawnPauseUntil, _t + WaveSpawnPause);
        }

        // ───────── 능력치 ─────────

        BuildStats MakeStats(IReadOnlyDictionary<string, int> passives)
        {
            var sheet = Stats.Compute(_g, _character, passives, _opt.Training ?? NoTraining);
            var gauge = _loadout.Gauge;
            double harmony = gauge.Band == YinYangBand.Harmony ? _g.Balance.Yinyang.Harmony.CooldownReduction : 0;
            return new BuildStats
            {
                Sheet = sheet,
                Cdr = Math.Min(_g.Balance.Stats.CooldownReductionCap, sheet.Bonus[StatKind.CooldownReduction] + harmony),
                Band = gauge.Band,
                Leaning = gauge.Leaning,
                BiasBonus = _data.BiasAspectBonus,
                ExtremeBonus = _data.ExtremeAspectBonus,
                Crit = sheet.Bonus[StatKind.CritChance],
                EliteDamage = sheet.Bonus[StatKind.EliteDamage],
                Burn = sheet.Bonus[StatKind.BurnDamage],
                Dot = sheet.Bonus[StatKind.DotDamage],
                ExplosionArea = sheet.Bonus[StatKind.ExplosionArea],
            };
        }

        void RecomputeStats()
        {
            _b = MakeStats(_loadout.Passives);
            _xp.Growth = _b.Sheet.Growth;
        }

        /// <summary>화력 욕심 정책용: 이 카드를 고른 뒤 무리에 쏟을 초당 피해 기대값. 음양 구간은 지금 그대로 둔다.</summary>
        double CrowdDpsAfter(Offer o)
        {
            var wl = new Dictionary<string, int>(_loadout.Weapons);
            var pl = new Dictionary<string, int>(_loadout.Passives);
            switch (o.Kind)
            {
                case OfferKind.NewWeapon: wl[o.Id] = 1; break;
                case OfferKind.UpgradeWeapon: wl[o.Id]++; break;
                case OfferKind.NewPassive: pl[o.Id] = 1; break;
                case OfferKind.UpgradePassive: pl[o.Id]++; break;
            }
            var b = MakeStats(pl);
            // 각성 파동 직후처럼 무리가 비어 있으면 비교가 안 되므로 바닥값을 둔다.
            double density = Math.Max(_density, 0.3);
            int inCrowd = Math.Max(_crowd.Count, 30);
            double sum = 0;
            foreach (var kv in wl)
            {
                var def = _g.Weapon(kv.Key);
                var ex = _data.Extras.TryGetValue(kv.Key, out var x) ? x : null;
                bool evolved = _loadout.Evolved.Contains(kv.Key);
                var strikes = WeaponModel.Strikes(def, ex, kv.Value, evolved, false, b, _weaponMax);
                sum += WeaponModel.ExpectedCrowdDps(strikes, WeaponModel.Interval(def, ex, kv.Value, evolved, b.Cdr, _weaponMax), density, inCrowd);
            }
            return sum;
        }

        double SingleDps()
        {
            double sum = 0;
            foreach (var w in _weapons)
            {
                var def = _g.Weapon(w.Id);
                int lv = _loadout.Weapons[w.Id];
                var ex = _data.Extras.TryGetValue(w.Id, out var x) ? x : null;
                var strikes = WeaponModel.Strikes(def, ex, lv, w.Evolved, false, _b, _weaponMax);
                sum += WeaponModel.SingleTargetDps(strikes, WeaponModel.Interval(def, ex, lv, w.Evolved, _b.Cdr, _weaponMax));
            }
            return sum;
        }

        // ───────── 기록 ─────────

        void Snapshot(int minute)
        {
            int alive = 0, inCrowd = 0;
            foreach (var e in _enemies)
            {
                if (e.Dead) continue;
                alive++;
                if (_t - e.Born >= _arrival) inCrowd++;
            }
            _result.Minutes.Add(new MinuteSnapshot
            {
                Minute = minute,
                Level = _xp.Level,
                RealmKo = _xp.Realm.NameKo,
                Kills = _kills,
                Spawned = _spawned,
                Alive = alive,
                InCrowd = inCrowd,
                SingleDps = SingleDps(),
                CrowdDamagePerSecond = minute == 0 ? 0 : _dealtThisMinute / 60,
                Evolved = _loadout.Evolved.Count,
                TotalXp = _xp.Total,
                Build = BuildText(),
            });
            _dealtThisMinute = 0;
        }

        string BuildText()
        {
            string Short(string nameKo) => nameKo.Length > 2 ? nameKo.Substring(0, 2) : nameKo;
            var w = _loadout.Weapons.Select(kv => Short(_g.Weapon(kv.Key).NameKo) + kv.Value + (_loadout.Evolved.Contains(kv.Key) ? "*" : ""));
            var p = _loadout.Passives.Select(kv => Short(_g.Passive(kv.Key).NameKo) + kv.Value);
            return string.Join(" ", w) + " / " + string.Join(" ", p);
        }
    }
}
