# 01 · Spike: persistencia en la web (SQLite WASM vs IndexedDB)

Status: in-review
Blocked by: ninguno
Cubre: 3MT-06

## What to build

Decidir con una prueba de concepto si la PWA usa EF Core + SQLite en WASM (OPFS) o repositorios propios sobre IndexedDB. El objetivo es evitar mantener dos implementaciones de persistencia divergentes.

## Acceptance criteria

- [x] Prototipo que guarda y lee tareas de ejemplo en ambas opciones en el navegador
- [x] Comparativa de tamaño de descarga, tiempo de arranque, soporte de navegadores y esfuerzo
- [x] Decisión registrada como ADR en `docs/adr/`
- [ ] Si gana SQLite WASM, la spec técnica (§1, §2.1, §3) se actualiza

## Blocked by

None - can start immediately

## Comments

- pm-1: el usuario delega la decisión en el PM (criterio: mejor encaje en dispositivos y gamas distintas, y mayor flexibilidad). Asignado a worker-2 (rama issue/01-spike-persistencia-web). El PM ratifica el ADR.
- pm-1: worker-2 quedó parado sin entregar (solo script de medida). Relanzado como worker-5 (rama issue/01-spike-persistencia-web). Por delegación del usuario (vía coordinador) el PM decide entre SQLite WASM e IndexedDB; ADR provisional, revisar por el usuario.
- worker-5: entregado. ADR 0001 (docs/adr/0001-persistencia-web.md) recomienda IndexedDB (opción B), estado propuesto/pendiente de ratificación del PM. Prototipo en spikes/persistencia-web/ (iniciado por worker-2, reconstruido y medido por worker-5). Último criterio (actualizar spec) sin marcar: solo aplica si gana SQLite WASM; el ADR propone ajustes menores a §1, §2.1, §3, §7 y §8 sin editar la spec. Limitaciones: mediciones en Chrome 155 headless en Windows; Safari/iOS y Firefox no probados (estimado por documentación); estrangulamiento de CPU x4 es emulado y ruidoso. La rama usada es issue/01-persistencia-web-w5 porque issue/01-spike-persistencia-web está en el worktree bloqueado de worker-2.
