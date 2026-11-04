"""PC에서 도는 API 크레딧 개발 루프 (Claude Agent SDK).

클라우드 예약 세션은 Unity 에디터가 없어서 씬·프리팹·플레이 검수를 못 한다. 그 몫을 여기서 한다.
Windows 작업 스케줄러가 깨우면(register_task.ps1) 한 번 돌고 끝난다.

한 번 실행할 때 하는 일
  1. 저장소를 claude/dev 최신으로 맞춘다. 작업 트리에 사람이 고치던 변경이 있으면 아무것도 안 하고 끝낸다.
  2. art_queue 에서 picked 상태인 이미지를 후처리해 Assets/Art 로 옮긴다(tools/postprocess_art.py).
  3. docs/unity_tasks.md 에 안 끝난 항목이 있으면 에이전트가 Unity MCP 로 1~3개를 처리한다.
     다른 프로젝트 Unity(회사 일)가 떠 있으면 건너뛴다. BloodRain Unity 가 꺼져 있으면 켜고, 끝나면 닫는다.
     BloodRain Unity 가 이미 떠 있으면 사람 입력이 20분 넘게 없을 때만 쓰고 닫지 않는다.
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
import os
import subprocess
import time
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
    r = subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} 실패: {r.stderr.strip()}")
    return (r.stdout or "").strip()


def load_json(p: Path, default: dict) -> dict:
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else dict(default)


UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe"
PROJECT = ROOT / "unity" / "BloodRain"


def unity_processes() -> list[tuple[int, str]]:
    """떠 있는 Unity.exe 의 (pid, 명령줄). Unity Hub 의 `unity.exe serve` 는 뺀다."""
    ps = ("Get-CimInstance Win32_Process -Filter \"name='Unity.exe'\" | "
          "ForEach-Object { \"$($_.ProcessId)`t$($_.CommandLine)\" }")
    r = subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = []
    for line in (r.stdout or "").splitlines():
        if "\t" not in line:
            continue
        pid, cmd = line.split("\t", 1)
        if " serve" in cmd.lower():
            continue
        out.append((int(pid), cmd))
    return out


def is_bloodrain(cmd: str) -> bool:
    c = cmd.lower().replace("/", "\\")
    return str(PROJECT).lower() in c


def idle_minutes() -> float:
    """마지막 키보드·마우스 입력 뒤로 지난 분(사람이 PC 앞에 있는지)."""
    import ctypes

    class LASTINPUTINFO(ctypes.Structure):
        _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]

    li = LASTINPUTINFO()
    li.cbSize = ctypes.sizeof(li)
    ctypes.windll.user32.GetLastInputInfo(ctypes.byref(li))
    return (ctypes.windll.kernel32.GetTickCount() - li.dwTime) / 60000.0


def launch_unity(wait_seconds: int = 720) -> int | None:
    """BloodRain Unity 를 켜고 창이 뜬 뒤 가져오기가 끝날 때까지 기다린다. 실패하면 None."""
    import time

    proc = subprocess.Popen([UNITY_EXE, "-projectPath", str(PROJECT)])
    start = time.time()
    while time.time() - start < wait_seconds:
        time.sleep(15)
        r = subprocess.run(["powershell", "-NoProfile", "-Command",
                            f"(Get-Process -Id {proc.pid} -ErrorAction SilentlyContinue).MainWindowTitle"],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
        if "BloodRain" in (r.stdout or ""):
            time.sleep(90)  # 스크립트 컴파일과 MCP 다리가 붙을 시간
            return proc.pid
    log("Unity 창이 12분 안에 안 떠서 닫음")
    close_unity(proc.pid)
    return None


def close_unity(pid: int) -> None:
    import time

    subprocess.run(["powershell", "-NoProfile", "-Command",
                    f"(Get-Process -Id {pid} -ErrorAction SilentlyContinue).CloseMainWindow() | Out-Null"])
    for _ in range(12):
        time.sleep(5)
        r = subprocess.run(["powershell", "-NoProfile", "-Command", f"Get-Process -Id {pid} -ErrorAction SilentlyContinue"],
                           capture_output=True, text=True)
        if not (r.stdout or "").strip():
            return
    subprocess.run(["powershell", "-NoProfile", "-Command", f"Stop-Process -Id {pid} -Force"])


def open_unity_tasks() -> list[str]:
    p = ROOT / "docs" / "unity_tasks.md"
    if not p.exists():
        return []
    return [l for l in p.read_text(encoding="utf-8").splitlines() if l.strip().startswith("- [ ]")]


def sync_repo() -> bool:
    """claude/dev 최신으로. 사람이 고치던 변경이 있으면 False."""
    # 추적 안 되는 파일(로그 등)은 사람 작업으로 보지 않는다. 고친 채 커밋 안 된 추적 파일만 본다
    dirty = [l for l in git("status", "--porcelain", "--untracked-files=no").splitlines() if "art_inbox" not in l]
    # TextMeshPro 동적 글꼴(*SDF.asset)은 Unity 를 켜기만 해도 글자 아틀라스가 바뀐다. 이것만 바뀌었으면 되돌리고 간다
    # (2026-11-04 21:32 실행 뒤 NotoSerifKR SDF.asset 하나 때문에 다음 실행이 막힘)
    sdf = [l for l in dirty if l.rstrip('"').endswith("SDF.asset")]
    if dirty and len(sdf) == len(dirty):
        paths = [l[3:].strip().strip('"') for l in sdf]
        git("checkout", "--", *paths)
        log("Unity 가 바꾼 동적 글꼴 애셋을 되돌림: " + "; ".join(paths))
        dirty = []
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
    # 윈도에서 자식 파이썬은 cp949 로 찍으니 UTF-8 로 맞춰 받는다(2026-11-04 dry-run 에서 깨짐)
    child_env = {**os.environ, "PYTHONIOENCODING": "utf-8", "PYTHONUTF8": "1"}
    r = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", env=child_env)
    log("이미지 후처리: " + ((r.stdout or "").strip().splitlines() or ["(출력 없음)"])[-1])
    if r.returncode != 0:
        log("이미지 후처리 실패: " + (r.stderr or "").strip()[-500:])


PROMPT = """너는 혈우강호(BloodRain) Unity 프로젝트의 PC 담당 개발자다. 저장소 루트는 {root}, Unity 프로젝트는 unity/BloodRain 이다.
먼저 CLAUDE.md 와 docs/feedback.md 를 읽어라. 그다음 docs/unity_tasks.md 에서 위에서부터 안 끝난 항목(- [ ]) 1~3개를 40분 안에 처리한다. 다른 세션이 '하는 중'으로 표시한 항목은 건너뛴다.
unity_tasks.md 맨 위 '항목 읽는 법'을 따른다: '할 차례' 절을 위에서부터, 첫 줄 '먼저:' 의 항목이 [x] 가 아니면 건너뛰고, 밑줄(할 것·쓸 것·메뉴·끝·찍기)대로 한다. 지난 진행 기록은 맨 아래 '옛 줄 원문'의 같은 ID 줄에 있다.

규칙
- Unity 에디터 조작은 unityMCP 도구로 한다. 스크립트를 고친 뒤에는 refresh_unity 후 read_console 로 오류가 없는지 꼭 본다.
- 끝낸 항목은 - [x] 로 바꾸고 한 줄로 결과를 적는다. 막히면 - [ ] 그대로 두고 아래에 '막힘:' 한 줄을 적는다.
- 화면에 보이는 작업이면 플레이 모드 스크린샷을 docs/unity_shots/<항목 id>/ 에 남긴다.
- 씬·프리팹은 Assets/Editor 의 [MenuItem] 부트스트랩 코드로 만들고 실행한다(다시 만들 수 있게). YAML 을 손으로 쓰지 않는다.
- 끝나면 열린 씬과 애셋을 저장한다. docs/daily/{today}.md 에 "### HH:MM PC API 루프" 로 한 일을 덧붙인다.
- Assets/Scripts/Core 에는 UnityEngine 을 쓰지 않는다. 바꿨으면 `dotnet test core/BloodRain.sln` 을 돌려 통과를 확인한다.
- 커밋은 하되 푸시는 하지 마라(바깥 스크립트가 한다). 커밋 메시지는 한국어 한 줄.
- 일을 지어내지 말 것. 할 항목이 없으면 아무것도 바꾸지 말고 끝낸다.
"""


def agent_env(cfg: dict) -> dict:
    """에이전트가 쓸 환경변수. 회사 키(PC 환경변수)를 덮어쓴다.
    워크스페이스에 묶이지 않은 키는 워크스페이스 id 머리말이 있어야 요청이 받아들여진다(2026-11-04 확인)."""
    env = {"ANTHROPIC_API_KEY": cfg["api_key"], "CLAUDE_AGENT_SDK_CLIENT_APP": "bloodrain-pc-loop"}
    if cfg.get("workspace_id"):
        env["ANTHROPIC_CUSTOM_HEADERS"] = f"anthropic-workspace-id: {cfg['workspace_id']}"
    return env


async def run_agent(cfg: dict) -> None:
    from claude_agent_sdk import ClaudeAgentOptions, ResultMessage, query  # 필요할 때만 불러온다

    tail: list[str] = []

    def keep_stderr(line: str) -> None:
        tail.append(line.rstrip())
        del tail[:-20]

    options = ClaudeAgentOptions(
        stderr=keep_stderr,
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
        env=agent_env(cfg),
        **({"cli_path": cfg["cli_path"]} if cfg.get("cli_path") else {}),
    )
    prompt = PROMPT.format(root=ROOT, today=f"{dt.date.today():%Y-%m-%d}")
    try:
        await _consume(query(prompt=prompt, options=options), ResultMessage)
    except Exception as e:
        # 예산·턴 한도에 닿아도 SDK 가 예외로 끝낼 수 있다. 남긴 뒤 바깥에서 커밋·푸시는 계속한다
        log(f"에이전트가 예외로 끝남: {e!r}"[:1500])
        if tail:
            log("CLI 끝 줄: " + " | ".join(tail)[-1500:])


async def _consume(stream, ResultMessage) -> None:
    async for message in stream:
        if isinstance(message, ResultMessage):
            cost = getattr(message, "total_cost_usd", None)
            log(f"에이전트 종료: {message.subtype}, 비용 {cost if cost is not None else '알 수 없음'} USD")
            state = load_json(STATE, {})
            month = f"{dt.date.today():%Y-%m}"
            state.setdefault("spend", {})
            state["spend"][month] = round(state["spend"].get(month, 0) + (cost or 0), 4)
            STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


# 바깥 스크립트가 대신 커밋할 때 넣는 곳. 이 밖의 새 파일(로그 따위)은 커밋하지 않는다
COMMIT_PATHS = ["docs", "art_queue", "core", "tools", "web",
                "unity/BloodRain/Assets", "unity/BloodRain/Packages", "unity/BloodRain/ProjectSettings"]


def commit_and_push(dry: bool) -> None:
    if git("status", "--porcelain", "--", *COMMIT_PATHS):
        if dry:
            log("(dry) 바뀐 파일이 있지만 커밋하지 않음")
            return
        git("add", "-A", "--", *COMMIT_PATHS)
        # status 에는 보여도 올릴 게 없을 때가 있다(윈도 줄끝만 바뀐 파일 등). 그때 commit 은 실패하니 건너뛴다
        # (2026-11-04 23:53 실행이 여기서 죽어 푸시를 못 했고, 01:41 실행이 대신 올림)
        staged = subprocess.run(["git", "diff", "--cached", "--quiet"], cwd=ROOT).returncode != 0
        if staged:
            git("commit", "-m", "PC 루프: 이미지 반영·Unity 작업")
        else:
            log("status 엔 바뀐 게 보였지만 올릴 변경이 없어 커밋하지 않음")
    ahead = git("rev-list", "--count", "origin/claude/dev..HEAD", check=False) or "0"
    if ahead != "0" and not dry:
        # 클라우드 세션이 같이 올려서 자주 거절된다. pull --rebase 하고 다섯 번까지 다시
        for attempt in range(5):
            try:
                git("pull", "--rebase", "origin", "claude/dev")
                git("push", "origin", "HEAD:claude/dev")
                log(f"claude/dev 에 커밋 {ahead}개 푸시")
                return
            except RuntimeError as e:
                log(f"푸시 실패({attempt + 1}/5): {e}"[:800])
                git("rebase", "--abort", check=False)
                time.sleep(20)
        log("푸시를 못 했다. 커밋은 PC 저장소에 남아 있어 다음 실행이 다시 올린다")


def main() -> int:
    try:
        sys.stdout.reconfigure(errors="replace")  # 작업 스케줄러 콘솔 글꼴과 상관없이 죽지 않게
    except Exception:
        pass
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true", help="평일 낮에도 돈다(사람이 직접 돌릴 때만)")
    args = ap.parse_args()

    # 평일 06~19시는 회사 일 시간이다. 꺼져 있다 깨어난 PC 에서 밀린 예약이 낮에 돌지 않게 막는다
    now = dt.datetime.now()
    if now.weekday() < 5 and 6 <= now.hour < 19 and not (args.dry_run or args.force):
        log("평일 낮(06~19시)이라 건너뜀")
        return 0

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
    procs = unity_processes()
    others = [cmd for _, cmd in procs if not is_bloodrain(cmd)]
    ours = [pid for pid, cmd in procs if is_bloodrain(cmd)]
    launched = None
    if not tasks:
        log("Unity 작업 목록이 비어 있어 에이전트는 건너뜀")
    elif others:
        # 회사 일(다른 프로젝트 Unity, 배치 빌드 포함)이 떠 있으면 절대 건드리지 않는다
        log("다른 프로젝트 Unity 가 떠 있어 건너뜀")
    elif ours and idle_minutes() < 20:
        log("BloodRain Unity 를 사람이 쓰는 중일 수 있어(입력 20분 안) 건너뜀")
    elif args.dry_run:
        log(f"(dry) 에이전트를 돌릴 차례 — 남은 Unity 작업 {len(tasks)}개, Unity {'떠 있음' if ours else '꺼짐(켤 예정)'}")
    else:
        if not ours:
            launched = launch_unity()
        if ours or launched:
            state.setdefault("runs", {})[today] = runs + 1
            STATE.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
            try:
                asyncio.run(run_agent(cfg))
            finally:
                if launched:
                    close_unity(launched)

    commit_and_push(args.dry_run)
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except SystemExit:
        raise
    except BaseException:
        import traceback
        log("실행 중 오류: " + traceback.format_exc()[-2000:])
        sys.exit(1)
