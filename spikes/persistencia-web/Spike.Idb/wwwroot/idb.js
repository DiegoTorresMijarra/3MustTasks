// Modulo idb minimo propio (opcion B). Sin dependencias.
const DB = '3mt-spike-idb';
let dbp;

function open() {
  return dbp ??= new Promise((res, rej) => {
    const r = indexedDB.open(DB, 1);
    r.onupgradeneeded = () => {
      const db = r.result;
      const s = db.createObjectStore('tasks', { keyPath: 'id' });
      s.createIndex('status', 'status');
    };
    r.onsuccess = () => res(r.result);
    r.onerror = () => rej(r.error);
  });
}

const done = (tx) => new Promise((res, rej) => {
  tx.oncomplete = () => res();
  tx.onerror = () => rej(tx.error);
  tx.onabort = () => rej(tx.error);
});
const req = (r) => new Promise((res, rej) => { r.onsuccess = () => res(r.result); r.onerror = () => rej(r.error); });

export async function init() { await open(); return true; }

export async function putMany(rows) {            // rows: [{id,status,doc}]
  const db = await open();
  const tx = db.transaction('tasks', 'readwrite');
  const s = tx.objectStore('tasks');
  for (const r of rows) s.put(r);
  await done(tx);
}
export async function get(id) {
  const db = await open();
  return (await req(db.transaction('tasks').objectStore('tasks').get(id))) ?? null;
}
export async function getAll(status) {
  const db = await open();
  const s = db.transaction('tasks').objectStore('tasks');
  return await req(status == null ? s.getAll() : s.index('status').getAll(status));
}
export async function del(id) {
  const db = await open();
  const tx = db.transaction('tasks', 'readwrite');
  const s = tx.objectStore('tasks');
  const existed = (await req(s.count(id))) > 0;
  s.delete(id);
  await done(tx);
  return existed;
}
export async function persist() {
  return navigator.storage?.persist ? await navigator.storage.persist() : null;
}
