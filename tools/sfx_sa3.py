"""Stable Audio 3 Small SFX 로 효과음 후보를 뽑는다 (backlog SFX-07·09).

회사 PC 의 E:\\Unity\\BloodRain-sfx\\ 에 깐 ComfyUI(0.44, CUDA torch)를 HTTP 로 부른다.
ComfyUI 는 따로 띄워 둔다(이 스크립트는 서버를 켜지 않는다):

    E:\\Unity\\BloodRain-sfx\\venv\\Scripts\\python.exe main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch
    (작업 폴더 E:\\Unity\\BloodRain-sfx\\ComfyUI)

쓰는 법:
    python tools/sfx_sa3.py "<영어 문구>" --seconds 1.5 --count 8 --name sfx_wpn_sword
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


def main(argv: list[str] | None = None) -> int:
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
