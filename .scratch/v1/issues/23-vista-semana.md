# 23 · Calendario: vista Semana

Status: in-review
Blocked by: 22
Cubre: 3MT-36

## What to build

Siete columnas (filas en móvil estrecho) con las tareas de cada día y su estado, respetando el primer día de la semana.

## Acceptance criteria

- [x] Navegación entre semanas
- [x] Respeta el primer día de la semana de Ajustes
- [x] Tocar un día abre la vista Día

## Blocked by

- 22-historial-dia-metricas

## Comments
- pm-3 (2026-10-22): asignado a worker-7. Rama issue/23-vista-semana.
- worker-7: implementado. Contenedor: `CalendarTabs` (selector Día/Semana por rutas `/calendario`, `/calendario/semana[/{fecha}]`); el 24 solo añade su pestaña "Mes" en ese componente y su página `/calendario/mes`. Servicio: `HistoryService.GetWeekAsync` / `WeekStart`.
- worker-7 decisión: tocar un día pasado abre la vista Día; hoy lleva a la pestaña Hoy (la vista Día solo muestra días cerrados, hasta ayer) y los días futuros no son enlaces (solo muestran sus tareas programadas). Semanas futuras navegables sin límite.
- worker-7 no verificado en navegador/emulador: layout visual (7 columnas desde 600 px, filas en móvil estrecho) y aspecto frente al mock; solo comprobado por bUnit/CSS revisado.
