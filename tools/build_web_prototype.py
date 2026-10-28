"""웹 프로토타입을 한 장짜리 HTML로 묶는다.

web/prototype/template.html 에 저장소 데이터 JSON과 인트로 컷 세 장을 넣어
web/prototype/index.html 을 만든다. 밸런스 숫자를 바꾸면 이 스크립트만 다시 돌리면 된다.

쓰는 법 (저장소 루트에서):  python tools/build_web_prototype.py
필요: pip install pillow
"""
from __future__ import annotations

import base64
import io
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "unity" / "BloodRain" / "Assets" / "Resources" / "Data"
CUTS = ROOT / "unity" / "BloodRain" / "Assets" / "Art" / "Cutscenes"
SRC = ROOT / "web" / "prototype" / "template.html"
OUT = ROOT / "web" / "prototype" / "index.html"

# 화면 이름 → (컷 파일, 긴 변 픽셀)
IMAGES = {
    "TITLE": ("sb_intro_12.jpg", 960),  # 객잔 앞에 선 진소운
    "OVER": ("sb_intro_11.jpg", 720),   # 강시에 둘러싸인 장면
    "WIN": ("sb_intro_03.jpg", 720),    # 칠성봉인
}


def load(name: str) -> dict:
    return json.loads((DATA / f"{name}.json").read_text(encoding="utf-8"))


def data_blob() -> str:
    enemies = load("enemies")
    blob = {
        "weapons": load("weapons")["weapons"],
        "passives": load("passives")["passives"],
        "evolutions": load("evolutions")["evolutions"],
        "enemies": enemies["enemies"],
        "elites": enemies.get("elites"),
        "bosses": enemies.get("bosses"),
        "characters": load("characters")["characters"],
        "ultimates": load("characters")["ultimates"],
        "balance": {k: v for k, v in load("balance").items() if not k.startswith("_")},
    }
    # </script> 가 끼어들 일은 없지만 혹시 몰라 막아 둔다
    return json.dumps(blob, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")


def image_uri(file: str, size: int) -> str:
    img = Image.open(CUTS / file).convert("RGB")
    img.thumbnail((size, size), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=78, optimize=True, progressive=True)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode("ascii")


def main() -> int:
    html = SRC.read_text(encoding="utf-8")
    if "/*__DATA__*/null" not in html:
        print("template.html 에 데이터 자리(/*__DATA__*/null)가 없다", file=sys.stderr)
        return 1
    html = html.replace("/*__DATA__*/null", data_blob(), 1)
    for key, (file, size) in IMAGES.items():
        mark = f"__IMG_{key}__"
        if mark not in html:
            print(f"template.html 에 {mark} 자리가 없다", file=sys.stderr)
            return 1
        html = html.replace(mark, image_uri(file, size), 1)
    OUT.write_text(html, encoding="utf-8")
    print(f"{OUT.relative_to(ROOT)} {OUT.stat().st_size / 1024:.0f}KB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
