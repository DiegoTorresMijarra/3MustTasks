# 16 · Tareas recurrentes

Status: in-review
Blocked by: 08, 10
Cubre: 3MT-24, 25

## What to build

El usuario define una regla (diaria, laborables, días de la semana, cada N días, mensual) y cada día se generan las instancias en el plan; si superan el cupo van al pool con aviso.

## Acceptance criteria

- [x] `RecurrenceRule` y `RecurrenceGenerator` integrados en el cierre de día
- [x] Tests con calendario (meses de 28 a 31 días)
- [x] Sección de recurrencia en el formulario y lista de recurrentes en Ajustes (pausar, editar, borrar)
- [x] Editar una instancia pregunta 'solo esta' o 'esta y las siguientes'

## Blocked by

- 08-cierre-de-dia
- 10-editar-tareas-categorias

## Comments
- pm-2: asignado a worker-11 (nuevo; rama issue/16-recurrentes).
- worker-11: entregado (in-review). Decisiones donde la spec no cierra el detalle (propuestas al PM, sin tocar la spec):
  - `RecurrenceRule` gana `IsPaused` (bool, por defecto false) para "pausar"; migración manual `AddRecurrenceIsPaused`. La spec §3 no lo lista: conviene añadirlo a la tabla del modelo. También se añade `IConfigRepository.DeleteRecurrenceAsync` (InMemory, Sqlite, Browser, con tests de contrato).
  - El cierre genera las instancias de hoy **siempre** (haya o no días pendientes que cerrar) y en la misma transacción. Los días saltados no se generan retroactivamente: solo hoy. Idempotente vía `LastGeneratedDate`.
  - Instancia que no cabe en su nivel (Max): se crea activa con `ScheduledDate = hoy`, sin asignación (queda en el pool). El aviso se deriva del estado (`TodayView.RecurringOverflow`: recurrentes activas de hoy fuera del plan) y se muestra como alerta en Hoy; desaparece al recuperarla al plan o al cambiar de día. `RolloverResult.Recurrences` también las informa a los hosts.
  - Reglas con nivel desactivado/borrado no generan ni se marcan como generadas (lo harán cuando el nivel vuelva).
  - Semántica: `Daily` ignora `Interval`; `EveryNDays` cuenta desde `StartDate`; `Weekly` con `Interval` N cuenta semanas lunes-domingo desde la de `StartDate` (la UI solo ofrece N=1); `Monthly` usa `DayOfMonth` (o el día de `StartDate`) y en meses cortos cae en el último día. "Laborables" = Weekly de lunes a viernes.
  - Crear una recurrente (desde el formulario de nueva tarea o de edición) convierte esa tarea en la primera instancia: la regla empieza hoy y queda con `LastGeneratedDate = hoy`, así que no se duplica hoy.
  - "Esta y las siguientes" actualiza la plantilla de la regla (título, nivel, categoría, hora) y, si se cambió, la frecuencia; la regla no guarda notas ni recordatorio (spec §3), así que esos campos solo valen para "solo esta". Una instancia no ofrece "No se repite": para parar una serie se pausa o se borra en Ajustes > Tareas recurrentes.
  - Borrar una regla desvincula (`RecurrenceRuleId = null`) las instancias ya generadas; siguen siendo tareas normales.
  - Fuera de alcance: `EndDate` no tiene control en la UI (el modelo y el generador lo respetan); exportar como RRULE (issue de .ics) y backup JSON de reglas.
