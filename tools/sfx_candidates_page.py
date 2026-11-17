# -*- coding: utf-8 -*-
"""효과음 후보를 어디서나 들어 보는 작은 페이지를 만든다(backlog SFX-09, 2026-11-17).

sfx_listen.html(SFX-04)은 PC 에서 art_inbox 폴더를 열어야 들을 수 있다. 이건 다듬은 후보(art_inbox/sfx/<id>/trimmed/*.ogg)를
페이지 옆에 같이 복사해 두어, 폴더째 웹에 올리면 휴대폰에서도 듣고 고를 수 있게 한다.
고른 것은 페이지 아래에 'sfx_wpn_sword 3·5 고름' 줄로 모이고, 그 줄을 docs/feedback.md 나 대화창에 붙이면 된다.

쓰는 법(저장소 루트): python tools/sfx_candidates_page.py [나갈 폴더]     # 기본 build/WebGL/BloodRain/sfx
  → 웹 빌드를 올릴 때 같이 올라가 https://dev.bloodrain.pages.dev/sfx/ 에서 열린다.
큐에서 status 가 generated 인 줄만 싣는다. 후보가 하나도 없으면 1 로 끝난다.
"""
from __future__ import annotations

import html
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

PAGE = """<!doctype html>
<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>혈우강호 효과음 후보</title>
<style>
:root { --paper:#EFE6D2; --card:#F6F0E2; --ink:#1A1A1A; --soft:#5A554C; --line:rgba(26,26,26,.18); --seal:#B3261E; }
* { box-sizing: border-box; }
body { margin:0; background:var(--paper); color:var(--ink); font-family:"Noto Serif KR","Apple SD Gothic Neo","Malgun Gothic",serif; }
main { max-width:1080px; margin:0 auto; padding:24px 16px 160px; display:grid; grid-template-columns:minmax(0,1fr); gap:18px; }
h1 { margin:0; font-size:28px; }
p.lead { margin:0; color:var(--soft); line-height:1.7; font-size:14px; max-width:66ch; word-break:keep-all; overflow-wrap:anywhere; }
.grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(min(100%,330px),1fr)); gap:12px; }
.card { background:var(--card); border:1px solid var(--line); border-radius:4px; padding:12px; display:grid; gap:8px; }
.card h2 { margin:0; font-size:16px; }
.card small { color:var(--soft); font-family:ui-monospace,Consolas,monospace; font-size:11px; }
.row { display:flex; flex-wrap:wrap; gap:6px; }
.cand { display:inline-flex; border:2px solid var(--ink); border-radius:3px; overflow:hidden; }
.cand button { border:0; background:transparent; color:var(--ink); padding:9px 12px; font:700 14px/1 ui-sans-serif,system-ui,sans-serif; cursor:pointer; min-width:40px; }
.cand button.pick { border-left:1px solid var(--line); font-weight:500; font-size:12px; color:var(--soft); }
.cand.on { background:var(--ink); }
.cand.on button { color:var(--paper); }
.cand.playing button:first-child { color:var(--seal); }
.out { position:fixed; left:0; right:0; bottom:0; background:#1E1C19; color:#EFE6D2; padding:12px 16px; display:grid; gap:8px; }
.out textarea { width:100%; height:64px; background:#2A2723; color:#EFE6D2; border:1px solid #57514A; border-radius:3px; padding:8px; font:13px/1.5 ui-monospace,Consolas,monospace; resize:vertical; }
.out .bar { display:flex; gap:8px; align-items:center; flex-wrap:wrap; font-size:13px; }
.out button { border:1px solid #EFE6D2; background:transparent; color:#EFE6D2; border-radius:3px; padding:7px 12px; font:700 13px/1 inherit; cursor:pointer; }
</style></head><body><main>
<h1>효과음 후보 듣기</h1>
<p class="lead">2026-11-17 밤에 다시 뽑았습니다. 앞의 것은 한 문구로 여덟을 뽑아 다 같은 소리였고, 이번에는 소리마다 문구를 네 갈래로 나눠 둘씩 뽑았습니다(1·2, 3·4, 5·6, 7·8 이 같은 갈래). 맨 위 두 칸은 진기 줍는 소리입니다 — 지금 게임에 든 바람 소리와, 같은 느낌으로 AI 가 뽑은 것.</p>
<p class="lead">소리마다 후보가 여덟 개씩 있습니다. 숫자를 누르면 들리고, 옆의 '고름'을 누르면 고른 것으로 표시됩니다. 게임에서는 고른 것들을 번갈아 틉니다(둘에서 넷쯤 고르면 됩니다). 마음에 드는 게 없는 소리는 비워 두세요 — 다시 뽑습니다. 다 고른 뒤 아래 줄을 복사해 대화창에 붙여 주세요.</p>
<div class="grid" id="grid"></div>
</main>
<div class="out"><div class="bar"><b>고른 것</b><span id="count"></span><button id="copy" type="button">복사</button><span id="copied"></span></div><textarea id="line" readonly></textarea></div>
<script>
const DATA = __DATA__;
const picked = {};
let audio = null, playingEl = null;
try { Object.assign(picked, JSON.parse(localStorage.getItem('sfx_picked_v2') || '{}')); } catch (e) {}
function play(src, el) {
  if (audio) { audio.pause(); }
  if (playingEl) playingEl.classList.remove('playing');
  audio = new Audio(src); playingEl = el; el.classList.add('playing');
  audio.onended = () => el.classList.remove('playing');
  audio.play().catch(() => el.classList.remove('playing'));
}
function refresh() {
  const lines = [];
  for (const s of DATA) { const p = (picked[s.id] || []).slice().sort((a, b) => a - b); if (p.length) lines.push(s.id + ' ' + p.join('·') + ' 고름'); }
  document.getElementById('line').value = lines.join('\\n');
  document.getElementById('count').textContent = lines.length + ' / ' + DATA.length + ' 소리';
  try { localStorage.setItem('sfx_picked_v2', JSON.stringify(picked)); } catch (e) {}
}
const grid = document.getElementById('grid');
for (const s of DATA) {
  const card = document.createElement('div'); card.className = 'card';
  const h = document.createElement('h2'); h.textContent = s.use; card.appendChild(h);
  const sm = document.createElement('small'); sm.textContent = s.id + ' · ' + s.seconds + '초'; card.appendChild(sm);
  const row = document.createElement('div'); row.className = 'row';
  for (const n of s.files) {
    const c = document.createElement('span'); c.className = 'cand' + ((picked[s.id] || []).includes(n) ? ' on' : '');
    const b = document.createElement('button'); b.type = 'button'; b.textContent = n;
    b.onclick = () => play(s.id + '/' + String(n).padStart(2, '0') + '.ogg', c);
    const k = document.createElement('button'); k.type = 'button'; k.className = 'pick'; k.textContent = '고름';
    k.onclick = () => { const a = picked[s.id] || (picked[s.id] = []); const i = a.indexOf(n); if (i >= 0) a.splice(i, 1); else a.push(n); c.classList.toggle('on', i < 0); refresh(); };
    c.appendChild(b); c.appendChild(k); row.appendChild(c);
  }
  card.appendChild(row); grid.appendChild(card);
}
document.getElementById('copy').onclick = () => {
  const t = document.getElementById('line'); t.select();
  const done = () => { document.getElementById('copied').textContent = '복사했습니다'; };
  if (navigator.clipboard) navigator.clipboard.writeText(t.value).then(done, () => { document.execCommand('copy'); done(); });
  else { document.execCommand('copy'); done(); }
};
refresh();
</script></body></html>
"""


def build(queue: Path, root: Path, out: Path) -> int:
    rows = [json.loads(l) for l in queue.read_text(encoding="utf-8").splitlines() if l.strip()]
    data = []
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    # 진기 줍는 소리: 지금 게임에 든 것(코드로 구운 바람, 낮은·가운데·높은 벌)과 AI 후보(variants.json 묶음, sfx_sa3 --also)를 맨 위에
    game = root / "unity" / "BloodRain" / "Assets" / "Audio" / "SFX"
    nums = []
    for k in (1, 4, 8):
        f = game / f"sfx_xp_absorb_n{k}_1.ogg"
        if f.is_file():
            (out / "sfx_xp_absorb_game").mkdir(exist_ok=True)
            shutil.copyfile(f, out / "sfx_xp_absorb_game" / f"{k:02d}.ogg")
            nums.append(k)
    if nums:
        data.append({"id": "sfx_xp_absorb_game", "use": "진기 줍는 소리 — 지금 게임에 든 바람(1 처음, 4 이어 주울 때, 8 많이 이어 주울 때)", "seconds": 0.35, "files": nums})
    extra = dict(id="sfx_xp_absorb", use="진기 줍는 소리 — AI 가 뽑은 바람 후보", seconds=0.4, save_dir="art_inbox/sfx/sfx_xp_absorb/", status="generated")
    for row in [extra] + rows:
        if row.get("status") != "generated":
            continue
        tdir = root / (row.get("save_dir") or f"art_inbox/sfx/{row['id']}/") / "trimmed"
        files = sorted(tdir.glob("*.ogg")) if tdir.is_dir() else []
        nums = []
        for f in files:
            try:
                n = int(f.stem.rsplit("_", 1)[1])
            except (IndexError, ValueError):
                continue
            (out / row["id"]).mkdir(exist_ok=True)
            shutil.copyfile(f, out / row["id"] / f"{n:02d}.ogg")
            nums.append(n)
        if nums:
            data.append({"id": row["id"], "use": row.get("use", row["id"]), "seconds": row.get("seconds"), "files": nums})
    if not data:
        return 0
    (out / "index.html").write_text(PAGE.replace("__DATA__", json.dumps(data, ensure_ascii=False)), encoding="utf-8")
    return len(data)


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "build" / "WebGL" / "BloodRain" / "sfx"
    n = build(ROOT / "sfx_queue" / "queue.jsonl", ROOT, out)
    if n == 0:
        print("X  generated 줄의 다듬은 후보가 없다(sfx_sa3.py --queue → postprocess_sfx.py --candidates 먼저)")
        sys.exit(1)
    print(f"O  소리 {n}개의 후보 페이지 → {out}")
