# 22 · Historial: vista Día y métricas

Status: in-review
Blocked by: 08
Cubre: 3MT-34, 35

## What to build

El usuario consulta un día pasado en solo lectura, navega con ←/→ y ve % de cumplimiento, racha actual y mejor racha.

## Acceptance criteria

- [x] Resumen por día para un rango (planificadas, completadas, % y si se cumplió)
- [x] Racha actual y mejor racha de días cumplidos
- [x] Vista Día de solo lectura con el resultado de cada tarea
- [x] Tests de dominio y bUnit

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-2 (nota del 10): borrar una tarea deja asignaciones huérfanas en días pasados; el historial debe ignorar las que apunten a una tarea inexistente.
- pm-2: asignado a worker-10 (nuevo; rama issue/22-historial-dia-metricas).
- worker-10 (entrega), notas para pm-2:
  - **Servicio**: `Core/Services/HistoryService` (scoped): `GetSummariesAsync(from,to)`, `GetDayAsync(date)`, `GetMetricsAsync(from,to)`. Sin métodos nuevos en puertos (usa `ListClosedLogsAsync`, `GetAssignmentsAsync`), así que no hay cambios en InMemory/Sqlite/Browser.
  - **Huérfanas**: se ignoran las asignaciones cuya tarea ya no existe (resumen y detalle). Las fijadas sin completar salen como NotCompleted cada día.
  - **Definiciones (conservadoras, a confirmar)**: planificadas = Completed + NotCompleted (las Removed no cuentan); % del día = completadas/planificadas; "% de cumplimiento" de las métricas = días cumplidos / días con plan (log cerrado) del rango (la UI usa los últimos 30 días hasta ayer). Mejor racha = mayor tramo de días consecutivos cerrados con Fulfilled; un día sin plan la corta; nunca menor que la racha actual (que reutiliza `StreakCalculator`, y cuenta hoy si ya cumplió).
  - **UI**: `CalendarPage` pasa a ser la vista Día en `/calendario` y `/calendario/{yyyy-MM-dd}` (por defecto ayer; ← siempre, → deshabilitada al llegar a ayer; fechas inválidas o >= hoy caen en ayer). Solo lectura (sin círculo ni enlace de edición). Semana y Mes quedan para los issues siguientes; el título de pestaña sigue siendo "Calendario".
  - Cada día se muestra con el resultado en texto (Completada / No completada / Quitada del plan), sin estilos nuevos por estado más allá del tachado de completadas.
