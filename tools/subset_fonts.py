#!/usr/bin/env python3
"""게임에 들어가는 글꼴을 쓰는 글자만 남겨 줄인다(WEB-01, 2026-11-19).

왜: TMP 동적 글꼴은 원본 글꼴 파일을 빌드에 통째로 싣는다. 원본 넷(Noto Serif KR·SC·TC, Yuji Boku)이
35.7MB 라 웹 판이 받는 양(76MB)의 큰 몫이었다. 쓰는 글자만 남기면 4.5MB 쯤.

원본: tools/fonts_src/ (빌드에 안 들어가는 자리). 결과: unity/BloodRain/Assets/Art/Fonts/ 에 같은 이름으로.
.meta 는 그대로라 guid 가 안 바뀌고, 글꼴 에셋(…SDF.asset)은 그대로 이 파일을 가리킨다.

남기는 글자:
  - Resources/Data 의 JSON 전부(현지화 strings 포함)와 Scripts 의 .cs 에 나오는 글자
  - 영문·라틴-1·일반 문장부호·CJK 문장부호·전각 꼴
  - KR 글꼴만: KS X 1001 한글 2350자(자주 쓰는 한글은 대사를 새로 써도 거의 다 있게)

데이터에 새 글자(새 한자 등)를 넣으면 테스트 FontSubsetTests 가 빠진 글자를 알려 준다. 그때 이걸 다시 돌린다:
    pip install fonttools
    python tools/subset_fonts.py
그 뒤 PC Unity 에서 동적 글꼴 에셋을 'Clear Dynamic Data' 하면 깔끔하다(빌드는 ClearDynamicDataOnBuild 가 비운다).
"""
import glob
import os
import sys

from fontTools import subset
from fontTools.ttLib import TTFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "tools", "fonts_src")
DST = os.path.join(ROOT, "unity", "BloodRain", "Assets", "Art", "Fonts")
ASSETS = os.path.join(ROOT, "unity", "BloodRain", "Assets")

FONTS = [
    "NotoSerifKR-Regular.otf",
    "NotoSerifSC-Regular.otf",
    "NotoSerifTC-Regular.otf",
    "YujiBoku-Regular.ttf",
]

RANGES = [(0x20, 0x7E), (0xA0, 0xFF), (0x2000, 0x206F), (0x3000, 0x303F), (0xFF00, 0xFF6F)]


def text_chars():
    files = glob.glob(os.path.join(ASSETS, "Resources", "Data", "**", "*.json"), recursive=True)
    files += glob.glob(os.path.join(ASSETS, "Scripts", "**", "*.cs"), recursive=True)
    chars = set()
    for p in sorted(files):
        with open(p, encoding="utf-8") as f:
            chars |= set(f.read())
    for a, b in RANGES:
        chars |= {chr(c) for c in range(a, b + 1)}
    return {c for c in chars if c >= " "}


def ks_hangul():
    out = set()
    for hi in range(0xB0, 0xC9):
        for lo in range(0xA1, 0xFF):
            try:
                out.add(bytes([hi, lo]).decode("cp949"))
            except UnicodeDecodeError:
                pass
    return out


def main():
    chars = text_chars()
    ks = ks_hangul()
    total_in = total_out = 0
    for name in FONTS:
        src = os.path.join(SRC, name)
        dst = os.path.join(DST, name)
        want = chars | (ks if "KR" in name else set())
        font = TTFont(src)
        opt = subset.Options()
        opt.layout_features = ["*"]
        opt.name_IDs = ["*"]
        opt.name_languages = ["*"]
        opt.notdef_outline = True
        opt.glyph_names = False
        s = subset.Subsetter(opt)
        s.populate(text="".join(sorted(want)))
        s.subset(font)
        font.save(dst)
        a, b = os.path.getsize(src), os.path.getsize(dst)
        total_in += a
        total_out += b
        print(f"{name}: {a / 1e6:.2f}MB -> {b / 1e6:.2f}MB")
    print(f"합계 {total_in / 1e6:.1f}MB -> {total_out / 1e6:.1f}MB (글자 {len(chars)}개 + KR 한글 {len(ks)}자)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
