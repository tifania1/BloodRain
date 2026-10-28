// 배포된 WebGL이 브라우저에서 실제로 열리는지 확인한다(PlanetRacer에서 가져옴).
// br 로 받아 딱 한 번 푼 뒤, 나온 것이 진짜 자바스크립트/wasm 인지 본다.
// 쓰는 법: node tools/check_web_deploy.js <주소>
const https = require('https');
const zlib = require('zlib');

const base = (process.argv[2] || '').replace(/\/$/, '');
if (!base) { console.log('X  확인할 주소를 넘겨야 한다'); process.exit(1); }
console.log(`대상: ${base}`);

function get(url) {
  return new Promise((resolve, reject) => {
    https.get(url, { headers: { 'Accept-Encoding': 'br', 'User-Agent': 'deploy-check' } }, res => {
      const chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(chunks) }));
    }).on('error', reject);
  });
}

// Build/ 안 파일 이름은 빌드마다 바뀌므로 index.html 에 적힌 실제 이름을 뽑아 쓴다.
async function resolveTargets() {
  const r = await get(base + '/');
  if (r.status !== 200) throw new Error(`index.html http ${r.status}`);
  let htmlBuf = r.body;
  const enc = (r.headers['content-encoding'] || '').trim();
  if (enc === 'br') htmlBuf = zlib.brotliDecompressSync(htmlBuf);
  else if (enc === 'gzip') htmlBuf = zlib.gunzipSync(htmlBuf);
  const html = htmlBuf.toString('utf8');
  const names = [...html.matchAll(/buildUrl \+ "\/([^"]+)"/g)].map(m => m[1]);
  if (names.length === 0) throw new Error('index.html 에서 Build 파일 이름을 못 찾았다');
  const kindOf = n => n.endsWith('.wasm.br') ? 'wasm' : n.endsWith('.data.br') ? 'data' : 'js';
  const list = names.map(n => ({ path: '/Build/' + n, kind: kindOf(n) }));
  list.push({ path: '/', kind: 'html' });
  return list;
}

(async () => {
  let bad = 0, targets;
  try { targets = await resolveTargets(); }
  catch (e) { console.log('X  대상 목록을 못 만들었다: ' + e.message); process.exit(1); }
  for (const t of targets) {
    let r;
    try { r = await get(base + t.path); } catch (e) { console.log(`X  ${t.path}  요청 실패: ${e.message}`); bad++; continue; }
    const enc = r.headers['content-encoding'] || '';
    if (r.status !== 200) { console.log(`X  ${t.path}  http ${r.status}`); bad++; continue; }
    if (enc.split(',').length > 1) { console.log(`X  ${t.path}  Content-Encoding 이 "${enc}" 다. _headers 규칙이 겹쳤다`); bad++; continue; }
    let body = r.body;
    if (enc.trim() === 'br') {
      try { body = zlib.brotliDecompressSync(body); }
      catch (e) { console.log(`X  ${t.path}  br 로 못 푼다: ${e.message}`); bad++; continue; }
    }
    let ok = false;
    if (t.kind === 'wasm') ok = body.length > 4 && body[0] === 0x00 && body[1] === 0x61 && body[2] === 0x73 && body[3] === 0x6d;
    else if (t.kind === 'js') ok = /unityFramework|createUnityInstance|function|var /.test(body.slice(0, 200).toString('utf8'));
    else if (t.kind === 'html') ok = /<canvas|unity|<!DOCTYPE/i.test(body.slice(0, 400).toString('utf8'));
    else ok = body.length > 1000;
    console.log(`${ok ? 'O' : 'X'}  ${t.path}  enc=[${enc}]  ${r.body.length} → ${body.length}`);
    if (!ok) bad++;
  }
  console.log(bad === 0 ? '\n전부 통과.' : `\n${bad}개 실패.`);
  process.exit(bad === 0 ? 0 : 1);
})();
