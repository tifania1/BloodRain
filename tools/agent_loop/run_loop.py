"""PC에서 도는 API 크레딧 개발 루프 (Claude Agent SDK).

클라우드 예약 세션은 Unity 에디터가 없어서 씬·프리팹·플레이 검수를 못 한다. 그 몫을 여기서 한다.
Windows 작업 스케줄러가 깨우면(register_task.ps1) 한 번 돌고 끝난다.

한 번 실행할 때 하는 일
  1. 저장소를 claude/dev 최신으로 맞춘다. 작업 트리에 사람이 고치던 변경이 있으면 아무것도 안 하고 끝낸다.
  2. art_queue 에서 picked 상태인 이미지를 후처리해 Assets/Art 로 옮긴다(tools/postprocess_art.py).
  3. Unity 에디터가 켜져 있고 docs/unity_tasks.md 에 안 끝난 항목이 있으면,
     에이전트가 Unity MCP로 1~2개를 처리하고 스크린샷을 docs/daily/ 에 남긴다.
  4. 바뀐 것이 있으면 claude/dev 로 커밋·푸시한다.

API 키는 config.json(커밋 안 됨)에만 둔다. PC의 ANTHROPIC_API_KEY 환경변수는 회사 키라서
건드리지 않고, 이 스크립트가 띄우는 에이전트 프로세스에만 개인 키를 넘긴다.

쓰는 법:  python tools/agent_loop/run_loop.py [--dry-run]
필요:    pip install claude-agent-sdk pillow
"""
from __future__ import annotations

import argparse
import asyncio
import datetime as dt
import json
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
CONFIG = HERE / "config.json"
STATE = HERE / "state.json"
LOGS = HERE / "logs"


def log(msg: str) -> None:
    LOGS.mkdir(exist_ok=True)
    line = f"[{dt.datetime.now():%Y-%m-%d %H:%M:%S}] {msg}"
    print(line)
    with open(LOGS / f"{dt.date.today():%Y-%m-%d}.log", "a", encoding="utf-8") as f:
        f.write(line + "\n")


def git(*args: str, check: bool = True) -> str:
    r = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    if check and r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} 실패: {r.stderr.strip()}")
    return r.stdout.strip()


def load_json(p: Path, default: dict) -> dict:
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else dict(default)


def unity_running() -> bool:
    r = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Unity.exe"], capture_output=True, text=True)
    return "Unity.exe" in r.stdout


def open_unity_tasks() -> list[str]:
    p = ROOT / "docs" / "unity_tasks.md"
    if not p.exists():
        return []
    return [l for l in p.read_text(encoding="utf-8").splitlines() if l.strip().startswith("- [ ]")]


def sync_repo() -> bool:
    """claude/dev 최신으로. 사람이 고치던 변경이 있으면 False."""
    dirty = [l for l in git("status", "--porcelain").splitlines() if not l.endswith("/") and "art_inbox" not in l]
    if dirty:
        log("작업 트리에 커밋 안 된 변경이 있어 이번 실행은 건너뜀: " + "; ".join(dirty[:5]))
        return False
    git("fetch", "origin")
    branches = git("branch", "--list", "claude/dev")
    if not branches:
        git("checkout", "-B", "claude/dev", "origin/claude/dev")
    else:
        git("checkout", "claude/dev")
        git("merge", "--ff-only", "origin/claude/dev")
    return True


def postprocess_art(dry: bool) -> None:
    args = [sys.executable, str(ROOT / "tools" / "postprocess_art.py")]
    if dry:
        args.append("--dry-run")
    r = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    log("이미지 후처리: " + (r.stdout.strip().splitlines() or ["(출력 없음)"])[-1])
    if r.returncode != 0:
        log("이미지 후처리 실패: " + r.stderr.strip()[-500:])


PROMPT = """너는 혈우강호(BloodRain) Unity 프로젝트의 PC 담당 개발자다. 저장소 루트는 {root}, Unity 프로젝트는 unity/BloodRain 이다.
먼저 CLAUDE.md 와 docs/feedback.md 를 읽어라. 그다음 docs/unity_tasks.md 에서 위에서부터 안 끝난 항목(- [ ]) 1~2개만 처리한다.

규칙
- Unity 에디터 조작은 unityMCP 도구로 한다. 스크립트를 고친 뒤에는 refresh_unity 후 read_console 로 오류가 없는지 꼭 본다.
- 끝낸 항목은 - [x] 로 바꾸고 한 줄로 결과를 적는다. 막히면 - [ ] 그대로 두고 아래에 '막힘:' 한 줄을 적는다.
- 화면에 보이는 작업이면 플레이 모드 스크린샷을 docs/daily/{today}-unity.png 로 남긴다.
- Assets/Scripts/Core 에는 UnityEngine 을 쓰지 않는다. 바꿨으면 `dotnet test core/BloodRain.sln` 을 돌려 통과를 확인한다.
- 커밋은 하되 푸시는 하지 마라(바깥 스크립트가 한다). 커밋 메시지는 한국어 한 줄.
- 일을 지어내지 말 것. 할 항목이 없으면 아무것도 바꾸지 말고 끝낸다.
"""


async def run_agent(cfg: dict) -> None:
    from claude_agent_sdk import ClaudeAgentOptions, ResultMessage, query  # 필요할 때만 불러온다

    options = ClaudeAgentOptions(
        cwd=str(ROOT),
        model=cfg.get("model", "sonnet"),
        permission_mode="acceptEdits",
        allowed_tools=["Read", "Edit", "Write", "Glob", "Grep", "Bash", "mcp__unityMCP"],
        disallowed_tools=["Bash(git push*)", "Bash(rm -rf*)", "Bash(git reset --hard*)"],
        max_turns=int(cfg.get("max_turns", 40)),
        max_budget_usd=float(cfg.get("max_budget_usd_per_run", 1.5)),
        setting_sources=["project"],
        system_prompt={"type": "preset", "preset": "claude_code"},
        mcp_servers={
            "unityMCP": {
                "type": "stdio",
                "command": cfg.get("unity_mcp_command", str(Path.home() / ".local" / "bin" / "uvx.exe")),
                "args": cfg.get("unity_mcp_args", ["--from", "mcpforunityserver==10.3.0", "mcp-for-unity", "--transport", "stdio"]),
            }
        },
        env={"ANTHROPIC_API_KEY": cfg["api_key"], "CLAUDE_AGENT_SDK_CLIENT_APP": "bloodrain-pc-loop"},
        **({"cli_path": cfg["cli_path"]} if cfg.get("cli_path") else {}),
    )
    prompt = PROMPT.format(root=ROOT, today=f"{dt.date.today():%Y-%m-%d}")
    async for message in query(prompt=prompt, options=options):
        if isinstance(message, ResultMessage):
            cost = getattr(message, "total_cost_usd", None)
            log(f"에이전트 종료: {message.subtype}, 비용 {cost if cost is not None else '알 수 없음'} USD")
            state = load_json(STATE, {})
            month = f"{dt.date.today():%Y-%m}"
            state.setdefault("spend", {})
            state["spend"][month] = round(state["spend"].get(month, 0) + (cost or 0), 4)
            STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def commit_and_push(dry: bool) -> None:
    if git("status", "--porcelain"):
        if dry:
            log("(dry) 바뀐 파일이 있지만 커밋하지 않음")
            return
        git("add", "-A")
        git("commit", "-m", "PC 루프: 이미지 반영·Unity 작업")
    ahead = git("rev-list", "--count", "origin/claude/dev..HEAD", check=False) or "0"
    if ahead != "0" and not dry:
        git("push", "origin", "HEAD:claude/dev")
        log(f"claude/dev 에 커밋 {ahead}개 푸시")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    if not CONFIG.exists():
        log("config.json 이 없다. config.example.json 을 복사해 개인 API 키를 넣을 것")
        return 1
    cfg = json.loads(CONFIG.read_text(encoding="utf-8"))

    state = load_json(STATE, {})
    today = f"{dt.date.today():%Y-%m-%d}"
    runs = state.get("runs", {}).get(today, 0)
    if runs >= int(cfg.get("max_runs_per_day", 4)):
        log(f"오늘 실행 상한({runs}회)에 닿아 건너뜀")
        return 0
    month = f"{dt.date.today():%Y-%m}"
    if state.get("spend", {}).get(month, 0) >= float(cfg.get("monthly_budget_usd", 30)):
        log("이번 달 예산을 다 써서 건너뜀")
        return 0

    if not sync_repo():
        return 0
    postprocess_art(args.dry_run)

    tasks = open_unity_tasks()
    if not tasks:
        log("Unity 작업 목록이 비어 있어 에이전트는 건너뜀")
    elif not unity_running():
        log(f"Unity 에디터가 꺼져 있어 에이전트는 건너뜀 (남은 Unity 작업 {len(tasks)}개)")
    elif args.dry_run:
        log(f"(dry) 에이전트를 돌릴 차례 — 남은 Unity 작업 {len(tasks)}개")
    else:
        state.setdefault("runs", {})[today] = runs + 1
        STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
        asyncio.run(run_agent(cfg))

    commit_and_push(args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
