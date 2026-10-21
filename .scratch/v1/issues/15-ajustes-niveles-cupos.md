# 15 · Ajustes: niveles, cupos y opciones generales

Status: resolved
Blocked by: 07
Cubre: 3MT-21, 26

## What to build

El usuario personaliza niveles (nombre, color, icono, mínimos, máximo, orden, activo) y ajustes generales (inicio del día lógico, tema, primer día de la semana, modo estricto, auto-añadir a futuro, VTODO).

## Acceptance criteria

- [x] CRUD de niveles; siempre queda al menos uno activo; mín. completar ≤ mín. añadir ≤ máx.
- [x] Reordenar arrastrando
- [x] Botón 'restaurar valores por defecto'
- [x] Ajustes generales persistidos con los valores por defecto del PRD §5.7
- [x] Tests de dominio y bUnit

## Blocked by

- 07-tachar-progreso-banner

## Comments
- pm-2: asignado a worker-8 (nuevo; rama issue/15-ajustes-niveles-cupos). Toca Ajustes y modelo de niveles; el 10 ya añadió el enlace a categorías en SettingsPage.
- worker-8 (entrega), notas para pm-2:
  - **Dónde**: ajustes generales en `/ajustes` (inicio del día, primer día de la semana, modo estricto, auto-añadir a futuro, VTODO; el tema ya estaba) y niveles en `/ajustes/niveles` (nueva `LevelsPage`, enlazada desde Ajustes). Lógica en `LevelService` (Core); no se ha tocado `TodayService`, `DayRolloverService` ni `QuotaPolicy`. Los ajustes se guardan al cambiar, releyendo antes el store para no pisar otros campos.
  - **Cambios de modelo/puertos**: `AppSettings.ExportVTodo` (false por defecto; la spec no lo lista en AppSettings, proponer añadirlo) con migración `AddExportVTodo` escrita a mano (no hay `dotnet-ef`; `HasPendingModelChanges` pasa), y `IConfigRepository.DeleteLevelAsync` (InMemory y Sqlite). `SqliteMigrationTests` ya no exige una única migración.
  - **Validación** (decisión conservadora): 0 ≤ mín. completar ≤ mín. añadir ≤ máx., y máx. ≥ 1 si se indica; nombre único (sin distinguir mayúsculas) de 1 a 30 caracteres; color `#RRGGBB`; icono de una lista fija (flag, star, lightbulb, bolt, heart, bookmark).
  - **Borrar un nivel**: solo si no lo usa ninguna tarea, favorita ni recurrencia y no es el único activo; si está en uso, hay que desactivarlo. Tras borrar se renumera el orden.
  - **Restaurar valores por defecto**: aplica solo a los niveles (no a los ajustes generales). Deja Must/Recommended/Maybe como en el PRD §5.1 (nombre, color, icono, cupos, orden, activos); los niveles propios se conservan (pueden tener tareas) pero quedan desactivados y al final. Sin diálogo de confirmación.
  - **Reordenar**: arrastrar y soltar HTML5 nativo sobre las filas (soltar sobre una fila coloca el nivel en esa posición) más flechas subir/bajar como alternativa accesible. Hoy ya ordena por `Level.Order`.
  - **Fuera de alcance / dudas**: `StrictMode`, `AutoAddScheduled` y `ExportVTodo` se persisten, pero su efecto lo implementan otros issues; `DayStartsAt` ya lo usa `LogicalDayCalculator`. No se tocan los ajustes de recordatorios (PRD §5.5). Cómo trata Hoy/Pool las tareas de un nivel desactivado no se ha cambiado.
- pm-2: revisado e integrado en main en bloque con 10/09/15/11/05 (conflictos de Strings.resx/DI/css resueltos conservando ambos lados; añadido DeleteLevelAsync a BrowserConfigRepository por el nuevo puerto de 15). build -warnaserror 0 avisos; tests Core 129, Infra 84, UI 85 en verde. Resolved.
