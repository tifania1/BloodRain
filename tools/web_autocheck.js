// 웹 자동 점검(WP-03): 빌드한 웹 프로토타입과 천하 시안을 헤드리스 크롬으로 열고 자동 판을 돌려
// 콘솔 오류·페이지 오류·멈춤(시계가 안 감)·숫자 깨짐(NaN)·화면 막힘(화면 둘이 겹쳐 뜸)을 찾는다.
// 쓰는 법:
//   python tools/build_web_prototype.py
//   node tools/web_autocheck.js [--runs 3] [--shots <폴더>] [--only prologue,songshan,m3]
// 끝 줄에 '문제 N' 을 찍고, 문제가 있으면 1 로 끝난다. 손맛 숫자는 건드리지 않고 보기만 한다.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const args = process.argv.slice(2);
const opt = (k, d) => { const i = args.indexOf('--' + k); return i >= 0 ? args[i + 1] : d; };
const RUNS = parseInt(opt('runs', '3'), 10);
const SHOTS = opt('shots', '');
const ONLY = (opt('only', 'prologue,songshan,m3,lords,flow')).split(',');
const SITE = path.join(__dirname, '..', 'web', 'prototype', 'site');
if (!fs.existsSync(path.join(SITE, 'index.html'))) { console.log('X  site/ 가 없다. python tools/build_web_prototype.py 를 먼저 돌린다'); process.exit(1); }

const TYPES = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.json': 'application/json', '.png': 'image/png', '.jpg': 'image/jpeg', '.css': 'text/css', '.woff2': 'font/woff2' };
function serve() {
  return new Promise(res => {
    const srv = http.createServer((q, r) => {
      let p = decodeURIComponent(q.url.split('?')[0].split('#')[0]);
      if (p.endsWith('/')) p += 'index.html';
      const f = path.join(SITE, p);
      if (!f.startsWith(SITE) || !fs.existsSync(f)) { r.writeHead(404); r.end(); return; }
      r.writeHead(200, { 'Content-Type': TYPES[path.extname(f)] || 'application/octet-stream' }); r.end(fs.readFileSync(f));
    });
    srv.listen(0, '127.0.0.1', () => res(srv));
  });
}

const problems = [];
const rows = [];
const say = s => console.log(s);

// 페이지 하나: 콘솔 오류·페이지 오류를 모은다. 바깥(구글 글꼴 등) 요청은 막는다(네트워크가 없어도 같은 결과).
async function openPage(browser, url, label, viewport) {
  const ctx = await browser.newContext({ viewport: viewport || { width: 1366, height: 768 } });
  const page = await ctx.newPage();
  const errs = [];
  await page.route('**/*', r => { const u = r.request().url(); return u.startsWith('http://127.0.0.1') || u.startsWith('data:') || u.startsWith('blob:') ? r.continue() : r.abort(); });
  page.on('pageerror', e => errs.push('pageerror: ' + (e.stack || e.message).split('\n').slice(0, 3).join(' | ')));
  page.on('console', m => { if (m.type() === 'error' && !/ERR_FAILED|net::|Failed to load resource/.test(m.text())) errs.push('console: ' + m.text().slice(0, 300)); });
  await page.goto(url, { waitUntil: 'load' });
  await page.waitForTimeout(300);
  return { ctx, page, errs, label };
}
function flush(p, label) {
  for (const e of p.errs.splice(0)) problems.push(`${label}: ${e}`);
}

// 1장 프로토타입: __hyeolu.sim 을 돌리되 걸음마다 멈춤·NaN 을 보고, 0.5초마다 그려 본다(그리기 오류는 sim 만으로는 안 드러난다).
async function protoRun(p, mode, seconds, label) {
  const r = await p.page.evaluate(([mode, seconds]) => {
    const bad = [];
    let lastT = -1, still = 0, n = 0, stuckAt = null;
    const onStep = G => {
      n++;
      if (G.t === lastT) { still++; if (still === 900 && !stuckAt) stuckAt = { t: +G.t.toFixed(1), overlay: __hyeolu.overlay() }; } else { still = 0; lastT = G.t; }
      const pl = G.player;
      if (![pl.x, pl.y, pl.hp].every(Number.isFinite) && bad.length < 5) bad.push(`플레이어 값 깨짐 t=${G.t.toFixed(1)} x=${pl.x} y=${pl.y} hp=${pl.hp}`);
      if (n % 30 === 0) {
        const e = G.enemies.find(e => !Number.isFinite(e.x) || !Number.isFinite(e.y) || Number.isNaN(e.hp));
        if (e && bad.length < 5) bad.push(`적 값 깨짐 t=${G.t.toFixed(1)} ${e.id || e.kind} x=${e.x} hp=${e.hp}`);
      }
      if (n % 15 === 0) { try { render(); } catch (err) { if (bad.length < 5) bad.push(`그리기 오류 t=${G.t.toFixed(1)} overlay=${__hyeolu.overlay()}: ${err.message}`); } }
      if (stuckAt) throw new Error('STUCK');
    };
    let res;
    try { res = __hyeolu.sim(mode, seconds, undefined, onStep); }
    catch (err) { if (err.message !== 'STUCK') bad.push('sim 오류: ' + err.message + ' ' + (err.stack || '').split('\n')[1]); }
    const G = __hyeolu.state();
    return { bad, stuckAt, t: G.t, lv: G.level, kills: G.kills, hp: Math.round(G.player.hp), won: !!G.won, boss: G.boss ? Math.round(G.boss.hp) : null, overlay: __hyeolu.overlay(), prologue: G.mode === 'prologue' };
  }, [mode, seconds]);
  for (const b of r.bad) problems.push(`${label}: ${b}`);
  if (r.stuckAt) problems.push(`${label}: 멈춤 — 시계가 30초 동안 ${r.stuckAt.t}초에 서 있음(떠 있는 화면 ${r.stuckAt.overlay})`);
  return r;
}

// 판이 끝난 뒤 실제 그리기 고리(requestAnimationFrame)를 몇 초 돌려 본다. 떠 있는 화면이 둘 이상이면 막힘으로 센다.
async function liveCheck(p, label, ms, screens) {
  await p.page.waitForTimeout(ms);
  const vis = await p.page.evaluate(ids => ids.filter(id => { const el = document.getElementById(id); return el && !el.hidden && getComputedStyle(el).display !== 'none'; }), screens);
  if (vis.length > 1) problems.push(`${label}: 화면 겹침 — ${vis.join('·')} 가 함께 떠 있음`);
  return vis;
}
async function shot(p, name) { if (SHOTS) { fs.mkdirSync(SHOTS, { recursive: true }); await p.page.screenshot({ path: path.join(SHOTS, name + '.png') }); } }

const PROTO_SCREENS = ['title', 'levelup', 'brk', 'chest', 'find', 'evopick', 'pause', 'result', 'procut', 'profirst', 'dex'];
const CH_SCREENS = ['title', 'rebirth', 'levelup', 'pause', 'dead'];

async function prologueRuns(browser, base) {
  for (let i = 0; i < RUNS; i++) {
    const label = `서장 ${i + 1}`;
    const p = await openPage(browser, base + '/', label);
    const r = await protoRun(p, 'prologue', 900, label);
    // 장삼을 잡은 걸음에 구슬로 레벨업(보갑 등)이 열리면 sim 은 G.won 에서 바로 멈춰 그 화면이 남고, 컷을 기다리는 몇 초 사이에도
    // 판이 돌아 구슬을 더 먹으면 레벨업이 또 열린다. 게임은 그 화면이 닫힐 때까지 컷을 기다리니(WP-02) 사람처럼 계속 닫아 준다.
    // 안 닫으면 판마다 들쭉날쭉 '컷이 안 뜸'이 났다(WP-04).
    const freeUp = () => p.page.evaluate(() => {
      for (let k = 0; k < 40 && overlay; k++) {
        if (overlay === 'levelup') chooseOffer(0); else if (overlay === 'brk') chooseInsight(0);
        else if (overlay === 'chest' || overlay === 'find') closeOverlay(overlay); else if (overlay === 'evopick') choosePick(0);
        else break;
      }
    });
    await freeUp();
    // 장삼을 잡았으면 끝 컷이 떠야 한다. 실제 고리로 돌려 컷이 뜨는지 본다.
    await p.page.evaluate(() => { running = true; lastTs = performance.now(); });
    if (r.won) for (let k = 0; k < 40 && (await p.page.evaluate(() => __hyeolu.overlay())) !== 'procut'; k++) { await p.page.waitForTimeout(200); await freeUp(); }
    const vis = await liveCheck(p, label, 2500, PROTO_SCREENS);
    const ov = await p.page.evaluate(() => __hyeolu.overlay());
    if (r.won && ov !== 'procut') problems.push(`${label}: 장삼을 잡았는데 끝 컷이 안 뜸(떠 있는 화면 ${ov})`);
    let flow = '';
    if (ov === 'procut') {
      // 컷을 넘겨 첫 환혼 화면까지
      for (let k = 0; k < 6 && (await p.page.evaluate(() => __hyeolu.overlay())) === 'procut'; k++) { await p.page.click('#procutnext').catch(() => {}); await p.page.waitForTimeout(700); }
      flow = await p.page.evaluate(() => __hyeolu.overlay());
      if (flow !== 'profirst') problems.push(`${label}: 끝 컷 뒤 첫 환혼 화면이 아님(${flow})`);
      await liveCheck(p, label + ' 첫 환혼', 500, PROTO_SCREENS);
      if (i === 0) await shot(p, 'prologue_first');
    }
    flush(p, label);
    rows.push({ what: label, res: `${Math.round(r.t)}초 Lv${r.lv} 처치 ${r.kills} ${r.won ? '장삼 격파' : r.hp <= 0 ? '쓰러짐' : '버팀'} → ${flow || vis.join('·') || '전투'}` });
    await p.ctx.close();
  }
}

async function songshanRuns(browser, base) {
  for (let i = 0; i < RUNS; i++) {
    const label = `숭산 ${i + 1}`;
    const p = await openPage(browser, base + '/', label);
    const r = await protoRun(p, 'full', 1300, label);
    // 화면을 닫은 직후처럼 lastTs 가 rAF 시각보다 늦은 프레임을 30번 만든다(음수 시간 걸음이면 그리기 오류가 난다)
    await p.page.evaluate(() => { running = true; let k = 0; const f = () => { lastTs = performance.now() + 40; if (++k < 30) requestAnimationFrame(f); }; requestAnimationFrame(f); });
    const vis = await liveCheck(p, label, 2500, PROTO_SCREENS);
    if (i === 0) await shot(p, 'songshan_end');
    if (i === 0) {
      // 한 프레임 안에서 쓰러진 뒤 같은 update 끝에 레벨업이 열리면 결과 화면과 겹친다(WP-03 에서 찾음)
      const v = await p.page.evaluate(() => { startRun('full'); G.player.iframe = 0; G.pendingLevels = 1; hurtPlayer(1e6); update(1 / 60); return overlay; });
      if (v !== 'result') problems.push(`${label}: 쓰러진 프레임에 레벨업이 열림(overlay ${v})`);
      await liveCheck(p, label + ' 쓰러진 프레임', 300, PROTO_SCREENS);
    }
    flush(p, label);
    rows.push({ what: label, res: `${Math.round(r.t)}초 Lv${r.lv} 처치 ${r.kills} ${r.won ? '원각 격파' : r.hp <= 0 ? '쓰러짐' : '버팀'} → ${vis.join('·') || '전투'}` });
    await p.ctx.close();
  }
}

// 천하 시안: 자기 땅이 M3 지역인 몸(당가·수로채·무당)과 소림을 자동 조종으로. 걸음마다 멈춤·NaN·그리기를 본다.
async function cheonhaRun(p, sect, seconds, label, before) {
  const r = await p.page.evaluate(([sect, seconds, before]) => {
    const bad = [];
    __ch.reset(); __ch.start(sect);
    if (before) (new Function('__ch', before))(__ch);
    const G = __ch.state(); G.auto = true;
    const ovNow = () => ['title', 'rebirth', 'levelup', 'pause', 'dead'].filter(id => !document.getElementById(id).hidden).join('·') || null;
    let lastT = G.t, still = 0, stuckAt = null, n = 0, err = null;
    const seen = new Set();
    try {
      for (let t = 0; t < seconds && !G.ended; t += 1 / 30) {
        __ch.step(1 / 30); n++;
        seen.add(G.region);
        if (G.t === lastT) { still++; if (still === 900 && !stuckAt) { stuckAt = { t: +G.t.toFixed(1), overlay: ovNow(), brk: !!G.brk }; break; } } else { still = 0; lastT = G.t; }
        if (![G.p.x, G.p.y, G.p.hp, G.view].every(Number.isFinite) && bad.length < 5) bad.push(`플레이어 값 깨짐 t=${G.t.toFixed(1)} x=${G.p.x} hp=${G.p.hp} view=${G.view}`);
        if (n % 30 === 0) {
          const f = G.foes.find(f => !f.dead && (!Number.isFinite(f.x) || !Number.isFinite(f.y) || Number.isNaN(f.hp)));
          if (f && bad.length < 5) bad.push(`적 값 깨짐 t=${G.t.toFixed(1)} ${f.def && f.def.id} x=${f.x} hp=${f.hp}`);
          if (G.fight && !Number.isFinite(G.fight.hp) && bad.length < 5) bad.push(`땅의 주인 기혈 깨짐 t=${G.t.toFixed(1)} ${G.fight.def.id}`);
        }
        if (n % 15 === 0) __ch.render();
      }
    } catch (e) { err = e.message + ' ' + (e.stack || '').split('\n')[1]; }
    if (err) bad.push(`t=${G.t.toFixed(1)} 오류: ${err}`);
    const res = { bad, stuckAt, t: Math.round(G.t), tier: G.tier, lv: G.lv, dead: G.ended, kills: G.kills, bossFirst: G.bossFirst.slice(), regions: [...seen], fight: G.fight ? G.fight.def.id + ' ' + Math.round(G.fight.hp / G.fight.maxHp * 100) + '%' : null };
    if (!G.ended) __ch.sim(0);   // 시안 안의 running 을 켜고 자동 조종을 끈다
    return res;
  }, [sect, seconds, before || '']);
  for (const b of r.bad) problems.push(`${label}: ${b}`);
  if (r.stuckAt) problems.push(`${label}: 멈춤 — 시계가 30초 동안 ${r.stuckAt.t}초에 서 있음(화면 ${r.stuckAt.overlay}, 돌파 ${r.stuckAt.brk})`);
  return r;
}

async function m3Runs(browser, base) {
  const realms = ['삼류', '이류', '일류', '절정', '초절정', '화경', '현경'];
  const plan = [
    { sect: 'tang', region: 'danggata', boss: 'dang_sohye' },
    { sect: 'waterfort', region: 'janggang', boss: 'ma_cheonpae' },
    { sect: 'wudang', region: 'wudangsan', boss: 'hyeolheo' },
    { sect: 'shaolin', region: 'songshan', boss: 'wongak' },
  ];
  for (const pl of plan) {
    for (let i = 0; i < RUNS; i++) {
      const label = `시안 ${pl.region} ${i + 1}`;
      const p = await openPage(browser, base + '/lookdev/cheonha.html', label);
      // 셋째 판은 절정으로 올리고 땅의 주인과 바로 붙여 패턴이 다 도는지 본다
      const before = i === RUNS - 1 && RUNS > 1 ? `__ch.setTier(3); __ch.warp('${pl.region}', 0.05); __ch.fight('${pl.region}');` : '';
      const r = await cheonhaRun(p, pl.sect, 400, label, before);
      const vis = await liveCheck(p, label, 2000, CH_SCREENS);
      if (i === 0) await shot(p, 'cheonha_' + pl.region);
      flush(p, label);
      const end = r.dead ? '쓰러짐' : '버팀';
      rows.push({ what: label + (before ? '(절정·땅의 주인)' : ''), res: `${r.t}초 ${realms[r.tier]} Lv${r.lv} ${end}${r.fight ? ' 싸우는 중 ' + r.fight : ''}${r.bossFirst.length ? ' 잡음 ' + r.bossFirst.join('·') : ''} → ${vis.join('·') || '전투'}` });
      await p.ctx.close();
    }
  }
}

// 땅의 주인 훑기: 지역마다 그 땅 경지 언저리로 올려 땅의 주인과 붙이고, 몸을 죽지 않게 둔 채 60초 동안 패턴을 다 돌린다.
// 자동 판으로는 M4 땅까지 잘 안 가서, 모양 처리(속도 없는 줄 등)가 값을 깨는지 여기서 본다.
async function lordRuns(browser, base) {
  const p = await openPage(browser, base + '/lookdev/cheonha.html', '땅의 주인 훑기');
  const regions = await p.page.evaluate(() => __ch.data.cheonha.regions.filter(r => r.boss && r.id !== 'bongmagok').map(r => r.id));
  for (const reg of regions) {
    const label = `땅의 주인 ${reg}`;
    const r = await p.page.evaluate(reg => {
      __ch.reset(); __ch.start('huashan'); __ch.setTier(4); __ch.warp(reg, 0.05);
      const nm = __ch.fight(reg), G = __ch.state(); G.auto = true; G.p.maxHp = G.p.hp = 1e9;
      const bad = [], used = new Set(); let n = 0, lastTimer = 1e9;
      try {
        for (; n < 30 * 60 && G.fight; n++) {
          // 기혈을 붙잡아 둔다: 처음 40초는 반, 그 뒤는 2막(phase2At 30%) 아래로
          const f0 = G.fight; if (f0 && !(f0.pat && f0.pat.split && f0.pat.split.on)) f0.hp = f0.maxHp * (n < 30 * 40 ? 0.5 : 0.2);
          __ch.step(1 / 30); const f = G.fight; if (!f) break;
          // 새 패턴을 고르면 timer 가 간격으로 다시 찬다. 그때 order 에서 고른 것(cycle-1)을 적는다
          if (f.pat && f.pat.timer > lastTimer + 0.5) used.add(f.pat.set.order[(f.pat.cycle - 1) % f.pat.set.order.length]);
          if (f.pat) lastTimer = f.pat.timer;
          if (f.pat && f.pat.phase2 && !used.has('2막')) used.add('2막');
          if (![f.x, f.y, f.hp].every(Number.isFinite)) { bad.push(`값 깨짐 t=${G.t.toFixed(1)} x=${f.x} hp=${f.hp} 마지막 패턴 ${[...used].pop()}`); break; }
          if (G.foes.some(e => !e.dead && !Number.isFinite(e.x))) { bad.push(`부른 적 값 깨짐 t=${G.t.toFixed(1)}`); break; }
          if (n % 15 === 0) __ch.render();
        }
      } catch (e) { bad.push('오류: ' + e.message + ' ' + (e.stack || '').split('\n')[1]); }
      const out = { nm, bad, used: [...used], t: Math.round(G.t), left: G.fight ? Math.round(G.fight.hp / G.fight.maxHp * 100) + '%' : '잡음' };
      G.p.maxHp = G.p.hp = 100; return out;
    }, reg);
    for (const b of r.bad) problems.push(`${label}: ${b}`);
    flush(p, label);
    rows.push({ what: `${label}(초절정·무적 60초, 40초부터 2막)`, res: `${r.nm} — 차례가 온 패턴 ${r.used.join('·') || '없음'}, ${r.left}` });
  }
  await p.ctx.close();
}

// 서장 → 첫 환혼 → 시안(#first=) 이어 받기와 휴대폰 화면
async function flowRuns(browser, base) {
  const label = '이어 받기';
  const p = await openPage(browser, base + '/lookdev/cheonha.html#first=shaolin&orb=900&realm=ilryu', label, { width: 360, height: 780 });
  await p.page.waitForTimeout(800);
  const st = await p.page.evaluate(() => { const G = __ch.state(); return { sect: G && G.sect && G.sect.id, prologue: __ch.prologue() }; });
  if (st.sect !== 'shaolin') problems.push(`${label}: #first=shaolin 로 열었는데 첫 생이 시작되지 않음(${JSON.stringify(st)})`);
  await p.page.waitForTimeout(2000);
  await shot(p, 'cheonha_first_phone');
  // 멈춤 화면 열고 닫기, 좌화 → 정산 → 침방
  await p.page.evaluate(() => { __ch.pause(true); });
  await liveCheck(p, label + ' 멈춤', 300, CH_SCREENS);
  await p.page.evaluate(() => { __ch.pause(false); __ch.end('zuohwa'); });
  await liveCheck(p, label + ' 정산', 300, CH_SCREENS);
  await shot(p, 'cheonha_dead_phone');
  await p.page.click('#again').catch(e => problems.push(`${label}: 정산 화면 '다시' 단추를 못 누름 ${e.message.split('\n')[0]}`));
  await p.page.waitForTimeout(500);
  const ov = (await liveCheck(p, label + ' 침방', 300, CH_SCREENS)).join('·');
  if (ov !== 'rebirth') problems.push(`${label}: 정산 뒤 침방이 아님(${ov})`);
  await shot(p, 'cheonha_rebirth_phone');
  flush(p, label);
  rows.push({ what: '서장 넘어옴(#first)·정산·침방 360x780', res: `${st.sect || '없음'} 첫 생, 서장 정산 ${st.prologue ? JSON.stringify(st.prologue).slice(0, 60) : '없음'} → ${ov}` });
  await p.ctx.close();
}

(async () => {
  const srv = await serve();
  const base = `http://127.0.0.1:${srv.address().port}`;
  const browser = await chromium.launch();
  const t0 = Date.now();
  try {
    if (ONLY.includes('prologue')) await prologueRuns(browser, base);
    if (ONLY.includes('songshan')) await songshanRuns(browser, base);
    if (ONLY.includes('m3')) await m3Runs(browser, base);
    if (ONLY.includes('lords')) await lordRuns(browser, base);
    if (ONLY.includes('flow')) await flowRuns(browser, base);
  } catch (e) { problems.push('점검 스크립트 오류: ' + (e.stack || e.message).split('\n').slice(0, 3).join(' | ')); }
  await browser.close(); srv.close();
  say('| 판 | 결과 |'); say('|---|---|');
  for (const r of rows) say(`| ${r.what} | ${r.res} |`);
  say('');
  for (const p of problems) say('X  ' + p);
  say(`문제 ${problems.length} (${Math.round((Date.now() - t0) / 1000)}초)`);
  process.exit(problems.length ? 1 : 0);
})();
