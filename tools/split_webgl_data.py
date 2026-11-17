# -*- coding: utf-8 -*-
# Cloudflare Pages 는 파일 하나가 25MiB 를 넘으면 안 올린다(2026-11-09 WG-01U: BloodRain.data.br 가 44MiB).
# 빌드 결과의 Build/BloodRain.data.br 를 풀어 여러 조각으로 나누고, 조각마다 따로 brotli 로 다시 묶는다
# (BloodRain.data.part0.data.br … — web/_headers 의 /Build/*.data.br 규칙이 그대로 걸린다).
# index.html 은 조각을 받아 이어 붙인 Blob 주소를 dataUrl 로 넘기게 고친다. 원래 .data.br 는 지운다.
# 쓰는 법(저장소 루트): python tools/split_webgl_data.py [build/WebGL/BloodRain]
# PC 올리기와 Actions(webgl.yml) 둘 다 이걸 부른다. 테스트는 tools/tests/test_split_webgl_data.py.
# 같은 김에 index.html 의 autoSyncPersistentDataPath 줄을 켠다(WG-01Ub, 혼 저장이 IndexedDB 에 남게).
# 끝으로 빌드 파일 주소에 ?v=<빌드 시각> 을 붙인다(2026-11-17 — 이름이 같은 파일이 1년 캐시돼 새로 올려도 옛 판이 떴다).
# PC 는 Editor/WebGLBuild.EnableAutoSync 가 이미 켜지만, Actions 는 game-ci 기본 빌드라 그 메서드를 안 거친다(QA-24).
import os, sys, re

LIMIT = 24 * 1024 * 1024          # 조각 하나 압축 크기 상한(여유를 둔 값)
CHUNK = 48 * 1024 * 1024          # 처음 시도하는 조각 원본 크기

LOADER = """
      // split_webgl_data.py: data 를 조각으로 받아 이어 붙인다(Cloudflare Pages 25MiB 상한).
      const dataParts = %s;
      async function joinData() {
        const bufs = await Promise.all(dataParts.map(n => fetch(buildUrl + "/" + n).then(r => { if (!r.ok) throw new Error(n + " " + r.status); return r.arrayBuffer(); })));
        return URL.createObjectURL(new Blob(bufs));
      }
"""


AUTOSYNC_OFF = "// config.autoSyncPersistentDataPath = true;"
AUTOSYNC_ON = "config.autoSyncPersistentDataPath = true; // WebGLBuild: 혼 저장을 IndexedDB 에 남긴다(WG-01Ub)"


def patch_autosync(html):
    """주석 처리된 autoSync 줄을 켠다(Editor/WebGLBuild.cs 와 같은 글). 이미 켜졌으면 그대로, 줄이 없으면 ValueError."""
    if AUTOSYNC_ON in html:
        return html
    if AUTOSYNC_OFF not in html:
        raise ValueError("index.html 에 autoSyncPersistentDataPath 줄이 없다(템플릿이 바뀌었나) — 혼 저장이 새로고침 뒤 안 남는다")
    return html.replace(AUTOSYNC_OFF, AUTOSYNC_ON, 1)


def autosync(root):
    idx = os.path.join(root, "index.html")
    html = open(idx, encoding="utf-8").read()
    out = patch_autosync(html)
    if out != html:
        open(idx, "w", encoding="utf-8").write(out)
    return out != html


def patch_index(html, names):
    """index.html 에 조각 받기를 넣는다. 세 군데 중 하나라도 못 찾으면 ValueError(반쯤 고친 채 두지 않는다)."""
    if "dataParts" in html:
        return html
    data_line = re.search(r'dataUrl:\s*buildUrl\s*\+\s*"/[^"]*\.data\.br",', html)
    if "var loaderUrl" not in html or not data_line or "document.body.appendChild(script);" not in html:
        raise ValueError("index.html 에서 var loaderUrl·dataUrl·appendChild(script) 중 하나를 못 찾았다(Unity 템플릿이 바뀌었나)")
    html = html.replace("var loaderUrl", LOADER % repr(names).replace("'", '"') + "      var loaderUrl", 1)
    html = html.replace(data_line.group(0), 'dataUrl: "",', 1)
    # 로더 스크립트를 붙이기 전에 조각을 받아 dataUrl 을 채운다
    return html.replace("document.body.appendChild(script);",
                        'joinData().then(u => { config.dataUrl = u; document.body.appendChild(script); })'
                        '.catch(e => { console.error("data 조각 받기 실패", e); });', 1)


def stamp_index(html, version):
    """빌드 파일 주소 끝에 ?v=<version> 을 붙인다(2026-11-17).
    web/_headers 가 /Build/*.br 을 1년·immutable 로 캐시하게 해 두었는데 파일 이름이 빌드마다 같아서,
    한 번 들어와 본 브라우저는 새로 올려도 옛 판(wasm·data)을 계속 띄웠다. 색인(index.html)은 캐시되지 않으니
    여기서 주소를 빌드마다 다르게 만든다. 이미 붙어 있으면 값만 바꾼다(두 번 해도 같다).
    못 찾는 주소가 있으면 ValueError — 반쯤 붙인 채 올리면 옛 조각과 새 조각이 섞인다."""
    v = "?v=" + str(version)
    html = re.sub(r"\?v=[0-9A-Za-z_.-]+", "", html)
    n_total = 0
    # "/BloodRain.loader.js" · "/BloodRain.framework.js.br" · "/BloodRain.wasm.br" · "/BloodRain.data.br"
    html, n = re.subn(r'(buildUrl\s*\+\s*"/[^"?]+\.(?:js|br))"', lambda m: m.group(1) + v + '"', html)
    n_total += n
    # 조각 받기: fetch(buildUrl + "/" + n)
    if "dataParts" in html:
        html, n = re.subn(r'fetch\(buildUrl \+ "/" \+ n(?: \+ "[^"]*")?\)', 'fetch(buildUrl + "/" + n + "' + v + '")', html)
        if n != 1:
            raise ValueError("index.html 에서 조각 받는 줄을 못 찾았다(?v= 를 못 붙임)")
    if n_total < 3:
        raise ValueError("index.html 에서 loader·framework·wasm 주소를 다 찾지 못했다(?v= 를 못 붙임)")
    return html


def stamp(root, version=None):
    """index.html 에 빌드 표를 붙인다. version 이 없으면 wasm 파일이 쓰인 시각(초). 돌려주는 값은 붙인 표."""
    idx = os.path.join(root, "index.html")
    if version is None:
        bdir = os.path.join(root, "Build")
        wasm = [f for f in os.listdir(bdir) if ".wasm" in f]
        if not wasm:
            raise FileNotFoundError(bdir + " 에 wasm 이 없다")
        version = int(os.path.getmtime(os.path.join(bdir, wasm[0])))
    html = open(idx, encoding="utf-8").read()
    out = stamp_index(html, version)
    if out != html:
        open(idx, "w", encoding="utf-8").write(out)
    return version


def split(root, limit=LIMIT, chunk=CHUNK):
    """나눴으면 조각 이름 목록, 나눌 필요가 없으면 []. 없는 파일이면 FileNotFoundError."""
    import brotli
    bdir = os.path.join(root, "Build")
    src = os.path.join(bdir, "BloodRain.data.br")
    idx = os.path.join(root, "index.html")
    if not os.path.exists(src):
        raise FileNotFoundError(src + " 가 없다(이미 나눴거나 빌드가 없다)")
    if os.path.getsize(src) <= limit:
        return []
    # index.html 을 먼저 고쳐 보고(실패하면 아무것도 안 바뀐 채 멈춤) 그다음 조각을 쓴다
    html = open(idx, encoding="utf-8").read()
    raw = brotli.decompress(open(src, "rb").read())
    while True:
        parts = [raw[i:i + chunk] for i in range(0, len(raw), chunk)]
        packed = [brotli.compress(p, quality=9, lgwin=24) for p in parts]
        if max(len(p) for p in packed) <= limit:
            break
        chunk //= 2
    names = ["BloodRain.data.part%d.data.br" % i for i in range(len(packed))]
    html = patch_index(html, names)
    for i, (n, p) in enumerate(zip(names, packed)):
        open(os.path.join(bdir, n), "wb").write(p)
        print("O  %s  %d → %d" % (n, len(parts[i]), len(p)))
    open(idx, "w", encoding="utf-8").write(html)
    os.remove(src)
    return names


if __name__ == "__main__":
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.join("build", "WebGL", "BloodRain")
    try:
        print("O  autoSync 켬" if autosync(root) else "O  autoSync 이미 켜져 있음")
    except (FileNotFoundError, ValueError) as e:
        print("X  " + str(e)); sys.exit(1)
    names = []
    try:
        names = split(root)
    except FileNotFoundError as e:
        # 이미 나눈 폴더에서 다시 돌려도 빌드 표는 붙인다
        if "dataParts" not in open(os.path.join(root, "index.html"), encoding="utf-8").read():
            print("X  " + str(e)); sys.exit(1)
        print("O  data 는 이미 나눠져 있음")
    except ValueError as e:
        print("X  " + str(e)); sys.exit(1)
    else:
        print("O  조각 %d개, index.html 고침" % len(names) if names else "O  data 가 상한 안이라 나누지 않는다")
    try:
        print("O  빌드 표 ?v=%s (옛 캐시를 안 쓰게)" % stamp(root))
    except (FileNotFoundError, ValueError) as e:
        print("X  " + str(e)); sys.exit(1)
