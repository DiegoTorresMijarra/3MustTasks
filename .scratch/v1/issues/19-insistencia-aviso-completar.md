# 19 · Insistencia por plan incompleto y aviso de la tarde

Status: claimed
Blocked by: 18
Cubre: 3MT-31, 33

## What to build

Mientras falte el mínimo a añadir, recordatorios cada X min con tope diario; por la tarde, aviso si falta el mínimo a completar. Ambos se cancelan al cumplirlos.

## Acceptance criteria

- [x] Intervalo y tope configurables (por defecto 30 min y 4)
- [x] Aviso de la tarde a las 18:00 configurable
- [x] Test de integración: al añadir la última tarea requerida desaparecen las insistencias de hoy
- [x] Modo estricto opcional que bloquea salir de Planificar

## Blocked by

- 18-recordatorio-planificacion-android

## Comments
- pm-3 (2026-10-22): asignado a worker-4 (reutilizado, ~30% de contexto; mismo ámbito). Rama issue/19-insistencia-aviso-completar.
- worker-4 (2026-10-23): entregado, `in-review`.

### Qué se hizo
- **Planner** (`ReminderPlanner`): nuevos tipos `Nag` y `CompletionReminder`; entrada `DayProgress` (mínimos cumplidos + qué falta por completar). Insistencia: solo hoy, mientras falte el mínimo a añadir, en una rejilla fija `hora de planificación + k × intervalo` (k = 1..tope, tope duro 9), así el tope diario se respeta aunque se recalcule varias veces; no cruza el fin del día lógico. IDs `2×10M + DayNumber×10 + k`. Aviso de la tarde: solo hoy, `3×10M + DayNumber`, texto "Te queda 1 Must por completar" / "Te quedan 1 Must y 2 Recommended por completar", su toque abre Hoy (ruta vacía). La sobrecarga antigua `Plan(settings, now, bool)` se conserva y solo planifica el recordatorio de planificación.
- **Sync** (`ReminderSyncService`): calcula el progreso de hoy con `QuotaPolicy` (con los niveles de fábrica si aún no se sembraron) y los hitos `PlanMinimumMetAt`/`CompletionMinimumMetAt` del log (si el hito ya se anotó no vuelve a avisar, spec §4.2). Al cumplir el mínimo, `PlanChanged` reprograma y las insistencias de hoy desaparecen.
- **Ajustes**: `NagEnabled` (true), `NagIntervalMin` (30), `NagMaxPerDay` (4), `CompletionReminderEnabled` (true), `CompletionReminderTime` (18:00) + migración `AddNagAndCompletionSettings` (a mano; `HasPendingModelChanges` en verde). UI en Ajustes > Notificaciones: valores fuera de rango (5-240 min, 1-9 avisos) se ignoran.
- **Modo estricto**: `PlanPage` registra un `LocationChangingHandler` que impide salir de `/planificar` mientras `StrictMode` y falte el mínimo a añadir; sigue permitido cambiar de pestaña o parámetros dentro de Planificar. Muestra aviso fijo y mensaje al bloquear. El ajuste `StrictMode` ya existía.

### Decisiones a revisar (PM / usuario)
- La insistencia cuelga del recordatorio de planificación: no suena si este está desactivado o si ese día de la semana está desmarcado. Si se prefiere independiente, es quitar dos condiciones en `AddNags`.
- El aviso de la tarde solo se programa para hoy (de los días siguientes no se sabe qué faltará); si el usuario no abre la app un día no hay aviso de la tarde de ese día. El recordatorio de planificación sí cubre los 7 días.
- La primera insistencia sale `intervalo` minutos después de la hora de planificación (el propio recordatorio de planificación es la 0).
- Spec §3: los ajustes quedan planos (`NagEnabled`, `NagIntervalMin`, `NagMaxPerDay`, `CompletionReminderEnabled`, `CompletionReminderTime`), no objetos anidados; proponer reflejarlo.

### No verificado
- Sin emulador: no se ha visto salir ninguna insistencia ni el aviso de la tarde, ni que el modo estricto bloquee el botón "atrás" del sistema en Android (en pruebas bUnit el `LocationChangingHandler` bloquea la navegación interna; el gesto atrás del WebView no se ha probado). Guion: con el guion del issue 18, poner hora de planificación 2 min en el futuro, intervalo 5 y tope 2, sin añadir tareas: deben salir el recordatorio y 2 insistencias; añadir el mínimo y comprobar que no salen más; con modo estricto, intentar salir de Planificar con el menú y con "atrás".
- Test intermitente ajeno: `ScheduledTasksTests.Future_pool_lists_only_active_tasks_after_today_grouped_by_ascending_date` (issue 13) falla de forma intermitente; no se ha tocado.
