# 25 · Exportar a .ics

Status: claimed
Blocked by: 22
Cubre: 3MT-38

## What to build

El usuario elige un rango y exporta un `.ics` con un VEVENT por asignación (día completo si no hay hora) y VTODO opcional.

## Acceptance criteria

- [ ] Formato según spec §4.6: UID, CATEGORIES, X-3MT-STATUS
- [ ] Test de ida y vuelta con Ical.Net
- [ ] Android: compartir o guardar; web: descarga
- [ ] Comprobación manual importándolo en Google Calendar

## Blocked by

- 22-historial-dia-metricas

## Comments
- pm-3 (2026-10-22): asignado a worker-5. Rama issue/25-exportar-ics.
