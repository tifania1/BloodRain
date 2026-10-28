"""웹 프로토타입을 PC에서 바로 Cloudflare Pages에 올린다.

GitHub 저장소에 Cloudflare 비밀값이 아직 없을 때 쓰는 길이다. PC에 wrangler 로그인이 되어 있어야 한다
(`npx wrangler login` 한 번). 키를 파일에 적지 않고 wrangler가 가진 로그인을 그대로 쓴다.

쓰는 법 (저장소 루트에서):
  python tools/deploy_web_prototype.py            # 정식 주소(main)로
  python tools/deploy_web_prototype.py --branch claude/dev   # 미리보기 주소로
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROJECT = "bloodrain-proto"
WRANGLER = ["npx", "--yes", "wrangler@3"]


def run(args: list[str], check: bool = True) -> subprocess.CompletedProcess:
    print("$", " ".join(args))
    return subprocess.run(args, cwd=ROOT, check=check, shell=sys.platform == "win32")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--branch", default="main")
    args = ap.parse_args()

    run([sys.executable, str(ROOT / "tools" / "build_web_prototype.py")])
    # 프로젝트가 없으면 deploy 가 거부하므로 먼저 만들어 본다(이미 있으면 실패해도 그만)
    run(WRANGLER + ["pages", "project", "create", PROJECT, "--production-branch", "main"], check=False)
    run(WRANGLER + ["pages", "deploy", "web/prototype/site", "--project-name", PROJECT,
                    "--branch", args.branch, "--commit-dirty=true"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
