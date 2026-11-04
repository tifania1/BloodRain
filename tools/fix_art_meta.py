"""원화 텍스처 meta 를 Sprite 로 맞춘다(B-12).

Unity 가 처음 들여올 때 만든 meta 는 Default 텍스처(textureType 0)라 SpriteRenderer·Image 에 못 꽂는다.
Unity 를 열지 않고 meta 의 값 몇 줄만 바꾼다. guid 와 나머지 줄은 그대로 두고, spriteID 는 Unity 가 다시 들여올 때 채운다.

  python tools/fix_art_meta.py          # 고칠 것을 고친다
  python tools/fix_art_meta.py --check  # 고칠 것이 있으면 목록을 찍고 1 로 끝낸다

규칙(폴더별):
- 모두: textureType 8(Sprite), spriteMode 1(Single), nPOTScale 0. PPU 는 기획서 13장대로 100 그대로.
- 배경을 지운 그림(Characters·Enemies·Bosses·Props·Portraits·UI): alphaIsTransparency 1, 가장자리 Clamp.
- 세상에 깔리는 그림(Tiles·Map·Props·Characters·Enemies·Bosses): 밉맵 켬. 경지마다 화면이 넓어져(7장) 멀리서 작게 보이기 때문.
- 화면 UI 그림(Cards·Backgrounds·UI·Icons·Portraits): 밉맵 끔.
- Tiles 는 이어 붙이므로 Repeat, 나머지는 Clamp.
Characters/Sheets(참고 시트)·Cutscenes·Store·VFX 는 건드리지 않는다(VFX 는 VfxBootstrap 이 맡음).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "unity" / "BloodRain" / "Assets" / "Art"

ALPHA = {"Characters", "Enemies", "Bosses", "Props", "Portraits", "UI"}
WORLD = {"Tiles", "Map", "Props", "Characters", "Enemies", "Bosses"}
SCREEN = {"Cards", "Backgrounds", "UI", "Icons", "Portraits"}
FOLDERS = ALPHA | WORLD | SCREEN


def wanted(folder: str) -> dict[str, str]:
    wrap = "0" if folder == "Tiles" else "1"
    w = {
        "textureType": "8",
        "spriteMode": "1",
        "nPOTScale": "0",
        "enableMipMap": "1" if folder in WORLD else "0",
        "wrapU": wrap,
        "wrapV": wrap,
        "wrapW": wrap,
    }
    if folder in ALPHA:
        w["alphaIsTransparency"] = "1"
    return w


def targets() -> list[tuple[Path, str]]:
    out = []
    for folder in sorted(FOLDERS):
        d = ART / folder
        if not d.is_dir():
            continue
        for meta in sorted(d.rglob("*.meta")):
            rel = meta.relative_to(d)
            if rel.parts[0] == "Sheets":
                continue
            if meta.with_suffix("").suffix.lower() not in (".png", ".jpg", ".jpeg"):
                continue
            out.append((meta, folder))
    return out


def fix(text: str, want: dict[str, str]) -> str:
    for key, val in want.items():
        # 맨 위 TextureImporter 바로 아래 칸(들여쓰기 2·4칸)만. platformSettings 안 같은 이름은 없다.
        text = re.sub(rf"(?m)^(\s{{2,4}}{key}: )\S+$", rf"\g<1>{val}", text, count=1)
    return text


def main() -> int:
    check = "--check" in sys.argv
    bad = []
    for meta, folder in targets():
        text = meta.read_text(encoding="utf-8")
        new = fix(text, wanted(folder))
        if new != text:
            bad.append(meta)
            if not check:
                meta.write_text(new, encoding="utf-8", newline="\n")
    for m in bad:
        print(("고칠 것 " if check else "고침 ") + str(m.relative_to(ROOT)))
    return 1 if check and bad else 0


if __name__ == "__main__":
    sys.exit(main())
