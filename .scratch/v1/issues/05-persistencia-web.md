# 05 · Persistencia real en la PWA

Status: ready-for-agent
Blocked by: 01, 03
Cubre: 3MT-06

## What to build

Implementar en la PWA la persistencia elegida en el spike, de modo que los datos sobrevivan a recargas y al cierre del navegador.

## Acceptance criteria

- [ ] La implementación elegida pasa la suite de contrato
- [ ] Versionado de esquema para migraciones futuras
- [ ] Se solicita `navigator.storage.persist()` al guardar los primeros datos
- [ ] Los datos persisten tras recargar la PWA

## Blocked by

- 01-spike-persistencia-web
- 03-esqueleto-web

## Comments
