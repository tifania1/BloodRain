"""웹 프로토타입을 묶는다.

web/prototype/template.html 에 저장소 데이터 JSON과 인트로 컷 세 장을 넣어 두 가지를 만든다.

  web/prototype/site/        바깥 공개용(Cloudflare Pages). 완전한 HTML 문서 + 링크 미리보기 이미지
  web/prototype/.artifact/   claude.ai 아티팩트용 본문(문서 뼈대 없이). --artifact 를 줄 때만
  site/lookdev/              이펙트 시안(web/lookdev/*.html)을 공개용 문서로 감싼 것.
                             시안에 데이터 자리(/*__DATA__*/null)가 있으면 LOOKDEV_DATA 의 묶음을 넣는다
  site/lookdev/vfx/          이펙트 갤러리가 읽는 Art/VFX 텍스처(Brush·Common·AI) 사본
  site/lookdev/sfx/          효과음 듣기 페이지가 읽는 Assets/Audio/SFX 의 ogg·sfx.json 사본

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
VFX = ROOT / "unity" / "BloodRain" / "Assets" / "Art" / "VFX"
SFX = ROOT / "unity" / "BloodRain" / "Assets" / "Audio" / "SFX"

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
        "elitePatterns": drop_notes(load("bosspatterns")["elites"]),
        "characters": chars["characters"],
        "ultimates": chars["ultimates"],
        "insights": {k: v for k, v in load("insights").items() if not k.startswith("_")},
        "synergies": {k: v for k, v in load("synergies").items() if not k.startswith("_")},
        "balance": {k: v for k, v in load("balance").items() if not k.startswith("_")},
        # 서장(backlog M1-04~06): chapters.json prologue 시간표, cheonha.json prologueBody, 장삼 패턴, 서장 대사·안내
        "prologue": drop_notes(next(c for c in load("chapters")["chapters"] if c["id"] == "prologue")),
        "prologueBody": drop_notes(load("cheonha")["prologueBody"]),
        "jangSam": drop_notes(load("bosspatterns")["bosses"]["jang_sam"]),
        "prologueDialogue": drop_notes(load("dialogue")["prologue"]),
        "speakers": {x["id"]: x["nameKo"] for x in load("dialogue")["speakers"]},
        # 서장 끝 컷 그림: Art/Cutscenes/<art id>.jpg 가 있을 때만(아직이면 먹 바탕에 자막만)
        "cutImages": {c["art"]: image_uri(f"{c['art']}.jpg", 960) for c in load("dialogue")["prologue"]["cuts"]
                      if c.get("art") and (CUTS / f"{c['art']}.jpg").exists()},
        "ui": ui_strings(),
        # 천하 축척(WP-12): 화면 대각선 = view0Meters × scalePerRealm^(경지 순번 + viewInRealmShare × 경지 안 진행), 7장
        "world": {k: v for k, v in load("cheonha")["world"].items() if k in ("view0Meters", "scalePerRealm", "viewInRealmShare", "boundaryViewMultiple", "noBoundaryFrom", "widthMeters", "heightMeters", "land", "rivers")},
        # 적 한 덩이·밟힘·돌파 연출(WP-15, 7장): 경지별 덩이 크기, 밟힌 진기·쓸림, 연출 시각
        "clusterSize": {r["realm"]: r["clusterSize"] for r in load("cheonha")["realmScale"]},
        "crush": drop_notes(load("cheonha")["crush"]),
        "breakthrough": drop_notes(load("cheonha")["breakthrough"]),
        # 축척 시험(WP-12)이 1장 마당을 중원 한 장의 숭산 자리에 놓고 둘레 지명을 그린다
        "regions": [{"id": r["id"], "nameKo": r["nameKo"], "hanja": r["hanja"], "pos": r["pos"], "chapter": r.get("chapter"), "final": bool(r.get("final")), "gimmicks": r.get("gimmicks", []), "density": r["density"], "boss": r.get("boss"), "sealStone": r.get("sealStone"), "sect": r.get("sect")} for r in load("cheonha")["regions"]],
        # 땅의 주인(WP-13e, 7장): 깨어남·싸움 원·기혈 배율·처치 보상(cheonha.json bosses)과 패턴(bosspatterns.json bosses, 서장 장삼은 위 jangSam)
        "lordRules": drop_notes(load("cheonha")["bosses"]),
        # 땅의 주인 컷 줄(docs/story/boss_cuts.md): 프로토타입은 만남 첫 줄·2막 예고·처치 첫 줄·혈영 메아리만 알림으로
        "lordLines": {l["id"]: {"cuts": drop_notes(l.get("cuts", [])), "phase2Ko": l.get("phase2Ko"), "echoKo": l.get("echoKo")} for l in load("dialogue")["lords"]["list"]},
        "lordPatterns": {k: drop_notes(v) for k, v in load("bosspatterns")["bosses"].items() if not k.startswith("_") and k != "jang_sam"},
        # 종장(WP-13e, 6장 '종장 보스', Core FinalFight): 혈영 일곱 차례·혈영 기혈 비율, 봉마곡에 들어설 때 컷 줄
        "finalBoss": {k: drop_notes(load("bosspatterns")["final"][k]) for k in ("boss", "shadows", "shadowHealthRatio", "stages")},
        "finalStageLines": {st["id"]: st["textKo"] for st in load("dialogue")["final"]["stages"]},
        "finalEnter": [c["lines"][0]["textKo"] for c in load("dialogue")["final"]["enter"]["cuts"] if c.get("lines")],
        # 정산(WP-14c): ?life= 판이 끝나면 10장 윤회의 기 식(Core RebirthQi)으로 셈해 혼 기록에 새긴다
        "rebirthQi": {k: v for k, v in drop_notes(load("cheonha")["rebirthQi"]).items() if k != "targets"},
        # 한 생의 고유 적 비율(WP-13c): 축척 모드에서 다른 땅에 서면 그 땅 고유 적을 이 비율로 섞는다
        # 지역 기믹(WP-13b): 독안개·낙석·눈보라·붉은 달은 gimmicks.json 값 그대로(배치 간격이 필요 없는 것부터)
        "gimmicks": {k: drop_notes(v) for k, v in load("gimmicks").items() if k in ("poisonFog", "fallingRocks", "blizzard", "redMoon")},
        # 한 생 시간표(WP-14b): ?life= 판이 1장 시간표 대신 chapters.json life 의 bands·events 를 쓴다
        "lifePlan": {k: drop_notes(next(c for c in load("chapters")["chapters"] if c["id"] == "life")[k]) for k in ("bands", "events")},
        "lifeLocalShare": next(c for c in load("chapters")["chapters"] if c["id"] == "life")["localShare"],
        "eliteFallback": next(c for c in load("chapters")["chapters"] if c["id"] == "life")["eliteFallback"],
        # 한 생 시험(WP-14a): 주소 끝 ?life=<문파> 면 그 문파 몸(기혈·걸음·시작 무공·타고난 능력치)으로 그 문파 땅에서 한 생을 연다
        "sects": [{"id": s["id"], "nameKo": s["nameKo"], "region": s["region"], "startWeapon": s["startWeapon"], "maxHealth": s["maxHealth"], "moveSpeed": s["moveSpeed"],
                   "stats": (s.get("traitEffect") or {}).get("stats", {}), "unlock": s.get("unlock"), "leader": s.get("leader")} for s in load("cheonha")["sects"]],
        # 침방·연무장(WP-14d, 10·21장): 환생 후보 수·첫 환혼·환혼 강화, 타고난 성질. 연무장 값은 balance.json training(위 balance 묶음)
        "rebirth": {k: v for k, v in drop_notes(load("cheonha")["rebirth"]).items() if k in ("candidates", "firstRebirth", "firstRebirthTraits", "upgrades", "femaleRatio", "maleOnlySects")},
        # 침방 목 노인 줄·후보 카드 글(21장, docs/story/mok.md·sects.md). 고르는 규칙은 Core InnDialogue BedroomTalk 와 같게
        "bedroom": drop_notes(load("dialogue")["bedroom"]["lines"]),
        "cards": drop_notes(load("dialogue")["cards"]["list"]),
        # 대표 인물 마당 줄(21장, docs/story/leaders.md): 그 문파로 태어나면 한 줄, 여자 몸이면 sister 칸. Core InnDialogue.YardLine
        "leaders": [{k: l.get(k) for k in ("id", "sect", "yardUnknown", "yardKnown", "sister", "join", "inn")} for l in load("dialogue")["leaders"]["list"]],
        # 귀환 대사(WP-13e, Core InnDialogue.OnReturn): 목 노인·운낭 줄과 한 귀환에 나오는 마디 수·갈래 순서
        "mokReturn": drop_notes(load("dialogue")["mok"]["lines"]),
        "unnang": drop_notes(load("dialogue")["unnang"]["lines"]),
        "returns": drop_notes(load("dialogue")["returns"]),
        "birthTraits": drop_notes(load("cheonha")["birthTraits"]),
        "firstRebirth": [{"id": s["id"], "nameKo": s["nameKo"], "region": s["region"]} for s in load("cheonha")["sects"] if s["id"] in load("cheonha")["rebirth"]["firstRebirth"]],
    }
    return to_js(blob)


def ui_strings() -> dict:
    """화면 문구(WP-11): strings/ko.json 의 ui. 키. 프로토타입과 천하 시안이 같은 말을 쓰게 두 묶음에 다 넣는다.
    고칠 때는 ko.json(과 en·zh)만 고치면 두 화면이 같이 바뀐다. 쓰는 쪽은 각 파일의 uiT(키, 값...)."""
    s = json.loads((DATA / "strings" / "ko.json").read_text(encoding="utf-8"))
    return {k: v for k, v in s.items() if k.startswith("ui.")}


def to_js(blob) -> str:
    # </script> 가 끼어들 일은 없지만 혹시 몰라 막아 둔다
    return json.dumps(blob, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")


def drop_notes(v):
    """'_note' 처럼 밑줄로 시작하는 설명 키를 빼서 묶음을 줄인다."""
    if isinstance(v, dict):
        return {k: drop_notes(x) for k, x in v.items() if not k.startswith("_")}
    if isinstance(v, list):
        return [drop_notes(x) for x in v]
    return v


def cheonha_blob() -> str:
    """천하 시안(web/lookdev/cheonha.html)이 읽는 묶음. 문파·지역·성질·윤회의 기는 cheonha.json,
    이름은 characters·weapons·enemies, 경지 문턱·진기 곡선·연무장은 balance.json 에서 그대로 가져온다."""
    chars = load("characters")
    bal = load("balance")
    enemies = load("enemies")
    life = next(c for c in load("chapters")["chapters"] if c["id"] == "life")
    return to_js({
        "cheonha": drop_notes(load("cheonha")),
        "ui": ui_strings(),
        "characters": [{"id": c["id"], "nameKo": c["nameKo"]} for c in chars["characters"]],
        # 문파 오의(R-16): 공력 배율·효과 숫자(params)·한 줄 설명. 게이지는 balance.json ultimateCharge
        "ultimates": [{k: u[k] for k in ("id", "nameKo", "mightMultiplier", "summary", "params") if k in u} for u in chars["ultimates"]],
        "weapons": [{"id": w["id"], "nameKo": w["nameKo"]} for w in load("weapons")["weapons"]],
        "bosses": [
            {k: b[k] for k in ("id", "nameKo", "health", "targetKillSeconds", "phases") if k in b}
            for b in enemies["bosses"]
        ],
        "balance": drop_notes({k: bal[k] for k in ("realms", "xpCurve", "training", "stats", "spawnTarget", "dash", "limits", "yinyang", "drops", "ultimateCharge")}),
        # 땅의 주인 패턴 시간표(T-21). 시안이 그리는 모양은 cheonha.html PAT_SHAPES
        "bossPatterns": drop_notes(load("bosspatterns")["bosses"]),
        # 종장(R4-12): 혈영 일곱 차례·기혈 비율과 혈마 세 단계 시간표(Core FinalFight)
        "finalBoss": drop_notes(load("bosspatterns")["final"]),
        # 레벨업 카드(T-21): 심법 이름과 능력치 효과, 레벨 상한
        "passives": [{k: x[k] for k in ("id", "nameKo", "yinyang", "yinyangPerLevelOverride", "effects") if k in x} for x in load("passives")["passives"]],
        "levels": bal["levels"],
        # 돌파 깨달음(RM-07): 보통 일곱·마공 넷과 보여 주는 수. 효과 숫자는 params 그대로
        "insights": drop_notes(load("insights")),
        # 지역 콘텐츠(WP-01): 고유 적·정예 정의와 정예 패턴, 장별 정예 표, 지형 기믹 수치, 한 생의 고유 적 비율·정예 대신 쓸 땅
        "regionFoes": [drop_notes(e) for e in enemies["enemies"] if e.get("chapter") or e.get("family") == "elite"],
        "elitesByChapter": enemies["elites"],
        "elitePatterns": drop_notes(load("bosspatterns")["elites"]),
        "gimmicks": drop_notes(load("gimmicks")),
        "life": dict(drop_notes({k: life[k] for k in ("localShare", "eliteFallback")}),
                     elites=[{"at": e["at"], "nth": e["nth"]} for e in life["events"] if e.get("kind") == "elite"],
                     # 한 생 사건(RM-14): 혈인 돌진·지역 무리·포위. 금두꺼비·보부상·은빛 등롱은 은자가 시안에 없어 뺀다
                     events=[{k: e[k] for k in ("at", "kind", "enemy", "count", "local") if k in e}
                             for e in life["events"] if e.get("kind") in ("charge", "group", "ring")]),
        # 한 생 사건에 나오는 공통 적(혈인·혈강시·동갑시) 정의
        "eventFoes": [drop_notes(e) for e in enemies["enemies"]
                      if e["id"] in {ev.get("enemy") for ev in life["events"] if ev.get("kind") in ("charge", "group", "ring")}],
    })


# 시안 파일 이름(확장자 빼고) → 데이터 묶음
def sfx_blob() -> str:
    """효과음 듣기 페이지(web/lookdev/sfx_listen.html)가 읽는 묶음: sfx_queue 의 줄(문구 빼고)."""
    rows = [json.loads(l) for l in (ROOT / "sfx_queue" / "queue.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    keep = ("id", "use", "category", "route", "candidates", "status", "picked", "note")
    return json.dumps([{k: r[k] for k in keep if k in r} for r in rows], ensure_ascii=False)


LOOKDEV_DATA = {"cheonha": cheonha_blob, "sfx_listen": sfx_blob}


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
        if "/*__DATA__*/null" in text:
            if src.stem not in LOOKDEV_DATA:
                sys.exit(f"{src.name} 에 데이터 자리가 있는데 LOOKDEV_DATA 에 묶음이 없다")
            text = text.replace("/*__DATA__*/null", LOOKDEV_DATA[src.stem](), 1)
        m = re.search(r"<title>(.*?)</title>", text)
        title = m.group(1) if m else src.stem
        out = SITE / "lookdev" / src.name
        out.parent.mkdir(exist_ok=True)
        out.write_text(site_document(text, args.site_url, title, f"혈우강호 {title}", f"lookdev/{src.name}"), encoding="utf-8")
        print(f"{out.relative_to(ROOT)}")

    # 이펙트 갤러리(lookdev/vfx_gallery.html)가 읽는 텍스처: Art/VFX 의 Brush·Common·AI 폴더 png·json 을 그대로(.meta 빼고)
    vfx_out = SITE / "lookdev" / "vfx"
    shutil.rmtree(vfx_out, ignore_errors=True)
    n = 0
    for sub in ("Brush", "Common", "AI"):
        for f in sorted((VFX / sub).glob("*")):
            if f.suffix in (".png", ".json"):
                (vfx_out / sub).mkdir(parents=True, exist_ok=True)
                shutil.copyfile(f, vfx_out / sub / f.name)
                n += 1
    print(f"{vfx_out.relative_to(ROOT)} 텍스처·목록 {n}개")

    # 효과음 듣기 페이지(lookdev/sfx_listen.html)가 읽는 소리: Assets/Audio/SFX 의 ogg·sfx.json 그대로(.meta 빼고)
    sfx_out = SITE / "lookdev" / "sfx"
    shutil.rmtree(sfx_out, ignore_errors=True)
    n = 0
    for f in sorted(SFX.glob("*")) if SFX.is_dir() else []:
        if f.suffix in (".ogg", ".json"):
            sfx_out.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(f, sfx_out / f.name)
            n += 1
    print(f"{sfx_out.relative_to(ROOT)} 효과음·목록 {n}개")

    if args.artifact:
        ARTIFACT.mkdir(parents=True, exist_ok=True)
        (ARTIFACT / "index.html").write_text(page, encoding="utf-8")
        print(f"{(ARTIFACT / 'index.html').relative_to(ROOT)} (아티팩트용)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
