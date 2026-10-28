"""웹 프로토타입을 묶는다.

web/prototype/template.html 에 저장소 데이터 JSON과 인트로 컷 세 장을 넣어 두 가지를 만든다.

  web/prototype/site/        바깥 공개용(Cloudflare Pages). 완전한 HTML 문서 + 링크 미리보기 이미지
  web/prototype/.artifact/   claude.ai 아티팩트용 본문(문서 뼈대 없이). --artifact 를 줄 때만
  site/lookdev/              이펙트 시안(web/lookdev/*.html)을 공개용 문서로 감싼 것

둘 다 빌드 결과라 커밋하지 않는다(.gitignore). 밸런스 숫자를 바꾸면 이 스크립트만 다시 돌리면 된다.
CI(.github/workflows/web-prototype.yml)가 같은 스크립트로 묶어 배포한다.

쓰는 법 (저장소 루트에서):
  python tools/build_web_prototype.py                 # site/ 만
  python tools/build_web_prototype.py --artifact      # site/ + .artifact/
  python tools/build_web_prototype.py --site-url https://예시.pages.dev
필요: pip install pillow
"""
from __future__ import annotations

import argparse
import base64
import html
import io
import json
import re
import shutil
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "unity" / "BloodRain" / "Assets" / "Resources" / "Data"
CUTS = ROOT / "unity" / "BloodRain" / "Assets" / "Art" / "Cutscenes"
PROTO = ROOT / "web" / "prototype"
SRC = PROTO / "template.html"
SITE = PROTO / "site"
ARTIFACT = PROTO / ".artifact"
LOOKDEV = ROOT / "web" / "lookdev"

SITE_URL = "https://bloodrain-proto.pages.dev"
TITLE = "혈우강호 프로토타입"
DESC = "혈마의 봉인이 깨지고 피의 비가 내린다. 강시 떼를 버티고 20분째 혈나한 원각을 쓰러뜨리는 무협 서바이벌. 휴대폰·PC 브라우저에서 바로 한다."

# 화면 이름 → (컷 파일, 긴 변 픽셀)
IMAGES = {
    "TITLE": ("sb_intro_12.jpg", 960),  # 객잔 앞에 선 진소운
    "OVER": ("sb_intro_11.jpg", 720),   # 강시에 둘러싸인 장면
    "WIN": ("sb_intro_03.jpg", 720),    # 칠성봉인
}
OG_IMAGE = "sb_intro_12.jpg"

# 아티팩트 뼈대가 해 주던 기본값을 공개용 문서에 직접 넣는다
RESET_CSS = (
    ":root{color-scheme:light}"
    "body{margin:0;font:14px system-ui,-apple-system,'Apple SD Gothic Neo','Malgun Gothic',sans-serif}"
    "img{max-width:100%}[hidden]{display:none!important}"
)
FAVICON = (
    "data:image/svg+xml,"
    "%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'%3E"
    "%3Crect width='64' height='64' rx='14' fill='%23EFE6D2'/%3E"
    "%3Ccircle cx='32' cy='32' r='22' fill='none' stroke='%231A1A1A' stroke-width='5'/%3E"
    "%3Ccircle cx='32' cy='32' r='9' fill='%23B3261E'/%3E%3C/svg%3E"
)


def load(name: str) -> dict:
    return json.loads((DATA / f"{name}.json").read_text(encoding="utf-8"))


def data_blob() -> str:
    enemies = load("enemies")
    chars = load("characters")
    blob = {
        "weapons": load("weapons")["weapons"],
        "passives": load("passives")["passives"],
        "evolutions": load("evolutions")["evolutions"],
        "enemies": enemies["enemies"],
        "elites": enemies.get("elites"),
        "bosses": enemies.get("bosses"),
        "characters": chars["characters"],
        "ultimates": chars["ultimates"],
        "synergies": {k: v for k, v in load("synergies").items() if not k.startswith("_")},
        "balance": {k: v for k, v in load("balance").items() if not k.startswith("_")},
    }
    # </script> 가 끼어들 일은 없지만 혹시 몰라 막아 둔다
    return json.dumps(blob, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")


def jpeg_bytes(img: Image.Image, quality: int = 78) -> bytes:
    buf = io.BytesIO()
    img.convert("RGB").save(buf, "JPEG", quality=quality, optimize=True, progressive=True)
    return buf.getvalue()


def image_uri(file: str, size: int) -> str:
    img = Image.open(CUTS / file)
    img.thumbnail((size, size), Image.LANCZOS)
    return "data:image/jpeg;base64," + base64.b64encode(jpeg_bytes(img)).decode("ascii")


def og_image() -> bytes:
    """링크 미리보기용 1200x630. 가운데를 잘라 쓴다."""
    img = Image.open(CUTS / OG_IMAGE).convert("RGB")
    w, h = img.size
    target = 1200 / 630
    if w / h > target:
        nw = int(h * target)
        img = img.crop(((w - nw) // 2, 0, (w - nw) // 2 + nw, h))
    else:
        nh = int(w / target)
        img = img.crop((0, (h - nh) // 2, w, (h - nh) // 2 + nh))
    return jpeg_bytes(img.resize((1200, 630), Image.LANCZOS), quality=82)


def body() -> str:
    """template.html 에 데이터와 이미지를 채운 아티팩트용 본문."""
    text = SRC.read_text(encoding="utf-8")
    if "/*__DATA__*/null" not in text:
        sys.exit("template.html 에 데이터 자리(/*__DATA__*/null)가 없다")
    text = text.replace("/*__DATA__*/null", data_blob(), 1)
    for key, (file, size) in IMAGES.items():
        mark = f"__IMG_{key}__"
        if mark not in text:
            sys.exit(f"template.html 에 {mark} 자리가 없다")
        text = text.replace(mark, image_uri(file, size), 1)
    return text


def site_document(page: str, site_url: str, title: str = TITLE, desc: str = DESC, path: str = "") -> str:
    """아티팩트 본문을 바깥 공개용 완전한 HTML 문서로 감싼다.
    본문 맨 앞의 <title>·<link>·<style> 까지를 head로 옮기고 나머지를 body에 둔다."""
    end = page.find("</style>")
    if end < 0:
        sys.exit("페이지 머리에 <style> 이 없다")
    end += len("</style>")
    head_part, body_part = page[:end], page[end:]
    # 아티팩트 전용 줄(제목, claude: 메타)은 빼고 새로 쓴다
    head_lines = [
        line for line in head_part.splitlines()
        if not line.lstrip().startswith(("<title>", '<meta name="claude:'))
    ]
    t, d, u = html.escape(title), html.escape(desc), site_url.rstrip("/")
    meta = f"""<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover, user-scalable=no">
<title>{t}</title>
<meta name="description" content="{d}">
<meta name="theme-color" content="#EFE6D2">
<meta name="mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-capable" content="yes">
<meta name="apple-mobile-web-app-status-bar-style" content="default">
<meta name="apple-mobile-web-app-title" content="혈우강호">
<link rel="icon" href="{FAVICON}">
<meta property="og:type" content="website">
<meta property="og:site_name" content="혈우강호">
<meta property="og:title" content="{t}">
<meta property="og:description" content="{d}">
<meta property="og:url" content="{u}/{path}">
<meta property="og:image" content="{u}/og.jpg">
<meta property="og:image:width" content="1200">
<meta property="og:image:height" content="630">
<meta name="twitter:card" content="summary_large_image">
<style>{RESET_CSS}</style>"""
    return (
        "<!doctype html>\n<html lang=\"ko\">\n<head>\n" + meta + "\n"
        + "\n".join(head_lines).strip() + "\n</head>\n<body>\n" + body_part.strip() + "\n</body>\n</html>\n"
    )


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--artifact", action="store_true", help="claude.ai 아티팩트용 본문도 만든다")
    ap.add_argument("--site-url", default=SITE_URL, help="링크 미리보기에 쓸 공개 주소")
    args = ap.parse_args()

    page = body()
    if SITE.exists():
        shutil.rmtree(SITE)
    SITE.mkdir(parents=True)
    (SITE / "index.html").write_text(site_document(page, args.site_url), encoding="utf-8")
    (SITE / "og.jpg").write_bytes(og_image())
    (SITE / "_headers").write_text(
        "/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: strict-origin-when-cross-origin\n"
        "/og.jpg\n  Cache-Control: public, max-age=86400\n",
        encoding="utf-8",
    )
    size = (SITE / "index.html").stat().st_size / 1024
    print(f"{(SITE / 'index.html').relative_to(ROOT)} {size:.0f}KB, og.jpg, _headers")

    # 이펙트 시안(web/lookdev/*.html)도 같은 사이트의 /lookdev/ 아래에 올린다
    for src in sorted(LOOKDEV.glob("*.html")):
        text = src.read_text(encoding="utf-8")
        m = re.search(r"<title>(.*?)</title>", text)
        title = m.group(1) if m else src.stem
        out = SITE / "lookdev" / src.name
        out.parent.mkdir(exist_ok=True)
        out.write_text(site_document(text, args.site_url, title, f"혈우강호 {title}", f"lookdev/{src.name}"), encoding="utf-8")
        print(f"{out.relative_to(ROOT)}")

    if args.artifact:
        ARTIFACT.mkdir(parents=True, exist_ok=True)
        (ARTIFACT / "index.html").write_text(page, encoding="utf-8")
        print(f"{(ARTIFACT / 'index.html').relative_to(ROOT)} (아티팩트용)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
