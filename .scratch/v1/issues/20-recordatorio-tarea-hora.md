# 20 · Recordatorio de una tarea a su hora

Status: resolved
Blocked by: 18
Cubre: 3MT-28 (parte)

## What to build

Las tareas con hora y recordatorio notifican a su hora o con la antelación elegida (0/5/15/30/60 min); también el resumen nocturno y la llegada de una tarea a futuro.

## Acceptance criteria

- [x] Antelación configurable por tarea y por defecto en ajustes
- [x] Resumen nocturno 'Te quedan N' opcional (21:00, desactivado)
- [x] Aviso al inicio del día para tareas a futuro
- [x] Cubierto por tests del planner

## Blocked by

- 18-recordatorio-planificacion-android

## Comments
- pm-3 (2026-10-22): asignado a worker-9. Rama issue/20-recordatorio-tarea-hora.
- worker-9 (2026-10-24): entregado, `in-review`.

### Qué se hizo
- **Planner** (`ReminderPlanner.Plan(settings, now, DayProgress, IReadOnlyList<ReminderTask>? tasks = null)`): tres tipos nuevos en `NotificationKind`.
  - `TaskReminder`: tarea con hora y antelación (0/5/15/30/60 o cualquier ≥ 0) → `hora − antelación`. Sin antelación (`ReminderOffset` null) o sin hora no avisa; los pasados se descartan; una hora anterior a `DayStartsAt` cae al día natural siguiente. Cubre hoy y los 6 días siguientes.
  - `EveningSummary`: "Te quedan N tareas" a la hora del resumen (21:00), solo hoy, solo si está activado (por defecto no) y quedan tareas sin completar (`DayProgress.PendingToday`, parámetro nuevo con valor por defecto 0).
  - `FutureTaskArrival`: aviso del día en que llega una tarea programada a futuro (días posteriores a hoy), una notificación por tarea.
- **Rangos de IDs** (sin colisión, documentados en el planner y con test): PlanReminder `1×10M+día`, Nag `2×10M+día×10+i`, Completion `3×10M+día`, **EveningSummary `5×10M+día`**, **TaskReminder `100M+día×100+ranura`**, **FutureTaskArrival `300M+día×100+ranura`** (ranura 0..99 por día = orden por hora y Guid; se asigna antes de descartar las pasadas, así el ID no cambia al avanzar el reloj; más de 100 por día se ignoran). El hueco 4×10M queda libre.
- **Sync**: `ReminderSyncService` pasa al planner las tareas del plan de hoy sin completar y las `Active` con `ScheduledDate` en la ventana (hoy..+6) aún fuera del plan, y cuenta pendientes. Para reprogramar al editar horas: `TaskEditService.UpdateAsync` lanza `PlanChanged` si cambian hora/antelación/fecha/título (antes solo si tocaba el plan), `DeleteAsync` también si la tarea tenía fecha, y la importación `.ics` si crea tareas a futuro.
- **Ajustes** (planos, como en el 19): `DefaultReminderOffsetMin` (0), `EveningSummaryEnabled` (false), `EveningSummaryTime` (21:00), `FutureTaskAlertEnabled` (true) + migración a mano `AddTaskReminderSettings` (`HasPendingModelChanges` en verde). UI en Ajustes > Notificaciones (bloque propio) y strings en bloque propio de `Strings.resx`.
- **Formulario de tarea**: la antelación se elige por tarea (select existente); al pasar una tarea de "sin hora" a "con hora" se preselecciona la antelación por defecto de Ajustes (no pisa una elección manual).

### Decisiones a revisar (PM / usuario)
- `ReminderOffset == null` sigue significando "Sin recordatorio" (opción existente del formulario). Por tanto "antelación por defecto en ajustes" actúa como preselección al poner hora a una tarea, no como valor implícito de las que ya tienen hora sin antelación (p. ej. importadas de `.ics`, que no avisan). Alternativa: tratar null como "usa la de Ajustes" y añadir una opción explícita "ninguno".
- El aviso de tarea a futuro suena a la **hora de planificación** de ese día (07:30 por defecto), no a la "hora de inicio del día" literal: con `DayStartsAt` = 00:00 por defecto sería una notificación a medianoche. Es una sola función (`AddFutureArrivals`) si se quiere cambiar. No depende de `AutoAddScheduled` ni del recordatorio de planificación.
- El resumen nocturno solo se programa para hoy y cuenta las tareas del plan de hoy sin completar (no las del pool).
- Spec §3/§4.5 (proponer): reflejar los cuatro ajustes planos nuevos y que el ID es tipo + día + ranura determinista (no tipo + día + tarea).
- El 21 (PWA) puede reutilizar tal cual `ReminderPlanner.Plan(...)` + `ReminderSyncService`.

### No verificado (sin emulador)
- No se ha visto salir ninguna de las tres notificaciones en un dispositivo. Guion manual (tras el del 18, con permiso concedido): (1) tarea de hoy con hora a +3 min y "A la hora", cerrar la app deslizándola de recientes: debe sonar a su hora; repetir con "5 min antes" y hora a +8 min; (2) activar el resumen nocturno con hora a +2 min y dejar una tarea sin tachar: debe salir "Te queda 1 tarea"; tachándola antes de la hora no debe salir; (3) tarea para mañana con hora: al llegar mañana a la hora de planificación debe salir "Hoy toca una tarea" y, a su hora, el recordatorio; (4) desactivar "Avisar cuando llega una tarea programada": no sale; (5) al tocar cualquiera abre Hoy.
- pm-3 (2026-10-22): resolved; integrado en main (build -warnaserror y tests 323/160/221 en verde x2). Nada verificado en dispositivo. DUDAS PARA EL USUARIO: (1) ReminderOffset==null = "sin recordatorio": las tareas con hora importadas de .ics no avisan; alternativa: null = usar el de Ajustes y opción explícita "ninguno". (2) El aviso de tarea a futuro suena a la hora de planificación (07:30), no a medianoche. (3) El resumen nocturno solo cuenta el plan de hoy, no el pool. Propuesta de spec §3/§4.5 (4 ajustes planos, IDs tipo+día+ranura) sin aplicar.
