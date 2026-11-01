#!/usr/bin/env python3
"""붓 획 텍스처 굽기(backlog V-01, docs/design/vfx.md).

web/lookdev/samjae.html 의 brushArc(터럭을 한 올씩 반원을 따라 긋는 획)·마른 붓 자국을 numpy·Pillow 로 옮겼다.
결과는 흰 획 + 알파 PNG 512px. 색은 Unity 셰이더가 입힌다(먹·담채·금빛).
시드를 고정해서 다시 구워도 같은 그림이 나온다.

  python tools/bake_brush.py              # 삼재검법 1·2·3단 각 3장 + 미리보기
  python tools/bake_brush.py --check      # 다시 구워서 저장소 파일과 같은지만 본다(바이트 비교)

한 장에 들어가는 것(이름 규칙 samjae_t{단}_{번호}_{층}.png):
  stroke  본 획(옅은 테두리·눌러 시작한 자리 포함). 늘 있다
  wash    담채 번짐(stroke 를 크게 흐린 것). 2단부터
  gold    금빛 터럭만. 3단(가장자리)·절기(가운데)
크기와 반지름(m)은 같은 폴더 samjae.json 에 적는다. Unity 는 sizeMeters 로 스프라이트 크기를 맞춘다.
"""
from __future__ import annotations

import argparse
import io
import json
import math
import random
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "unity/BloodRain/Assets/Art/VFX/Brush"
PREVIEW = ROOT / "docs/vfx_preview/brush_samjae.png"
SIZE = 512

# samjae.html TIERS 의 획 모양 값(1단 먹, 2단 담채, 3단 금빛 테두리). 시안과 같은 값이어야 한다.
TIERS = [
    {"id": "t1", "R": 2.2, "span": 2.3, "thick": 0.3, "bristles": 22, "wash": False, "goldEdge": False},
    {"id": "t2", "R": 2.45, "span": 2.4, "thick": 0.42, "bristles": 28, "wash": True, "goldEdge": False},
    {"id": "t3", "R": 2.7, "span": 2.5, "thick": 0.5, "bristles": 32, "wash": True, "goldEdge": True},
]
VARIANTS = 3
DRY = 0.7

# 미리보기 색(samjae.html 과 같다)
PAPER = (0xEF, 0xE6, 0xD2)
INK = (0x14, 0x14, 0x14)
TEAL = (0x4F, 0x7F, 0x7A)
GOLD = (0xC9, 0xA2, 0x27)
GOLD_HI = (0xF4, 0xD9, 0x8A)
WASH_ALPHA = {"t2": 0.42, "t3": 0.48}


def clamp(v, a=0.0, b=1.0):
    return max(a, min(b, v))


def lerp(a, b, t):
    return a + (b - a) * t


def noise1(s, x):
    n = len(s)
    i = math.floor(x)
    f = x - i
    u = f * f * (3 - 2 * f)
    return lerp(s[i % n], s[(i + 1) % n], u)


class Canvas:
    """알파 한 장(0~1 float). 선분을 둥근 끝 캡슐로 칠하고 'over' 로 쌓는다(캔버스 2D 와 같은 겹침)."""

    def __init__(self, size: int):
        self.size = size
        self.a = np.zeros((size, size), dtype=np.float32)

    def _over(self, x0, y0, x1, y1, cov, alpha):
        if x1 <= x0 or y1 <= y0:
            return
        src = cov * alpha
        dst = self.a[y0:y1, x0:x1]
        self.a[y0:y1, x0:x1] = src + dst * (1 - src)

    def line(self, ax, ay, bx, by, width, alpha):
        r = width / 2
        x0 = max(0, int(math.floor(min(ax, bx) - r - 1)))
        y0 = max(0, int(math.floor(min(ay, by) - r - 1)))
        x1 = min(self.size, int(math.ceil(max(ax, bx) + r + 1)) + 1)
        y1 = min(self.size, int(math.ceil(max(ay, by) + r + 1)) + 1)
        if x1 <= x0 or y1 <= y0:
            return
        ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        px, py = xs + 0.5, ys + 0.5
        dx, dy = bx - ax, by - ay
        ll = dx * dx + dy * dy
        if ll < 1e-9:
            t = np.zeros_like(px)
        else:
            t = np.clip(((px - ax) * dx + (py - ay) * dy) / ll, 0, 1)
        d = np.hypot(px - (ax + t * dx), py - (ay + t * dy))
        cov = np.clip(r + 0.5 - d, 0, 1)
        self._over(x0, y0, x1, y1, cov, alpha)

    def ellipse(self, cx, cy, rx, ry, rot, alpha):
        rr = max(rx, ry)
        x0, y0 = max(0, int(cx - rr - 1)), max(0, int(cy - rr - 1))
        x1, y1 = min(self.size, int(cx + rr + 2)), min(self.size, int(cy + rr + 2))
        if x1 <= x0 or y1 <= y0:
            return
        ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        c, s = math.cos(rot), math.sin(rot)
        u = (xs + 0.5 - cx) * c + (ys + 0.5 - cy) * s
        v = -(xs + 0.5 - cx) * s + (ys + 0.5 - cy) * c
        k = np.sqrt((u / rx) ** 2 + (v / ry) ** 2)
        cov = np.clip((1 - k) * min(rx, ry) + 0.5, 0, 1)
        self._over(x0, y0, x1, y1, cov, alpha)


def brush_arc(t: dict, seed: int):
    """samjae.html brushArc 와 같은 순서로 긋는다. 돌려주는 것: stroke·gold 알파, 텍스처 한 변 길이(m)."""
    rng = random.Random(seed)
    rand = lambda a, b: a + rng.random() * (b - a)
    seed_arr = lambda n: [rng.random() for _ in range(n)]

    R, span, thick, bristles = t["R"], t["span"], t["thick"], t["bristles"]
    pad = thick * 0.9 + 0.4
    size_m = (R + pad) * 2
    tex = SIZE / size_m  # 1m 가 몇 px 인가(시안은 56, 여기선 512 한 장에 맞춘다)
    cx = cy = SIZE / 2
    n_steps = max(70, round(span * R * tex / 3.5))
    a0, a1 = -span / 2, span / 2
    hair = thick * tex / bristles

    stroke, gold = Canvas(SIZE), Canvas(SIZE)

    def pass_(cv, K, lo, hi, alpha_mul, width_mul, dry_mul):
        for k in range(K):
            offs = lerp(lo, hi, 0.5 if K == 1 else k / (K - 1)) + rand(-0.025, 0.025)
            sd = seed_arr(96)
            ink0 = rand(0.82, 1.1)
            dry_rate = rand(0.25, 1.35) * DRY * dry_mul
            w = hair * rand(1.4, 2.6) * width_mul
            start_s = rand(0, 0.04)
            end_s = 1 - rand(0, 0.08) - abs(offs) * 0.16
            px = py = None
            for i in range(n_steps + 1):
                s = i / n_steps
                if s < start_s or s > end_s:
                    px = None
                    continue
                press = math.pow(clamp(s / 0.1), 0.5) * (1 - 0.82 * math.pow(s, 2.4))
                wob = (noise1(sd, s * 10) - 0.5) * 0.05 * thick
                r = (R + offs * thick * (0.3 + 0.7 * press) + wob) * tex
                a = lerp(a0, a1, s)
                x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
                ink = ink0 - dry_rate * math.pow(s, 1.5)
                if px is not None and noise1(sd, s * 34 + 11) < ink + 0.12:
                    cv.line(px, py, x, y, max(0.7, w * (0.45 + 0.55 * press)), clamp(ink) * alpha_mul * (0.5 + 0.5 * press))
                px, py = x, y

    faint = round(bristles * 0.45)
    pass_(stroke, faint, -0.62, 0.62, 0.15, 2.4 * bristles / faint, 0.25)  # 종이에 스민 옅은 테두리
    pass_(stroke, bristles, -0.5, 0.5, 1, 1, 1)  # 본 획
    for _ in range(5):  # 붓을 처음 누른 자리
        a = a0 + rand(0.01, 0.07)
        r = (R + rand(-0.25, 0.25) * thick) * tex
        alpha = rand(0.22, 0.45)
        rx, ry = thick * tex * rand(0.25, 0.45), thick * tex * rand(0.18, 0.32)
        stroke.ellipse(cx + math.cos(a) * r, cy + math.sin(a) * r, rx, ry, a, alpha)
    gold_hi = None
    if t.get("goldEdge"):  # 금빛은 몇 올만, 끊기게
        pass_(gold, 5, 0.36, 0.5, 0.85, 0.8, 1.5)
        gold_hi = Canvas(SIZE)
        pass_(gold_hi, 2, 0.4, 0.46, 0.8, 0.55, 1.8)
    return stroke.a, (gold.a if t.get("goldEdge") else None), gold_hi.a if gold_hi else None, size_m


def wash_of(alpha: np.ndarray) -> np.ndarray:
    """시안 blurred(c, 7): 7분의 1로 줄이고 다시 반으로 줄였다가 키워서 흐린다."""
    img = Image.fromarray(np.uint8(np.clip(alpha, 0, 1) * 255), "L")
    a = img.resize((max(1, SIZE // 7), max(1, SIZE // 7)), Image.BICUBIC)
    b = a.resize((max(1, a.width // 2), max(1, a.height // 2)), Image.BICUBIC)
    return np.asarray(b.resize((SIZE, SIZE), Image.BICUBIC), dtype=np.float32) / 255


def white_png(alpha: np.ndarray) -> bytes:
    a = np.uint8(np.round(np.clip(alpha, 0, 1) * 255))
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = a
    buf = io.BytesIO()
    Image.fromarray(rgba, "RGBA").save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def bake():
    """{파일 이름: 바이트}, 설명 json, 미리보기에 쓸 층들."""
    files, meta, layers = {}, {"_note": "tools/bake_brush.py 가 쓴다. 손으로 고치지 않는다. sizeMeters 는 텍스처 한 변이 게임에서 몇 m 인가, 획 중심 반지름은 R(m), 펼친 각은 span(라디안, 가운데가 +x 방향).", "textures": []}, []
    for ti, t in enumerate(TIERS):
        for v in range(1, VARIANTS + 1):
            seed = 1000 * (ti + 1) + v
            stroke, gold, gold_hi, size_m = brush_arc(t, seed)
            base = f"samjae_{t['id']}_{v}"
            entry = {"name": base, "tier": ti + 1, "seed": seed, "sizeMeters": round(size_m, 4), "R": t["R"], "span": t["span"], "thick": t["thick"], "layers": ["stroke"]}
            files[f"{base}_stroke.png"] = white_png(stroke)
            wash = None
            if t["wash"]:
                wash = wash_of(stroke)
                files[f"{base}_wash.png"] = white_png(wash)
                entry["layers"].append("wash")
            if gold is not None:
                # 금빛 두 겹(GOLD·GOLD_HI)을 한 장에: 알파는 합친 것, 밝은 쪽은 미리보기에서만 따로 쓴다
                both = gold + gold_hi * (1 - gold)
                files[f"{base}_gold.png"] = white_png(both)
                entry["layers"].append("gold")
            meta["textures"].append(entry)
            layers.append((t, v, stroke, wash, gold, gold_hi))
    files["samjae.json"] = (json.dumps(meta, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return files, layers


def preview(layers) -> Image.Image:
    cell, gap, top = 300, 12, 44
    w = VARIANTS * cell + (VARIANTS + 1) * gap + 60
    h = len(TIERS) * cell + (len(TIERS) + 1) * gap + top
    sheet = np.zeros((h, w, 3), dtype=np.float32)
    sheet[:] = np.array(PAPER, dtype=np.float32) / 255

    def put(alpha, color, x, y, mul=1.0):
        a = np.asarray(Image.fromarray(np.uint8(np.clip(alpha, 0, 1) * 255), "L").resize((cell, cell), Image.LANCZOS), dtype=np.float32) / 255 * mul
        col = np.array(color, dtype=np.float32) / 255
        region = sheet[y:y + cell, x:x + cell]
        sheet[y:y + cell, x:x + cell] = region * (1 - a[..., None]) + col * a[..., None]

    for row, (t, v, stroke, wash, gold, gold_hi) in enumerate(layers):
        r, c = row // VARIANTS, row % VARIANTS
        x, y = 60 + gap + c * (cell + gap), top + gap + r * (cell + gap)
        if wash is not None:
            put(wash, TEAL, x, y, WASH_ALPHA[t["id"]])
        put(stroke, INK, x, y)
        if gold is not None:
            put(gold, GOLD, x, y)
            put(gold_hi, GOLD_HI, x, y)
    img = Image.fromarray(np.uint8(np.clip(sheet, 0, 1) * 255), "RGB")
    d = ImageDraw.Draw(img)
    font = None
    for f in ["/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc", "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc", "/usr/share/fonts/opentype/noto/NotoSerifCJK-Regular.ttc", "/usr/share/fonts/opentype/noto/NotoSerifCJK-Bold.ttc"]:
        if Path(f).exists():
            font = ImageFont.truetype(f, 22)
            break
    font = font or ImageFont.load_default()
    d.text((60 + gap, 10), "삼재검법 붓 획(tools/bake_brush.py) — 행: 1단 먹 · 2단 담채 · 3단 금빛 테두리, 열: 시드 1·2·3", fill=INK, font=font)
    for r in range(len(TIERS)):
        d.text((14, top + gap + r * (cell + gap) + cell // 2 - 12), f"{r + 1}단", fill=INK, font=font)
    return img


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="다시 구워서 저장소 파일과 바이트가 같은지만 본다")
    args = ap.parse_args(argv)
    files, layers = bake()
    if args.check:
        bad = [n for n, b in files.items() if not (OUT_DIR / n).exists() or (OUT_DIR / n).read_bytes() != b]
        if bad:
            print("다시 구운 것과 다른 파일:", ", ".join(bad))
            return 1
        print(f"같음: {len(files)}개")
        return 0
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for n, b in files.items():
        (OUT_DIR / n).write_bytes(b)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    preview(layers).save(PREVIEW, optimize=True)
    print(f"{len(files)}개를 {OUT_DIR.relative_to(ROOT)} 에, 미리보기 {PREVIEW.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
