# 26 · Importar desde .ics

Status: resolved
Blocked by: 25, 11
Cubre: 3MT-39

## What to build

El usuario elige un `.ics`, previsualiza los eventos, asigna nivel por defecto e importa: lo de hoy al plan si cabe, lo futuro al pool a futuro.

## Acceptance criteria

- [x] Previsualización con selección de eventos
- [x] Deduplicado por UID
- [x] Soporta eventos de día completo y con hora, y RRULE básicas
- [x] Tests con ficheros de ejemplo de Google Calendar

## Blocked by

- 25-exportar-ics
- 11-pool-ver-recuperar

## Comments
- pm-3 (2026-10-22): asignado a worker-5 (reutilizado, ~30% de contexto; mismo ámbito ICS). Rama issue/26-importar-ics.
- worker-5 (2026-10-22): entregado en rama issue/26-importar-ics.
  - Diseño: `IcsImportService` (Core; `PreviewAsync` + `ImportAsync`) con el puerto `IIcsReader`, implementado en `IcalNetReader` (Infrastructure.Shared, Ical.Net). UI: `IcsImportPanel` en Ajustes bajo el export (elegir fichero, lista con casillas, nivel por defecto, importar). Registrado en `AddThreeMustTasksUI`.
  - Destinos: hoy -> plan si cabe (si el nivel está lleno, pool); futuro -> tarea con `ScheduledDate` (pool a futuro; AutoAddScheduled la sube al plan ese día); pasado -> pool sin fecha y sin marcar por defecto. Nivel = el de CATEGORIES si coincide con un nivel activo (export propio) o el nivel por defecto; categoría igual por nombre. Horas con TZID/UTC se convierten a la hora local del dispositivo; las flotantes (export propio) se toman tal cual.
  - Dedupe: por `IcsUid` (UID canónico) y, para UID propios (`{TaskId}@3musttasks`), por `TaskItem.Id`. El sufijo `-yyyyMMdd` se quita al canonizar: las apariciones de una tarea aplazada se agrupan en una (la más reciente). Si ya existe se actualiza título/notas/hora (y la fecha si sigue pendiente, no está en el plan de hoy y la nueva es futura); no se resucitan tareas completadas/archivadas.
  - RRULE soportadas: DAILY (INTERVAL: >1 -> EveryNDays), WEEKLY (INTERVAL=1, BYDAY sin ordinales; sin BYDAY usa el día de DTSTART), MONTHLY (BYMONTHDAY único o el día de DTSTART, INTERVAL) y UNTIL. Crean un `RecurrenceRule`; si aplica hoy se crea la instancia de hoy y la regla queda `LastGeneratedDate=hoy`. No soportadas (COUNT, YEARLY, BYSETPOS, WEEKLY con INTERVAL>1...): se avisa en la previsualización y se importa una sola tarea en su fecha. Las reglas no guardan UID, así que el dedupe de recurrentes se hace por título + inicio + hora + frecuencia.
  - Se ignoran: VTODO, eventos CANCELLED, modificaciones de instancia (RECURRENCE-ID), EXDATE/RDATE; un evento de varios días entra en su primer día.
  - Decisiones/limitaciones a revisar por el PM: (1) la importación no es una única transacción (reutiliza `TodayService.AddAsync`, que guarda por tarea); un fallo a medias deja lo ya importado, que al reimportar se deduplica. (2) El nivel por defecto es uno solo para todo el fichero (más el sugerido por CATEGORIES), no hay selector por evento. (3) `BackupPanelTests` se ajustó para localizar su `InputFile` por `data-testid` (ahora hay dos en Ajustes).
  - NO VERIFICADO: el fixture `google-calendar.ics` está escrito a mano imitando el formato de Google Calendar (PRODID, VTIMEZONE Europe/Madrid, TZID, UTC, RRULE, RECURRENCE-ID, CANCELLED, líneas plegadas); no es una descarga real. No se ha probado con Apple/Outlook ni con un `.ics` real de Google, ni en Android (selección de fichero en el WebView de MAUI).
- pm-3 (2026-10-22): resolved con reserva: integrado en main (build -warnaserror y tests en verde: 252/160/184). El fixture google-calendar.ics es de imitación; sin probar con .ics reales de Google/Apple/Outlook ni en el selector de ficheros del WebView de MAUI. Importación no transaccional (aceptado; el dedupe permite reintentar).
