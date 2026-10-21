# 08 · Cierre de día idempotente: lo no hecho pasa al pool

Status: resolved
Blocked by: 07
Cubre: 3MT-14, 48

## What to build

Al abrir la app o volver a primer plano, los días pendientes se cierran: las tareas no completadas pasan al pool sumando `TimesPostponed`, el día queda en el historial como cumplido o no cumplido y el historial no se reescribe.

## Acceptance criteria

- [x] Algoritmo de la spec §4.1 en una sola transacción
- [x] Se invoca al iniciar y al reanudar en ambos hosts
- [x] Tests: un día, varios días saltados, idempotencia, inicio de día distinto de 00:00 y día sin plan
- [x] `DayLog.Fulfilled` se calcula al cerrar; la racha cuenta solo días cumplidos
- [x] Asunción a confirmar: pasar un día sin completar suma `TimesPostponed` también a las fijadas

## Blocked by

- 07-tachar-progreso-banner

## Comments
- pm-1: asignado a worker-6 (rama issue/08-cierre-de-dia). Camino crítico.
- worker-6 (entrega), notas para pm-1:
  - **Servicio**: `Core/Services/DayRolloverService.RunAsync()` (singleton en `AddThreeMustTasksUI`) y `StreakCalculator.GetAsync()` (racha). Devuelve `RolloverResult.ClosedDates`; publica `PlanChanged(hoy)` solo si cerró algo. Serializado con un semáforo (llamadas concurrentes cierran cada día una vez).
  - **Transacción**: nuevo `IUnitOfWork.ExecuteInTransactionAsync(Func<CancellationToken,Task>)`. SQLite: transacción real compartida por todos los repositorios del flujo (nuevo `SqliteContextFactory`, ámbito ambiental con `AsyncLocal`); en memoria: instantánea + restauración si falla. Cubierto por contratos comunes a ambas implementaciones (`UnitOfWorkContract`), incluido rollback. 09, 11, 16, 22 y 27 pueden usar ese mismo método.
  - **Puertos ampliados (IDayRepository)**: `ListUnclosedDatesAsync(before)` (días < before con asignación Planned/Completed o log sin cerrar, y sin log cerrado) y `ListClosedLogsAsync(from,to)`. Así no hace falta "fecha de instalación": se cierran solo los días con algo pendiente, y un día sin plan no genera `DayLog`. Con tests de contrato.
  - **Fulfilled** = mínimos a completar de todos los niveles activos alcanzados (`QuotaPolicy`), con los niveles vigentes al cerrar. Se conservan `PlanMinimumMetAt`/`CompletionMinimumMetAt` del log.
  - **Racha**: días cerrados consecutivos con `Fulfilled == true` hasta ayer; un día no cumplido o sin plan (sin log) la corta; hoy suma 1 si ya tiene `CompletionMinimumMetAt`, y si no, no la corta. Aún no hay UI que la muestre.
  - **Fijadas** (spec §4.1, línea de tareas fijadas): las fijadas Active no completadas de los días cerrados se replanifican como nueva asignación Planned hoy (sin duplicar si ya están), sin comprobar `Max`. La spec no dice nada del cupo; si el PM prefiere respetarlo, es un cambio en `PlanPinnedForTodayAsync`.
  - **Hosts**: Web: `Program.cs` ejecuta el cierre antes de `RunAsync` y registra `visibilitychange` (index.html) -> `ForegroundListener.OnForeground`. MAUI: `Window.Created` y `Window.Resumed` en `App.xaml.cs`. Los errores del cierre se registran y no tumban la app.
  - **Cambio de ciclo de vida DI** (UI): `LogicalDayCalculator` y `PlanEvents` pasan de scoped a singleton, porque en MAUI el cierre al reanudar corre desde el contenedor raíz y `PlanChanged` debe llegar al ámbito del WebView. Hay test.
  - **Asunción vigente**: pasar un día sin completar suma `TimesPostponed` también a las fijadas (implementado tal cual; test `Pinned_unfinished_task_counts_a_postponement_and_is_replanned_for_today`).
  - **Fuera de alcance** (resto de §4.1, otros issues): generar recurrentes, autoañadir programadas con `AutoAddScheduled`, `ReminderPlanner.Rebuild()`. `TodayService.GetAsync` no invoca el cierre ("antes de cada consulta del plan de hoy" de la spec): solo inicio y reanudación, como pide el criterio. Un cambio de día con la app abierta y en primer plano no se cierra hasta el siguiente primer plano; se puede proponer como mejora.
  - **Verificación manual pendiente**: no he probado en emulador ni en navegador real que `Window.Resumed` / `visibilitychange` disparen el cierre (solo cableado estático + build). App.Web y App.Maui compilan con `-warnaserror`.
- pm-1: revisado y aceptado. Core 66, Infrastructure 51, UI 47 tests en verde con -warnaserror; App.Web compila; integrado en main. Pendiente de verificación manual: Window.Resumed (MAUI) y visibilitychange (web). Mejora propuesta: TodayService.GetAsync no invoca el cierre (cambio de día con la app en primer plano) -> issue posterior. Asunción vigente: las fijadas también suman TimesPostponed y la replanificación de fijadas no comprueba Max (a revisar en 09).
