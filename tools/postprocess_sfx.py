"""효과음 후보(Stable Audio·합성·녹음 재료)를 게임용 모노 ogg 로 다듬는다 (backlog SFX-03).

postprocess_audio.py(배경음)를 본떴다. 한 소리를
  1) 30Hz 아래를 자르고(ffmpeg highpass, 2차) 48kHz 모노로 읽는다
  2) 앞 무음을 잘라 첫 타격이 5ms 안에 오게 한다(문턱 -50dBFS 를 처음 넘는 곳 앞 2ms 만 남기고, 그 2ms 는 0 에서 올라옴)
  3) 뒤 무음을 자른다(20ms 남김)
  4) 최대 길이(줄의 max_s, 없으면 갈래 기본값)를 넘으면 자르고 끝을 줄인다. 루프 소리(빗소리)는 배경음처럼 끝을 앞머리에 겹친다
  5) 루프가 아니면 끝 10ms 를 0 까지 줄여 딸깍 소리를 없앤다
  6) 최고점을 맞춘다: 실제 최고점(ffmpeg ebur128 true peak)이 -1 dBTP 가 되게 올리거나 내린 뒤 갈래 이득을 더한다.
     0.4초도 안 되는 소리가 많아 통합 LUFS 대신 최고점으로 맞춘다(sfx_research.md 4절 4).
     갈래 이득(CATEGORY_GAIN_DB)은 지금 모두 0 이다. 사람이 게임 안에서 섞어 듣고 정한다(같은 절 9).
  7) Vorbis 48kHz 모노 ogg

두 가지로 쓴다(저장소 루트에서):
  python tools/postprocess_sfx.py --candidates [--only <id>] [--inbox <art_inbox 폴더>]
      sfx_queue/queue.jsonl 의 status generated 줄마다 art_inbox/sfx/<id>/ 의 후보 전부를 다듬어
      art_inbox/sfx/<id>/trimmed/<id>_NN.ogg 로 둔다. 사람이 듣고 고르는 페이지(SFX-04)는 이 파일을 읽는다.
  python tools/postprocess_sfx.py [--dry-run] [--only <id>] [--inbox ...]
      status picked 줄에서 picked 번호(후보 번호, 1부터)의 후보를 다듬어
      unity/BloodRain/Assets/Audio/SFX/<id>_<k>.ogg(k=1,2,...)로 넣고 sfx.json 을 고친 뒤 imported 로 바꾼다.
      한 벌이면 <id>.ogg 하나. Unity 는 id 마다 여러 벌을 Audio Random Container 에 넣는다.
  python tools/postprocess_sfx.py --file <입력> --out <출력.ogg> [--category weapon] [--max-s 1.2] [--loop]
      큐 없이 파일 하나만.
필요: ffmpeg(libvorbis 포함). 파이썬 표준 라이브러리만 쓴다. 테스트: python -m unittest discover -s tools/tests
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from array import array
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import postprocess_audio as pa  # noqa: E402  같은 샘플 함수(무음 자르기·페이드·루프 이음매)를 쓴다

ROOT = Path(__file__).resolve().parent.parent
QUEUE = ROOT / "sfx_queue" / "queue.jsonl"
OUT_DIR = ROOT / "unity" / "BloodRain" / "Assets" / "Audio" / "SFX"
MANIFEST = "sfx.json"

RATE = 48000
CH = 1
HIGHPASS_HZ = 30
SILENCE_DB = -50.0
LEAD_PAD_S = 0.002      # 첫 타격 앞에 남기는 몫. 5ms 안
TAIL_PAD_S = 0.02
END_FADE_S = 0.01
CUT_FADE_S = 0.05       # 최대 길이로 자를 때 끝 페이드
LOOP_FADE_S = 0.5       # 빗소리 같은 루프 효과음의 이음매
PEAK_DBTP = -1.0
VORBIS_Q = 5
SOURCE_EXTS = (".flac", ".wav", ".ogg", ".mp3")

# 갈래(sfx_queue 의 category, sfx_list.md 절과 같음): 기본 최대 길이(초)와 이득(dB)
CATEGORY_MAX_S = {"weapon": 1.5, "enemy": 2.0, "reward": 3.0, "scene": 4.0, "ambient": 30.0, "ui": 0.5}
CATEGORY_GAIN_DB = {k: 0.0 for k in CATEGORY_MAX_S}  # 사람이 게임 안에서 섞어 듣고 정한다(sfx_research.md 4절 9)


# ---------- 샘플 ----------

def trim_lead(samples: array, rate: int = RATE, threshold_db: float = SILENCE_DB, pad_s: float = LEAD_PAD_S) -> array:
    """앞 무음을 잘라 문턱을 처음 넘는 샘플이 pad_s 뒤에 오게 한다. 남긴 pad 는 0 에서 선형으로 올라온다."""
    limit = 32768 * 10 ** (threshold_db / 20)
    first = next((i for i, v in enumerate(samples) if abs(v) > limit), None)
    if first is None:
        return array("h")
    pad = int(pad_s * rate)
    a = max(0, first - pad)
    out = array("h", samples[a:])
    k = first - a
    for i in range(k):
        out[i] = pa._clip(out[i] * i / k)
    return out


def trim_tail(samples: array, rate: int = RATE, threshold_db: float = SILENCE_DB, pad_s: float = TAIL_PAD_S) -> array:
    limit = 32768 * 10 ** (threshold_db / 20)
    last = next((i for i in range(len(samples) - 1, -1, -1) if abs(samples[i]) > limit), None)
    if last is None:
        return array("h")
    return samples[:min(len(samples), last + 1 + int(pad_s * rate))]


def lead_silence_s(samples: array, rate: int = RATE, threshold_db: float = SILENCE_DB) -> float:
    """문턱을 처음 넘을 때까지의 초. 검수·테스트용."""
    limit = 32768 * 10 ** (threshold_db / 20)
    first = next((i for i, v in enumerate(samples) if abs(v) > limit), len(samples))
    return first / rate


def max_seconds(row: dict) -> float:
    return float(row.get("max_s") or CATEGORY_MAX_S.get(row.get("category", ""), 2.0))


def shape(samples: array, row: dict, rate: int = RATE) -> array:
    """2~5단계. 결과가 비면 ValueError(받은 파일이 무음)."""
    s = trim_tail(trim_lead(samples, rate), rate)
    if len(s) < int(0.02 * rate):
        raise ValueError(f"{row.get('id', '?')}: 무음을 자르고 나니 20ms 도 안 남는다")
    limit = int(max_seconds(row) * rate)
    loop = bool(row.get("loop"))
    if len(s) > limit:
        s = s[:limit] if loop else pa.fade_out(s[:limit], CUT_FADE_S, CH, rate)
    if loop:
        s = pa.crossfade_loop(s, CH, rate, LOOP_FADE_S)
    else:
        s = pa.fade_out(s, END_FADE_S, CH, rate)
    return s


def gain_db(true_peak_db: float, category: str = "", extra_db: float = 0.0) -> float:
    """최고점을 PEAK_DBTP 에 맞추는 이득 + 갈래 이득 + 줄의 gain_db. 갈래 이득은 0 이하만 의미가 있다(천장)."""
    g = PEAK_DBTP - true_peak_db + CATEGORY_GAIN_DB.get(category, 0.0) + extra_db
    return round(min(g, PEAK_DBTP - true_peak_db), 2)


# ---------- ffmpeg ----------

def decode(src: Path, rate: int = RATE) -> array:
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(src), "-af", f"highpass=f={HIGHPASS_HZ}:poles=2",
                          "-f", "s16le", "-ac", str(CH), "-ar", str(rate), "-"],
                         check=True, capture_output=True).stdout
    a = array("h")
    a.frombytes(raw)
    if sys.byteorder == "big":
        a.byteswap()
    return a


def true_peak(samples: array, rate: int = RATE) -> float:
    """실제 최고점(dBTP). ffmpeg ebur128 은 짧은 소리에도 최고점을 준다."""
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-f", "s16le", "-ar", str(rate), "-ac", str(CH), "-i", "-",
                        "-af", "ebur128=peak=true", "-f", "null", "-"],
                       input=pa._le_bytes(samples), capture_output=True, check=True)
    text = p.stderr.decode("utf-8", "replace")
    m = re.search(r"True peak:\s*Peak:\s*(-?[\d.]+|-inf)", text)
    if not m or m.group(1) == "-inf":
        raise RuntimeError("최고점을 못 읽었다(무음?)")
    return float(m.group(1))


def encode(samples: array, dst: Path, gain: float, rate: int = RATE) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "s16le", "-ar", str(rate), "-ac", str(CH), "-i", "-",
                    "-af", f"volume={gain}dB,alimiter=limit={10 ** (PEAK_DBTP / 20):.4f}:level=disabled:attack=0.1",
                    "-c:a", "libvorbis", "-q:a", str(VORBIS_Q), str(dst)],
                   input=pa._le_bytes(samples), check=True)


def process_file(src: Path, dst: Path, row: dict, dry: bool = False) -> dict:
    s = shape(decode(src), row)
    tp = true_peak(s)
    g = gain_db(tp, row.get("category", ""), float(row.get("gain_db", 0.0)))
    if not dry:
        encode(s, dst, g)
    return {"file": dst.name, "seconds": round(len(s) / RATE, 3), "sourcePeakDb": tp, "gainDb": g}


# ---------- 큐 ----------

def candidate_dir(row: dict, root: Path = ROOT, inbox: Path | None = None) -> Path:
    rel = row.get("save_dir") or f"art_inbox/sfx/{row['id']}/"
    if inbox is not None and rel.startswith("art_inbox/"):
        return inbox / rel[len("art_inbox/"):]
    return root / rel


def candidates(row: dict, root: Path = ROOT, inbox: Path | None = None) -> list[Path]:
    """후보 파일을 번호 순으로. 이름 끝의 숫자가 후보 번호(sfx_sa3.py 는 <id>_000NN.flac)."""
    d = candidate_dir(row, root, inbox)
    if not d.is_dir():
        return []
    files = [p for p in d.iterdir() if p.is_file() and p.suffix.lower() in SOURCE_EXTS]
    return sorted(files, key=lambda p: (candidate_number(p), p.name))


def candidate_number(p: Path) -> int:
    m = re.search(r"(\d+)$", p.stem)
    return int(m.group(1)) if m else 0


def read_rows(queue: Path) -> list[dict]:
    return [json.loads(l) for l in queue.read_text(encoding="utf-8").splitlines() if l.strip()]


def write_rows(queue: Path, rows: list[dict]) -> None:
    queue.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")


def run_candidates(queue: Path = QUEUE, root: Path = ROOT, inbox: Path | None = None, only: str | None = None) -> int:
    """generated 줄의 후보를 전부 다듬어 trimmed/ 에. 큐는 고치지 않는다."""
    pa.need_ffmpeg()
    n = 0
    for row in read_rows(queue):
        if row.get("status") != "generated" or (only and row["id"] != only):
            continue
        files = candidates(row, root, inbox)
        for i, src in enumerate(files, 1):
            dst = candidate_dir(row, root, inbox) / "trimmed" / f"{row['id']}_{i:02d}.ogg"
            try:
                e = process_file(src, dst, row)
            except (ValueError, RuntimeError) as ex:
                print(f"  건너뜀 {src.name}: {ex}")
                continue
            print(f"  {row['id']} {i:02d} ← {src.name} {e['seconds']}s, 최고점 {e['sourcePeakDb']} → 이득 {e['gainDb']} dB")
            n += 1
    print(f"후보 {n}개 다듬음")
    return n


def run(queue: Path = QUEUE, out_dir: Path = OUT_DIR, root: Path = ROOT, inbox: Path | None = None,
        only: str | None = None, dry: bool = False) -> int:
    """picked 줄을 Assets/Audio/SFX 로. 고른 번호는 후보 번호(1부터, trimmed/ 이름과 같음)."""
    pa.need_ffmpeg()
    rows = read_rows(queue)
    entries = []
    for row in rows:
        if row.get("status") != "picked" or (only and row["id"] != only):
            continue
        files = candidates(row, root, inbox)
        picked = [int(k) for k in row.get("picked", [])]
        if not picked or any(k < 1 or k > len(files) for k in picked):
            print(f"  고른 번호가 후보에 없음 {row['id']}: {picked} (후보 {len(files)}개)")
            continue
        variants = []
        for v, k in enumerate(picked, 1):
            name = f"{row['id']}.ogg" if len(picked) == 1 else f"{row['id']}_{v}.ogg"
            e = process_file(files[k - 1], out_dir / name, row, dry)
            print(f"  {'(dry) ' if dry else ''}{row['id']} 후보 {k} → {name} {e['seconds']}s, 이득 {e['gainDb']} dB")
            variants.append(e)
        if not dry:
            row["status"] = "imported"
            entries.append({"id": row["id"], "category": row.get("category", ""), "loop": bool(row.get("loop")),
                            "variants": variants})
    if entries:
        write_manifest(out_dir, entries)
        write_rows(queue, rows)
    print(f"{len(entries)}개 반영")
    return len(entries)


def write_manifest(out_dir: Path, entries: list[dict]) -> None:
    path = out_dir / MANIFEST
    old = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"sounds": []}
    by_id = {t["id"]: t for t in old.get("sounds", [])}
    for e in entries:
        by_id[e["id"]] = e
    data = {"_note": "tools/postprocess_sfx.py 가 쓴다. 손으로 고치지 않는다(sfx_research.md).",
            "peakDb": PEAK_DBTP, "categoryGainDb": CATEGORY_GAIN_DB,
            "sounds": sorted(by_id.values(), key=lambda t: t["id"])}
    out_dir.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--candidates", action="store_true", help="generated 줄의 후보를 trimmed/ 로")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--only")
    ap.add_argument("--inbox", type=Path, help="art_inbox 폴더(기본: 저장소 안 art_inbox)")
    ap.add_argument("--file", type=Path, help="큐 없이 파일 하나")
    ap.add_argument("--out", type=Path)
    ap.add_argument("--category", default="")
    ap.add_argument("--max-s", type=float)
    ap.add_argument("--loop", action="store_true")
    args = ap.parse_args()
    if args.file:
        if not args.out:
            ap.error("--file 에는 --out 이 필요하다")
        pa.need_ffmpeg()
        row = {"id": args.file.stem, "category": args.category, "max_s": args.max_s, "loop": args.loop}
        e = process_file(args.file, args.out, row, args.dry_run)
        print(f"{args.out} {e['seconds']}s, 최고점 {e['sourcePeakDb']} → 이득 {e['gainDb']} dB")
    elif args.candidates:
        run_candidates(inbox=args.inbox, only=args.only)
    else:
        run(inbox=args.inbox, only=args.only, dry=args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
