#!/usr/bin/env python3
"""붓 획 텍스처 굽기(backlog V-01, docs/design/vfx.md).

web/lookdev/samjae.html 의 brushArc(터럭을 한 올씩 반원을 따라 긋는 획)·마른 붓 자국을 numpy·Pillow 로 옮겼다.
결과는 흰 획 + 알파 PNG. 512px 칸에 그린 뒤 획이 있는 쪽만 잘라 쓴다(V-10, 자른 자리·피벗은 json). 색은 Unity 셰이더가 입힌다(먹·담채·금빛).
시드를 고정해서 다시 구워도 같은 그림이 나온다.

  python tools/bake_brush.py              # 삼재검법 1·2·3단 각 3장, 나머지 시작 무공 여덟 × 3단 각 2장 + 미리보기 둘
  python tools/bake_brush.py --check      # 다시 구워서 저장소 파일과 같은지만 본다(PNG 는 픽셀, 나머지는 바이트 비교)

한 장에 들어가는 것(이름 규칙 {무공}_t{단}_{번호}_{층}.png, 무공 앞 이름은 WEAPONS):
  stroke  본 획(옅은 테두리·눌러 시작한 자리 포함). 늘 있다
  wash    담채 번짐(stroke 를 크게 흐린 것). 2단부터
  gold    금빛 터럭만. 3단(가장자리)·절기(가운데)
크기와 모양 값(m)은 같은 폴더 {무공}.json 에 적는다. Unity 는 sizeMeters 로 스프라이트 크기를 맞춘다.
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
COMMON_DIR = ROOT / "unity/BloodRain/Assets/Art/VFX/Common"
PREVIEW_COMMON = ROOT / "docs/vfx_preview/vfx_common.png"
PREVIEW = ROOT / "docs/vfx_preview/brush_samjae.png"
PREVIEW_START = ROOT / "docs/vfx_preview/brush_start9.png"
SIZE = 512

# 삼재검법: samjae.html TIERS 의 획 모양 값(1단 먹, 2단 담채, 3단 금빛 테두리). 시안과 같은 값이어야 한다.
TIERS = [
    {"id": "t1", "R": 2.2, "span": 2.3, "thick": 0.3, "bristles": 22, "wash": False, "goldEdge": False},
    {"id": "t2", "R": 2.45, "span": 2.4, "thick": 0.42, "bristles": 28, "wash": True, "goldEdge": False},
    {"id": "t3", "R": 2.7, "span": 2.5, "thick": 0.5, "bristles": 32, "wash": True, "goldEdge": True},
]
VARIANTS = 3
DRY = 0.7


def grow(base: dict, shape_keys=()) -> list:
    """1단 값에서 2·3단을 만든다. 삼재검법(시안)의 비율: 굵기 ×1.4·×1.67, 터럭 ×1.27·×1.45, 크기 ×1.11·×1.23.
    2단부터 담채, 3단부터 금빛."""
    out = []
    for i, (tk, br, sz) in enumerate(((1, 1, 1), (1.4, 1.27, 1.11), (1.67, 1.45, 1.23))):
        t = dict(base, id=f"t{i + 1}", wash=i >= 1, goldEdge=i >= 2)
        if "thick" in t:
            t["thick"] = round(base["thick"] * tk, 3)
        if "bristles" in t:
            t["bristles"] = round(base["bristles"] * br)
        for k in shape_keys:
            t[k] = round(base[k] * sz, 3)
        out.append(t)
    return out


# 시작 무공 아홉의 획(V-02, 모양표는 docs/design/vfx.md '무공별 획 모양'). (파일 앞 이름, 무공 id, 단계별 값, 장 수)
WEAPONS = [
    ("samjae", "samjae_sword", TIERS, VARIANTS),
    ("nahan", "nahan_fist", grow({"kind": "line", "L": 0.7, "thick": 0.55, "bristles": 20, "dry": 0.5}, ("L",)), 2),
    ("dagger", "flying_dagger", grow({"kind": "line", "L": 1.2, "thick": 0.12, "bristles": 10, "dry": 0.9}, ("L",)), 2),
    ("changcheon", "changcheon_qi", grow({"R": 1.6, "span": 4.4, "thick": 0.22, "bristles": 18, "dry": 0.8}, ("R",)), 2),
    ("palm", "palm_wind", grow({"R": 0.7, "span": 2.2, "thick": 0.45, "bristles": 26, "dry": 0.6}, ("R",)), 2),
    ("flash", "flash_finger", grow({"kind": "line", "L": 2.4, "thick": 0.08, "bristles": 8, "dry": 1.0}, ("L",)), 2),
    ("bomb", "thunder_bomb", grow({"kind": "blot", "Rb": 1.0, "spikes": 9, "drops": 20}, ("Rb",)), 2),
    ("mist", "poison_mist", grow({"kind": "blot", "Rb": 1.4, "soft": True, "lobes": 11, "drops": 8, "softAlpha": 0.7}, ("Rb",)), 2),
    ("staff", "drunken_staff", grow({"R": 1.5, "span": 2.0, "thick": 0.38, "bristles": 24, "dry": 1.1}, ("R",)), 2),
]

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
        self.hair = None

    def _over(self, x0, y0, x1, y1, cov, alpha):
        if x1 <= x0 or y1 <= y0:
            return
        src = cov * alpha
        dst = self.a[y0:y1, x0:x1]
        if self.hair is not None:  # 한 올을 긋는 중: 겹치는 이음매가 두 번 쌓이지 않게 가장 진한 것만 남긴다
            np.maximum(dst, src, out=dst)
            return
        self.a[y0:y1, x0:x1] = src + dst * (1 - src)

    def begin_hair(self):
        """한 올을 따로 받아 둔다. end_hair 에서 1-(1-a)^2 로 진하기를 맞춰(이음매를 두 번 칠하던 시안과 비슷) 한 번에 얹는다.
        V-10: 선분마다 따로 'over' 로 얹으면 이음매가 두 번 겹쳐 구슬 무늬가 생겼다."""
        self.hair, self.a = self.a, np.zeros_like(self.a)

    def end_hair(self):
        h, self.a, self.hair = self.a, self.hair, None
        h = 1 - (1 - h) ** 2
        self.a = h + self.a * (1 - h)

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

    thick, bristles = t["thick"], t["bristles"]
    dry = t.get("dry", DRY)
    pad = thick * 0.9 + 0.4
    line = t.get("kind") == "line"
    if line:  # 찌르기: 가운데를 지나 +x 로 곧게. 길이 L(m)
        L = t["L"]
        R, span = 0.0, 0.0
        size_m = (L / 2 + pad) * 2
    else:     # 호: 몸(텍스처 가운데)을 중심으로 반지름 R, 펼친 각 span
        R, span = t["R"], t["span"]
        size_m = (R + pad) * 2
    tex = SIZE / size_m  # 1m 가 몇 px 인가(시안은 56, 여기선 512 한 장에 맞춘다)
    cx = cy = SIZE / 2
    n_steps = max(70, round((L if line else span * R) * tex / 3.5))
    a0, a1 = -span / 2, span / 2
    hair = thick * tex / bristles

    def at(s, n):
        """획 길이 비율 s, 획 가로 위치 n(m, 바깥쪽 +) → 픽셀 좌표와 진행 방향 각."""
        if line:
            return cx + lerp(-L / 2, L / 2, s) * tex, cy + n * tex, 0.0
        a = lerp(a0, a1, s)
        r = (R + n) * tex
        return cx + math.cos(a) * r, cy + math.sin(a) * r, a

    stroke, gold = Canvas(SIZE), Canvas(SIZE)

    def pass_(cv, K, lo, hi, alpha_mul, width_mul, dry_mul):
        for k in range(K):
            offs = lerp(lo, hi, 0.5 if K == 1 else k / (K - 1)) + rand(-0.025, 0.025)
            sd = seed_arr(96)
            ink0 = rand(0.82, 1.1)
            dry_rate = rand(0.25, 1.35) * dry * dry_mul
            w = hair * rand(1.4, 2.6) * width_mul
            start_s = rand(0, 0.04)
            end_s = 1 - rand(0, 0.08) - abs(offs) * 0.16
            px = py = None
            cv.begin_hair()
            for i in range(n_steps + 1):
                s = i / n_steps
                if s < start_s or s > end_s:
                    px = None
                    continue
                press = math.pow(clamp(s / 0.1), 0.5) * (1 - 0.82 * math.pow(s, 2.4))
                wob = (noise1(sd, s * 10) - 0.5) * 0.05 * thick
                x, y, _ = at(s, offs * thick * (0.3 + 0.7 * press) + wob)
                ink = ink0 - dry_rate * math.pow(s, 1.5)
                if px is not None and noise1(sd, s * 34 + 11) < ink + 0.12:
                    cv.line(px, py, x, y, max(0.7, w * (0.45 + 0.55 * press)), clamp(ink) * alpha_mul * (0.5 + 0.5 * press))
                px, py = x, y
            cv.end_hair()

    faint = round(bristles * 0.45)
    pass_(stroke, faint, -0.62, 0.62, 0.15, 2.4 * bristles / faint, 0.25)  # 종이에 스민 옅은 테두리
    pass_(stroke, bristles, -0.5, 0.5, 1, 1, 1)  # 본 획
    for _ in range(5):  # 붓을 처음 누른 자리
        x, y, a = at(rand(0.01, 0.07) / (span if span > 0 else 1) if not line else rand(0.0, 0.03), rand(-0.25, 0.25) * thick)
        alpha = rand(0.22, 0.45)
        rx, ry = thick * tex * rand(0.25, 0.45), thick * tex * rand(0.18, 0.32)
        stroke.ellipse(x, y, rx, ry, a, alpha)
    gold_hi = None
    if t.get("goldEdge"):  # 금빛은 몇 올만, 끊기게
        lo, hi = (0.36, 0.5) if not line else (-0.07, 0.07)  # 찌르기는 가운데 심에
        pass_(gold, 5, lo, hi, 0.85, 0.8, 1.5)
        gold_hi = Canvas(SIZE)
        pass_(gold_hi, 2, (lo + hi) / 2 - 0.03, (lo + hi) / 2 + 0.03, 0.8, 0.55, 1.8)
    return stroke.a, (gold.a if t.get("goldEdge") else None), gold_hi.a if gold_hi else None, size_m


def blot(t: dict, seed: int):
    """번짐·점: 가운데 먹 얼룩 + 둘레로 튄 방울과 짧은 터럭(시안 makeSplat 을 사방으로). 반지름 Rb(m).
    soft 면 얼룩을 흐리고 구멍을 내 안개처럼, spikes 는 사방으로 튄 줄기 수."""
    rng = random.Random(seed)
    rand = lambda a, b: a + rng.random() * (b - a)
    Rb = t["Rb"]
    size_m = Rb * 2 * 1.5
    tex = SIZE / size_m
    c = SIZE / 2
    cv = Canvas(SIZE)
    # 가운데 얼룩: 들쭉날쭉한 반지름을 가진 타원 여러 겹
    for k in range(t.get("lobes", 7)):
        a = rand(0, 2 * math.pi)
        d = rand(0, 0.35) * Rb * tex
        cv.ellipse(c + math.cos(a) * d, c + math.sin(a) * d, Rb * tex * rand(0.45, 0.7), Rb * tex * rand(0.35, 0.6), rand(0, math.pi), rand(0.55, 0.9))
    # 사방으로 튄 방울
    for _ in range(t.get("drops", 18)):
        a = rand(0, 2 * math.pi)
        d = rand(0.75, 1.35) * Rb * tex
        r = rand(0.02, 0.07) * Rb * tex
        cv.ellipse(c + math.cos(a) * d, c + math.sin(a) * d, r, r, 0, rand(0.6, 0.95))
    # 줄기(붓 끝이 튄 자국): 가운데서 바깥으로 가늘어지는 선
    for _ in range(t.get("spikes", 0)):
        a = rand(0, 2 * math.pi)
        l0, l1 = rand(0.4, 0.6) * Rb * tex, rand(0.95, 1.4) * Rb * tex
        steps = 10
        for i in range(steps):
            u0, u1 = i / steps, (i + 1) / steps
            w = lerp(0.09, 0.01, u1) * Rb * tex
            cv.line(c + math.cos(a) * lerp(l0, l1, u0), c + math.sin(a) * lerp(l0, l1, u0),
                    c + math.cos(a) * lerp(l0, l1, u1), c + math.sin(a) * lerp(l0, l1, u1), w, 0.85)
    alpha = cv.a
    if t.get("soft"):
        img = Image.fromarray(np.uint8(np.clip(alpha, 0, 1) * 255), "L").resize((SIZE // 6, SIZE // 6), Image.BICUBIC).resize((SIZE, SIZE), Image.BICUBIC)
        alpha = np.asarray(img, dtype=np.float32) / 255
        # 구멍: 값 노이즈 두 겹(시안 noiseField 와 같은 짜임)
        field = np.zeros((SIZE, SIZE), dtype=np.float32)
        for cell, amp in ((SIZE // 10, 0.62), (SIZE // 32, 0.38)):
            g = np.array([[rng.random() for _ in range(SIZE // cell + 2)] for _ in range(SIZE // cell + 2)], dtype=np.float32)
            gi = Image.fromarray(np.uint8(g * 255), "L").resize((SIZE + 2 * cell, SIZE + 2 * cell), Image.BICUBIC)
            field += amp * np.asarray(gi, dtype=np.float32)[cell:cell + SIZE, cell:cell + SIZE] / 255
        alpha = alpha * np.clip((field - 0.25) / 0.35, 0.25, 1) * t.get("softAlpha", 0.8)
    gold = gold_hi = None
    if t.get("goldEdge"):  # 금빛 가루: 둘레에 작은 점
        gcv, hcv = Canvas(SIZE), Canvas(SIZE)
        for i in range(16):
            a = rand(0, 2 * math.pi)
            d = rand(0.55, 1.05) * Rb * tex
            r = rand(0.015, 0.04) * Rb * tex
            (hcv if i % 4 == 0 else gcv).ellipse(c + math.cos(a) * d, c + math.sin(a) * d, r, r, 0, rand(0.7, 0.95))
        gold, gold_hi = gcv.a, hcv.a
    return alpha, gold, gold_hi, size_m


def wash_of(alpha: np.ndarray) -> np.ndarray:
    """시안 blurred(c, 7): 7분의 1로 줄이고 다시 반으로 줄였다가 키워서 흐린다."""
    img = Image.fromarray(np.uint8(np.clip(alpha, 0, 1) * 255), "L")
    a = img.resize((max(1, SIZE // 7), max(1, SIZE // 7)), Image.BICUBIC)
    b = a.resize((max(1, a.width // 2), max(1, a.height // 2)), Image.BICUBIC)
    return np.asarray(b.resize((SIZE, SIZE), Image.BICUBIC), dtype=np.float32) / 255


def crop_box(*alphas, margin=2, align=4):
    """층들(같은 512 칸)을 함께 덮는 사각형 (x, y, w, h) — PIL 좌표(위가 0). V-10 (2).
    PNG 에 0 이 아닌 알파로 남는 픽셀을 모두 담고, 둘레에 투명 margin px 를 두고(겹선형 거르기가 가장자리를 끌지 않게),
    너비·높이를 align 의 배수로 늘린다(휴대폰 압축 ETC2·ASTC 블록이 4). 비면 가운데 align 칸."""
    mask = np.zeros((SIZE, SIZE), dtype=bool)
    for a in alphas:
        if a is not None:
            mask |= np.round(np.clip(a, 0, 1) * 255) > 0
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        c = SIZE // 2 - align // 2
        return c, c, align, align
    x0, x1 = max(0, int(xs.min()) - margin), min(SIZE, int(xs.max()) + 1 + margin)
    y0, y1 = max(0, int(ys.min()) - margin), min(SIZE, int(ys.max()) + 1 + margin)

    def grow_to(lo, hi):
        n = -(-(hi - lo) // align) * align
        extra = n - (hi - lo)
        lo -= extra // 2
        lo = max(0, min(lo, SIZE - n))
        return lo, n

    x0, w = grow_to(x0, x1)
    y0, h = grow_to(y0, y1)
    return x0, y0, w, h


def white_png(alpha: np.ndarray, box=None) -> bytes:
    """흰색 + 알파 PNG. box(x, y, w, h)가 있으면 그 사각형만 잘라 쓴다."""
    if box is not None:
        x, y, w, h = box
        alpha = alpha[y:y + h, x:x + w]
    a = np.uint8(np.round(np.clip(alpha, 0, 1) * 255))
    rgba = np.zeros(a.shape + (4,), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = a
    buf = io.BytesIO()
    Image.fromarray(rgba, "RGBA").save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def bake():
    """{파일 이름: 바이트}, 설명 json, 미리보기에 쓸 층들."""
    files, layers = {}, []
    for wi, (prefix, weapon, tiers, variants) in enumerate(WEAPONS):
      meta = {"_note": "tools/bake_brush.py 가 쓴다. 손으로 고치지 않는다. sizeMeters 는 원래 512px 칸 한 변이 게임에서 몇 m 인가(Pixels Per Unit = 512 / sizeMeters, 잘라도 그대로). 칸 가운데가 몸: 호(arc)는 몸을 중심으로 반지름 R(m)·펼친 각 span(라디안, 가운데가 +x), 찌르기(line)는 몸을 지나 +x 로 길이 L(m), 번짐(blot)은 반지름 Rb(m). PNG 는 획이 있는 쪽만 자른 것: crop [x, y, w, h] 는 512 칸에서 잘라 낸 사각형(px, 위가 0, 너비·높이 4의 배수), pivot 은 잘린 그림에서 몸 위치(왼쪽 아래 0, 위로 +, 0~1 밖일 수 있음). 셰이더 _Rect = (x/512, 1-(y+h)/512, w/512, h/512).",
              "weapon": weapon, "textures": []}
      for ti, t in enumerate(tiers):
        for v in range(1, variants + 1):
            seed = 1000 * (ti + 1) + v + 100000 * wi
            kind = t.get("kind", "arc")
            stroke, gold, gold_hi, size_m = (blot if kind == "blot" else brush_arc)(t, seed)
            base = f"{prefix}_{t['id']}_{v}"
            entry = {"name": base, "tier": ti + 1, "seed": seed, "kind": kind, "sizeMeters": round(size_m, 4)}
            entry.update({k: t[k] for k in ("R", "span", "L", "Rb", "thick") if k in t})
            entry["layers"] = ["stroke"]
            wash = wash_of(stroke) if t["wash"] else None
            # 금빛 두 겹(GOLD·GOLD_HI)을 한 장에: 알파는 합친 것, 밝은 쪽은 미리보기에서만 따로 쓴다
            both = gold + gold_hi * (1 - gold) if gold is not None else None
            # V-10 (2): 층들이 함께 덮는 쪽만 잘라 쓴다. 한 텍스처의 층은 같은 사각형이라 그대로 겹친다.
            box = crop_box(stroke, wash, both)
            files[f"{base}_stroke.png"] = white_png(stroke, box)
            if wash is not None:
                files[f"{base}_wash.png"] = white_png(wash, box)
                entry["layers"].append("wash")
            if both is not None:
                files[f"{base}_gold.png"] = white_png(both, box)
                entry["layers"].append("gold")
            x, y, w, h = box
            entry["crop"] = [x, y, w, h]
            # 피벗: 몸(원래 512 칸 가운데)이 잘린 그림의 어디인가. Unity 스프라이트 꼴(왼쪽 아래 0, 위로 +), 0~1 밖일 수 있다(호는 몸이 획 밖)
            entry["pivot"] = [round((SIZE / 2 - x) / w, 5), round((y + h - SIZE / 2) / h, 5)]
            meta["textures"].append(entry)
            layers.append((prefix, t, v, stroke, wash, gold, gold_hi))
      files[f"{prefix}.json"] = (json.dumps(meta, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return files, layers


# ---- 공통 텍스처(V-03): Assets/Art/VFX/Common ----

def splat(seed: int):
    """시안 makeSplat: 가운데 얼룩 + 한쪽(+x)으로 튄 방울과 줄기. 텍스처 한 변 1.4m 기준."""
    rng = random.Random(seed)
    rand = lambda a, b: a + rng.random() * (b - a)
    S = SIZE
    cv = Canvas(S)
    cx, cy = S * 0.38, S / 2
    pts, r0 = 20, S * 0.1
    rr = [r0 * rand(0.65, 1.3) for _ in range(pts)]
    poly = []
    for i in range(pts * 4):
        a = i / (pts * 4) * 2 * math.pi
        f = (i % 4) / 4
        r = lerp(rr[(i // 4) % pts], rr[(i // 4 + 1) % pts], f * f * (3 - 2 * f))
        poly.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    m = Image.new("L", (S * 2, S * 2), 0)  # 두 배로 그려 줄이면 가장자리가 매끈하다
    ImageDraw.Draw(m).polygon([(x * 2, y * 2) for x, y in poly], fill=int(255 * 0.88))
    cv.a = np.asarray(m.resize((S, S), Image.LANCZOS), dtype=np.float32) / 255
    for _ in range(14):
        a = rand(-0.65, 0.65)
        d = rand(0.14, 0.58) * S
        r = rand(0.006, 0.03) * S * (1.1 - d / S)
        cv.ellipse(cx + math.cos(a) * d, cy + math.sin(a) * d, r, r, 0, rand(0.6, 0.95))
    for _ in range(4):
        a = rand(-0.4, 0.4)
        l = rand(0.18, 0.42) * S
        w = rand(0.012, 0.025) * S
        for i in range(10):
            u0, u1 = i / 10, (i + 1) / 10
            cv.line(cx + math.cos(a) * l * u0, cy + math.sin(a) * l * u0, cx + math.cos(a) * l * u1, cy + math.sin(a) * l * u1,
                    max(0.8, 2 * w * (1 - u1)), 0.8)
    return cv.a


def value_noise(rng: random.Random, cells=((10, 0.62), (32, 0.38))) -> np.ndarray:
    """시안 noiseField 짜임(값 노이즈 두 겹). 격자를 감아서 이어 붙여도 이음매가 없다."""
    out = np.zeros((SIZE, SIZE), dtype=np.float32)
    ys, xs = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    for n, amp in cells:
        g = np.array([[rng.random() for _ in range(n)] for _ in range(n)], dtype=np.float32)
        gx, gy = xs / SIZE * n, ys / SIZE * n
        ix, iy = np.floor(gx).astype(int), np.floor(gy).astype(int)
        fx, fy = gx - ix, gy - iy
        ux, uy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
        x0, x1, y0, y1 = ix % n, (ix + 1) % n, iy % n, (iy + 1) % n
        top = g[y0, x0] * (1 - ux) + g[y0, x1] * ux
        bot = g[y1, x0] * (1 - ux) + g[y1, x1] * ux
        out += amp * (top * (1 - uy) + bot * uy)
    return out


def seal_base(seed: int):
    """낙관 바탕(글자 없이): 둥근 네모 인주 자국, 가장자리는 갉히고 안에는 인주가 덜 묻은 자리."""
    rng = random.Random(seed)
    S = SIZE
    m = Image.new("L", (S, S), 0)
    pad = int(S * 0.12)
    ImageDraw.Draw(m).rounded_rectangle([pad, pad, S - pad, S - pad], radius=int(S * 0.06), fill=255)
    a = np.asarray(m, dtype=np.float32) / 255
    n = value_noise(rng, ((16, 0.5), (64, 0.5)))
    edge = np.asarray(m.resize((S // 8, S // 8), Image.BICUBIC).resize((S, S), Image.BICUBIC), dtype=np.float32) / 255
    a = a * np.clip((n - (1 - edge) * 0.9 - 0.12) / 0.12, 0, 1)  # 가장자리일수록 잘 갉힌다
    holes = value_noise(rng, ((24, 0.6), (64, 0.4)))
    a = a * np.clip((holes - 0.12) / 0.12, 0.6, 1)  # 덜 묻은 자리
    return a


def ground_stain(seed: int):
    """바닥 먹 자국: 옅게 번진 얼룩. 8초쯤 마르며 옅어지는 건 셰이더 몫."""
    t = {"kind": "blot", "Rb": 1.0, "soft": True, "lobes": 9, "drops": 6, "softAlpha": 0.55}
    a, _, _, _ = blot(t, seed)
    return a


def gray_png(a: np.ndarray) -> bytes:
    buf = io.BytesIO()
    Image.fromarray(np.uint8(np.round(np.clip(a, 0, 1) * 255)), "L").save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def bake_common():
    files, sheet = {}, []
    files["seal_base.png"] = white_png(seal_base(7001))
    sheet.append(("낙관 바탕", files["seal_base.png"]))
    for i in range(1, 7):
        files[f"ink_splat_{i}.png"] = white_png(splat(7100 + i))
    for i in range(1, 5):
        files[f"ground_ink_{i}.png"] = white_png(ground_stain(7200 + i))
    for i in range(1, 3):
        n = value_noise(random.Random(7300 + i))
        n = (n - n.min()) / (n.max() - n.min())
        files[f"noise_{i}.png"] = gray_png(n)
    meta = {"_note": "tools/bake_brush.py 가 쓴다. 손으로 고치지 않는다. 흰색 + 알파(색은 셰이더): seal_base 낙관 바탕(글자는 Unity 에서 얹음, 색은 낙관 주홍), ink_splat 먹 튐(+x 로 튐, 한 변 1.4m), ground_ink 바닥 먹 자국(한 변 3m). 회색: noise 소멸(_Dissolve)용 값 노이즈, 감아 이어 붙여도 이음매가 없다.",
            "sizeMeters": {"ink_splat": 1.4, "ground_ink": 3.0}}
    files["common.json"] = (json.dumps(meta, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
    return files


def common_preview(files) -> Image.Image:
    names = ["seal_base.png"] + [f"ink_splat_{i}.png" for i in range(1, 7)] + [f"ground_ink_{i}.png" for i in range(1, 5)] + ["noise_1.png", "noise_2.png"]
    cell, gap, cols = 200, 10, 5
    rows = (len(names) + cols - 1) // cols
    img = Image.new("RGB", (cols * (cell + gap) + gap, rows * (cell + gap) + gap + 40), PAPER)
    d = ImageDraw.Draw(img)
    d.text((gap, 8), "공통 텍스처(V-03): 낙관 바탕 · 먹 튐 6 · 바닥 먹 자국 4 · 노이즈 2", fill=INK, font=_font(20))
    for k, n in enumerate(names):
        x, y = gap + (k % cols) * (cell + gap), 40 + gap + (k // cols) * (cell + gap)
        im = Image.open(io.BytesIO(files[n]))
        if im.mode == "L":
            img.paste(im.convert("RGB").resize((cell, cell)), (x, y))
            continue
        a = im.split()[3].resize((cell, cell), Image.LANCZOS)
        color = (0xB3, 0x26, 0x1E) if n.startswith("seal") else INK
        img.paste(Image.new("RGB", (cell, cell), color), (x, y), a)
    return img


def _font(size):
    for f in ["/usr/share/fonts/truetype/noto/NotoSansCJK-Regular.ttc", "/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc",
              "/usr/share/fonts/opentype/noto/NotoSerifCJK-Regular.ttc", "/usr/share/fonts/opentype/noto/NotoSerifCJK-Bold.ttc"]:
        if Path(f).exists():
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def sheet_of(grid, row_labels, title, cell=300) -> Image.Image:
    """grid[행][열] = (t, stroke, wash, gold, gold_hi). 종이 위에 먹·담채·금으로 얹은 한 장."""
    gap, top, left = 12, 44, 110
    cols = max(len(r) for r in grid)
    w = cols * cell + (cols + 1) * gap + left
    h = len(grid) * cell + (len(grid) + 1) * gap + top
    sheet = np.zeros((h, w, 3), dtype=np.float32)
    sheet[:] = np.array(PAPER, dtype=np.float32) / 255

    def put(alpha, color, x, y, mul=1.0):
        a = np.asarray(Image.fromarray(np.uint8(np.clip(alpha, 0, 1) * 255), "L").resize((cell, cell), Image.LANCZOS), dtype=np.float32) / 255 * mul
        col = np.array(color, dtype=np.float32) / 255
        region = sheet[y:y + cell, x:x + cell]
        sheet[y:y + cell, x:x + cell] = region * (1 - a[..., None]) + col * a[..., None]

    for r, row in enumerate(grid):
        for c, (t, stroke, wash, gold, gold_hi) in enumerate(row):
            x, y = left + gap + c * (cell + gap), top + gap + r * (cell + gap)
            if wash is not None:
                put(wash, TEAL, x, y, WASH_ALPHA[t["id"]])
            put(stroke, INK, x, y)
            if gold is not None:
                put(gold, GOLD, x, y)
                put(gold_hi, GOLD_HI, x, y)
    img = Image.fromarray(np.uint8(np.clip(sheet, 0, 1) * 255), "RGB")
    d = ImageDraw.Draw(img)
    d.text((left + gap, 10), title, fill=INK, font=_font(22))
    for r, label in enumerate(row_labels):
        d.text((10, top + gap + r * (cell + gap) + cell // 2 - 12), label, fill=INK, font=_font(20))
    return img


def previews(layers):
    """삼재검법 한 장(행 단계, 열 시드)과 시작 무공 아홉 한 장(행 무공, 열 단계, 첫 시드)."""
    sam = [[(t, s, w, g, gh) for (p, t, v, s, w, g, gh) in layers if p == "samjae" and t["id"] == f"t{i}"] for i in (1, 2, 3)]
    one = sheet_of(sam, ["1단", "2단", "3단"], "삼재검법 붓 획(tools/bake_brush.py) — 행: 1단 먹 · 2단 담채 · 3단 금빛 테두리, 열: 시드 1·2·3")
    names = {"samjae": "삼재검법", "nahan": "나한권", "dagger": "비도술", "changcheon": "창천검기", "palm": "장풍",
             "flash": "섬광지", "bomb": "벽력탄", "mist": "독무", "staff": "취선봉"}
    grid, labels = [], []
    for prefix, _, _, _ in WEAPONS:
        grid.append([(t, s, w, g, gh) for (p, t, v, s, w, g, gh) in layers if p == prefix and v == 1])
        labels.append(names.get(prefix, prefix))
    nine = sheet_of(grid, labels, "시작 무공 아홉 — 열: 1·2·3단(첫 시드), 칸 크기 맞춤", cell=220)
    return one, nine


def same_file(path: Path, data: bytes) -> bool:
    """PNG 는 풀어 놓은 픽셀로, 나머지는 바이트로 견준다. Pillow·zlib 판이 다르면 같은 그림도 바이트가 달라진다."""
    if not path.exists():
        return False
    old = path.read_bytes()
    if old == data:
        return True
    if path.suffix != ".png":
        return False
    a, b = Image.open(io.BytesIO(old)), Image.open(io.BytesIO(data))
    return a.mode == b.mode and a.size == b.size and np.array_equal(np.asarray(a), np.asarray(b))


def write_if_changed(path: Path, data: bytes) -> None:
    """그림이 같으면 손대지 않는다(다른 판 Pillow 로 다시 구워도 저장소가 흔들리지 않게)."""
    if not same_file(path, data):
        path.write_bytes(data)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="다시 구워서 저장소 파일과 같은지만 본다(PNG 는 픽셀 비교)")
    args = ap.parse_args(argv)
    files, layers = bake()
    common = bake_common()
    if args.check:
        bad = [n for n, b in files.items() if not same_file(OUT_DIR / n, b)]
        bad += [n for n, b in common.items() if not same_file(COMMON_DIR / n, b)]
        if bad:
            print("다시 구운 것과 다른 파일:", ", ".join(bad))
            return 1
        print(f"같음: {len(files) + len(common)}개")
        return 0
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for n, b in files.items():
        write_if_changed(OUT_DIR / n, b)
    PREVIEW.parent.mkdir(parents=True, exist_ok=True)
    one, nine = previews(layers)
    one.save(PREVIEW, optimize=True)
    COMMON_DIR.mkdir(parents=True, exist_ok=True)
    for n, b in common.items():
        write_if_changed(COMMON_DIR / n, b)
    common_preview(common).save(PREVIEW_COMMON, optimize=True)
    nine.save(PREVIEW_START, optimize=True)
    print(f"{len(files)}개를 {OUT_DIR.relative_to(ROOT)} 에, 미리보기 {PREVIEW.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
