#!/usr/bin/env python3
"""간체 화면 문구(strings/zh-Hans.json)에서 번체(strings/zh-Hant.json)를 만든다(backlog L-08).

zh-Hans 에 문구를 더하면 번체도 같은 키가 있어야 테스트(HantStringsTests)가 통과한다. 그때 이걸 돌린다.
  pip install opencc
  python3 tools/make_hant.py          # 번체에 없는 키만 새로 바꿔 넣는다(손으로 고친 값은 그대로)
  python3 tools/make_hant.py --all    # 모든 값을 다시 바꾼다(손질도 다시 해야 한다)
  python3 tools/make_hant.py --check  # 바꿀 것이 있으면 1로 끝난다(파일은 안 고침)

OpenCC s2twp(대만 낱말)로 바꾼 뒤, s2twp 가 뜻을 잘못 짚는 말과 따옴표를 고친다.
고치는 말은 docs/design/glossary.md '번체' 표 아래 설명과 같다. 새로 틀린 것을 찾으면 FIXES 와 용어집을 같이 고친다.
키 순서는 zh-Hans 를 따르고, 간체에 없는 키는 지운다.
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
STRINGS = ROOT / "unity/BloodRain/Assets/Resources/Data/strings"

# (s2twp 결과, 바른 번체). 순서대로 바꾼다.
FIXES = [
    ("回覆", "回復"),      # 回复(회복)을 답장으로 읽음
    ("鍾", "鐘"),          # 钟(범종·종루)을 성씨·술잔으로 읽음
    ("毒後", "毒后"),      # 당소혜 별호, 后는 왕후
    ("20髮", "20發"),      # '20발 지풍'의 发을 머리카락으로 읽음
    ("開啟寶匣", "打開寶匣"),
    ("開啟行囊", "打開行囊"),
]

NOTE = ("키 = 값. zh-Hans.json 을 OpenCC s2twp(대만 낱말)로 바꾼 뒤 손으로 고쳤다(L-08, 도구 tools/make_hant.py, "
        "고친 말은 docs/design/glossary.md 번체 표). 원문과 키 규칙은 Core/Data/Strings.cs. "
        "complete 에 적은 갈래(키 첫 칸)는 빠진 키가 없어야 테스트가 통과한다.")


def quotes(s: str) -> str:
    s = re.sub("“([^”]*)”", r"「\1」", s)
    return re.sub("‘([^’]*)’", r"『\1』", s)


def convert(cc, text: str) -> str:
    out = cc.convert(text)
    for wrong, right in FIXES:
        out = out.replace(wrong, right)
    return quotes(out)


def main(argv):
    redo_all = "--all" in argv
    check = "--check" in argv
    try:
        import opencc
    except ImportError:
        sys.exit("opencc 가 없다: pip install opencc")
    cc = opencc.OpenCC("s2twp")

    hans = json.loads((STRINGS / "zh-Hans.json").read_text(encoding="utf-8"))
    hant_path = STRINGS / "zh-Hant.json"
    old = json.loads(hant_path.read_text(encoding="utf-8")) if hant_path.exists() else {}

    out = {"_meta": {"lang": "zh-Hant", "complete": hans["_meta"]["complete"], "_note": NOTE}}
    added, removed = [], [k for k in old if k != "_meta" and k not in hans]
    for key, value in hans.items():
        if key == "_meta":
            continue
        if not redo_all and key in old:
            out[key] = old[key]
        else:
            out[key] = convert(cc, value)
            if old.get(key) != out[key]:
                added.append(key)

    changed = bool(added or removed) or old.get("_meta") != out["_meta"]
    for k in added:
        print(f"+ {k}: {out[k]}")
    for k in removed:
        print(f"- {k}")
    if check:
        sys.exit(1 if changed else 0)
    if changed:
        hant_path.write_text(json.dumps(out, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"zh-Hant.json: 새로 {len(added)}, 지움 {len(removed)}. 새 줄은 용어집 번체 표와 맞는지 눈으로 본다.")
    else:
        print("zh-Hant.json: 바꿀 것 없음")


if __name__ == "__main__":
    main(sys.argv[1:])
