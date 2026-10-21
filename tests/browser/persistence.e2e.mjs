// E2E de persistencia en navegador real (Chrome headless) contra el publish de App.Web.
// Uso: node persistence.e2e.mjs <dir-publish/wwwroot>
// Requiere: npm ci (playwright-core) y Chrome instalado (canal "chrome"; no descarga navegadores).
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { chromium } from 'playwright-core';

const root = path.resolve(process.argv[2] ?? '');
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css', '.png': 'image/png', '.webmanifest': 'application/manifest+json' };
const server = http.createServer((req, res) => {
  const p = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  let f = path.join(root, p.endsWith('/') ? p + 'index.html' : p);
  if (!f.startsWith(root) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) f = path.join(root, 'index.html');
  const ae = req.headers['accept-encoding'] ?? '';
  const h = { 'content-type': types[path.extname(f)] ?? 'application/octet-stream' };
  let file = f;
  if (ae.includes('br') && fs.existsSync(f + '.br')) { file = f + '.br'; h['content-encoding'] = 'br'; }
  res.writeHead(200, h); res.end(fs.readFileSync(file));
}).listen(0);
const base = `http://localhost:${server.address().port}/`;

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const ctx = await browser.newContext({ serviceWorkers: 'block' });
const page = await ctx.newPage();
const errors = [];
page.on('pageerror', (e) => errors.push(String(e)));
page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
// Espía de storage.persist() para comprobar que se pide al guardar los primeros datos.
await page.addInitScript(() => {
  window.__persistCalls = 0;
  const orig = navigator.storage.persist?.bind(navigator.storage);
  navigator.storage.persist = async () => { window.__persistCalls++; return orig ? await orig() : false; };
});

const results = [];
const check = (name, ok, detail = '') => { results.push({ name, ok, detail }); console.log(`${ok ? 'OK  ' : 'FAIL'} ${name} ${detail}`); };

async function open() {
  await page.goto(base, { waitUntil: 'load' });
  await page.waitForSelector('[data-testid=fab-add]', { timeout: 120000 });
}
async function idbRows(table) {
  return await page.evaluate((t) => new Promise((res, rej) => {
    const r = indexedDB.open('3musttasks');
    r.onsuccess = () => { const q = r.result.transaction(t).objectStore(t).getAll(); q.onsuccess = () => { res(q.result); r.result.close(); }; q.onerror = () => rej(q.error); };
    r.onerror = () => rej(r.error);
  }), table);
}

try {
  await open();
  check('el primer arranque (siembra de niveles) ya solicita persistencia', (await page.evaluate(() => window.__persistCalls)) >= 1);

  // 1) Crear una tarea desde la UI.
  await page.click('[data-testid=fab-add]');
  await page.fill('[data-testid=task-form] input', 'Tarea persistente');
  await page.click('[data-testid=save]');
  await page.waitForSelector('[data-testid=task-title]');
  

  // 2) Tachar la tarea y recargar.
  await page.click('[data-testid=toggle]');
  await page.waitForSelector('[data-testid=task].done');
  await open();
  await page.waitForSelector('[data-testid=task-title]');
  const titles = await page.locator('[data-testid=task-title]').allTextContents();
  check('la tarea sobrevive a recargar', titles.includes('Tarea persistente'), JSON.stringify(titles));
  check('el tachado sobrevive a recargar', (await page.locator('[data-testid=task].done').count()) === 1);

  // 3) Contenido real de IndexedDB: version de esquema y documentos.
  const info = await page.evaluate(() => new Promise((res) => { const r = indexedDB.open('3musttasks'); r.onsuccess = () => { res({ v: r.result.version, stores: [...r.result.objectStoreNames] }); r.result.close(); }; }));
  check('IndexedDB en la version de esquema 1 con todos los stores', info.v === 1 && info.stores.length === 8, JSON.stringify(info));
  const tasks = await idbRows('tasks');
  check('el documento de tarea esta en IndexedDB (JSON + indice por estado)', tasks.length === 1 && tasks[0].ix === JSON.parse(tasks[0].doc).Status && JSON.parse(tasks[0].doc).Title === 'Tarea persistente');
  const assignments = await idbRows('assignments');
  check('la asignacion del dia esta en IndexedDB con indice por fecha', assignments.length === 1 && /^\d{4}-\d\d-\d\d$/.test(assignments[0].ix) && JSON.parse(assignments[0].doc).Outcome === 'Completed');

  // 4) Migracion de esquema: pasar a la version 2 conserva los datos y ejecuta la migracion.
  const mig = await page.evaluate(async () => {
    const m = await import('./_content/ThreeMustTasks.Infrastructure.Browser/idb.js');
    await m.close();
    await m.open(2, [(db) => db.createObjectStore('futuro', { keyPath: 'key' })]);
    const kept = (await m.getAll('tasks', null)).length;
    const db = await new Promise((res) => { const r = indexedDB.open('3musttasks'); r.onsuccess = () => res(r.result); });
    const out = { v: db.version, hasNew: db.objectStoreNames.contains('futuro'), kept };
    db.close(); await m.close();
    return out;
  });
  check('migracion v1 a v2 aplica la migracion y conserva datos', mig.v === 2 && mig.hasNew && mig.kept === 1, JSON.stringify(mig));
  check('sin errores de consola/pagina', errors.length === 0, errors.join(' | '));
} catch (e) {
  check('ejecucion sin excepciones', false, String(e));
} finally {
  await browser.close(); server.close();
}
const failed = results.filter((r) => !r.ok).length;
console.log(failed ? `${failed} fallos` : 'TODO OK');
process.exit(failed ? 1 : 0);


