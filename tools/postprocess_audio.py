"""Gemini 에서 받은 배경음 MP3 를 게임용 ogg 로 다듬어 Unity Assets/Audio/BGM 으로 옮긴다.

audio_queue/queue.jsonl 에서 status 가 picked(사람이 듣고 고름)인 줄을 찾아, save_as 의 MP3 를
  1) 앞뒤 무음 자르기(-50dBFS 아래가 이어지는 곳, 끝에 50ms 남김)
  2) cut_s 가 있으면 앞에서 그만큼만 남기고 0.5초 페이드아웃
  3) loop 곡은 끝 2초(곡 길이 1/4 이 상한)를 앞머리에 등가 출력 크로스페이드로 겹쳐
     마지막 샘플 다음이 곧 첫 샘플이 되게 이음매를 지움. 루프가 아닌 곡은 끝 50ms 페이드아웃
  4) 소리 크기 맞춤: 통합 소리 크기를 -18 LUFS 로(실제 최고점 -1.5 dBTP 를 넘지 않는 데까지만 올림).
     곡 전체에 같은 이득을 곱하므로 루프 이음매가 다시 벌어지지 않는다
  5) ogg(Vorbis, 48kHz 스테레오)로 unity/BloodRain/Assets/Audio/BGM/<id>.ogg
한 뒤 bgm.json(목록)을 고치고 status 를 imported 로 바꾼다.

쓰는 법 (저장소 루트에서):  python tools/postprocess_audio.py [--dry-run] [--only <id>] [--inbox <art_inbox 폴더>]
  --inbox 를 주면 save_as 의 'art_inbox/' 를 그 폴더로 바꿔 읽는다(PC 작업 폴더가 따로일 때).
필요: ffmpeg·ffprobe 가 PATH 에 있을 것(libvorbis 포함 빌드). 파이썬 표준 라이브러리만 쓴다.
테스트: python -m unittest discover -s tools/tests
"""
from __future__ import annotations

import argparse
import json
import math
import re
import shutil
import subprocess
import sys
from array import array
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
QUEUE = ROOT / "audio_queue" / "queue.jsonl"
OUT_DIR = ROOT / "unity" / "BloodRain" / "Assets" / "Audio" / "BGM"
MANIFEST = "bgm.json"

RATE = 48000
CHANNELS = 2
TARGET_LUFS = -18.0
TRUE_PEAK_DB = -1.5
SILENCE_DB = -50.0
KEEP_PAD_S = 0.05
LOOP_FADE_S = 2.0
CUT_FADE_S = 0.5
END_FADE_S = 0.05
VORBIS_Q = 6


# ---------- 샘플 다루기(인터리브된 16비트 정수, 프레임 = 채널 수만큼의 샘플) ----------

def frames(samples: array, ch: int = CHANNELS) -> int:
    return len(samples) // ch


def _clip(v: float) -> int:
    return -32768 if v < -32768 else 32767 if v > 32767 else int(round(v))


def trim_silence(samples: array, ch: int = CHANNELS, rate: int = RATE,
                 threshold_db: float = SILENCE_DB, pad_s: float = KEEP_PAD_S) -> array:
    """앞뒤로 문턱보다 작은 소리만 이어지는 부분을 자른다. pad_s 만큼은 남긴다. 전부 무음이면 빈 배열."""
    limit = 32768 * 10 ** (threshold_db / 20)
    n = frames(samples, ch)
    first = next((f for f in range(n) if any(abs(samples[f * ch + c]) > limit for c in range(ch))), None)
    if first is None:
        return array("h")
    last = next(f for f in range(n - 1, -1, -1) if any(abs(samples[f * ch + c]) > limit for c in range(ch)))
    pad = int(pad_s * rate)
    a, b = max(0, first - pad), min(n, last + 1 + pad)
    return samples[a * ch:b * ch]


def fade_out(samples: array, seconds: float, ch: int = CHANNELS, rate: int = RATE) -> array:
    """끝 seconds 동안 선형으로 0 까지 줄인다. 마지막 프레임은 0."""
    out = array("h", samples)
    n = frames(out, ch)
    k = min(n, int(seconds * rate))
    for i in range(k):
        g = (k - 1 - i) / max(1, k - 1)
        f = n - k + i
        for c in range(ch):
            out[f * ch + c] = _clip(out[f * ch + c] * g)
    return out


def cut(samples: array, seconds: float, ch: int = CHANNELS, rate: int = RATE, fade_s: float = CUT_FADE_S) -> array:
    """앞에서 seconds 만 남기고 끝을 페이드아웃."""
    head = samples[:int(seconds * rate) * ch]
    return fade_out(head, fade_s, ch, rate)


def loop_fade_frames(n: int, rate: int = RATE, seconds: float = LOOP_FADE_S) -> int:
    return max(1, min(int(seconds * rate), n // 4))


def crossfade_loop(samples: array, ch: int = CHANNELS, rate: int = RATE, seconds: float = LOOP_FADE_S) -> array:
    """끝 k 프레임(꼬리)을 앞 k 프레임에 등가 출력으로 겹치고 꼬리는 버린다.

    결과의 첫 프레임은 원래 꼬리의 첫 프레임이고, 결과의 마지막 프레임은 원래 꼬리 바로 앞 프레임이다.
    그래서 결과를 이어 돌리면 '마지막 → 처음'이 원곡의 이어진 두 프레임과 같다(이음매 없음).
    """
    n = frames(samples, ch)
    k = loop_fade_frames(n, rate, seconds)
    body = array("h", samples[:(n - k) * ch])
    tail = samples[(n - k) * ch:]
    for i in range(k):
        t = i / k
        g_in, g_out = math.sin(t * math.pi / 2), math.cos(t * math.pi / 2)
        for c in range(ch):
            j = i * ch + c
            body[j] = _clip(body[j] * g_in + tail[j] * g_out)
    return body


def gain_db(measured_lufs: float, measured_tp: float,
            target_lufs: float = TARGET_LUFS, ceiling_tp: float = TRUE_PEAK_DB) -> float:
    """목표 소리 크기까지의 이득. 올릴 때는 최고점이 천장을 넘지 않는 데까지만."""
    g = target_lufs - measured_lufs
    return round(min(g, ceiling_tp - measured_tp), 2)


# ---------- ffmpeg ----------

def need_ffmpeg() -> None:
    for tool in ("ffmpeg", "ffprobe"):
        if shutil.which(tool) is None:
            raise SystemExit(f"{tool} 가 PATH 에 없다. ffmpeg(libvorbis 포함)를 설치한다.")


def decode(src: Path, rate: int = RATE, ch: int = CHANNELS) -> array:
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(src), "-f", "s16le", "-ac", str(ch), "-ar", str(rate), "-"],
                         check=True, capture_output=True).stdout
    a = array("h")
    a.frombytes(raw)
    if sys.byteorder == "big":
        a.byteswap()
    return a


def _raw_input(rate: int, ch: int) -> list[str]:
    return ["-f", "s16le", "-ar", str(rate), "-ac", str(ch), "-i", "-"]


def measure(samples: array, rate: int = RATE, ch: int = CHANNELS) -> tuple[float, float]:
    """(통합 소리 크기 LUFS, 실제 최고점 dBTP). ffmpeg loudnorm 측정."""
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", *_raw_input(rate, ch),
                        "-af", f"loudnorm=I={TARGET_LUFS}:TP={TRUE_PEAK_DB}:print_format=json", "-f", "null", "-"],
                       input=_le_bytes(samples), capture_output=True, check=True)
    m = re.search(r"\{[^{}]*\"input_i\"[^{}]*\}", p.stderr.decode("utf-8", "replace"))
    if not m:
        raise RuntimeError("loudnorm 측정값을 못 읽었다")
    j = json.loads(m.group(0))
    return float(j["input_i"]), float(j["input_tp"])


def encode(samples: array, dst: Path, gain: float, rate: int = RATE, ch: int = CHANNELS) -> None:
    dst.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["ffmpeg", "-v", "error", "-y", *_raw_input(rate, ch), "-af", f"volume={gain}dB",
                    "-c:a", "libvorbis", "-q:a", str(VORBIS_Q), str(dst)],
                   input=_le_bytes(samples), check=True)


def _le_bytes(samples: array) -> bytes:
    if sys.byteorder == "big":
        samples = array("h", samples)
        samples.byteswap()
    return samples.tobytes()


# ---------- 큐 ----------

def source_path(row: dict, root: Path = ROOT, inbox: Path | None = None) -> Path:
    rel = row["save_as"]
    if inbox is not None and rel.startswith("art_inbox/"):
        return inbox / rel[len("art_inbox/"):]
    return root / rel


def shape(samples: array, row: dict, rate: int = RATE, ch: int = CHANNELS) -> array:
    """1~3단계: 무음 자르기, 자르기, 루프 이음매."""
    s = trim_silence(samples, ch, rate)
    if frames(s, ch) < rate:  # 1초도 안 남으면 받은 파일이 잘못된 것
        raise ValueError(f"{row['id']}: 무음을 자르고 나니 1초도 안 남는다")
    if row.get("cut_s"):
        s = cut(s, float(row["cut_s"]), ch, rate)
    if row.get("loop"):
        s = crossfade_loop(s, ch, rate)
    elif not row.get("cut_s"):
        s = fade_out(s, END_FADE_S, ch, rate)
    return s


def process(row: dict, out_dir: Path = OUT_DIR, root: Path = ROOT, inbox: Path | None = None,
            dry: bool = False) -> dict | None:
    src = source_path(row, root, inbox)
    if not src.exists():
        print(f"  없음 {row['id']}: {src}")
        return None
    s = shape(decode(src), row)
    lufs, tp = measure(s)
    g = gain_db(lufs, tp)
    dst = out_dir / f"{row['id']}.ogg"
    seconds = round(frames(s) / RATE, 3)
    if dry:
        print(f"  (dry) {row['id']} → {dst.name} {seconds}s, {lufs} LUFS → 이득 {g} dB")
    else:
        encode(s, dst, g)
        print(f"  완료 {row['id']} → {dst.name} {seconds}s, {lufs} LUFS → 이득 {g} dB")
    return {"id": row["id"], "file": dst.name, "use": row["use"], "loop": bool(row.get("loop")),
            "seconds": seconds, "sourceLufs": lufs, "gainDb": g}


def write_manifest(out_dir: Path, entries: list[dict]) -> None:
    path = out_dir / MANIFEST
    old = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {"tracks": []}
    by_id = {t["id"]: t for t in old.get("tracks", [])}
    for e in entries:
        by_id[e["id"]] = e
    data = {"_note": "tools/postprocess_audio.py 가 쓴다. 손으로 고치지 않는다(audio_pipeline.md).",
            "targetLufs": TARGET_LUFS, "truePeakDb": TRUE_PEAK_DB,
            "tracks": sorted(by_id.values(), key=lambda t: t["id"])}
    out_dir.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def run(queue: Path = QUEUE, out_dir: Path = OUT_DIR, root: Path = ROOT, inbox: Path | None = None,
        only: str | None = None, dry: bool = False) -> int:
    need_ffmpeg()
    rows = [json.loads(l) for l in queue.read_text(encoding="utf-8").splitlines() if l.strip()]
    entries = []
    for row in rows:
        if only and row["id"] != only:
            continue
        if row.get("status") != "picked":
            continue
        e = process(row, out_dir, root, inbox, dry)
        if e and not dry:
            row["status"] = "imported"
            entries.append(e)
    if not dry and entries:
        write_manifest(out_dir, entries)
        queue.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
    print(f"{len(entries)}곡 반영")
    return len(entries)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--only")
    ap.add_argument("--inbox", type=Path, help="art_inbox 폴더(기본: 저장소 안 art_inbox)")
    args = ap.parse_args()
    run(inbox=args.inbox, only=args.only, dry=args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
