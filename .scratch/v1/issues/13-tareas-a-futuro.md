# 13 · Tareas programadas a futuro

Status: claimed
Blocked by: 11
Cubre: 3MT-19, 20

## What to build

El usuario asigna una fecha futura a una tarea; espera en la sección 'Programadas a futuro' del Pool y el día que toca entra sola al plan si cabe en el cupo (P5), o se avisa si no.

## Acceptance criteria

- [x] Sección 'Programadas a futuro' agrupada por fecha
- [x] Con `AutoAddScheduled` activo entra al plan en el cierre de día si hay cupo; si no, queda en el pool con aviso
- [x] Test de integración del ciclo completo: crear → no completar → cierre → pool → recuperar → completar, con el día 1 aún como no completada

## Blocked by

- 11-pool-ver-recuperar

## Comments
- pm-2 (nota del 10): A futuro debe enlazar a `tarea/{id}` (edición ya implementada, con selector de fecha).
- worker-6 (2026-10-22): entregado. Decisiones: (1) Pestañas Pendientes / A futuro / Archivadas como rutas (`pool`, `pool/futuro`, `pool/archivadas`) con el componente `PoolTabs`; sin contadores en las pestañas. (2) El autoañadido faltaba en `DayRolloverService` (solo existía el ajuste): ahora, con AutoAddScheduled, añade las Active con ScheduledDate == hoy si caben (QuotaPolicy); las que ya tienen cualquier asignación hoy (p. ej. quitadas por el usuario) se respetan; es idempotente. Las que no caben se devuelven en `RolloverResult.ScheduledToPool` y Hoy muestra un aviso (`TodayView.ScheduledOverflow`, solo con AutoAddScheduled activo). Con el ajuste apagado quedan en el pool sin aviso. (3) No se permite archivar tareas a futuro (el issue no lo pide). (4) Programadas con fecha ya pasada (app cerrada ese día) no se autoañaden: la spec dice == hoy; quedan en el pool. (5) Sin test bUnit del aviso en Hoy (cubierto a nivel de vista en Core).
- pm-3 (2026-10-22): asignado a worker-6 (reutilizado, ~25% de contexto; mismo ámbito Pool). Rama issue/13-tareas-a-futuro.
- pm-3 (2026-10-22): revisión del bloque: ScheduledTasksTests.Future_pool_lists_only_active_tasks_after_today_grouped_by_ascending_date es intermitente (falla ~50%, p. ej. orden d25-b antes que d25-a): el orden por CreatedAt y luego Id (Guid aleatorio) no es determinista en el test. Devuelto a worker-6 para arreglar el test o dar un orden estable; sin push hasta entonces.
