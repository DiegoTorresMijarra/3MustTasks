// Almacen de documentos sobre IndexedDB (ADR 0001). Sin dependencias.
// Cada tabla es un object store con keyPath 'key'; la fila es { key, doc (JSON en cadena), ix (valor indexado o null) }.
// El esquema se versiona con la version de IndexedDB: MIGRATIONS[n-1] lleva de la version n-1 a la n.
const DB_NAME = '3musttasks';
export const TABLES = ['tasks', 'assignments', 'logs', 'levels', 'categories', 'favorites', 'recurrences', 'meta'];

// Para una version 2 o posterior, anadir aqui una funcion (db, tx, oldVersion) => void.
export const MIGRATIONS = [
  (db) => {
    for (const name of TABLES) {
      const s = db.createObjectStore(name, { keyPath: 'key' });
      s.createIndex('ix', 'ix');
    }
  },
];

let dbp = null;

// extraMigrations solo lo usan las pruebas para simular una version futura del esquema.
export function open(version, extraMigrations) {
  const migrations = extraMigrations ? MIGRATIONS.concat(extraMigrations) : MIGRATIONS;
  if (version > migrations.length) return Promise.reject(new Error(`Falta la migracion a la version ${version}`));
  return (dbp ??= new Promise((res, rej) => {
    const r = indexedDB.open(DB_NAME, version);
    r.onupgradeneeded = (e) => {
      for (let v = e.oldVersion; v < version; v++) migrations[v](r.result, r.transaction, e.oldVersion);
    };
    r.onsuccess = () => res(r.result);
    r.onerror = () => rej(r.error);
    r.onblocked = () => rej(new Error('IndexedDB bloqueada por otra pestana con una version anterior'));
  }).catch((e) => { dbp = null; throw e; }));
}

export async function close() {
  const p = dbp; dbp = null;
  if (!p) return false;
  (await p).close();
  return true;
}

const req = (r) => new Promise((res, rej) => { r.onsuccess = () => res(r.result); r.onerror = () => rej(r.error); });
const done = (tx) => new Promise((res, rej) => {
  tx.oncomplete = () => res();
  tx.onerror = () => rej(tx.error);
  tx.onabort = () => rej(tx.error ?? new Error('Transaccion abortada'));
});
const need = () => { if (!dbp) throw new Error('IndexedDB no abierta'); return dbp; };

export async function get(table, key) {
  const db = await need();
  const row = await req(db.transaction(table).objectStore(table).get(key));
  return row ? row.doc : null;
}

export async function getAll(table, ix) {
  const db = await need();
  const s = db.transaction(table).objectStore(table);
  const rows = await req(ix == null ? s.getAll() : s.index('ix').getAll(ix));
  return rows.map((r) => ({ key: r.key, doc: r.doc }));
}

// ops: [{ kind: 'put'|'delete', table, key, doc, ix }]. Una unica transaccion: o se aplican todas o ninguna.
export async function apply(ops) {
  if (ops.length === 0) return;
  const db = await need();
  const tables = [...new Set(ops.map((o) => o.table))];
  const tx = db.transaction(tables, 'readwrite');
  for (const o of ops) {
    const s = tx.objectStore(o.table);
    if (o.kind === 'put') s.put({ key: o.key, doc: o.doc, ix: o.ix ?? null });
    else s.delete(o.key);
  }
  await done(tx);
}

export async function persist() {
  if (!navigator.storage?.persist) return false;
  if (await navigator.storage.persisted?.()) return true;
  return await navigator.storage.persist();
}
