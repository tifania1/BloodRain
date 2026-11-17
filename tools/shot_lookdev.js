// 이펙트 시안(web/lookdev/sects.html)을 헤드리스로 찍는다. 시간을 멈추고 `window.__sects.adv` 로
// 손수 넘기니 느린 컨테이너에서도 정한 순간이 찍힌다(실시간으로 찍으면 늦은 순간만 남는다).
//
//   node tools/shot_lookdev.js <출력 폴더> spear-5 magyo-j ...
//   (환경변수 FRAMES=6 STEP=0.2 — 몇 장을, 장 사이 몇 초씩)
//
// 주소 뒷부분은 sects.html 과 같다(`#문파-단계`, 절기는 `-j`). 무공마다 <이름>_<n>.png 와
// 여러 장을 한 장에 모은 <이름>_grid.png 를 남기고, 페이지 오류가 있으면 출력한다.
const path = require('path');
const { chromium } = require('playwright');

(async () => {
  const [out, ...hashes] = process.argv.slice(2);
  if (!out || hashes.length === 0) {
    console.error('쓰는 법: node tools/shot_lookdev.js <출력 폴더> <주소 뒷부분>...');
    process.exit(2);
  }
  const frames = +(process.env.FRAMES || 6);
  const step = +(process.env.STEP || 0.2);
  require('fs').mkdirSync(out, { recursive: true });
  const page_url = 'file://' + path.resolve(__dirname, '../web/lookdev/sects.html');
  const browser = await chromium.launch({ args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
  let bad = 0;
  for (const h of hashes) {
    const p = await browser.newPage({ viewport: { width: 900, height: 600 } });
    const errs = [];
    p.on('pageerror', e => errs.push(e.message));
    p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
    await p.goto(page_url + '#' + h);
    await p.waitForTimeout(800);
    await p.evaluate(() => window.__sects.pause(true));
    const canvas = await p.$('canvas');
    const files = [];
    for (let i = 1; i <= frames; i++) {
      // 1/60 초씩 나눠 넘겨야 탄·이펙트가 프레임마다 도는 것과 같아진다
      await p.evaluate(s => { for (let k = 0; k < Math.round(s * 60); k++) window.__sects.adv(1 / 60); }, step);
      await p.waitForTimeout(100);
      const f = path.join(out, `${h}_${i}.png`);
      await canvas.screenshot({ path: f });
      files.push(f);
    }
    // 모아 보기: 캔버스에 반 크기로 셋씩 줄 세움
    const shots = files.map(f => 'data:image/png;base64,' + require('fs').readFileSync(f).toString('base64'));
    const grid = await p.evaluate(async srcs => {
      const imgs = await Promise.all(srcs.map(s => new Promise(r => { const im = new Image(); im.onload = () => r(im); im.src = s; })));
      const w = imgs[0].width / 2, hh = imgs[0].height / 2, cols = Math.min(3, imgs.length);
      const c = document.createElement('canvas');
      c.width = w * cols; c.height = hh * Math.ceil(imgs.length / cols);
      const g = c.getContext('2d');
      imgs.forEach((im, i) => g.drawImage(im, (i % cols) * w, Math.floor(i / cols) * hh, w, hh));
      return c.toDataURL('image/png');
    }, shots);
    require('fs').writeFileSync(path.join(out, `${h}_grid.png`), Buffer.from(grid.split(',')[1], 'base64'));
    const n = await p.evaluate(() => window.__sects.n);
    console.log(`${h}: 적 ${n}, 오류 ${errs.length}${errs.length ? ' — ' + errs.slice(0, 3).join(' / ') : ''}`);
    if (errs.length) bad++;
    await p.close();
  }
  await browser.close();
  process.exit(bad ? 1 : 0);
})();
