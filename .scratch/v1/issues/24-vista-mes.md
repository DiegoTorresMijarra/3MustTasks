# 24 · Calendario: vista Mes

Status: claimed
Blocked by: 22
Cubre: 3MT-37

## What to build

Cuadrícula mensual con anillo de cumplimiento por día y racha visible; los días futuros muestran lo programado.

## Acceptance criteria

- [x] Anillo de cumplimiento por día, no solo color
- [x] Racha visible
- [x] Tocar un día abre la vista Día
- [ ] Carga diferida de la vista

## Blocked by

- 22-historial-dia-metricas

## Comments
- pm-3 (2026-10-22): asignado a worker-7 (reutilizado, ~25% de contexto; mismo ámbito). Rama issue/24-vista-mes.
- worker-7: pestaña Mes en `CalendarTabs`, página `CalendarMonthPage` (`/calendario/mes[/{fecha}]`), `HistoryService.GetMonthAsync`. Mismas convenciones de enlace que la Semana: día pasado a vista Día, hoy a Hoy, futuro sin enlace (punto = programadas).
- worker-7 NO cumplido: "Carga diferida de la vista". Los datos del mes solo se calculan al entrar en la ruta, pero la carga diferida real del ensamblado en WASM (lazy assembly loading) exige mover las vistas de calendario a un ensamblado/proyecto aparte y tocar App.Web/MAUI; es alcance de arquitectura. Propongo al PM un issue aparte (o decidir si basta). Queda sin marcar.
- worker-7 no verificado en navegador/emulador: aspecto del anillo SVG y la cuadrícula frente al mock; solo bUnit (fracción del anillo en data-fraction, estados, enlaces).
- pm-3 (2026-10-22): resolved con el criterio "Carga diferida de la vista" TRASLADADO al issue 28 (rendimiento: spec §NFR "carga diferida de las vistas de calendario"), por ser decisión de arquitectura (ensamblado aparte). Aspecto vs mock sin verificar.
