// Mide tamaño transferido y tiempos de arranque de una publicación (Spike.Sqlite o Spike.Idb) con Chrome headless.
// Uso: node measure.mjs <dir-publish/wwwroot> [runs=5] [cpuThrottle=1]
// Requiere: npm i playwright-core (usa Chrome instalado, canal "chrome"; no descarga navegadores).
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { chromium } from 'playwright-core';

const root = path.resolve(process.argv[2]);
const runs = Number(process.argv[3] ?? 5);
const cpu = Number(process.argv[4] ?? 1);
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.dll': 'application/octet-stream', '.dat': 'application/octet-stream', '.css': 'text/css', '.png': 'image/png', '.webmanifest': 'application/manifest+json' };
let sent = 0, files = 0;
const server = http.createServer((req, res) => {
  let p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  if (p.endsWith('/')) p += 'index.html';
  let f = path.join(root, p);
  if (!f.startsWith(root) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { f = path.join(root, 'index.html'); }
  const ae = req.headers['accept-encoding'] ?? '';
  const h = { 'content-type': types[path.extname(f)] ?? 'application/octet-stream', 'cache-control': 'max-age=3600' };
  let file = f;
  if (ae.includes('br') && fs.existsSync(f + '.br')) { file = f + '.br'; h['content-encoding'] = 'br'; }
  else if (ae.includes('gzip') && fs.existsSync(f + '.gz')) { file = f + '.gz'; h['content-encoding'] = 'gzip'; }
  const buf = fs.readFileSync(file);
  sent += buf.length; files++;
  res.writeHead(200, h); res.end(buf);
}).listen(0);
const base = `http://localhost:${server.address().port}/`;

const median = (a) => [...a].sort((x, y) => x - y)[Math.floor(a.length / 2)];

async function load(page, q) {
  const t = Date.now();
  await page.goto(base + q, { waitUntil: 'commit' });
  await page.waitForFunction(() => window.spikeResult, null, { timeout: 120000, polling: 20 });
  return { wall: Date.now() - t, r: await page.evaluate(() => window.spikeResult) };
}

const out = { dir: root, cpuThrottle: cpu, runs };
const browser = await chromium.launch({ channel: 'chrome', headless: true });
out.browser = browser.version();

async function newCtx() {
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  if (cpu > 1) { const c = await ctx.newCDPSession(page); await c.send('Emulation.setCPUThrottlingRate', { rate: cpu }); }
  return { ctx, page };
}

// 1) Descarga en frío (contexto limpio) y arranque en frío
const cold = [], coldBytes = [], coldFiles = [];
for (let i = 0; i < runs; i++) {
  sent = 0; files = 0;
  const { ctx, page } = await newCtx();
  const { wall, r } = await load(page, '?phase=read');
  cold.push(wall); coldBytes.push(sent); coldFiles.push(files);
  out.firstResult ??= r;
  await ctx.close();
}
out.coldBootMs = { median: median(cold), all: cold };
out.transferredBytesCold = median(coldBytes);
out.requestsCold = median(coldFiles);

// 2) Escritura + persistencia tras recarga en el mismo contexto
const warm = [], persisted = [];
const writeRes = [];
for (let i = 0; i < runs; i++) {
  const { ctx, page } = await newCtx();
  const w = await load(page, '?phase=write');
  writeRes.push(w.r);
  sent = 0;
  const r = await load(page, '?phase=read');   // recarga: caché HTTP caliente + datos persistidos
  warm.push(r.wall);
  persisted.push({ tasksRead: r.r.TasksRead, sample: r.r.SampleTitles, err: r.r.Error, storage: r.r.Storage, openMs: r.r.OpenMs, readAllMs: r.r.ReadAllMs });
  out.persistTest = await page.evaluate(async () => ({ persisted: await navigator.storage.persisted?.(), estimate: await navigator.storage.estimate() }));
  await ctx.close();
}
out.writeScenario = writeRes[Math.floor(writeRes.length / 2)];
out.warmReloadMs = { median: median(warm), all: warm };
out.afterReload = persisted[0];
out.allReloadsOk = persisted.every((p) => !p.err && p.tasksRead === 1004);

await browser.close(); server.close();
console.log(JSON.stringify(out, null, 2));
