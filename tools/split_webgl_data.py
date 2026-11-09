# -*- coding: utf-8 -*-
# Cloudflare Pages 는 파일 하나가 25MiB 를 넘으면 안 올린다(2026-11-09 WG-01U: BloodRain.data.br 가 44MiB).
# 빌드 결과의 Build/BloodRain.data.br 를 풀어 여러 조각으로 나누고, 조각마다 따로 brotli 로 다시 묶는다
# (BloodRain.data.part0.data.br … — web/_headers 의 /Build/*.data.br 규칙이 그대로 걸린다).
# index.html 은 조각을 받아 이어 붙인 Blob 주소를 dataUrl 로 넘기게 고친다. 원래 .data.br 는 지운다.
# 쓰는 법(저장소 루트): python tools/split_webgl_data.py [build/WebGL/BloodRain]
import os, sys, re, brotli

LIMIT = 24 * 1024 * 1024          # 조각 하나 압축 크기 상한(여유를 둔 값)
CHUNK = 48 * 1024 * 1024          # 처음 시도하는 조각 원본 크기

root = sys.argv[1] if len(sys.argv) > 1 else os.path.join("build", "WebGL", "BloodRain")
bdir = os.path.join(root, "Build")
src = os.path.join(bdir, "BloodRain.data.br")
idx = os.path.join(root, "index.html")
if not os.path.exists(src):
    print("X  " + src + " 가 없다(이미 나눴거나 빌드가 없다)"); sys.exit(1)
if os.path.getsize(src) <= LIMIT:
    print("O  data 가 " + str(os.path.getsize(src)) + " 바이트라 나누지 않는다"); sys.exit(0)

raw = brotli.decompress(open(src, "rb").read())
chunk = CHUNK
while True:
    parts = [raw[i:i + chunk] for i in range(0, len(raw), chunk)]
    packed = [brotli.compress(p, quality=9, lgwin=24) for p in parts]
    if max(len(p) for p in packed) <= LIMIT: break
    chunk //= 2
names = []
for i, p in enumerate(packed):
    n = "BloodRain.data.part%d.data.br" % i
    open(os.path.join(bdir, n), "wb").write(p)
    names.append(n)
    print("O  %s  %d → %d" % (n, len(parts[i]), len(p)))
os.remove(src)

html = open(idx, encoding="utf-8").read()
loader = """
      // split_webgl_data.py: data 를 조각으로 받아 이어 붙인다(Cloudflare Pages 25MiB 상한).
      const dataParts = %s;
      async function joinData() {
        const bufs = await Promise.all(dataParts.map(n => fetch(buildUrl + "/" + n).then(r => { if (!r.ok) throw new Error(n + " " + r.status); return r.arrayBuffer(); })));
        return URL.createObjectURL(new Blob(bufs));
      }
""" % repr(names).replace("'", '"')
if "dataParts" not in html:
    html = html.replace('var loaderUrl', loader + '      var loaderUrl', 1)
    html = html.replace('dataUrl: buildUrl + "/BloodRain.data.br",', 'dataUrl: "",', 1)
    # 로더 스크립트를 붙이기 전에 조각을 받아 dataUrl 을 채운다
    html, n = re.subn(r'document\.body\.appendChild\(script\);', 'joinData().then(u => { config.dataUrl = u; document.body.appendChild(script); }).catch(e => { console.error("data 조각 받기 실패", e); });', html, count=1)
    if n != 1: print("X  index.html 에서 appendChild(script) 를 못 찾았다"); sys.exit(1)
    open(idx, "w", encoding="utf-8").write(html)
print("O  조각 %d개, index.html 고침" % len(names))
