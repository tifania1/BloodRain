# PC 루프 (API 크레딧)

Unity 에디터가 필요한 일과 이미지 후처리를 PC에서 맡는다. 클라우드 예약 세션과 같은 저장소(`claude/dev`)를 쓴다.

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
- Unity 에디터가 꺼져 있으면 에이전트는 돌지 않고 이미지 후처리만 한다.

기록은 `tools/agent_loop/logs/날짜.log`, 실행 횟수와 지출은 `state.json` (둘 다 커밋 안 됨).
