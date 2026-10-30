// 서장 끝 순서(기획서 4장 '서장 상세 흐름', 21장 '서장의 절기 맛보기와 첫 환혼').
// 장삼전에서 기혈이 바닥나면 천지인 삼검이 고정으로 깨어나고(늘 긴 연출), 장삼을 쓰러뜨리면
// 진소운의 몸이 다하는 컷과 목 노인이 회혼침을 꽂는 컷을 지나 첫 환혼으로 간다.
// 화면·연출은 Unity가 하고 여기는 '지금 무엇을 할 차례인가'만 정한다. 값은 cheonha.json prologueBody.
using System;
using System.Collections.Generic;
using System.Linq;
using BloodRain.Core.Build;
using BloodRain.Core.Combat;
using BloodRain.Core.Data;
using BloodRain.Core.Meta;

namespace BloodRain.Core.Run
{
    public enum PrologueStep
    {
        /// <summary>0:00~9:00 객잔 마당.</summary>
        Fight,
        /// <summary>장삼이 나왔다. 아직 각성 전.</summary>
        Boss,
        /// <summary>천지인 삼검이 깨어나는 연출 중.</summary>
        Awakening,
        /// <summary>각성 뒤 장삼을 마저 친다. 진소운은 쓰러지지 않는다.</summary>
        Finish,
        /// <summary>장삼 처치 뒤 컷(몸이 다함 → 회혼침).</summary>
        Cutscene,
        /// <summary>침방에서 첫 환혼 후보를 고른다.</summary>
        FirstRebirth,
        /// <summary>첫 환혼을 골랐다. 서장 끝.</summary>
        Done,
    }

    /// <summary>진소운이 쓰러질 때(기혈 0) 무엇을 하나.</summary>
    public enum PrologueDown
    {
        /// <summary>장삼 전: 서장을 처음부터 다시(혼을 붙잡을 목 노인이 아직 안 나왔다).</summary>
        Restart,
        /// <summary>장삼전 첫 바닥: 쓰러지지 않고 고정 각성을 시작한다. 기혈은 1로 남긴다.</summary>
        Awaken,
        /// <summary>각성 뒤: 쓰러지지 않는다. 기혈 1.</summary>
        Hold,
        /// <summary>컷이나 침방 중이라 상관없다.</summary>
        Ignore,
    }

    public sealed class Prologue
    {
        readonly PrologueBodyDef _def;
        readonly GameData _data;
        int _cut;

        public Prologue(GameData data)
        {
            _data = data;
            _def = data.Cheonha.PrologueBody;
            Evolution = data.Evolutions.First(e => e.Id == _def.FixedAwakening);
        }

        public PrologueStep Step { get; private set; } = PrologueStep.Fight;
        /// <summary>장삼전에서 깨어나는 절기(천지인 삼검).</summary>
        public EvolutionDef Evolution { get; }
        /// <summary>몇 번 처음부터 다시 했나(장삼 전에 쓰러진 횟수).</summary>
        public int Restarts { get; private set; }
        /// <summary>각성을 부른 것이 진소운의 기혈 바닥이 아니라 장삼 기혈 바닥이었나(센 플레이어).</summary>
        public bool AwakenedByBossFloor { get; private set; }

        /// <summary>지금 보여 줄 컷(art_queue id). 컷 차례가 아니면 null.</summary>
        public string? CurrentCut => Step switch
        {
            PrologueStep.Awakening => _def.AwakenCut,
            PrologueStep.Cutscene => _cut < _def.EndCuts.Count ? _def.EndCuts[_cut] : null,
            _ => null,
        };

        public void BossAppeared(string enemyId)
        {
            if (enemyId != _def.Boss) throw new ArgumentException($"서장 보스는 {_def.Boss} 입니다: {enemyId}");
            if (Step == PrologueStep.Fight) Step = PrologueStep.Boss;
        }

        /// <summary>진소운의 기혈이 0이 됐다.</summary>
        public PrologueDown PlayerDown()
        {
            switch (Step)
            {
                case PrologueStep.Fight:
                    Restarts++;
                    return PrologueDown.Restart;
                case PrologueStep.Boss:
                    Step = PrologueStep.Awakening;
                    return PrologueDown.Awaken;
                case PrologueStep.Awakening:
                case PrologueStep.Finish:
                    return PrologueDown.Hold;
                default:
                    return PrologueDown.Ignore;
            }
        }

        /// <summary>
        /// 장삼에게 들어간 피해를 깎은 뒤의 기혈. 각성 전에는 최대 기혈 × bossHealthFloor 아래로 안 내려가고,
        /// 거기 닿으면 그 순간 각성을 시작한다(Step 이 Awakening 이 된다). 각성 뒤에는 그대로.
        /// </summary>
        public float ApplyBossDamage(float health, float maxHealth, float damage)
        {
            float next = health - damage;
            if (Step == PrologueStep.Fight || Step == PrologueStep.Boss || Step == PrologueStep.Awakening)
            {
                float floor = maxHealth * _def.BossHealthFloor;
                if (next <= floor)
                {
                    next = Math.Min(health, floor);
                    if (Step == PrologueStep.Boss) { Step = PrologueStep.Awakening; AwakenedByBossFloor = true; }
                }
            }
            return Math.Max(0f, next);
        }

        /// <summary>각성 연출을 시작한다. 계정 첫 각성이 아니어도, 설정이 짧은 연출이어도 늘 긴 연출이다.</summary>
        public void StartAwakening(Awakening awakening, Loadout loadout)
        {
            if (Step != PrologueStep.Awakening) throw new InvalidOperationException($"각성 차례가 아닙니다: {Step}");
            loadout.Evolve(Evolution);
            awakening.Start(Evolution, firstOnAccount: true, AwakeningLength.Short, forceLong: true);
        }

        /// <summary>각성 연출이 끝났다(AwakeningCue.End).</summary>
        public void AwakeningEnded()
        {
            if (Step == PrologueStep.Awakening) Step = PrologueStep.Finish;
        }

        /// <summary>장삼이 쓰러졌다. 각성 전에는 쓰러질 수 없다(ApplyBossDamage 가 막는다).</summary>
        public void BossKilled()
        {
            if (Step != PrologueStep.Finish) throw new InvalidOperationException($"각성 뒤에만 장삼이 쓰러집니다: {Step}");
            Step = PrologueStep.Cutscene;
            _cut = 0;
            if (_def.EndCuts.Count == 0) Step = PrologueStep.FirstRebirth;
        }

        /// <summary>지금 컷을 넘겼다. 마지막 컷 다음은 침방(첫 환혼).</summary>
        public void NextCut()
        {
            if (Step != PrologueStep.Cutscene) return;
            _cut++;
            if (_cut >= _def.EndCuts.Count) Step = PrologueStep.FirstRebirth;
        }

        /// <summary>첫 환혼 후보(소림·개방·남궁, 성질 없음).</summary>
        public List<RebirthCandidate> FirstCandidates(Random rng)
        {
            if (Step != PrologueStep.FirstRebirth) throw new InvalidOperationException($"첫 환혼 차례가 아닙니다: {Step}");
            return Rebirth.First(_data.Cheonha, rng);
        }

        /// <summary>첫 환혼 후보를 골랐다.</summary>
        public RebirthBody ChooseFirst(RebirthCandidate candidate)
        {
            if (Step != PrologueStep.FirstRebirth) throw new InvalidOperationException($"첫 환혼 차례가 아닙니다: {Step}");
            Step = PrologueStep.Done;
            return Rebirth.Body(_data.Cheonha, candidate);
        }

        /// <summary>장삼 전에 쓰러져 처음부터 다시 할 때. 다시 한 횟수는 남긴다.</summary>
        public void Restart()
        {
            Step = PrologueStep.Fight;
            _cut = 0;
            AwakenedByBossFloor = false;
        }
    }
}
