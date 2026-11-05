"""여러 재료를 겹치는 효과음(sfx_queue route layer)의 후보를 굽는다 (sfx_research.md 4절 7, backlog SFX-11).

줄의 layers 마다 재료를 마련해 같은 순간에 겹친다. 재료마다 offset_s(늦게 시작할 초, 기본 0)와 gain_db(기본 0)를 줄에 적는다.
  synth  : tools/synth_sfx.py 의 LAYER_RECIPES[(id, what)] 로 그 자리에서 굽는다. 후보 k 번째는 시드 seed + k
  ai     : <save_dir>/ai/ 의 AI 후보(sfx_sa3.py 를 출력 폴더 <save_dir>/ai 로 돌린 것). 후보 k 번째(모자라면 처음부터 다시)
  record : 줄의 layers[].file (art_inbox/… 또는 저장소 기준 경로). 사람이 docs/design/sfx_sources.md '씀' 묶음에서 고른다
재료가 하나라도 없으면 그 줄은 건너뛰고 무엇이 없는지 적는다.
후보 k(1..candidates)마다 <save_dir>/<id>_mix_NN.wav 를 쓰고, status todo 였으면 generated 로 바꾼다.
그다음은 다른 줄과 같다: postprocess_sfx.py --candidates → 듣기 페이지(lookdev/sfx_listen.html) → picked → postprocess_sfx.py.
AI 후보를 ai/ 아래 두는 것은 postprocess_sfx.py --candidates 가 save_dir 바로 아래 파일만 후보로 보기 때문이다.

쓰는 법(저장소 루트에서): python tools/layer_sfx.py [--only <id>] [--inbox <art_inbox 폴더>] [--dry-run]
필요: numpy, ffmpeg. 테스트: python -m unittest discover -s tools/tests
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import postprocess_sfx as ps  # noqa: E402
import synth_sfx as ss  # noqa: E402

RATE = ps.RATE


def load(path: Path) -> np.ndarray:
    a = ps.decode(path)
    return np.asarray(a, dtype=np.float64) / 32768.0


def material(row: dict, layer: dict, k: int, root: Path, inbox: Path | None) -> tuple[np.ndarray | None, str]:
    """재료 한 층의 후보 k(0부터). 없으면 (None, 까닭)."""
    src = layer.get("from")
    if src == "synth":
        if (row["id"], layer["what"]) not in ss.LAYER_RECIPES:
            return None, f"synth_sfx.LAYER_RECIPES 에 ({row['id']}, {layer['what']}) 없음"
        return ss.bake_layer(row, layer["what"], k), ""
    if src == "ai":
        d = ps.candidate_dir(row, root, inbox) / "ai"
        files = [p for p in sorted(d.iterdir(), key=lambda p: (ps.candidate_number(p), p.name))
                 if p.is_file() and p.suffix.lower() in ps.SOURCE_EXTS] if d.is_dir() else []
        if not files:
            return None, f"AI 후보 없음({d})"
        return load(files[k % len(files)]), ""
    if src == "record":
        rel = layer.get("file")
        if not rel:
            return None, f"녹음 재료 file 칸 없음({layer.get('what', '')})"
        p = (inbox / rel[len("art_inbox/"):]) if inbox is not None and rel.startswith("art_inbox/") else root / rel
        if not p.is_file():
            return None, f"녹음 재료 없음({p})"
        return load(p), ""
    return None, f"모르는 재료 갈래 {src}"


def mix(parts: list[tuple[np.ndarray, float, float]]) -> np.ndarray:
    """(소리, offset_s, gain_db) 들을 겹친다. 최고점을 0.89 로 맞춘다(다듬기가 -1 dBTP 로 다시 맞춘다)."""
    n = max(int(round(off * RATE)) + len(x) for x, off, _ in parts)
    out = np.zeros(n)
    for x, off, g in parts:
        i = int(round(off * RATE))
        out[i:i + len(x)] += x * 10 ** (g / 20)
    return out / max(1e-9, float(np.max(np.abs(out)))) * 0.89


def run(queue: Path = ps.QUEUE, root: Path = ps.ROOT, inbox: Path | None = None, only: str | None = None,
        dry: bool = False) -> int:
    ps.pa.need_ffmpeg()
    rows = ps.read_rows(queue)
    done = 0
    for row in rows:
        if row.get("route") != "layer" or (only and row["id"] != only) or row.get("status") in ("rejected", "imported"):
            continue
        out = []
        why = ""
        for k in range(int(row["candidates"])):
            parts = []
            for layer in row.get("layers", []):
                x, why = material(row, layer, k, root, inbox)
                if x is None:
                    break
                parts.append((x, float(layer.get("offset_s", 0)), float(layer.get("gain_db", 0))))
            if why:
                break
            out.append(mix(parts))
        if why:
            print(f"  건너뜀 {row['id']}: {why}")
            continue
        box = ps.candidate_dir(row, root, inbox)
        if not dry:
            for k, x in enumerate(out, 1):
                ss.write_wav(x, box / f"{row['id']}_mix_{k:02d}.wav")
            if row.get("status") == "todo":
                row["status"] = "generated"
        print(f"  {'(dry) ' if dry else ''}{row['id']}: 후보 {len(out)}개, {len(out[0]) / RATE:.2f}s")
        done += 1
    if done and not dry:
        ps.write_rows(queue, rows)
    print(f"{done}줄 겹침")
    return done


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    ap.add_argument("--inbox", type=Path)
    ap.add_argument("--dry-run", action="store_true")
    a = ap.parse_args()
    run(inbox=a.inbox, only=a.only, dry=a.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
