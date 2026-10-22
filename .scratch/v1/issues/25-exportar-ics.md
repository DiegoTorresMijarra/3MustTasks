# 25 · Exportar a .ics

Status: resolved
Blocked by: 22
Cubre: 3MT-38

## What to build

El usuario elige un rango y exporta un `.ics` con un VEVENT por asignación (día completo si no hay hora) y VTODO opcional.

## Acceptance criteria

- [x] Formato según spec §4.6: UID, CATEGORIES, X-3MT-STATUS
- [x] Test de ida y vuelta con Ical.Net
- [ ] Android: compartir o guardar; web: descarga
- [ ] Comprobación manual importándolo en Google Calendar

## Blocked by

- 22-historial-dia-metricas

## Comments
- pm-3 (2026-10-22): asignado a worker-5. Rama issue/25-exportar-ics.
- worker-5 (2026-10-22): entregado en rama issue/25-exportar-ics.
  - Diseño: `IcsExportService` (Core) arma las entradas y `IcalNetWriter` (Infrastructure.Shared, Ical.Net 5.2.3) las serializa tras el puerto `IIcsWriter`. Se registra en `AddThreeMustTasksUI` (la UI referencia ahora Infrastructure.Shared) para no tocar los hosts web/MAUI. UI: `IcsExportPanel` en Ajustes (rango Desde/Hasta, por defecto el mes en curso) reutiliza `backup.js` (`downloadText` con `mimeType` opcional).
  - Formato v1 (documentado en `IcalNetWriter`, fixture `tests/ThreeMustTasks.Infrastructure.Tests/Fixtures/export-v1.ics` para el issue 26): VEVENT por asignación del rango salvo `Removed`; `UID={TaskId}@3musttasks`; con hora = hora flotante local, duración fija 30 min; sin hora = `DTSTART;VALUE=DATE` + `DTEND` día siguiente; `CATEGORIES` = nivel (+ categoría); `STATUS:CONFIRMED`; `X-3MT-STATUS:COMPLETED|PENDING`. Con `ExportVTodo`: un VTODO por asignación (`UID={TaskId}-todo@3musttasks`, `DUE`, `STATUS:COMPLETED|NEEDS-ACTION`, `COMPLETED` UTC).
  - DECISIÓN PARA EL PM (spec §4.6 no lo cubre): una tarea aplazada tiene varias asignaciones y el UID `{TaskId}@3musttasks` se repetiría. La asignación más reciente del rango conserva ese UID y las anteriores llevan sufijo de fecha: `{TaskId}-yyyyMMdd@3musttasks`. El import (26) debe tratar el sufijo al deduplicar.
  - NO VERIFICADO: (a) Android compartir/guardar: se usa la misma descarga por blob/`<a download>` que el backup JSON; en el WebView de MAUI puede no guardar el fichero y no hay emulador. Por eso el criterio 3 queda sin marcar (web cubierto solo con bUnit contra JS simulado, no en un navegador real). (b) Importación manual en Google Calendar (criterio 4): sin hacer; tampoco Apple/Outlook.
- pm-3 (2026-10-22): resolved con reserva: criterio Android compartir/guardar y descarga en navegador real e importación en Google Calendar sin verificar (sin emulador). Regla de UID para tareas aplazadas (sufijo de fecha en asignaciones anteriores) aceptada; el 26 debe deduplicarla. Hora flotante local 30 min: decisión del worker, aceptada.
