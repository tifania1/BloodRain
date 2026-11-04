# PC 루프 (API 크레딧)

Unity 에디터가 필요한 일(`docs/unity_tasks.md`)과 이미지 후처리를 PC에서 맡는다. 개인 Console 조직의 API 크레딧(Agent SDK)으로 돈다. 클라우드 예약 세션과 같은 저장소(`claude/dev`)를 쓴다.

시각: 매일 21:32·23:32·01:32·03:32, 주말 낮 10:30·12:30·14:30·16:30. PC Unity 예약 세션(20:37·22:37·00:37·02:37·04:37) 사이 틈이다.

## 처음 한 번
1. 개인 Claude Console 조직에서 API 키를 만든다. 월 지출 한도도 그 조직 설정에서 걸어 둔다.
2. `config.example.json` 을 `config.json` 으로 복사하고 `api_key` 를 넣는다. 회사 키(PC의 ANTHROPIC_API_KEY)는 쓰지 않는다.
3. `pip install claude-agent-sdk pillow`
4. 시험: `python tools/agent_loop/run_loop.py --dry-run`
5. 예약 등록: `powershell -ExecutionPolicy Bypass -File tools\agent_loop\register_task.ps1`

## 안전장치
- 작업 트리에 사람이 고치던 변경이 있으면 그 회차는 건너뛴다.
- 하루 실행 횟수(`max_runs_per_day`), 회당 비용(`max_budget_usd_per_run`), 월 예산(`monthly_budget_usd`) 상한.
- 에이전트는 커밋만 하고 푸시는 스크립트가 `claude/dev` 로만 한다.
- 다른 프로젝트 Unity(회사 일, 배치 빌드 포함)가 떠 있으면 아무것도 안 한다.
- BloodRain Unity 가 꺼져 있으면 켜서 쓰고 끝나면 닫는다. 이미 떠 있으면 사람 입력이 20분 넘게 없을 때만 쓰고 닫지 않는다.
- 2026-11 프로모션 크레딧(US$100, 11-13 UTC 만료)을 쓰려고 opus·회당 3달러·하루 8회·월 95달러로 잡았다. 자동 충전은 꺼 둔다.

기록은 `tools/agent_loop/logs/날짜.log`, 실행 횟수와 지출은 `state.json` (둘 다 커밋 안 됨).
