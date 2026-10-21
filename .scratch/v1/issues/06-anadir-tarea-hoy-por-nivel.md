# 06 · Añadir una tarea a Hoy y verla agrupada por nivel

Status: in-review
Blocked by: 03
Cubre: 3MT-08, 09, parte de 10, 11 y 12

## What to build

Primer recorrido completo: el usuario abre Hoy, añade una tarea con título y nivel, y la ve agrupada bajo su nivel con los huecos de cupo visibles. Incluye entidades, niveles por defecto (Must 1/1, Recommended 2/0, Maybe 0/0), `QuotaPolicy`, cálculo del día lógico y formulario mínimo.

## Acceptance criteria

- [x] Niveles por defecto sembrados según el PRD §5.1
- [x] `QuotaPolicy` respeta máximos y ignora niveles desactivados (100 % de cobertura)
- [x] `LogicalDayCalculator` con tests, incl. inicio de día 04:00 y cambio de horario
- [x] Hoy agrupa por nivel con huecos vacíos visibles; el nivel se distingue también por icono o texto
- [x] No se puede guardar en Hoy si el nivel está lleno; se ofrece mandarla al pool
- [x] Tests de dominio y bUnit

## Blocked by

- 03-esqueleto-web

## Comments

- worker-4 (entrega): implementado en la rama `issue/06-anadir-tarea-hoy-por-nivel`. Notas y dudas para pm-1:
  - **Máximos por defecto**: el PRD §5.1 deja el máximo de los tres niveles vacío (sin tope), así que con los valores por defecto ningún nivel se llena nunca; "nivel lleno" solo ocurre al configurar un `Max` (issue 15). Se sembró `Max = null` según el PRD y el flujo "lleno → pool" se prueba con un `Max` explícito. Si se quería un tope por defecto, hay que decidirlo (PRD).
  - **Huecos visibles** = `Max` del nivel, o `MinToAdd` si no tiene máximo (por defecto: Must 1, Recommended 2, Maybe 0). Los niveles sin huecos pendientes muestran un enlace "Añadir X (opcional)".
  - Una tarea añadida a Hoy se guarda con `ScheduledDate = hoy`; la mandada al pool, con `ScheduledDate = null`.
  - `QuotaPolicy.Evaluate` se implementó como `Evaluate(niveles, IEnumerable<PlanEntry>)` (la spec §4.2 dice `Evaluate(plan)`); `DayLog.PlanMinimumMetAt` y el banner de mínimos quedan para 07/08.
  - Los niveles se siembran en `TodayService.GetLevelsAsync` al primer uso (aún no hay paso de arranque); no se tocaron puertos ni infraestructura.
  - Para 07 y 10: `TaskForm` (componente) es reutilizable y solo tiene título + nivel; `TodayService`/`TodayView`/`LevelGroup` exponen el plan agrupado con `AssignmentId` y `Outcome` por tarea.

- pm-1: asignado a worker-4 (rama issue/06-anadir-tarea-hoy-por-nivel). Camino crítico.
