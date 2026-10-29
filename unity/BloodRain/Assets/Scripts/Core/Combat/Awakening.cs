#nullable enable
// 절기 각성 — 깨어나는 순간부터 벌어지는 일(기획서 8장 '절기 그릇과 각성'의 각성 연출 표, docs/design/vfx.md).
// 언제 깨어나는지는 Build.Loadout(AwakenOnBreakthrough, AwakenOnChest)이 정한다. 여기서는 그 뒤를 맡는다.
// 숫자는 balance.json awakening. 웹 프로토타입 template.html 의 AW·tickAwaken·awakenShock 과 같은 규칙이다.
//
// 시계가 둘이다.
//   Tick(실제 시간)       연출 시간표. 장면 신호(낙관, 파동, 끝 …)와 세상 시간 배율 WorldTimeScale 을 낸다.
//   TickWorld(세상 시간)  여운(그 절기 피해 1.5배), 적 생성 멈춤, 무적처럼 게임 안 시간으로 줄어드는 것.
//                         레벨업 창 같은 일시정지 동안에는 부르지 않는다.
//
// Unity 쪽 흐름:
//   1. 깨어날 절기가 정해지면 Loadout.Evolve(evo) 후 Start(evo, 계정에서 처음인지, 설정).
//   2. 매 프레임 Tick(Time.unscaledDeltaTime) → Time.timeScale = WorldTimeScale. 연출 코루틴·카메라는 언스케일 시간.
//   3. Shock 신호가 오면 화면 안 적을 ShockTarget 목록으로 만들어 ResolveShock(목록, ShockRadius(화면 폭, 높이)) 결과를 적용한다.
//      처치·피해는 깨어난 절기의 몫으로 센다(처치 보상·오의 충전도 평소처럼).
//   4. 게임 갱신에서 TickWorld(Time.deltaTime). 피격은 IsPlayerInvulnerable, 생성은 SpawnPaused,
//      무공 피해는 DamageMultiplier(무공 id), 레벨업 창·보갑 줍기는 DefersLevelUpAndChest 를 본다.
using System;
using System.Collections.Generic;
using BloodRain.Core.Data;

namespace BloodRain.Core.Combat
{
    /// <summary>설정 '각성 연출': 자동(그 절기를 처음 깨울 때만 길게), 항상 길게, 항상 짧게.</summary>
    public enum AwakeningLength { Auto, Long, Short }

    /// <summary>연출 장면 신호. Tick 한 번에 여럿이 올 수 있어서 묶음으로 돌려준다.</summary>
    [Flags]
    public enum AwakeningCue
    {
        None = 0,
        /// <summary>문턱: 무공 칸 글자가 날아온다(긴 연출만).</summary>
        Glyph = 1,
        /// <summary>균열(긴 연출만).</summary>
        Crack = 2,
        /// <summary>한자 이름 붓질.</summary>
        Name = 4,
        /// <summary>絶 낙관. 무공 칸이 절기 그림으로 바뀐다.</summary>
        Stamp = 8,
        /// <summary>파동. 이 신호에 ResolveShock 을 적용한다. 여운과 적 생성 멈춤도 여기서 시작된다.</summary>
        Shock = 16,
        /// <summary>연출이 끝났다. 미뤄 둔 레벨업 창을 연다.</summary>
        End = 32,
    }

    public enum ShockTargetKind { Normal, Elite, Boss }

    /// <summary>파동 판정에 넘기는 적 하나. 위치 대신 플레이어와의 거리만 받는다.</summary>
    public readonly struct ShockTarget
    {
        public readonly ShockTargetKind Kind;
        public readonly float MaxHealth;
        public readonly float Distance;

        public ShockTarget(ShockTargetKind kind, float maxHealth, float distance)
        {
            Kind = kind;
            MaxHealth = maxHealth;
            Distance = distance;
        }
    }

    /// <summary>파동이 적 하나에게 하는 일.</summary>
    public readonly struct ShockHit
    {
        /// <summary>ResolveShock 에 넘긴 목록에서의 순번.</summary>
        public readonly int Index;
        public readonly ShockTargetKind Kind;
        /// <summary>true면 그 자리에서 처치(잡몹). Damage 는 기록용으로 최대 기혈을 넣는다.</summary>
        public readonly bool Kill;
        /// <summary>깎을 기혈. 정예는 최대 기혈의 15%, 보스는 4%.</summary>
        public readonly float Damage;
        /// <summary>플레이어 반대쪽으로 밀어내는 힘. 정예만 있다.</summary>
        public readonly float Knockback;

        public ShockHit(int index, ShockTargetKind kind, bool kill, float damage, float knockback)
        {
            Index = index;
            Kind = kind;
            Kill = kill;
            Damage = damage;
            Knockback = knockback;
        }
    }

    public sealed class ShockResult
    {
        /// <summary>반경 안에 든 적만, 넘긴 순서대로.</summary>
        public IReadOnlyList<ShockHit> Hits { get; }
        /// <summary>화면의 적 탄을 모두 지운다(반경과 상관없이).</summary>
        public bool ClearEnemyProjectiles { get; }
        /// <summary>깨어난 절기의 재사용 대기를 0으로.</summary>
        public bool ResetCooldown { get; }

        public ShockResult(IReadOnlyList<ShockHit> hits, bool clearEnemyProjectiles, bool resetCooldown)
        {
            Hits = hits;
            ClearEnemyProjectiles = clearEnemyProjectiles;
            ResetCooldown = resetCooldown;
        }

        public int Kills
        {
            get
            {
                int n = 0;
                foreach (var h in Hits) if (h.Kill) n++;
                return n;
            }
        }
    }

    public sealed class Awakening
    {
        readonly AwakeningDef _def;
        AwakeningTimelineDef _timeline;
        float _t;
        AwakeningCue _fired;
        float _afterglow;
        float _spawnPause;
        float _invulnerable;

        /// <summary>장면 신호를 시간 순서대로 하나씩 알린다(Tick 의 반환값과 같은 내용).</summary>
        public event Action<AwakeningCue>? Cue;

        public Awakening(AwakeningDef def)
        {
            _def = def;
            _timeline = def.Short;
        }

        /// <summary>연출이 진행 중인가.</summary>
        public bool IsActive { get; private set; }
        /// <summary>지금(또는 마지막) 연출이 긴 연출인가.</summary>
        public bool IsLong { get; private set; }
        /// <summary>지금(또는 마지막) 연출의 시간표.</summary>
        public AwakeningTimelineDef Timeline => _timeline;
        /// <summary>지금(또는 마지막)으로 깨어난 절기.</summary>
        public EvolutionDef? Evolution { get; private set; }
        /// <summary>연출 시작부터 흐른 실제 시간.</summary>
        public float Elapsed => _t;
        /// <summary>이번 연출의 파동이 이미 지나갔나.</summary>
        public bool ShockDone => (_fired & AwakeningCue.Shock) != 0;

        /// <summary>금빛 여운이 남은 절기. 없으면 null.</summary>
        public EvolutionDef? AfterglowEvolution { get; private set; }
        public float AfterglowRemaining => _afterglow;
        /// <summary>적이 새로 몰려오지 않는가.</summary>
        public bool SpawnPaused => _spawnPause > 0f;
        /// <summary>플레이어가 피해를 받지 않는가. 연출 동안 내내, 그리고 연출 길이 + 0.8초가 세상 시간으로 다 줄 때까지.</summary>
        public bool IsPlayerInvulnerable => IsActive || _invulnerable > 0f;
        /// <summary>레벨업 창과 보갑 줍기를 미루는가.</summary>
        public bool DefersLevelUpAndChest => IsActive;

        /// <summary>긴 연출인가. 설정이 자동이면 그 절기를 계정에서 처음 깨울 때만 길게.</summary>
        public static bool ChooseLong(AwakeningLength setting, bool firstOnAccount) => setting switch
        {
            AwakeningLength.Long => true,
            AwakeningLength.Short => false,
            _ => firstOnAccount,
        };

        /// <summary>연출을 시작한다.</summary>
        /// <param name="firstOnAccount">이 절기를 계정에서 한 번도 깨운 적이 없는가(장경각 기록).</param>
        /// <param name="setting">설정 '각성 연출'.</param>
        /// <param name="forceLong">설정보다 앞선다. 서장 고정 각성이나 시연처럼 늘 길게 보여 줄 때 true.</param>
        public void Start(EvolutionDef evolution, bool firstOnAccount, AwakeningLength setting = AwakeningLength.Auto, bool? forceLong = null)
        {
            if (IsActive) throw new InvalidOperationException("각성 연출이 이미 진행 중입니다");
            IsLong = forceLong ?? ChooseLong(setting, firstOnAccount);
            _timeline = IsLong ? _def.Long : _def.Short;
            Evolution = evolution;
            _t = 0f;
            _fired = AwakeningCue.None;
            IsActive = true;
            _invulnerable = Math.Max(_invulnerable, _timeline.DurationSeconds + _def.InvulnerableExtraSeconds);
        }

        /// <summary>연출 시계를 실제 시간으로 민다. 이번에 지나간 장면 신호를 돌려준다.</summary>
        public AwakeningCue Tick(float unscaledDt)
        {
            if (!IsActive) return AwakeningCue.None;
            if (unscaledDt > 0f) _t += unscaledDt;

            var p = _timeline;
            var cues = Pass(AwakeningCue.Glyph, p.GlyphSeconds)
                     | Pass(AwakeningCue.Crack, p.CrackSeconds)
                     | Pass(AwakeningCue.Name, p.NameSeconds)
                     | Pass(AwakeningCue.Stamp, p.StampSeconds)
                     | Pass(AwakeningCue.Shock, p.ShockSeconds)
                     | Pass(AwakeningCue.End, p.DurationSeconds);

            if ((cues & AwakeningCue.Shock) != 0)
            {
                AfterglowEvolution = Evolution;
                _afterglow = _def.AfterglowSeconds;
                _spawnPause = Math.Max(_spawnPause, _def.SpawnPauseSeconds);
            }
            if ((cues & AwakeningCue.End) != 0) IsActive = false;

            // 상태를 다 바꾼 뒤에 알린다. End 를 받은 쪽이 바로 다음 각성을 시작해도 되게.
            if (Cue != null && cues != AwakeningCue.None)
                foreach (var c in CueOrder)
                    if ((cues & c) != 0) Cue(c);
            return cues;
        }

        static readonly AwakeningCue[] CueOrder =
        {
            AwakeningCue.Glyph, AwakeningCue.Crack, AwakeningCue.Name, AwakeningCue.Stamp, AwakeningCue.Shock, AwakeningCue.End,
        };

        AwakeningCue Pass(AwakeningCue cue, float? at)
        {
            if (at == null || (_fired & cue) != 0 || _t < at.Value) return AwakeningCue.None;
            _fired |= cue;
            return cue;
        }

        /// <summary>여운·적 생성 멈춤·무적을 세상 시간으로 줄인다.</summary>
        public void TickWorld(float dt)
        {
            if (dt <= 0f) return;
            _afterglow = Math.Max(0f, _afterglow - dt);
            if (_afterglow <= 0f) AfterglowEvolution = null;
            _spawnPause = Math.Max(0f, _spawnPause - dt);
            _invulnerable = Math.Max(0f, _invulnerable - dt);
        }

        /// <summary>세상 시간 배율. 처음 0.18초 동안 0.04까지 느려지고, 파동 뒤 0.25초 동안 1로 돌아온다. 연출이 없으면 1.</summary>
        public float WorldTimeScale
        {
            get
            {
                if (!IsActive) return 1f;
                float slow = _def.WorldTimeScale;
                float shock = _timeline.ShockSeconds;
                return _t < shock
                    ? Lerp(1f, slow, Ramp(_t, _def.TimeScaleInSeconds))
                    : Lerp(slow, 1f, Ramp(_t - shock, _def.TimeScaleOutSeconds));
            }
        }

        /// <summary>여운 동안 깨어난 그 절기(무공 id 기준)의 피해 배율 1.5. 나머지는 1.</summary>
        public float DamageMultiplier(string weaponId) =>
            AfterglowEvolution != null && _afterglow > 0f && AfterglowEvolution.Weapon == weaponId
                ? _def.AfterglowDamageMultiplier
                : 1f;

        /// <summary>파동 반경. 화면(월드 단위) 대각선 × 0.62.</summary>
        public float ShockRadius(float viewWidth, float viewHeight) =>
            (float)Math.Sqrt(viewWidth * viewWidth + viewHeight * viewHeight) * _def.ShockRadiusViewDiagonal;

        /// <summary>파동 판정. 반경 안(경계 포함) 잡몹은 처치, 정예는 최대 기혈 15%와 밀어내기, 보스는 4%.</summary>
        public ShockResult ResolveShock(IReadOnlyList<ShockTarget> targets, float radius)
        {
            var hits = new List<ShockHit>();
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                if (e.Distance > radius) continue;
                switch (e.Kind)
                {
                    case ShockTargetKind.Boss:
                        hits.Add(new ShockHit(i, e.Kind, false, e.MaxHealth * _def.BossMaxHealthRatio, 0f));
                        break;
                    case ShockTargetKind.Elite:
                        hits.Add(new ShockHit(i, e.Kind, false, e.MaxHealth * _def.EliteMaxHealthRatio, _def.EliteKnockback));
                        break;
                    default:
                        hits.Add(new ShockHit(i, e.Kind, true, e.MaxHealth, 0f));
                        break;
                }
            }
            return new ShockResult(hits, _def.ClearEnemyProjectiles, _def.ResetCooldownOnShock);
        }

        static float Ramp(float x, float length) => length <= 0f ? 1f : Math.Max(0f, Math.Min(1f, x / length));
        static float Lerp(float a, float b, float k) => a + (b - a) * k;
    }
}
