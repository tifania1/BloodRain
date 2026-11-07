"""받은 이미지를 게임용으로 다듬어 Unity Assets/Art 로 옮긴다.

art_queue/queue.jsonl 에서 status 가 picked 인 줄을 찾아, save_as 경로의 원본을
  1) 단색 배경(#F2EBDD) 제거 (스프라이트·초상·소품·UI)
     그림 가장자리에서 이어진 배경만 지운다. 스프라이트는 안쪽에 갇힌 배경색 그대로인 넓은 덩이(팔 사이 등)도 지우고,
     UI 바탕(ui)은 틀 안쪽 종이를 남긴다. 예전 remove_bg(배경색과 비슷하면 어디든 지움)는 얼굴·흰 옷을 뚫어 안 쓴다
  2) 여백 자르고 정사각 패딩 (ui 는 정사각으로 만들지 않고 여백만 자름)
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

from PIL import Image, ImageChops, ImageDraw, ImageFilter

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
    "ui": ("UI", 1024, True),
    "store": ("Store", 1536, False),  # 스토어 시안. 사람이 Steam 규격으로 잘라 손본다(art_list.md)
    "vfx_ink": ("VFX/AI", 512, False),
    "vfx_glow": ("VFX/AI", 512, False),
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


def remove_bg_edges(img: Image.Image, tol: int = 28) -> Image.Image:
    """UI 바탕용. 배경색 가운데 그림 가장자리에서 이어진 곳만 투명하게 한다.
    카드 틀·족자처럼 먹 테두리 안쪽 종이가 배경색과 비슷해도 테두리에 막혀 남는다.
    속을 비울 틀은 Unity Image(Sliced)의 Fill Center 를 끈다."""
    img = img.convert("RGBA")
    rgb = img.convert("RGB")
    r, g, b = ImageChops.difference(rgb, Image.new("RGB", rgb.size, BG)).split()
    d = ImageChops.lighter(ImageChops.lighter(r, g), b)  # 채널별 차이 가운데 큰 값
    near = d.point(lambda v: 255 if v <= tol * 2 else 0)
    w, h = near.size
    px = near.load()
    seeds = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    for xy in seeds:
        if px[xy] == 255:
            ImageDraw.floodfill(near, xy, 128)
    outside = near.point(lambda v: 255 if v == 128 else 0)
    ramp = d.point(lambda v: 0 if v <= tol else (255 if v > tol * 2 else int(255 * (v - tol) / tol)))
    alpha = ImageChops.lighter(ramp, ImageChops.invert(outside))  # 바깥은 색 차이만큼, 안쪽은 불투명
    img.putalpha(alpha.filter(ImageFilter.GaussianBlur(0.6)))
    return img


POCKET_MIN = 0.002  # 안쪽 빈 종이(팔과 몸 사이·수레바퀴 살 사이)로 볼 최소 넓이(그림 넓이 비율)


def remove_bg_sprite(img: Image.Image, tol: int = 28) -> Image.Image:
    """스프라이트·초상·소품용(2026-11-08 클라우드, backlog PR-02).
    remove_bg 는 배경색과 비슷한 픽셀을 그림 안쪽까지 지워 얼굴·흰 옷·만두 속·등롱 종이가 비쳤다(초상 셋 넓이의 27~34%).
    그래서 가장자리에서 이어진 배경만 지우고(remove_bg_edges), 안쪽에 갇힌 곳은 '배경색 그대로인 넓은 덩이'만 지운다.
    그림 속 밝은 곳은 붓 결이 있어 배경색 tol/2 안에 넓게 고르지 않다는 것을 기댄 것이다."""
    out = remove_bg_edges(img, tol)
    rgb = img.convert("RGB")
    r, g, b = ImageChops.difference(rgb, Image.new("RGB", rgb.size, BG)).split()
    d = ImageChops.lighter(ImageChops.lighter(r, g), b)
    a = out.getchannel("A")
    # 안쪽(알파가 남은 곳)이면서 배경색에 아주 가까운 픽셀
    flat = ImageChops.multiply(d.point(lambda v: 255 if v <= tol // 2 else 0), a.point(lambda v: 255 if v >= 250 else 0))
    w, h = flat.size
    need = max(16, int(w * h * POCKET_MIN))
    cut = Image.new("L", flat.size, 0)
    # 씨앗은 5px 로 깎아도 남는 곳에서만 고른다 — 붓 결 사이 가는 틈·점은 여기서 빠져 반복이 적다
    seeds = flat.filter(ImageFilter.MinFilter(5))
    while True:
        box = seeds.getbbox()
        if not box:
            break
        sp = seeds.load()
        seed = next((x, y) for y in range(box[1], box[3]) for x in range(box[0], box[2]) if sp[x, y] == 255)
        if flat.getpixel(seed) != 255:
            seeds.putpixel(seed, 0)
            continue
        ImageDraw.floodfill(flat, seed, 128)
        one = flat.point(lambda v: 255 if v == 128 else 0)
        if one.histogram()[255] >= need:
            cut = ImageChops.lighter(cut, one)
        flat = ImageChops.subtract(flat, flat.point(lambda v: 127 if v == 128 else 0))  # 128 → 1(다시 안 봄)
        seeds = ImageChops.subtract(seeds, one)
    if cut.getbbox():
        a = ImageChops.subtract(a, cut.filter(ImageFilter.GaussianBlur(0.6)))
        out.putalpha(a)
    return out


def trim(img: Image.Image, margin: float = 0.02) -> Image.Image:
    """알파가 있는 곳만 남기고 가장자리 여백을 조금 둔다. 비율은 그대로."""
    bbox = img.getchannel("A").getbbox()
    if not bbox:
        return img
    img = img.crop(bbox)
    m = int(max(img.size) * margin)
    canvas = Image.new("RGBA", (img.width + m * 2, img.height + m * 2), (0, 0, 0, 0))
    canvas.paste(img, (m, m), img)
    return canvas


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


def redo_bg(row: dict, dry: bool, from_assets: bool) -> Path | None:
    """이미 들인 스프라이트의 배경 제거를 remove_bg_sprite 로 다시 한다(backlog PR-02).
    원본(save_as)이 있으면 process 와 똑같이 다시 만들고, 없고 from_assets 면 지금 Assets 그림을 배경색 위에 얹어 다시 지운다
    (예전 remove_bg 가 지운 자리는 색이 안 남아 배경색 종이로 채워진다 — 원본으로 하는 편이 낫다)."""
    kind = row["kind"]
    if kind not in KINDS or kind == "ui" or kind.startswith("vfx_") or not KINDS[kind][2]:
        return None
    if (ROOT / row["save_as"]).exists():
        return process(row, dry)
    folder, size, _ = KINDS[kind]
    dst = ART / folder / (row["id"] + ".png")
    if not from_assets or not dst.exists():
        print(f"  없음 {row['id']}: 원본도 없고 --from-assets 도 아님")
        return None
    cur = Image.open(dst).convert("RGBA")
    flat = Image.new("RGBA", cur.size, BG + (255,))
    flat.alpha_composite(cur)
    img = remove_bg_sprite(flat.convert("RGB"))
    if kind != "props":  # 소품 시트는 split_props.py 가 칸을 찾으니 크기·자리를 그대로
        img = trim_and_square(img)
        img.thumbnail((size, size), Image.LANCZOS)
    if dry:
        print(f"  (dry) {row['id']} → {dst.relative_to(ROOT)} {img.size} (Assets 에서)")
        return dst
    img.save(dst, optimize=True)
    print(f"  다시 {row['id']} → {dst.relative_to(ROOT)} {img.size} (Assets 에서)")
    return dst


def process(row: dict, dry: bool) -> Path | None:
    kind = row["kind"]
    if kind not in KINDS:
        print(f"  건너뜀 {row['id']}: 모르는 종류 {kind}")
        return None
    if kind.startswith("vfx_"):
        print(f"  건너뜀 {row['id']}: 이펙트 재료는 tools/import_vfx_ai.py 가 맡는다")
        return None
    folder, size, cut_bg = KINDS[kind]
    src = ROOT / row["save_as"]
    if not src.exists():
        print(f"  없음 {row['id']}: {src}")
        return None
    img = Image.open(src)
    if kind == "ui":
        img = trim(remove_bg_edges(img))
    elif cut_bg:
        img = trim_and_square(remove_bg_sprite(img))
    if kind == "tile":
        img = make_seamless(img)
    img.thumbnail((size, size), Image.LANCZOS)
    ext = ".jpg" if kind in ("storyboard", "map", "card", "background", "store") else ".png"
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
    ap.add_argument("--redo-bg", action="store_true", help="imported 스프라이트의 배경 제거를 다시(PR-02)")
    ap.add_argument("--from-assets", action="store_true", help="--redo-bg 에서 원본이 없으면 Assets 그림으로")
    args = ap.parse_args()

    rows = [json.loads(l) for l in QUEUE.read_text(encoding="utf-8").splitlines() if l.strip()]
    if args.redo_bg:
        n = sum(1 for row in rows
                if row.get("status") == "imported" and (not args.only or row["id"] == args.only)
                and redo_bg(row, args.dry_run, args.from_assets))
        print(f"{n}개 다시 지움")
        return 0
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
    if not args.dry_run:
        # Unity 가 이미 meta 를 만들어 둔 그림은 Sprite 로 맞춘다. 새 그림은 Unity 가 meta 를 만든 뒤 한 번 더 돌린다.
        import fix_art_meta
        fix_art_meta.main()
    return 0


if __name__ == "__main__":
    sys.exit(main())
