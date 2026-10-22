# 13 · Tareas programadas a futuro

Status: claimed
Blocked by: 11
Cubre: 3MT-19, 20

## What to build

El usuario asigna una fecha futura a una tarea; espera en la sección 'Programadas a futuro' del Pool y el día que toca entra sola al plan si cabe en el cupo (P5), o se avisa si no.

## Acceptance criteria

- [ ] Sección 'Programadas a futuro' agrupada por fecha
- [ ] Con `AutoAddScheduled` activo entra al plan en el cierre de día si hay cupo; si no, queda en el pool con aviso
- [ ] Test de integración del ciclo completo: crear → no completar → cierre → pool → recuperar → completar, con el día 1 aún como no completada

## Blocked by

- 11-pool-ver-recuperar

## Comments
- pm-2 (nota del 10): A futuro debe enlazar a `tarea/{id}` (edición ya implementada, con selector de fecha).
- pm-3 (2026-10-22): asignado a worker-6 (reutilizado, ~25% de contexto; mismo ámbito Pool). Rama issue/13-tareas-a-futuro.
