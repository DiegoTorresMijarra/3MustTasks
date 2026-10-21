# 07 · Tachar tareas, progreso por nivel y banner de mínimos

Status: in-review
Blocked by: 06
Cubre: 3MT-10, 13, parte de 48

## What to build

El usuario tacha y destacha tareas con un toque, ve el progreso 'completadas X/mín.' por nivel y un banner mientras falte el mínimo a añadir.

## Acceptance criteria

- [x] Casos de uso: completar, destachar, quitar de hoy (va al pool) y reordenar
- [x] Se registra `PlanMinimumMetAt` y `CompletionMinimumMetAt` la primera vez que se cumplen
- [x] Se publica el evento `PlanChanged`
- [x] Banner 'Te falta añadir…' que abre Planificar y desaparece al cumplir
- [x] Tests de dominio y bUnit

## Blocked by

- 06-anadir-tarea-hoy-por-nivel

## Comments

- pm-1: asignado a worker-4 (reutilizado, ~25 % de contexto), rama issue/07-tachar-progreso-banner. Camino crítico.
- worker-4 (entrega), notas para pm-1:
  - **Destino del banner**: la pantalla Planificar es del issue 14 y aún no existe. El banner enlaza provisionalmente a `tarea/nueva?nivel=<primer nivel con faltante>`; el cambio a Planificar es una sola función (`TodayPage.PlanUrl`). Conviene anotarlo en el criterio de 14 ("es el destino del banner").
  - **PlanChanged**: `PlanEvents.PlanChanged` (`Action<DateOnly>`), servicio scoped en UI; se publica en añadir a Hoy, completar, destachar, quitar y reordenar (no al mandar al pool ni en rechazos). La página Hoy se suscribe y se refresca sola.
  - Quitar de hoy: `Outcome = Removed`, y si la tarea tenía `ScheduledDate == hoy` pasa a null (pool); solo se pueden quitar las pendientes. Completar pone `TaskItem.Status = Completed`; destachar lo devuelve a `Active`.
  - `ReorderAsync(ids)`: las asignaciones indicadas ocupan, en ese orden, las posiciones que ya tenían. Sin UI de reordenar ni de "quitar de hoy" (el issue solo pide los casos de uso; la UI llegará con 10/12).
  - `PlanMinimumMetAt`/`CompletionMinimumMetAt` se anotan en mutaciones (no al abrir la app) y no se borran si luego deja de cumplirse.
  - `TodayView` ahora lleva `Quota` y `LevelGroup.Completed` (cambio de forma de los records de 06).
