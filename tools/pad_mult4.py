#!/usr/bin/env python3
"""낱장 원화의 가로·세로를 4의 배수로 맞춘다(투명 여백을 위아래·좌우에 고르게, WEB-01 2026-11-19).

왜: DXT/BC 압축은 가로·세로가 4의 배수여야 한다. 아니면 Unity 가 압축하지 못하고 RGBA32 로 실어
같은 그림이 네 배 무거워진다(Inspector 에 'multiple of 4' 경고). 소품 낱장(Art/Props/Parts)은 split_props.py 가
그림 크기대로 잘라 거의 다 여기 걸렸다. 여백은 많아야 3px 이고 가운데 기준점(0.5, 0.5)은 반 픽셀 안에서 그대로다.

    python tools/pad_mult4.py                 # Art/Props/Parts
    python tools/pad_mult4.py Art/Portraits   # 다른 폴더(Assets 아래 경로)
"""
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "unity" / "BloodRain" / "Assets"


def pad4(img: Image.Image) -> Image.Image:
    w, h = img.size
    W, H = (w + 3) // 4 * 4, (h + 3) // 4 * 4
    if (W, H) == (w, h):
        return img
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(img.convert("RGBA"), ((W - w) // 2, (H - h) // 2))
    return out


def main(argv: list[str]) -> int:
    folders = argv or ["Art/Props/Parts"]
    n = 0
    for f in folders:
        for p in sorted((ASSETS / f).glob("*.png")):
            img = Image.open(p)
            out = pad4(img)
            if out is img:
                continue
            out.save(p, optimize=True)
            n += 1
            print(f"{p.relative_to(ASSETS)}: {img.size} -> {out.size}")
    print(f"고친 그림 {n}장")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
