#!/usr/bin/env python3
"""ChatGPT 로 뽑은 이펙트 재료(vfx_ink·vfx_glow)를 bake_brush.py 와 같은 꼴(층마다 흰색 + 알파 512px)로 바꾼다.

docs/design/art_pipeline.md 의 '기계 검수와 반영' 단계. 이미지가 있는 PC 에서 돌린다.
  vfx_ink   흰 바탕 위 먹  → {id}_stroke.png (어두운 만큼 알파, 색은 셰이더가 먹으로)
  vfx_glow  검은 바탕 위 빛 → {id}_gold.png (밝은 만큼 알파, 셰이더가 금빛으로 더하기)
                              + {id}_core.png (하얗게 달아오른 가운데만, 셰이더가 흰빛으로 더하기)
정사각 512px, 그림 가운데가 텍스처 가운데. 크기·그어짐 방향은 `Assets/Art/VFX/AI/ai.json`
(sizeMeters 는 한 변이 게임에서 몇 m, progress 는 radial·down·up·right·left).

기계 검수: 바탕이 단색인지, 덮임이 너무 적거나 많은지, 가장자리에 걸렸는지(edgeOk 줄은 봐줌), 먹 재료에 색이 섞였는지.
걸리면 큐 줄을 rejected + reason 으로 적고 텍스처를 만들지 않는다. 글자·모양은 세션이 미리보기
(docs/vfx_preview/ai_<날짜>.png: 원본 | 게임 색 | 그어짐 50% | 흩어짐 40%)를 눈으로 보고 --reject 로 떨어뜨린다.

  python tools/import_vfx_ai.py --inbox E:/Unity/BloodRain     # status generated 인 vfx 줄 전부
  python tools/import_vfx_ai.py --only fx_mist_ink
  python tools/import_vfx_ai.py --reject fx_mist_ink "글자가 들어감"
필요: numpy, Pillow
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
QUEUE = ROOT / "art_queue" / "queue.jsonl"
OUT = ROOT / "unity/BloodRain/Assets/Art/VFX/AI"
META = OUT / "ai.json"
NOISE = ROOT / "unity/BloodRain/Assets/Art/VFX/Common/noise_1.png"
PREVIEW = ROOT / "docs/vfx_preview"
SIZE = 512
INK, GOLD, PAPER, DARK = (0.078, 0.078, 0.078), (0.788, 0.635, 0.153), (0.937, 0.902, 0.824), (0.10, 0.09, 0.08)


def lum(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def border(a, frac=0.02):
    b = max(2, int(min(a.shape[:2]) * frac))
    return np.concatenate([a[:b].ravel(), a[-b:].ravel(), a[:, :b].ravel(), a[:, -b:].ravel()])


def key(kind, rgb):
    """배경을 빼 층(알파)들과 문제 목록, 숫자를 돌려준다."""
    problems = []
    L = lum(rgb)
    bl = border(L)
    bg = float(np.median(bl))
    if kind == "vfx_ink":
        if bg < 0.9:
            problems.append(f"바탕이 흰색이 아니다(가장자리 밝기 {bg:.2f})")
        if float(np.percentile(np.abs(bl - bg), 95)) > 0.08:
            problems.append("바탕이 고르지 않다(종이 결·그라디언트)")
        dark = float(np.percentile(L, 0.5))
        a = np.clip((bg - L) / max(0.05, bg - dark), 0, 1)
        a = np.clip((a - 0.02) / 0.98, 0, 1)
        sat = rgb.max(-1) - rgb.min(-1)
        m = a > 0.2
        if m.any() and float((sat[m] > 0.18).mean()) > 0.05:
            problems.append("먹 재료에 색이 섞였다")
        layers = {"stroke": a}
    else:
        if bg > 0.08:
            problems.append(f"바탕이 검은색이 아니다(가장자리 밝기 {bg:.2f})")
        mx = rgb.max(-1)
        a = np.clip((mx - bg - 0.03) / 0.9, 0, 1) ** 0.85
        white = np.clip(rgb.min(-1) / np.maximum(mx, 1e-4), 0, 1)
        core = a * np.clip((white - 0.45) / 0.45, 0, 1)
        layers = {"gold": a, "core": core}
    cov = float((a > 0.1).mean())
    if cov < 0.01:
        problems.append(f"그림이 거의 없다(덮임 {cov:.1%})")
    if cov > 0.7:
        problems.append(f"화면을 너무 많이 덮는다(덮임 {cov:.1%})")
    edge = float((border(a, 0.01) > 0.25).mean())
    return layers, problems, {"background": round(bg, 3), "coverage": round(cov, 4), "edge": round(edge, 4)}


def square(layers, main):
    """덮인 곳만 잘라 가운데 두고 정사각 SIZE 로."""
    ys, xs = np.nonzero(main > 0.5 / 255)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    side = int(max(y1 - y0, x1 - x0) * 1.06) + 4
    cy, cx = (y0 + y1) // 2, (x0 + x1) // 2
    out = {}
    for name, ch in layers.items():
        canvas = np.zeros((side, side), np.float32)
        sy, sx = cy - side // 2, cx - side // 2
        ty0, tx0 = max(0, -sy), max(0, -sx)
        sy0, sx0 = max(0, sy), max(0, sx)
        h = min(ch.shape[0] - sy0, side - ty0)
        w = min(ch.shape[1] - sx0, side - tx0)
        canvas[ty0:ty0 + h, tx0:tx0 + w] = ch[sy0:sy0 + h, sx0:sx0 + w]
        im = Image.fromarray((canvas * 255 + 0.5).astype(np.uint8)).resize((SIZE, SIZE), Image.LANCZOS)
        out[name] = np.asarray(im, np.float32) / 255
    return out


def white_alpha_png(a, path):
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(path, optimize=True)


def progress_mask(mode, a, p):
    """셰이더 _Progress 와 같은 뜻: 방향을 따라 p 만큼 그어진 곳만 1(경계 0.06)."""
    h, w = a.shape
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    ys, xs = (ys + 0.5) / h, (xs + 0.5) / w
    t = {"down": ys, "up": 1 - ys, "right": xs, "left": 1 - xs}.get(mode)
    if t is None:
        t = np.hypot(ys - 0.5, xs - 0.5) / 0.5
    m = a > 0.05
    if m.any():
        lo, hi = float(t[m].min()), float(t[m].max())
        t = (t - lo) / max(1e-6, hi - lo)
    return np.clip((p + 0.06 - t) / 0.06, 0, 1)


def sheet(items, path):
    noise = np.asarray(Image.open(NOISE).convert("L").resize((SIZE, SIZE)), np.float32) / 255 if NOISE.exists() else None
    cell = 256
    canvas = np.ones((cell * len(items) + 16, cell * 4 + 40, 3), np.float32) * np.array(PAPER)
    for r, (row, layers, orig) in enumerate(items):
        y = 8 + r * cell
        canvas[y:y + cell - 8, 8:8 + cell - 8] = np.asarray(Image.fromarray(orig).resize((cell - 8, cell - 8)), np.float32) / 255
        glow = row["kind"] == "vfx_glow"
        for c, (p, d) in enumerate(((1.1, 0.0), (0.5, 0.0), (1.1, 0.4))):
            x = 16 + (c + 1) * cell
            tile = canvas[y:y + cell - 8, x:x + cell - 8]
            if glow:
                tile[:] = DARK
            main = layers["gold" if glow else "stroke"]
            k = progress_mask(row.get("progress", "radial"), main, p)
            if d > 0 and noise is not None:
                k = k * np.clip((noise - d) / 0.16, 0, 1)
            rs = lambda ch: np.asarray(Image.fromarray((np.clip(ch, 0, 1) * 255).astype(np.uint8)).resize((cell - 8, cell - 8)), np.float32)[..., None] / 255
            if glow:
                tile += rs(layers["gold"] * k) * np.array(GOLD) + rs(layers["core"] * k) * 0.9
            else:
                al = rs(main * k)
                tile[:] = tile * (1 - al) + np.array(INK) * al
            canvas[y:y + cell - 8, x:x + cell - 8] = np.clip(tile, 0, 1)
    Image.fromarray((np.clip(canvas, 0, 1) * 255).astype(np.uint8)).save(path, optimize=True)


def load_meta():
    if META.exists():
        return json.loads(META.read_text(encoding="utf-8"))
    return {"_note": "tools/import_vfx_ai.py 가 쓴다. 손으로 고치지 않는다. ChatGPT 로 뽑은 이펙트 재료. 층은 흰색 + 알파 512px "
                     "(stroke 먹 · gold 금빛 더하기 · core 흰빛 더하기). sizeMeters 는 한 변이 몇 m, 텍스처 가운데가 기준점. "
                     "progress 는 그어지는 방향(radial 가운데서 바깥 · down 위→아래 · up · right 왼→오른 · left).",
            "textures": []}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--only")
    ap.add_argument("--inbox", help="art_inbox 가 들어 있는 폴더(기본: 저장소 루트)")
    ap.add_argument("--reject", nargs=2, metavar=("ID", "REASON"), help="눈 검수에서 떨어뜨림")
    args = ap.parse_args(argv)
    inbox = Path(args.inbox) if args.inbox else ROOT
    rows = [json.loads(l) for l in QUEUE.read_text(encoding="utf-8").splitlines() if l.strip()]
    meta = load_meta()
    by_name = {t["name"]: t for t in meta["textures"]}

    def save():
        meta["textures"] = sorted(by_name.values(), key=lambda t: t["name"])
        OUT.mkdir(parents=True, exist_ok=True)
        META.write_text(json.dumps(meta, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        QUEUE.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")

    if args.reject:
        rid, reason = args.reject
        for row in rows:
            if row["id"] == rid:
                row.update(status="rejected", reason=reason, tries=int(row.get("tries", 0)) + 1)
        t = by_name.pop(rid, None)
        for layer in (t or {}).get("layers", []):
            (OUT / f"{rid}_{layer}.png").unlink(missing_ok=True)
            (OUT / f"{rid}_{layer}.png.meta").unlink(missing_ok=True)
        save()
        print(f"떨어뜨림 {rid}: {reason}")
        return 0

    shown, done, bad = [], 0, 0
    for row in rows:
        if not row["kind"].startswith("vfx_") or row.get("status") != "generated":
            continue
        if args.only and row["id"] != args.only:
            continue
        src = inbox / row["save_as"]
        if not src.exists():
            print(f"없음 {row['id']}: {src}")
            continue
        im = Image.open(src).convert("RGB")
        if max(im.size) > 1024:
            s = 1024 / max(im.size)
            im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
        rgb = np.asarray(im, np.float32) / 255
        layers, problems, stats = key(row["kind"], rgb)
        if stats["edge"] > 0.04 and not row.get("edgeOk"):
            problems.append(f"그림이 가장자리에 걸렸다({stats['edge']:.1%})")
        row["check"] = stats
        if problems:
            row.update(status="rejected", reason=" · ".join(problems), tries=int(row.get("tries", 0)) + 1)
            print(f"탈락 {row['id']}: {row['reason']}")
            bad += 1
            continue
        main_name = "stroke" if row["kind"] == "vfx_ink" else "gold"
        sq = square(layers, layers[main_name])
        OUT.mkdir(parents=True, exist_ok=True)
        for name, a in sq.items():
            white_alpha_png(a, OUT / f"{row['id']}_{name}.png")
        by_name[row["id"]] = {"name": row["id"], "kind": row["kind"].replace("vfx_", "ai_"), "sizeMeters": float(row.get("sizeM", 2.0)),
                              "layers": list(sq.keys()), "progress": row.get("progress", "radial"), "use": row.get("use", "")}
        row.update(status="imported", picked=dt.date.today().isoformat())
        row.pop("reason", None)
        shown.append((row, sq, np.asarray(im)))
        done += 1
        print(f"반영 {row['id']} → Assets/Art/VFX/AI/{row['id']}_{{{','.join(sq)}}}.png {stats}")
    if shown:
        PREVIEW.mkdir(parents=True, exist_ok=True)
        out = PREVIEW / f"ai_{dt.datetime.now().strftime('%Y-%m-%d_%H%M')}.png"
        sheet(shown, out)
        print(f"미리보기 {out.relative_to(ROOT)}")
    save()
    print(f"반영 {done} · 탈락 {bad}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
