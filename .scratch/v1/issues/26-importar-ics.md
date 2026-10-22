# 26 · Importar desde .ics

Status: claimed
Blocked by: 25, 11
Cubre: 3MT-39

## What to build

El usuario elige un `.ics`, previsualiza los eventos, asigna nivel por defecto e importa: lo de hoy al plan si cabe, lo futuro al pool a futuro.

## Acceptance criteria

- [ ] Previsualización con selección de eventos
- [ ] Deduplicado por UID
- [ ] Soporta eventos de día completo y con hora, y RRULE básicas
- [ ] Tests con ficheros de ejemplo de Google Calendar

## Blocked by

- 25-exportar-ics
- 11-pool-ver-recuperar

## Comments
- pm-3 (2026-10-22): asignado a worker-5 (reutilizado, ~30% de contexto; mismo ámbito ICS). Rama issue/26-importar-ics.
