"""코드로 되는 효과음을 굽는다 (backlog SFX-02, sfx_research.md 4절 2).

sfx_queue/queue.jsonl 의 route synth 줄(징·풍경·범종·심장)을 numpy 로 만든다. 같은 시드면 같은 소리가 나온다.
악기는 배음 표(기본음에 대한 비율, 세기, 감쇠 시간)로 적고, 칠 때마다 배음의 높이·세기를 조금씩 흔들어
한 소리에 candidates 벌(보통 5)을 굽는다. Unity 는 이 벌들을 Audio Random Container 에 넣어 번갈아 튼다.
  - 맥놀이: 큰 쇠붙이(징·범종)는 배음마다 높이가 조금 다른 짝을 하나 더 두어 '웅웅' 하고 울게 한다
  - 친 세기별 밝기: 세게 칠수록 높은 배음이 크고 빨리 줄어든다(strike 0~1)
  - 진기 흡수 풍경은 5음 음계(궁상각치우)로 8음을 미리 굽는다(실행 중 음높이를 바꾸면 음색이 뭉개짐)

흐름: 굽기 → art_inbox/sfx/<id>/<id>_NN.wav (후보, 커밋 안 함) → postprocess_sfx.py 와 같은 다듬기
      → unity/BloodRain/Assets/Audio/SFX/<id>_<k>.ogg(풍경은 <id>_n<음>_<k>.ogg) + sfx.json, 큐는 imported.
사람이 듣고 버릴 벌이 있으면 그 줄의 picked 를 고쳐 다시 돌린다(없으면 전부).

쓰는 법(저장소 루트에서): python tools/synth_sfx.py [--only <id>] [--dry-run]
필요: numpy, ffmpeg(libvorbis). 테스트: python -m unittest discover -s tools/tests
"""
from __future__ import annotations

import argparse
import sys
import wave
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import postprocess_sfx as ps  # noqa: E402

RATE = ps.RATE

# 배음 표: (기본음에 대한 비율, 세기, 감쇠 시간 초). 비율은 둥근 쇠붙이 악기의 흔한 값에서 잡았다.
GONG = [(1.0, 1.0, 2.4), (1.48, 0.55, 1.8), (2.0, 0.45, 1.4), (2.42, 0.35, 1.1), (2.91, 0.28, 0.9),
        (3.62, 0.2, 0.7), (4.41, 0.14, 0.5), (5.3, 0.1, 0.35)]
TEMPLE_BELL = [(0.5, 0.7, 6.0), (1.0, 1.0, 4.5), (1.19, 0.45, 3.2), (1.5, 0.35, 2.6), (2.0, 0.4, 2.2),
               (2.52, 0.22, 1.6), (2.66, 0.18, 1.4), (3.0, 0.12, 1.1), (4.07, 0.08, 0.7)]
CHIME = [(1.0, 1.0, 0.35), (2.76, 0.38, 0.18), (5.40, 0.16, 0.1), (8.93, 0.06, 0.06)]
PENTATONIC = [1.0, 9 / 8, 5 / 4, 3 / 2, 5 / 3]  # 궁상각치우

# id → 굽는 법. 기본음(Hz), 친 세기, 맥놀이 폭(Hz)은 소리를 듣고 잡은 시작값이다(사람이 듣고 바꾼다).
RECIPES = {
    "sfx_boss_appear": {"kind": "metal", "partials": GONG, "f0": 98.0, "strike": 0.9, "beat_hz": 1.6, "glide": 0.012},
    "sfx_levelup": {"kind": "metal", "partials": GONG, "f0": 196.0, "strike": 0.55, "beat_hz": 2.2, "glide": 0.008,
                    "decay_scale": 0.7},
    "sfx_gimmick_bell": {"kind": "metal", "partials": TEMPLE_BELL, "f0": 130.8, "strike": 0.7, "beat_hz": 0.9,
                         "glide": 0.0, "thump": 0.6},
    "sfx_xp_absorb": {"kind": "chime", "partials": CHIME, "f0": 1046.5, "notes": 8, "strike": 0.5},
    "sfx_low_health_heart": {"kind": "heart", "f0": 55.0, "gap_s": 0.26},
}


# ---------- 소리 ----------

def _env(t: np.ndarray, tau: float, attack_s: float = 0.002) -> np.ndarray:
    return np.minimum(1.0, t / attack_s) * np.exp(-t / tau)


def metal(seconds: float, partials, f0: float, strike: float, beat_hz: float, rng: np.random.Generator,
          glide: float = 0.0, decay_scale: float = 1.0, thump: float = 0.0) -> np.ndarray:
    """징·범종. 배음마다 맥놀이 짝을 두고, 세게 칠수록 높은 배음이 크다. glide 는 징 특유의 음높이가 조금 오르는 몫."""
    t = np.arange(int(seconds * RATE)) / RATE
    out = np.zeros_like(t)
    for ratio, amp, tau in partials:
        r = ratio * (1 + rng.normal(0, 0.003))
        bright = (1 + strike) ** (ratio - 1) / (1 + strike)  # 높은 배음일수록 친 세기를 더 탄다
        a = amp * bright * (1 + rng.normal(0, 0.12))
        d = tau * decay_scale * (1.15 - 0.3 * strike * min(1.0, ratio / 4))
        f = f0 * r * (1 + glide * (1 - np.exp(-t / 0.4)))
        phase = 2 * np.pi * np.cumsum(f) / RATE
        bp = 2 * np.pi * (beat_hz * (0.6 + 0.8 * rng.random())) * t
        out += a * _env(t, d) * (np.sin(phase) + 0.6 * np.sin(phase + bp + rng.random() * 6.28)) / 1.6
    # 채로 친 순간의 짧은 소음
    n = int(0.03 * RATE)
    click = rng.normal(0, 1, n) * np.exp(-np.arange(n) / (0.006 * RATE)) * (0.15 + 0.25 * strike)
    out[:n] += click
    if thump:  # 범종 당목(나무)의 둔탁한 첫소리
        m = int(0.12 * RATE)
        tt = np.arange(m) / RATE
        out[:m] += thump * np.sin(2 * np.pi * (f0 * 0.42) * tt) * np.exp(-tt / 0.03)
    return out


def chime(seconds: float, partials, f0: float, strike: float, rng: np.random.Generator) -> np.ndarray:
    t = np.arange(int(seconds * RATE)) / RATE
    out = np.zeros_like(t)
    for ratio, amp, tau in partials:
        r = ratio * (1 + rng.normal(0, 0.002))
        a = amp * (1 + strike) ** (ratio / 4) * (1 + rng.normal(0, 0.1))
        out += a * _env(t, tau * (1 + rng.normal(0, 0.08)), 0.001) * np.sin(2 * np.pi * f0 * r * t + rng.random() * 6.28)
    return out


def heart(seconds: float, f0: float, gap_s: float, rng: np.random.Generator) -> np.ndarray:
    """심장 두 박(쿵-쿵). 낮은 사인이 아래로 떨어지며 빨리 준다. 둘째 박은 조금 작고 높다."""
    t = np.arange(int(seconds * RATE)) / RATE
    out = np.zeros_like(t)
    for start, amp, fm in ((0.0, 1.0, 1.0), (gap_s * (1 + rng.normal(0, 0.04)), 0.7, 1.15)):
        tt = t - start
        on = tt >= 0
        x = tt[on]
        f = f0 * fm * (1 + rng.normal(0, 0.03)) * (1 + 0.8 * np.exp(-x / 0.02))
        phase = 2 * np.pi * np.cumsum(f) / RATE
        out[on] += amp * (1 + rng.normal(0, 0.08)) * np.minimum(1, x / 0.004) * np.exp(-x / 0.07) * np.sin(phase)
    return out


def note_freq(f0: float, k: int) -> float:
    """5음 음계 k번째 음(0부터). 다섯 음을 넘으면 한 옥타브 위."""
    return f0 * PENTATONIC[k % 5] * 2 ** (k // 5)


def bake(row: dict, variant: int, note: int = 0) -> np.ndarray:
    """줄 하나의 variant 번째 벌(0부터). 시드 = 줄 seed + 음*100 + 벌."""
    rec = RECIPES[row["id"]]
    rng = np.random.default_rng(int(row["seed"]) + note * 100 + variant)
    sec = float(row["seconds"])
    if rec["kind"] == "metal":
        x = metal(sec, rec["partials"], rec["f0"], rec["strike"] * (1 + rng.normal(0, 0.1)), rec["beat_hz"], rng,
                  rec.get("glide", 0.0), rec.get("decay_scale", 1.0), rec.get("thump", 0.0))
    elif rec["kind"] == "chime":
        x = chime(sec, rec["partials"], note_freq(rec["f0"], note), rec["strike"], rng)
    else:
        x = heart(sec, rec["f0"], rec["gap_s"], rng)
    return x / max(1e-9, float(np.max(np.abs(x)))) * 0.89


def write_wav(x: np.ndarray, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.clip(np.round(x * 32767), -32768, 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())


# ---------- 큐 ----------

def outputs(row: dict) -> list[tuple[int, int, str]]:
    """(음, 벌, Unity 파일 이름). picked 가 있으면 그 벌(1부터)만."""
    rec = RECIPES[row["id"]]
    picked = row.get("picked") or list(range(1, int(row["candidates"]) + 1))
    notes = rec.get("notes", 1)
    res = []
    for n in range(notes):
        for v, k in enumerate(picked, 1):
            stem = f"{row['id']}_n{n + 1}_{v}" if notes > 1 else (row["id"] if len(picked) == 1 else f"{row['id']}_{v}")
            res.append((n, k - 1, f"{stem}.ogg"))
    return res


def run(queue: Path = ps.QUEUE, out_dir: Path = ps.OUT_DIR, root: Path = ps.ROOT, only: str | None = None,
        dry: bool = False) -> int:
    ps.pa.need_ffmpeg()
    rows = ps.read_rows(queue)
    entries = []
    for row in rows:
        if row.get("route") != "synth" or row["id"] not in RECIPES or (only and row["id"] != only):
            continue
        if row.get("status") == "rejected":
            continue
        variants = []
        box = ps.candidate_dir(row, root)
        for n, v, name in outputs(row):
            wav = box / (f"{row['id']}_n{n + 1}_{v + 1:02d}.wav" if RECIPES[row["id"]].get("notes", 1) > 1
                         else f"{row['id']}_{v + 1:02d}.wav")
            write_wav(bake(row, v, n), wav)
            e = ps.process_file(wav, out_dir / name, row, dry)
            if RECIPES[row["id"]].get("notes", 1) > 1:
                e["note"] = n + 1
            variants.append(e)
        print(f"  {'(dry) ' if dry else ''}{row['id']}: {len(variants)}개, {variants[0]['seconds']}s")
        if not dry:
            row["status"] = "imported"
            row.setdefault("picked", list(range(1, int(row["candidates"]) + 1)))
            entries.append({"id": row["id"], "category": row.get("category", ""), "loop": bool(row.get("loop")),
                            "route": "synth", "variants": variants})
    if entries:
        ps.write_manifest(out_dir, entries)
        ps.write_rows(queue, rows)
    print(f"{len(entries)}개 구움")
    return len(entries)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--only")
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    run(only=args.only, dry=args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
