# 05 · Persistencia real en la PWA

Status: in-review
Blocked by: 01, 03
Cubre: 3MT-06

## What to build

Implementar en la PWA la persistencia elegida en el spike, de modo que los datos sobrevivan a recargas y al cierre del navegador.

## Acceptance criteria

- [x] La implementación elegida pasa la suite de contrato
- [x] Versionado de esquema para migraciones futuras
- [x] Se solicita `navigator.storage.persist()` al guardar los primeros datos
- [x] Los datos persisten tras recargar la PWA

## Blocked by

- 01-spike-persistencia-web
- 03-esqueleto-web

## Comments
- pm-1: asignado a worker-5 (reutilizado, ~15 %; rama issue/05-persistencia-web). Opción elegida: IndexedDB (ADR 0001).
- worker-5: entregado en rama issue/05-persistencia-web. Infrastructure.Browser = BrowserDatabase (logica comun, transacciones por lote atomico) + IDocumentStore (JsDocumentStore sobre idb.js / fake en tests) + repositorios + ajustes en localStorage (ISettingsStore es sincrono). Suite de contrato abstracta ejecutada contra el fake del almacen (misma logica C# y mismo idb.js no; el JS real se prueba con Playwright en tests/browser/persistence.e2e.mjs: persistencia tras recarga, esquema v1, migracion v1 a v2, persist()). Limitaciones: solo Chrome 155 headless en Windows; Safari/Firefox no probados; Chrome headless no concede persist() (se comprueba la llamada, no la concesion); ajustes en localStorage no cubiertos por el e2e de navegador (probado con fake); escrituras fuera de transaccion durante una transaccion en curso entran en ella (una sola pestana asumida). Serializador: DocumentJson (en Browser) porque Infrastructure.Shared aun no tiene uno; mover cuando llegue el backup (27).
