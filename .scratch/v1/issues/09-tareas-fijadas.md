# 09 · Tareas fijadas que sobreviven al cierre de día

Status: resolved
Blocked by: 08
Cubre: 3MT-47

## What to build

El usuario fija una tarea; si no la completa, al cerrar el día pasa al plan del día siguiente en lugar de al pool y ocupa su hueco (P3).

## Acceptance criteria

- [x] Fijar y desfijar desde el formulario y desde un menú en Hoy
- [x] Las fijadas no completadas se asignan al día nuevo y cuentan para los cupos
- [x] Indicador 'Fijada · día N' en Hoy
- [x] El historial de cada día pasado la muestra como no completada
- [x] Tests de cierre de día con fijadas

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-2 (nota del 08): asunción abierta: las fijadas suman TimesPostponed al cerrar el día y replanificar fijadas no comprueba Max; revisar y decidir aquí. TodayService.GetAsync no invoca el cierre con la app en primer plano (mejora propuesta, no bloqueante).
- pm-2: asignado a worker-7 (nuevo; rama issue/09-tareas-fijadas). No en paralelo con 16.
- worker-7 (entrega), notas para pm-2:
  - **Ya hecho en el 08**: la replanificación de fijadas en el cierre (`DayRolloverService.PlanPinnedForTodayAsync`) y que cada día pasado quede `NotCompleted`. Aquí se añade el resto y los tests de extremo a extremo (`Core.Tests/PinnedTasksTests.cs`, `UI.Tests/PinnedTasksTests.cs`).
  - **Servicio**: `TodayService.SetPinnedAsync(taskId, pinned)` (solo tareas Active; publica `PlanChanged`); `NewTaskRequest.IsPinned` y `EditTaskRequest.IsPinned` (`bool?`, null = no cambiar). `TodayTask.PinnedDay` = "día N".
  - **UI**: casilla "Fijar" en el formulario (nueva y edición); menú "⋯" por tarea pendiente en Hoy con Fijar/Desfijar (menú en línea propio, no `MudMenu`, para poder testearlo con bUnit); badge "Fijada · día N".
  - **Decisión "día N"**: 1 el día en que está en el plan + 1 por cada día consecutivo anterior en que la tarea tuvo una asignación `NotCompleted` (se calcula del historial, no de `TimesPostponed`, que también cuenta arrastres previos desde el pool y la haría inexacta). Una fijada creada hoy es "día 1".
  - **Decisión (asunción del 08) `TimesPostponed`**: las fijadas siguen sumándolo cada día sin completar. La spec §4.1 lo suma a toda asignación Planned que pasa a NotCompleted sin excepción, y el PRD §6.2 lo describe como "veces arrastrada". Test `Decision_each_missed_day_counts_a_postponement_also_for_pinned_tasks`.
  - **Decisión (asunción del 08) `Max`**: replanificar fijadas NO comprueba `Max`. PRD/spec dicen que la fijada "no va al pool" y "cuenta para los cupos"/"ocupa su hueco"; respetar `Max` la mandaría al pool o la perdería, contra el texto. Efecto: si se baja `Max` de un nivel entre un día y otro, el nivel puede quedar por encima y marcado como lleno (no admite más hasta completar o desfijar). Test `Decision_a_pinned_task_is_never_dropped_even_if_the_level_max_was_lowered`. Si el PM prefiere otra cosa, es un cambio local en `PlanPinnedForTodayAsync`.
  - **Propuesta (no hecha)**: la spec §3.1 podría mencionar el cálculo de "día N".
  - **Fuera de alcance**: no se han tocado SettingsPage ni el modelo de niveles. La vista de historial (issue 22) aún no existe: el criterio del historial queda cubierto a nivel de datos (asignación `NotCompleted` por cada día cerrado, con test).
  - Build `-warnaserror` ok; Core 98, Infrastructure 51, UI 63 tests en verde. No probado manualmente en navegador/emulador.
- pm-2: revisado e integrado en main en bloque con 10/09/15/11/05 (conflictos de Strings.resx/DI/css resueltos conservando ambos lados; añadido DeleteLevelAsync a BrowserConfigRepository por el nuevo puerto de 15). build -warnaserror 0 avisos; tests Core 129, Infra 84, UI 85 en verde. Resolved.
