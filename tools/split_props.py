"""소품 시트를 한 장씩 잘라 낱장 Sprite 로 둔다(backlog PR-01, 2026-11-08 클라우드).

밤 이미지 세션이 들여온 `Assets/Art/Props/props_*.png` 는 소품 대여섯 개가 한 장에 든 시트다.
Unity 에 그대로 꽂으면 한 덩이로 보이니, 알파로 덩이를 찾아 낱장 `Assets/Art/Props/Parts/<이름>.png` 로 자른다.
이름은 queue.jsonl 그 줄 프롬프트에 적은 차례(위 줄부터, 줄 안에서는 왼쪽부터)와 같다.
이름이 데이터 id 와 같으면 그 id 를 쓴다(balance.json drops.lantern 의 kind 등).

  python tools/split_props.py           # 자르고 없는 meta 는 prop_bell.png.meta 를 본떠 새 guid 로 만든다
  python tools/split_props.py --check   # 시트 덩이 수가 이름 수와 안 맞거나 낱장이 빠졌으면 1 로 끝낸다

크기는 줄이지 않는다(덩이 둘레 여백 PAD 만 남김). 시트에 이미 난 속 구멍(만두 속·등롱 종이처럼 배경색과 비슷한 곳을
postprocess_art.py remove_bg 가 지운 것)은 여기서 못 메운다 — 지운 자리의 색이 남아 있지 않다. backlog PR-02. 세상에서 몇 m 로 그릴지는 Unity 쪽(unity_tasks PR-01U)이 정한다.
"""
from __future__ import annotations

import re
import sys
import uuid
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PROPS = ROOT / "unity" / "BloodRain" / "Assets" / "Art" / "Props"
OUT = PROPS / "Parts"
TEMPLATE = PROPS / "prop_bell.png.meta"

ALPHA_MIN = 40      # 이보다 옅은 픽셀은 덩이로 안 본다(가장자리 번짐)
JOIN = 12           # 이 픽셀 안에 붙은 조각은 한 덩이(먹 튐·끈 끝이 떨어져 있어도)
MIN_AREA = 2500     # 이보다 작은 덩이(테두리 너비 x 높이)는 버림
PAD = 8


# 시트 → 프롬프트 차례대로 낱장 이름
SHEETS: dict[str, list[str]] = {
    "props_pickups": ["bun", "duck", "silver_bag", "blood_crystal", "seal_stone_shard"],
    "props_items": ["chwigi_talisman", "jeongsin_talisman", "thunder_bomb", "chest_closed", "chest_open"],
    "props_breakables": ["lantern", "lantern_silver", "cart", "storage_jar", "wine_jar", "sword_rack"],
}


def _dilate(mask: list[bytearray], w: int, h: int, r: int) -> list[bytearray]:
    """정사각 r 만큼 넓힌다(가로 한 번, 세로 한 번)."""
    row = []
    for y in range(h):
        src = mask[y]
        out = bytearray(w)
        last = -10**9
        for x in range(w):
            if src[x]:
                last = x
            if x - last <= r:
                out[x] = 1
        last = 10**9
        for x in range(w - 1, -1, -1):
            if src[x]:
                last = x
            if last - x <= r:
                out[x] = 1
        row.append(out)
    col = [bytearray(w) for _ in range(h)]
    for x in range(w):
        last = -10**9
        for y in range(h):
            if row[y][x]:
                last = y
            if y - last <= r:
                col[y][x] = 1
        last = 10**9
        for y in range(h - 1, -1, -1):
            if row[y][x]:
                last = y
            if last - y <= r:
                col[y][x] = 1
    return col


def find_parts(img: Image.Image) -> list[tuple[tuple[int, int, int, int], Image.Image]]:
    """덩이마다 ((왼, 위, 오른, 아래), 그 덩이만 255 인 시트 크기 마스크) — 읽는 차례(줄 → 왼쪽부터).
    칸이 서로 겹쳐도 마스크로 거르니 옆 덩이 끝이 묻어오지 않는다."""
    a = img.getchannel("A")
    # 4배 줄여서 찾고 다시 키운다(2048 시트도 금방)
    s = 4
    sw, sh = max(1, a.width // s), max(1, a.height // s)
    small = a.resize((sw, sh), Image.BOX)
    px = small.load()
    mask = [bytearray(1 if px[x, y] >= ALPHA_MIN // 2 else 0 for x in range(sw)) for y in range(sh)]
    grown = _dilate(mask, sw, sh, max(1, JOIN // s))
    seen = [bytearray(sw) for _ in range(sh)]
    label = Image.new("L", (sw, sh), 0)
    lp = label.load()
    boxes = []
    n = 0
    for y0 in range(sh):
        for x0 in range(sw):
            if not grown[y0][x0] or seen[y0][x0]:
                continue
            n += 1
            stack = [(x0, y0)]
            seen[y0][x0] = 1
            l, t, r, b = x0, y0, x0, y0
            while stack:
                x, y = stack.pop()
                lp[x, y] = n
                l, t, r, b = min(l, x), min(t, y), max(r, x), max(b, y)
                for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if 0 <= nx < sw and 0 <= ny < sh and grown[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx] = 1
                        stack.append((nx, ny))
            box = (l * s, t * s, min(a.width, (r + 1) * s), min(a.height, (b + 1) * s))
            if (box[2] - box[0]) * (box[3] - box[1]) >= MIN_AREA:
                boxes.append((box, n))
    big = label.resize(img.size, Image.NEAREST)
    order = reading_order([b for b, _ in boxes], img.height)
    ids = {b: k for b, k in boxes}
    return [(b, big.point(lambda v, k=ids[b]: 255 if v == k else 0)) for b in order]


def reading_order(boxes: list[tuple[int, int, int, int]], height: int) -> list[tuple[int, int, int, int]]:
    """가운데 높이가 시트 높이 0.15 안이면 같은 줄, 줄 안에서는 왼쪽부터."""
    rows: list[list[tuple[int, int, int, int]]] = []
    for bx in sorted(boxes, key=lambda b: (b[1] + b[3]) / 2):
        cy = (bx[1] + bx[3]) / 2
        if rows and abs(cy - sum((r[1] + r[3]) / 2 for r in rows[-1]) / len(rows[-1])) <= height * 0.15:
            rows[-1].append(bx)
        else:
            rows.append([bx])
    return [b for r in rows for b in sorted(r, key=lambda b: b[0])]


def tight(img: Image.Image, box: tuple[int, int, int, int], mask: Image.Image) -> Image.Image:
    """덩이 칸 안을 그 덩이 마스크로 거르고 알파로 다시 조인 뒤 PAD 여백을 둔다."""
    part = img.crop(box)
    a = part.getchannel("A")
    part.putalpha(Image.composite(a, Image.new("L", a.size, 0), mask.crop(box)))
    bb = part.getchannel("A").point(lambda v: 255 if v >= 8 else 0).getbbox() or (0, 0, part.width, part.height)
    part = part.crop(bb)
    out = Image.new("RGBA", (part.width + PAD * 2, part.height + PAD * 2), (0, 0, 0, 0))
    out.paste(part, (PAD, PAD))
    return out


def make_meta(path: Path) -> None:
    meta = path.with_name(path.name + ".meta")
    if meta.exists():
        return
    text = TEMPLATE.read_text(encoding="utf-8")
    text = re.sub(r"(?m)^guid: \S+$", "guid: " + uuid.uuid4().hex, text, count=1)
    text = re.sub(r"(?m)^(\s+spriteID: )\S+$", r"\g<1>" + uuid.uuid4().hex, text, count=1)
    meta.write_text(text, encoding="utf-8", newline="\n")


def folder_meta(d: Path) -> None:
    meta = d.with_name(d.name + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\nfolderAsset: yes\nDefaultImporter:\n"
        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8", newline="\n")


def main() -> int:
    check = "--check" in sys.argv
    problems = []
    if not check:
        OUT.mkdir(exist_ok=True)
        folder_meta(OUT)
    for sheet, names in SHEETS.items():
        src = PROPS / (sheet + ".png")
        if not src.exists():
            continue  # 아직 안 뽑은 시트
        img = Image.open(src).convert("RGBA")
        parts = find_parts(img)
        if len(parts) != len(names):
            problems.append(f"{sheet}: 덩이 {len(parts)}개, 이름 {len(names)}개")
            continue
        for name, (box, mask) in zip(names, parts):
            dst = OUT / ("prop_" + name + ".png")
            if check:
                if not dst.exists() or not dst.with_name(dst.name + ".meta").exists():
                    problems.append(f"빠짐 {dst.relative_to(ROOT)}")
                continue
            tight(img, box, mask).save(dst, optimize=True)
            make_meta(dst)
            print(f"{sheet} → {dst.name} {box}")
    for p in problems:
        print(p)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
