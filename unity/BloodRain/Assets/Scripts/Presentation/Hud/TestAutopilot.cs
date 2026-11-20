using BloodRain.Presentation.Hud;
using UnityEngine;

namespace BloodRain.Presentation.Hud
{
    // 시험 메뉴의 '자동 플레이'(2026-11-20 사람: "플레이 테스팅도 해 보고 이상한 점이 있다면 고쳐 줘").
    // 사람 손 없이 한 생을 끝까지 굴려 보려고 만든 것이다. 개발 판(TestPanel.Allowed)에만 붙는다.
    //   걷기: 가까운 적에게서 멀어지는 쪽(거리 제곱에 반비례)에 천천히 도는 배회를 섞는다. 이동 키 대신 ControlInput.MoveOverride 로 넣는다
    //   레벨업: 열린 지 0.35초 뒤 LevelUpScreen.AutoPick(UI-01b 자동 고르기 차례)
    //   기록: 1초마다 시간·경지·레벨·기혈·적 수를 Log 에 쌓아 두었다가 Report() 로 한 줄씩(유니티 MCP 나 콘솔에서 읽음)
    public sealed class TestAutopilot : MonoBehaviour
    {
        public LifeRunner runner;
        public bool On;
        public float fleeRadius = 6f;      // 이 거리 안의 적만 본다(Unity 좌표 = 세계 m)
        public float wanderWeight = 0.35f;

        float _wanderAngle, _pickWait, _logWait, _clock;
        LevelUpScreen _lu;
        readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
        public int Picks { get; private set; }
        public float MinHealthSeen { get; private set; } = float.MaxValue;

        void OnDisable() { ControlInput.MoveOverride = null; }

        void Update()
        {
            if (!On || runner == null || runner.player == null) { if (ControlInput.MoveOverride.HasValue && !On) ControlInput.MoveOverride = null; return; }
            float dt = Time.unscaledDeltaTime;

            // 레벨업 화면: 잠깐 뒤 자동으로 고른다
            if (_lu == null) _lu = FindFirstObjectByType<LevelUpScreen>();
            var lu = _lu;
            if (lu != null && lu.IsOpen)
            {
                ControlInput.MoveOverride = Vector2.zero;
                _pickWait += dt;
                if (_pickWait > 0.35f) { if (lu.AutoPick()) Picks++; _pickWait = 0f; }
                return;
            }
            _pickWait = 0f;

            // 걷기: 적에게서 멀어지기 + 배회
            Vector2 p = runner.player.position;
            Vector2 away = Vector2.zero;
            var sim = runner.enemies != null ? runner.enemies.Sim : null;
            if (sim != null)
            {
                var agents = sim.Agents;
                float r2 = fleeRadius * fleeRadius;
                for (int i = 0; i < sim.Count; i++)
                {
                    var e = agents[i].Pos;
                    var d = p - new Vector2(e.x, e.y);
                    float m2 = d.sqrMagnitude;
                    if (m2 > r2 || m2 < 1e-4f) continue;
                    away += d / m2;
                }
            }
            _wanderAngle += dt * 0.4f;
            var wander = new Vector2(Mathf.Cos(_wanderAngle), Mathf.Sin(_wanderAngle));
            var move = away.sqrMagnitude > 1e-6f ? away.normalized + wander * wanderWeight : wander;
            ControlInput.MoveOverride = Vector2.ClampMagnitude(move, 1f);

            _logWait += Time.deltaTime; _clock += Time.deltaTime;
            if (runner.vitals != null) MinHealthSeen = Mathf.Min(MinHealthSeen, runner.vitals.Health);
            if (_logWait >= 1f)
            {
                _logWait = 0f;
                var life = runner.Life;
                if (life != null)
                    _log.Append($"{_clock:0}s 분{life.Minutes:0.0} 경지{life.Realm} Lv{life.Level} 기혈{(runner.vitals != null ? runner.vitals.Health : -1):0}/{(runner.vitals != null ? runner.vitals.MaxHealth : -1):0} 적{(runner.enemies != null ? runner.enemies.Count : 0)} 땅{life.RegionId}\n");
            }
        }

        /// <summary>쌓인 기록을 돌려주고 비운다.</summary>
        public string Report() { var s = _log.ToString(); _log.Clear(); return s; }
    }
}
