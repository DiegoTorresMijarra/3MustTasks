// Persistencia del fichero .db (opcion A): OPFS si hay createWritable; si no, IndexedDB (blob).
let backend = 'none';
export const getBackend = () => backend;

const IDB = '3mt-spike-sqlite-file';
const idb = () => new Promise((res, rej) => {
  const r = indexedDB.open(IDB, 1);
  r.onupgradeneeded = () => r.result.createObjectStore('files');
  r.onsuccess = () => res(r.result);
  r.onerror = () => rej(r.error);
});
const idbReq = (r) => new Promise((res, rej) => { r.onsuccess = () => res(r.result); r.onerror = () => rej(r.error); });

async function opfsFile(create) {
  const root = await navigator.storage.getDirectory();
  return await root.getFileHandle('app.db', { create });
}
const hasOpfsWrite = () => !!(navigator.storage?.getDirectory && globalThis.FileSystemFileHandle?.prototype?.createWritable);

export async function load() {
  if (hasOpfsWrite()) {
    backend = 'opfs';
    try { return new Uint8Array(await (await (await opfsFile(false)).getFile()).arrayBuffer()); }
    catch (e) { if (e.name === 'NotFoundError') return null; throw e; }
  }
  backend = 'idb-blob';
  const db = await idb();
  const v = await idbReq(db.transaction('files').objectStore('files').get('app.db'));
  return v ? new Uint8Array(v) : null;
}

export async function save(bytes) {
  if (backend === 'opfs') {
    const w = await (await opfsFile(true)).createWritable();
    await w.write(bytes); await w.close();
  } else {
    const db = await idb();
    const tx = db.transaction('files', 'readwrite');
    tx.objectStore('files').put(bytes.slice().buffer, 'app.db');
    await new Promise((res, rej) => { tx.oncomplete = res; tx.onerror = () => rej(tx.error); });
  }
  return navigator.storage?.persist ? await navigator.storage.persist() : null;
}
