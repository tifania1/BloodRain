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
        /// <summary>장면이 다시 열려도(서장 다시 하기·새 생) 켜 둔 채로(TestPanel 이 새 것에 넣는다).</summary>
        public static bool Sticky;
        public float fleeRadius = 6f;      // 이 거리 안의 적만 본다(Unity 좌표 = 세계 m)
        public float keepOut = 1.8f;      // 이보다 가까운 적에게서는 물러난다

        float _wanderAngle, _pickWait, _logWait, _clock;
        LevelUpScreen _lu;
        BloodRain.Presentation.Fx.OrbLayer _orbs;
        PlayerController _pc;
        readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
        public int Picks { get; private set; }
        public float MinHealthSeen { get; private set; } = float.MaxValue;

        void OnDisable() { ControlInput.MoveOverride = null; }

        void Update()
        {
            if (!On || runner == null || runner.player == null) { if (ControlInput.MoveOverride.HasValue && !On) ControlInput.MoveOverride = null; return; }
            float dt = Time.unscaledDeltaTime;

            // 다른 멈춤 화면(깨달음·절기 고르기·검총·상인·보갑 목록): 잠깐 뒤 첫 칸을 고르거나 닫는다
            if (_wait == null) _wait = new Waiters();
            if (_wait.Handle(dt)) { ControlInput.MoveOverride = Vector2.zero; return; }

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

            // 걷기: 사람처럼 무리 가장자리를 돈다 — 너무 가까운 적(keepOut 안)에게서는 물러나고, 아무도 가깝지 않으면
            // 가장 가까운 적 쪽으로 다가가되 옆으로 비껴(접선) 돈다. 근접 무공(삼재검법)이 닿게 하려는 것(멀리 도망만 치면 처치 0)
            Vector2 p = runner.player.position;
            Vector2 away = Vector2.zero, nearest = Vector2.zero;
            float nearD2 = float.MaxValue;
            var sim = runner.enemies != null ? runner.enemies.Sim : null;
            if (sim != null)
            {
                var agents = sim.Agents;
                // 경지가 오르면 세상 배율이 바뀌니 거리도 경지 m 로 잰다(이류 돌파 뒤 1.8m 고정이면 몸에 닿는 거리 안에 서 있게 됨)
                var wsk = BloodRain.Sim.WorldScale.Current;
                float ko = wsk != null ? wsk.Length(keepOut) : keepOut;
                float k2 = ko * ko;
                for (int i = 0; i < sim.Count; i++)
                {
                    var e = agents[i].Pos;
                    var d = p - new Vector2(e.x, e.y);
                    float m2 = d.sqrMagnitude;
                    if (m2 < nearD2) { nearD2 = m2; nearest = new Vector2(e.x, e.y); }
                    if (m2 > k2 || m2 < 1e-4f) continue;
                    away += d / m2;
                }
            }
            _wanderAngle += dt * 0.4f;
            Vector2 move;
            if (away.sqrMagnitude > 1e-6f) move = away.normalized;
            else if (BloodRain.Sim.WorldScale.Current == null || nearD2 < BloodRain.Sim.WorldScale.Current.Length(fleeRadius * 2f) * BloodRain.Sim.WorldScale.Current.Length(fleeRadius * 2f))
            {
                var to = (nearest - p).normalized;
                var side = new Vector2(-to.y, to.x);
                move = (to * 0.6f + side * 0.8f).normalized;
            }
            else move = new Vector2(Mathf.Cos(_wanderAngle), Mathf.Sin(_wanderAngle));
            // 혈우 장막(경지마다 갈 수 있는 테두리)에 밀리기 시작하면 태어난 자리 쪽으로 돌아선다
            if (_pc == null) _pc = runner.player.GetComponent<PlayerController>();
            var ws = BloodRain.Sim.WorldScale.Current;
            if (ws != null && _pc != null)
            {
                float push = ws.Scale.PushStrength(new System.Numerics.Vector2(p.x, p.y), new System.Numerics.Vector2(_pc.birthplace.x, _pc.birthplace.y), ws.Realm);
                if (push > 0.02f) move = ((_pc.birthplace - p).normalized * 2f + move).normalized;
            }
            // 가까운 적이 없으면 진기 구슬을 주우러 간다(사람처럼). 12m 안 가장 가까운 것
            if (away.sqrMagnitude < 1e-6f)
            {
                if (_orbs == null) _orbs = FindFirstObjectByType<BloodRain.Presentation.Fx.OrbLayer>();
                if (_orbs != null && _orbs.Field != null)
                {
                    float best = BloodRain.Sim.WorldScale.Current != null ? BloodRain.Sim.WorldScale.Current.Length(12f) * BloodRain.Sim.WorldScale.Current.Length(12f) : 144f; Vector2 tgt = Vector2.zero; bool found = false;
                    var list = _orbs.Field.Orbs;
                    for (int i = 0; i < list.Count; i++)
                    {
                        float m2 = (list[i].Pos - p).sqrMagnitude;
                        if (m2 < best) { best = m2; tgt = list[i].Pos; found = true; }
                    }
                    if (found && (nearD2 > (BloodRain.Sim.WorldScale.Current != null ? BloodRain.Sim.WorldScale.Current.Length(keepOut * 2f) * BloodRain.Sim.WorldScale.Current.Length(keepOut * 2f) : keepOut * keepOut * 4f))) move = ((tgt - p).normalized * 1.5f + move * 0.5f).normalized;
                }
            }
            ControlInput.MoveOverride = Vector2.ClampMagnitude(move, 1f);

            _logWait += Time.deltaTime; _clock += Time.deltaTime;
            if (runner.vitals != null) MinHealthSeen = Mathf.Min(MinHealthSeen, runner.vitals.Health);
            if (_logWait >= 1f)
            {
                _logWait = 0f;
                var life = runner.Life;
                if (life != null)
                    _log.Append($"{(_pc != null && BloodRain.Sim.WorldScale.Current != null ? "장막" + BloodRain.Sim.WorldScale.Current.Scale.PushStrength(new System.Numerics.Vector2(p.x, p.y), new System.Numerics.Vector2(_pc.birthplace.x, _pc.birthplace.y), BloodRain.Sim.WorldScale.Current.Realm).ToString("0.00") + " " : "")}{_clock:0}s 분{life.Minutes:0.0} 경지{life.Realm} Lv{life.Level} 기혈{(runner.vitals != null ? runner.vitals.Health : -1):0}/{(runner.vitals != null ? runner.vitals.MaxHealth : -1):0} 적{(runner.enemies != null ? runner.enemies.Count : 0)} 땅{life.RegionId} 맞음{(runner.vitals != null ? runner.vitals.Hits : 0)} 피해{(runner.vitals != null ? runner.vitals.DamageTaken : 0):0} 배속{Time.timeScale:0.##}\n");
            }
        }

        Waiters _wait;
        sealed class Waiters
        {
            InsightScreen _insight; EvolutionPickScreen _evo; SwordTombScreen _tomb; MerchantScreen _shop; BloodRain.Presentation.Fx.ChestLayer _chest;
            float _t;
            public int Handled;
            public bool Handle(float dt)
            {
                if (_insight == null) _insight = Object.FindFirstObjectByType<InsightScreen>();
                if (_evo == null) _evo = Object.FindFirstObjectByType<EvolutionPickScreen>();
                if (_tomb == null) _tomb = Object.FindFirstObjectByType<SwordTombScreen>();
                if (_shop == null) _shop = Object.FindFirstObjectByType<MerchantScreen>();
                if (_chest == null) _chest = Object.FindFirstObjectByType<BloodRain.Presentation.Fx.ChestLayer>();
                bool any = (_insight != null && _insight.IsOpen) || (_evo != null && _evo.IsOpen) || (_tomb != null && _tomb.IsOpen)
                           || (_shop != null && _shop.IsOpen) || (_chest != null && _chest.ListOpen);
                if (!any) { _t = 0f; return false; }
                _t += dt;
                if (_t < 0.6f) return true;
                _t = 0f; Handled++;
                if (_insight != null && _insight.IsOpen) _insight.Pick(0);
                else if (_evo != null && _evo.IsOpen) _evo.Pick(0);
                else if (_tomb != null && _tomb.IsOpen) _tomb.Pick(0);
                else if (_shop != null && _shop.IsOpen) _shop.Close();
                else if (_chest != null && _chest.ListOpen) _chest.CloseListForTest();
                return true;
            }
        }

        /// <summary>쌓인 기록을 돌려주고 비운다.</summary>
        public string Report() { var s = _log.ToString(); _log.Clear(); return s; }
    }
}
