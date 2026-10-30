"""받은 이미지를 게임용으로 다듬어 Unity Assets/Art 로 옮긴다.

art_queue/queue.jsonl 에서 status 가 picked 인 줄을 찾아, save_as 경로의 원본을
  1) 단색 배경(#F2EBDD) 제거 (스프라이트·아이콘만)
  2) 여백 자르고 정사각 패딩
  3) 규격 크기로 줄이기
  4) 바닥 타일은 좌우·위아래 이음새를 크로스페이드로 지움
한 뒤 unity/BloodRain/Assets/Art/<종류>/ 에 저장하고 status 를 imported 로 바꾼다.

쓰는 법 (저장소 루트에서):  python tools/postprocess_art.py [--dry-run] [--only <id>]
필요: pip install pillow
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
QUEUE = ROOT / "art_queue" / "queue.jsonl"
ART = ROOT / "unity" / "BloodRain" / "Assets" / "Art"

BG = (0xF2, 0xEB, 0xDD)

# 종류 → (출력 폴더, 출력 크기(긴 변), 배경 제거 여부)
KINDS = {
    "character": ("Characters", 256, True),
    "character_sheet": ("Characters/Sheets", 1024, False),
    "enemy": ("Enemies", 128, True),
    "elite": ("Enemies", 256, True),
    "boss": ("Bosses", 512, True),
    "icon": ("Icons", 128, False),
    "tile": ("Tiles", 512, False),
    "storyboard": ("Cutscenes", 1536, False),
    "map": ("Map", 2560, False),
    "portrait": ("Portraits", 1024, True),
    "card": ("Cards", 1536, False),
    "background": ("Backgrounds", 1536, False),
    "props": ("Props", 2048, True),
}


def remove_bg(img: Image.Image, tol: int = 28) -> Image.Image:
    """배경색과 가까운 픽셀을 투명하게. 가장자리는 부드럽게."""
    img = img.convert("RGBA")
    px = img.load()
    w, h = img.size
    mask = Image.new("L", (w, h), 255)
    mp = mask.load()
    for y in range(h):
        for x in range(w):
            r, g, b, _ = px[x, y]
            d = max(abs(r - BG[0]), abs(g - BG[1]), abs(b - BG[2]))
            if d <= tol:
                mp[x, y] = 0
            elif d <= tol * 2:
                mp[x, y] = int(255 * (d - tol) / tol)
    mask = mask.filter(ImageFilter.GaussianBlur(0.6))
    img.putalpha(mask)
    return img


def trim_and_square(img: Image.Image, margin: float = 0.06) -> Image.Image:
    bbox = img.getchannel("A").getbbox() if img.mode == "RGBA" else None
    if bbox:
        img = img.crop(bbox)
    side = int(max(img.size) * (1 + margin * 2))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(img, ((side - img.width) // 2, (side - img.height) // 2), img if img.mode == "RGBA" else None)
    return canvas


def make_seamless(img: Image.Image, band_ratio: float = 0.0625) -> Image.Image:
    """오른쪽 띠를 왼쪽에, 아래 띠를 위에 크로스페이드로 겹쳐 이음새를 없앤다(PlanetRacer A-01 방식)."""
    img = img.convert("RGB")
    w, h = img.size
    band = max(8, int(w * band_ratio))
    out = img.crop((0, 0, w - band, h))
    right = img.crop((w - band, 0, w, h))
    for i in range(band):
        a = i / band
        col_l = out.crop((i, 0, i + 1, h))
        col_r = right.crop((i, 0, i + 1, h))
        out.paste(Image.blend(col_r, col_l, a), (i, 0))
    img = out
    w, h = img.size
    band = max(8, int(h * band_ratio))
    out = img.crop((0, 0, w, h - band))
    bottom = img.crop((0, h - band, w, h))
    for i in range(band):
        a = i / band
        row_t = out.crop((0, i, w, i + 1))
        row_b = bottom.crop((0, i, w, i + 1))
        out.paste(Image.blend(row_b, row_t, a), (0, i))
    return out


def process(row: dict, dry: bool) -> Path | None:
    kind = row["kind"]
    if kind not in KINDS:
        print(f"  건너뜀 {row['id']}: 모르는 종류 {kind}")
        return None
    folder, size, cut_bg = KINDS[kind]
    src = ROOT / row["save_as"]
    if not src.exists():
        print(f"  없음 {row['id']}: {src}")
        return None
    img = Image.open(src)
    if cut_bg:
        img = trim_and_square(remove_bg(img))
    if kind == "tile":
        img = make_seamless(img)
    img.thumbnail((size, size), Image.LANCZOS)
    ext = ".jpg" if kind in ("storyboard", "map", "card", "background") else ".png"
    dst = ART / folder / (row["id"] + ext)
    if dry:
        print(f"  (dry) {row['id']} → {dst.relative_to(ROOT)} {img.size}")
        return dst
    dst.parent.mkdir(parents=True, exist_ok=True)
    if ext == ".jpg":
        img.convert("RGB").save(dst, quality=88)
    else:
        img.save(dst, optimize=True)
    print(f"  완료 {row['id']} → {dst.relative_to(ROOT)} {img.size}")
    return dst


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--only")
    args = ap.parse_args()

    rows = [json.loads(l) for l in QUEUE.read_text(encoding="utf-8").splitlines() if l.strip()]
    done = 0
    for row in rows:
        if args.only and row["id"] != args.only:
            continue
        if row.get("status") != "picked":
            continue
        if process(row, args.dry_run) and not args.dry_run:
            row["status"] = "imported"
            done += 1
    if not args.dry_run:
        QUEUE.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
    print(f"{done}개 반영")
    return 0


if __name__ == "__main__":
    sys.exit(main())
