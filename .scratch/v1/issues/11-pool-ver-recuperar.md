# 11 · Pool: ver pendientes y recuperarlas a hoy

Status: in-review
Blocked by: 08
Cubre: 3MT-16, 17

## What to build

El usuario abre el Pool, filtra y ordena las tareas no completadas, selecciona varias y las recupera al día de hoy respetando los cupos.

## Acceptance criteria

- [x] `PoolService.Recover` valida cupos y devuelve los rechazados con motivo
- [x] Filtros por nivel, categoría, antigüedad y veces pospuesta; orden por más antigua o más pospuesta
- [x] Selección múltiple con acciones Recuperar a hoy y Archivar
- [x] Indicador 'pospuesta N veces'
- [x] Tests de dominio y bUnit

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-1: asignado a worker-6 (reutilizado, ~25 %; rama issue/11-pool-ver-recuperar). Camino crítico.
- pm-2 (nota del 10): la lista del Pool debe enlazar cada tarea a `tarea/{id}` (edición ya existe, TaskEditService). Las tareas Completed/Archived sin asignación hoy son solo lectura en esa edición hasta que 12 lo relaje.
- worker-6 (entrega), notas para pm-1:
  - **`PoolService`** (Core, scoped en UI): `GetAsync(PoolQuery)`, `RecoverAsync(ids)`, `ArchiveAsync(ids)`. Pool = spec §3.1 (Active, sin asignación Planned/Completed hoy, `ScheduledDate` nula o <= hoy). `RecoverAsync` va en una transacción (`ExecuteInTransactionAsync`), respeta `QuotaPolicy.CanAdd` contando lo ya planificado hoy, recupera lo que cabe y devuelve `Rejected` con motivo (`LevelFull` / `NotInPool`); fija `ScheduledDate = hoy`, anota hitos del día y publica `PlanChanged`.
  - **Antigüedad** = días lógicos desde `CreatedAt` (no hay puerto para saber cuándo cayó al pool; si se prefiere "desde el último cierre" habría que añadir una consulta de asignaciones por tarea). El mock muestra "Ayer · pospuesta 1 vez"; lo hago con esa antigüedad.
  - **Filtros** (nivel, categoría, antigüedad >= 7/30 días, pospuesta >= 1/3) y orden (más antigua por defecto / chip "Más pospuestas"). Los umbrales de los selects son valores fijos míos; los de dominio son libres.
  - **UI** (`PoolPage`): lista con casillas, indicador "pospuesta N veces", barra Recuperar a hoy / Archivar / Cancelar al haber selección, aviso con los rechazos (las rechazadas siguen seleccionadas). Controles nativos (no MudCheckBox). Se publicó `TodayService.RecordMilestonesAsync` como `internal`.
  - **Fuera de alcance** (otros issues): pestañas "A futuro" y "Archivadas" del mock, desarchivar, borrar, secciones "Esta semana / Más antiguas", mostrar la recuperación desde Planificar (14). No toqué el formulario ni la persistencia web.
  - Tests: Core 76/76 (10 de Pool), UI 55/55 (8 bUnit de Pool), Infrastructure 51/51; todo con `-warnaserror`. No he ejecutado la UI en un navegador.
