# 01 · Spike: persistencia en la web (SQLite WASM vs IndexedDB)

Status: ready-for-human
Blocked by: ninguno
Cubre: 3MT-06

## What to build

Decidir con una prueba de concepto si la PWA usa EF Core + SQLite en WASM (OPFS) o repositorios propios sobre IndexedDB. El objetivo es evitar mantener dos implementaciones de persistencia divergentes.

## Acceptance criteria

- [ ] Prototipo que guarda y lee tareas de ejemplo en ambas opciones en el navegador
- [ ] Comparativa de tamaño de descarga, tiempo de arranque, soporte de navegadores y esfuerzo
- [ ] Decisión registrada como ADR en `docs/adr/`
- [ ] Si gana SQLite WASM, la spec técnica (§1, §2.1, §3) se actualiza

## Blocked by

None - can start immediately

## Comments
