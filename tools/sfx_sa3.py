"""Stable Audio 3 Small SFX 로 효과음 후보를 뽑는다 (backlog SFX-07·09).

회사 PC 의 E:\\Unity\\BloodRain-sfx\\ 에 깐 ComfyUI(0.44, CUDA torch)를 HTTP 로 부른다.
ComfyUI 는 따로 띄워 둔다(이 스크립트는 서버를 켜지 않는다):

    E:\\Unity\\BloodRain-sfx\\venv\\Scripts\\python.exe main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch
    (작업 폴더 E:\\Unity\\BloodRain-sfx\\ComfyUI)

쓰는 법:
    python tools/sfx_sa3.py "<영어 문구>" --seconds 1.5 --count 8 --name sfx_wpn_sword
    python tools/sfx_sa3.py --queue [--milestone M2] [--only <id>]   # 큐의 ai·todo 줄을 차례로 뽑아 art_inbox/sfx/<id>/ 에(2026-11-17)
결과는 ComfyUI output/sfx/<name>_000NN.flac 에 떨어지고, 경로를 한 줄씩 찍는다.

알아 둘 것(2026-11-05 시험):
- 그래프는 ComfyUI 공식 템플릿(audio_stable_audio_3_medium)과 같다. 체크포인트 하나가 모델과 VAE 를 같이 갖고,
  글 인코더는 t5gemma 를 CLIPLoader type=stable_audio 로 읽는다. 8스텝, cfg 1, lcm, simple.
- EmptyLatentAudio 는 Stable Audio 1 비율(2048)로 길이를 잡는데 SA3 는 4096 이라 소리가 두 배 길게 나온다.
  그래서 원하는 초의 절반을 넣는다(seconds_to_latent).
- 소리 하나에 1~2초(RTX 3080 Ti). 4개 묶음이 4~6초.
- 문구 끝에 'dry close-mic, no music, no reverb tail. Length: N seconds' 를 붙이면 꼬리가 짧다.
- 사서 쓰는 라이브러리 소리를 참조 입력으로 넣지 않는다(sfx_sources.md).
"""
from __future__ import annotations

import argparse
import json
import random
import sys
import time
import urllib.error
import urllib.request

CKPT = "stable_audio_3_small_sfx.safetensors"
TEXT_ENCODER = "t5gemma_b_b_ul2.safetensors"
TAIL = "dry close-mic, no music, no reverb tail."


def seconds_to_latent(seconds: float) -> float:
    """EmptyLatentAudio 에 넣을 값. SA3 는 시간 축을 4096 으로 줄이는데 노드는 2048 로 계산해 두 배가 된다."""
    return max(1.0, round(seconds / 2.0, 2))


def build_prompt(text: str, seconds: float, count: int, name: str, seed: int) -> dict:
    """ComfyUI /prompt 에 보낼 API 꼴 그래프."""
    if TAIL.split(",")[0] not in text:
        text = f"{text.rstrip('. ')}, {TAIL} Length: {max(1, round(seconds))} seconds"
    return {
        "25": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": CKPT}},
        "26": {"class_type": "CLIPLoader", "inputs": {"clip_name": TEXT_ENCODER, "type": "stable_audio", "device": "default"}},
        "6": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["26", 0], "text": text}},
        "7": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["26", 0], "text": ""}},
        "11": {"class_type": "EmptyLatentAudio", "inputs": {"seconds": seconds_to_latent(seconds), "batch_size": count}},
        "3": {"class_type": "KSampler", "inputs": {
            "model": ["25", 0], "positive": ["6", 0], "negative": ["7", 0], "latent_image": ["11", 0],
            "seed": seed, "steps": 8, "cfg": 1.0, "sampler_name": "lcm", "scheduler": "simple", "denoise": 1.0}},
        "12": {"class_type": "VAEDecodeAudio", "inputs": {"samples": ["3", 0], "vae": ["25", 2]}},
        "57": {"class_type": "SaveAudioAdvanced", "inputs": {"audio": ["12", 0], "filename_prefix": f"sfx/{name}", "format": "flac"}},
    }


def run(server: str, prompt: dict, timeout: float = 300) -> list[str]:
    req = urllib.request.Request(f"{server}/prompt", data=json.dumps({"prompt": prompt}).encode(),
                                 headers={"Content-Type": "application/json"})
    try:
        pid = json.loads(urllib.request.urlopen(req, timeout=30).read())["prompt_id"]
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"ComfyUI 가 그래프를 받지 않음: {e.code} {e.read().decode()[:800]}")
    t0 = time.time()
    while time.time() - t0 < timeout:
        hist = json.loads(urllib.request.urlopen(f"{server}/history/{pid}", timeout=10).read())
        if pid in hist:
            st = hist[pid].get("status", {})
            if st.get("status_str") == "error":
                raise RuntimeError("생성 실패: " + json.dumps(st.get("messages", []))[:800])
            files = []
            for out in hist[pid].get("outputs", {}).values():
                files += [f"{a.get('subfolder')}/{a.get('filename')}" for a in out.get("audio", [])]
            return files
        time.sleep(1)
    raise TimeoutError("ComfyUI 가 제한 시간 안에 끝내지 않음")


# 같은 문구를 한 번 더 뽑을 때 붙이는 말(차례로 돌려 쓴다)
TWISTS = ["lower pitched and heavier", "higher pitched, lighter and quicker", "distant, with a short echo in a stone hall", "very close and dry, punchy"]


def load_variants(path) -> dict:
    """sfx_queue/variants.json: id → 문구 갈래 목록(또는 {seconds, prompts}). 없으면 빈 것."""
    from pathlib import Path
    path = Path(path)
    if not path.is_file():
        return {}
    return {k: v for k, v in json.loads(path.read_text(encoding="utf-8")).items() if not k.startswith("_")}


def generate(sid: str, prompts: list[str], seconds: float, count: int, seed: int, dst, comfy_output, server: str, runner=None) -> int:
    """문구 갈래마다, 후보 하나마다 따로(시드도 다르게) 뽑아 dst 에 <id>_000NN 으로 옮긴다. 같은 문구 한 묶음은 거의 같은 소리가 나온다(2026-11-17).
    dst 의 옛 후보와 trimmed/ 는 먼저 지운다 — 번호가 섞이면 사람이 고른 번호가 다른 소리를 가리킨다."""
    import shutil
    from pathlib import Path
    dst, comfy_output = Path(dst), Path(comfy_output)
    dst.mkdir(parents=True, exist_ok=True)
    for old in dst.iterdir():
        if old.is_file() and old.name.startswith(sid + "_"):
            old.unlink()
    if (dst / "trimmed").is_dir():
        shutil.rmtree(dst / "trimmed")
    per = max(1, -(-count // len(prompts)))
    n = 0
    for vi, text in enumerate(prompts):
        take = min(per, count - n)
        if take <= 0:
            break
        # 한 번에 하나씩, 시드를 바꿔 가며 뽑는다 — 한 묶음(batch)으로 뽑은 것끼리는 문구가 달라도 거의 같은 소리였다
        # (2026-11-17 밤: 혈인 비명은 같은 묶음 두 개의 스펙트럼이 96% 겹침)
        for k in range(take):
            # 시드만 바꿔서는 거의 같은 소리가 나온다(8걸음 증류 모델이라 문구가 소리를 정한다 — 같은 문구 두 번의 스펙트럼이 95% 겹침).
            # 그래서 갈래의 둘째부터는 문구 끝에 성격을 바꾸는 말을 붙인다
            said = text if k == 0 else f"{text.rstrip('. ')}, {TWISTS[(vi + k - 1) % len(TWISTS)]}"
            files = (runner or run)(server, build_prompt(said, seconds, 1, f"{sid}_v{vi + 1}_{k + 1}", seed + 7919 * vi + 104729 * k))
            for f in files:
                n += 1
                src = comfy_output / f
                shutil.copyfile(src, dst / f"{sid}_{n:05d}{src.suffix}")
    return n


def run_queue(queue, root, comfy_output, server: str = "http://127.0.0.1:8188", only: str | None = None,
              milestone: str | None = None, max_seconds: float = 6.0, runner=None, redo: bool = False,
              variants: dict | None = None) -> list[str]:
    """큐의 route ai · status todo 줄을 차례로 뽑는다(backlog SFX-09). 뽑은 id 목록을 돌려준다.
    줄마다 candidates 개를 뽑아 save_dir(art_inbox/sfx/<id>/)로 옮기고 status 를 generated 로. 문구 갈래(variants)가 있으면 갈래마다 나눠 뽑는다.
    redo 면 이미 generated 인 줄도 다시 뽑는다(picked·imported 는 건드리지 않는다).
    max_seconds 보다 긴 줄(빗소리 루프 20초)은 건너뛴다 — 길이가 달라 따로 본다. runner 는 시험용(없으면 run)."""
    from pathlib import Path
    queue, root, comfy_output = Path(queue), Path(root), Path(comfy_output)
    variants = variants or {}
    rows = [json.loads(l) for l in queue.read_text(encoding="utf-8").splitlines() if l.strip()]
    done = []
    for row in rows:
        if row.get("route") != "ai" or row.get("status") not in (("todo", "generated") if redo else ("todo",)):
            continue
        if (only and row["id"] != only) or (milestone and row.get("milestone") != milestone):
            continue
        if float(row.get("seconds", 1.5)) > max_seconds:
            print(f"  건너뜀 {row['id']}: {row.get('seconds')}초(긴 것은 따로)")
            continue
        seed = int(row["seed"]) if row.get("seed") is not None else random.randint(1, 2**40)
        v = variants.get(row["id"])
        prompts = (v["prompts"] if isinstance(v, dict) else v) if v else [row["prompt"]]
        dst = root / (row.get("save_dir") or f"art_inbox/sfx/{row['id']}/")
        n = generate(row["id"], prompts, float(row["seconds"]), int(row.get("candidates", 8)), seed, dst, comfy_output, server, runner)
        row["status"] = "generated"
        done.append(row["id"])
        print(f"  {row['id']}: {n}개(문구 {len(prompts)}갈래) → {dst}")
        # 한 줄 끝날 때마다 적는다(도중에 끊겨도 뽑은 데까지 남게)
        queue.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")
    return done


def run_also(sid: str, root, comfy_output, variants: dict, server: str = "http://127.0.0.1:8188", count: int = 8, runner=None) -> int:
    """큐의 길(route)과 상관없이 variants.json 의 묶음({seconds, prompts})으로 후보만 뽑아 다듬어 둔다(큐는 안 고친다).
    코드로 구운 소리(진기 흡수음)를 AI 후보와 견주어 들을 때 쓴다. 다듬은 것은 art_inbox/sfx/<id>/trimmed/ 에."""
    from pathlib import Path
    import postprocess_sfx as pp
    v = variants[sid]
    root = Path(root)
    dst = root / "art_inbox" / "sfx" / sid
    n = generate(sid, v["prompts"], float(v["seconds"]), count, int(v.get("seed", 4242)), dst, comfy_output, server, runner)
    row = {"id": sid, "seconds": float(v["seconds"]), "loop": False, "category": v.get("category", "reward"), "max_s": float(v.get("max_s", float(v["seconds"]) + 0.15))}
    files = sorted(p for p in dst.iterdir() if p.is_file() and p.name.startswith(sid + "_"))
    for i, src in enumerate(files, 1):
        pp.process_file(src, dst / "trimmed" / f"{sid}_{i:02d}.ogg", row)
    return n


def main(argv: list[str] | None = None) -> int:
    if argv is None:
        argv = sys.argv[1:]
    if argv and argv[0] == "--queue":
        from pathlib import Path
        qp = argparse.ArgumentParser(prog="sfx_sa3.py --queue")
        qp.add_argument("--only")
        qp.add_argument("--milestone")
        qp.add_argument("--redo", action="store_true", help="이미 generated 인 줄도 다시 뽑는다")
        qp.add_argument("--also", help="큐의 길과 상관없이 variants.json 묶음으로 후보만 뽑을 id(예: sfx_xp_absorb)")
        qp.add_argument("--comfy-output", default=r"E:\Unity\BloodRain-sfx\ComfyUI\output")
        qp.add_argument("--server", default="http://127.0.0.1:8188")
        q = qp.parse_args(argv[1:])
        root = Path(__file__).resolve().parent.parent
        variants = load_variants(root / "sfx_queue" / "variants.json")
        if q.also:
            print(f"  {q.also}: {run_also(q.also, root, q.comfy_output, variants, q.server)}개 뽑아 다듬음")
            return 0
        done = run_queue(root / "sfx_queue" / "queue.jsonl", root, q.comfy_output, q.server, q.only, q.milestone, redo=q.redo, variants=variants)
        print(f"{len(done)}줄 뽑음. 다음: python tools/postprocess_sfx.py --candidates")
        return 0
    ap = argparse.ArgumentParser()
    ap.add_argument("text")
    ap.add_argument("--seconds", type=float, default=1.5, help="원하는 실제 길이(초)")
    ap.add_argument("--count", type=int, default=8)
    ap.add_argument("--name", default="sfx_try")
    ap.add_argument("--seed", type=int, default=None, help="같은 결과가 필요하면 고정")
    ap.add_argument("--server", default="http://127.0.0.1:8188")
    a = ap.parse_args(argv)
    seed = a.seed if a.seed is not None else random.randint(1, 2**40)
    for f in run(a.server, build_prompt(a.text, a.seconds, a.count, a.name, seed)):
        print(f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
