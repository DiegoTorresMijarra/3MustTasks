# 3MustTasks — Especificación técnica

> Estado: borrador v0.1 · Complementa a [01-producto.md](01-producto.md)

## 1. Decisiones de base

| Tema | Decisión |
|---|---|
| Runtime | **.NET 10 (LTS)** |
| UI | **Blazor** (componentes Razor compartidos) + **MudBlazor** |
| Android | **.NET MAUI Blazor Hybrid** (BlazorWebView) |
| Web | **Blazor WebAssembly standalone como PWA** |
| Persistencia | Local: **SQLite + EF Core** (Android) · **IndexedDB** (web) |
| Backend | Ninguno en v1 |
| iCalendar | **Ical.Net** |
| Notificaciones Android | **Plugin.LocalNotification** (o API nativa vía `AlarmManager` si da problemas) |
| Tests | xUnit · bUnit · FluentAssertions/Shouldly · `Microsoft.Extensions.TimeProvider.Testing` |
| CI | GitHub Actions |

> En C# los namespaces no pueden empezar por un dígito, así que el código usa el prefijo **`ThreeMustTasks`**. El nombre visible de la app sigue siendo *3MustTasks*.

## 2. Arquitectura

```
                 ┌───────────────────────────┐
                 │   ThreeMustTasks.UI (RCL) │  páginas, componentes, estado de vista
                 └─────────────┬─────────────┘
                               │ usa
                 ┌─────────────▼─────────────┐
                 │    ThreeMustTasks.Core    │  dominio + casos de uso + interfaces (puertos)
                 └──▲──────────▲──────────▲──┘
     implementa     │          │          │   implementa
┌───────────────────┴──┐  ┌────┴─────────────────┐  ┌─────┴────────────────┐
│ Infrastructure.Sqlite│  │ Infrastructure.Browser│ │ Infrastructure.Shared │ (Ical.Net, backup JSON)
└──────────▲───────────┘  └──────────▲───────────┘  └──────────────────────┘
           │                         │
┌──────────┴───────────┐  ┌──────────┴───────────┐
│  App.Maui (Android)  │  │   App.Web (PWA)      │  hosts: DI, notificaciones, ficheros, permisos
└──────────────────────┘  └──────────────────────┘
```

### 2.1 Proyectos

| Proyecto | Tipo | Contenido |
|---|---|---|
| `ThreeMustTasks.Core` | classlib | Entidades, reglas (`QuotaPolicy`), servicios de dominio, interfaces. Sin dependencias de UI ni de plataforma. |
| `ThreeMustTasks.Infrastructure.Shared` | classlib | `IcsExporter`/`IcsImporter` (Ical.Net) y `BackupService` (System.Text.Json). |
| `ThreeMustTasks.Infrastructure.Sqlite` | classlib | `DbContext` de EF Core, migraciones y repositorios. |
| `ThreeMustTasks.Infrastructure.Browser` | RCL | Repositorios sobre IndexedDB (JS interop con un módulo `idb` pequeño propio) y `localStorage` para ajustes. Un object store por entidad, documentos JSON con el mismo serializador que el backup, índices por `status` y `logicalDate`, `IUnitOfWork` = una transacción IndexedDB por `SaveChangesAsync`; migración por `onupgradeneeded`. Ver ADR 0001. |
| `ThreeMustTasks.UI` | RCL | Páginas, componentes, tema MudBlazor y recursos de texto (`.resx`). |
| `ThreeMustTasks.App.Maui` | MAUI | Host Android, `INotificationScheduler` nativo, `IFileExchange` (FilePicker/Share) y receptor de arranque del sistema. |
| `ThreeMustTasks.App.Web` | Blazor WASM | Host PWA, `manifest.webmanifest`, service worker, notificaciones web y descarga/subida de archivos. |
| `tests/*.Tests` | xUnit | `Core.Tests`, `Infrastructure.Tests` (incluye **tests de contrato** comunes a los dos repositorios) y `UI.Tests` (bUnit). |

### 2.2 Puertos (interfaces en Core)

```csharp
public interface ITaskRepository        { /* CRUD TaskItem + consultas de pool */ }
public interface IDayRepository         { /* DayAssignment, DayLog */ }
public interface IConfigRepository      { /* Level, Category, FavoriteTemplate, RecurrenceRule */ }
public interface ISettingsStore         { AppSettings Load(); void Save(AppSettings s); }
public interface IUnitOfWork            { Task SaveChangesAsync(CancellationToken ct = default); }
public interface INotificationScheduler { Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> n); Task<bool> RequestPermissionAsync(); }
public interface IFileExchange          { Task ExportAsync(string fileName, string mime, Stream content); Task<Stream?> PickAsync(string[] extensions); }
// El tiempo se obtiene siempre de System.TimeProvider (inyectado)
```

## 3. Modelo de datos

```
Level 1───* TaskItem *───0..1 Category
              │  1
              │
              * DayAssignment *───1 DayLog (por LogicalDate)
TaskItem *───0..1 RecurrenceRule
FavoriteTemplate (independiente: plantilla para crear TaskItem)
AppSettings (documento único)
```

| Entidad | Campos |
|---|---|
| **Level** | `Id`, `Name`, `Color`, `Icon`, `MinToAdd`, `MinToComplete`, `Max?` (null = ilimitado), `Order`, `Enabled` |
| **Category** | `Id`, `Name`, `Color` |
| **TaskItem** | `Id (Guid)`, `Title`, `Notes?`, `LevelId`, `CategoryId?`, `DueTime? (TimeOnly)`, `ReminderOffset? (TimeSpan)`, `ScheduledDate? (DateOnly)`, `Status {Active, Completed, Archived}`, `IsPinned`, `TimesPostponed`, `RecurrenceRuleId?`, `IcsUid?`, `IsFavoriteSource`, `CreatedAt`, `UpdatedAt` |
| **DayAssignment** | `Id`, `TaskId`, `LogicalDate (DateOnly)`, `Outcome {Planned, Completed, NotCompleted, Removed}`, `CompletedAt?`, `Order` |
| **DayLog** | `LogicalDate (PK)`, `PlanMinimumMetAt?`, `CompletionMinimumMetAt?`, `Fulfilled (bool?)`, `ClosedAt?` |
| **FavoriteTemplate** | `Id`, `Title`, `LevelId`, `CategoryId?`, `DueTime?`, `ReminderOffset?`, `UseCount` |
| **RecurrenceRule** | `Id`, `TemplateTitle`, `LevelId`, `CategoryId?`, `DueTime?`, `Frequency {Daily, Weekly, EveryNDays, Monthly}`, `Interval`, `DaysOfWeek (flags)`, `DayOfMonth?`, `StartDate`, `EndDate?`, `LastGeneratedDate?`, `IsPaused` (pausa la generación sin borrar la regla) (se exporta como RRULE) |
| **AppSettings** | `DayStartsAt (TimeOnly, 00:00)`, `PlanReminder {Enabled, Time 07:30, Days}`, `Nag {Enabled, IntervalMin 30, MaxPerDay 4}`, `CompletionReminder {Enabled, Time 18:00}`, `EveningSummary {Enabled false, Time 21:00}`, `StrictMode false`, `AutoAddScheduled true`, `ExportVTodo false` (P8), `Theme`, `FirstDayOfWeek Monday`, `SchemaVersion` |

### 3.1 Consultas derivadas (no se guardan)
- **Plan de hoy**: `DayAssignment` con `LogicalDate == hoy` y `Outcome ∈ {Planned, Completed}`.
- **Pool (pendientes)**: `TaskItem.Status == Active` sin asignación `Planned` en el día de hoy y con `ScheduledDate` nula o ≤ hoy.
- **Pool a futuro**: `Status == Active && ScheduledDate > hoy`.
- **Archivadas**: `Status == Archived`.
- **Historial de un día**: todas las `DayAssignment` de esa fecha, con su resultado.

### 3.2 Día lógico
`LogicalDate = DateOnly.FromDateTime(now - DayStartsAt)`. Se calcula siempre a través de `IClock`/`TimeProvider`, y en un solo sitio (`LogicalDayCalculator`).

## 4. Servicios de dominio clave

### 4.1 `DayRolloverService` (idempotente)
Se ejecuta al iniciar la app, al volver a primer plano y antes de cada consulta del plan de hoy.
```
último = max(DayLog.ClosedAt != null).LogicalDate  (o fecha de instalación)
para cada día d en (último, hoy):
    asignaciones Planned de d → Outcome = NotCompleted; TaskItem.TimesPostponed++
    DayLog(d).Fulfilled = mínimos a completar alcanzados; DayLog(d).ClosedAt = now
tareas fijadas no completadas → nueva DayAssignment Planned para hoy (no van al pool)
generar instancias de recurrentes para hoy (RecurrenceGenerator)
si AutoAddScheduled: añadir al plan las tareas con ScheduledDate == hoy (respetando QuotaPolicy)
ReminderPlanner.Rebuild()
```
- Todo ocurre en **una transacción**. Ejecutarlo dos veces no tiene efecto adicional.
- Los días en que no se abrió la app y no tenían plan no generan `DayLog` (el historial los muestra como "sin plan").

### 4.2 `QuotaPolicy`
- `CanAdd(level, plannedCountForLevel)` → respeta `Max`.
- `Evaluate(levels, entries) → QuotaStatus { IsPlanMinimumMet, MissingToAdd: [(Level, n)], IsCompletionMinimumMet, MissingToComplete: [(Level, n)] }`.
- Cuando se cumple el mínimo a añadir por primera vez → `DayLog.PlanMinimumMetAt` y se cancelan las insistencias. Igual con el de completar → `CompletionMinimumMetAt` y se cancela el aviso de la tarde.

### 4.3 `PoolService`
`Recover(taskIds, today)` (valida cupos y devuelve los rechazados con su motivo), `Archive(ids)`, `Unarchive(ids)`, `Delete(ids)`.
En la UI el pool pendiente se agrupa por antigüedad como el mock: sección "Esta semana" (7 días o menos) arriba y "Más antiguas" debajo; el orden dentro de cada sección es el de la consulta (más antigua o más pospuesta).

### 4.4 `RecurrenceGenerator`
Genera las instancias de `TaskItem` para una fecha si la regla aplica y no se generaron ya (`LastGeneratedDate`). Si se supera el máximo del nivel, la instancia va al pool y se avisa.

### 4.5 `ReminderPlanner`
Función pura `(settings, plan, pool futuro, ahora) → List<PlannedNotification>` para una **ventana móvil de 7 días**. Así se respetan los límites de notificaciones pendientes y la lógica se puede testear. Se ejecuta tras cada cambio relevante y el host aplica el resultado con `INotificationScheduler.ReplaceAllAsync`.
- IDs deterministas por tipo + fecha + tarea, para poder cancelar y reemplazar.
- La insistencia solo se programa para hoy, mientras falte el mínimo.

### 4.6 Import/Export
- **Export `.ics`**: un `VCALENDAR` con un `VEVENT` por asignación (`DTSTART;VALUE=DATE` para tareas sin hora). `UID = {TaskId}@3musttasks`, `CATEGORIES:Must,Trabajo`, `STATUS:CONFIRMED` y `X-3MT-STATUS:COMPLETED|PENDING`. Si se activa en ajustes, también un `VTODO` (`STATUS:COMPLETED`/`NEEDS-ACTION`).
- **Import `.ics`**: se parsea con Ical.Net, se muestra una previsualización (título, fecha, nivel asignado) y se crea un `TaskItem` con `IcsUid`. Si el UID ya existe, se actualiza en lugar de duplicar. Lo de hoy entra al plan (si cabe en el cupo) y lo futuro va al pool a futuro.
- **Backup JSON**: `{ schemaVersion, exportedAt, settings, levels, categories, favorites, recurrences, tasks, assignments, dayLogs }`. Al restaurar, se puede reemplazar todo o fusionar por Id.

## 5. Plataforma

### 5.1 Android (MAUI)
- `minSdk` 26 y `targetSdk` el último exigido por Play.
- Permisos: `POST_NOTIFICATIONS` (13+), `SCHEDULE_EXACT_ALARM` (si se deniega, se usan alarmas inexactas y se informa al usuario) y `RECEIVE_BOOT_COMPLETED` (para reprogramar).
- En las notificaciones de insistencia o planificación, el toque abre la pantalla Planificar mediante un deep link interno.
- Se recomienda que el usuario excluya la app del ahorro de batería, con una pantalla de ayuda en los ajustes.

### 5.2 PWA (Blazor WASM)
- `service-worker.published.js` en modo offline-first, `manifest` con iconos y la PWA instalable.
- `navigator.storage.persist()` se pide al guardar los primeros datos.
- Notificaciones: solo con la pestaña o la PWA abierta (`Notification` + temporizador). La UI lo indica en los ajustes.
- Exportar = descarga de un blob. Importar = `<InputFile>`.
- Despliegue: GitHub Pages (o Azure Static Web Apps).

## 6. UI
- Shell con navegación inferior: **Hoy · Pool · Calendario · Ajustes**, y un FAB "+" en Hoy y Pool.
- Estado: servicios *scoped* con eventos `OnChange` (sin librería de estado en v1).
- Tema: los colores de cada nivel salen de los ajustes. Modo oscuro.
- Textos en `.resx`, en español (preparado para i18n).

## 7. Calidad
- **Core ≥ 90 %** de cobertura. Imprescindible: cierre de día (varios días saltados, idempotencia, día lógico con `DayStartsAt` ≠ 00:00, cambio de horario de verano), cupos, recurrencias y planificador de recordatorios.
- **Tests de contrato** de repositorio que se ejecutan contra SQLite en memoria y contra un fake del comportamiento de IndexedDB (y en CI, opcionalmente, con Playwright contra la PWA real).
- **bUnit** para Hoy, Pool y el formulario de tarea.
- **E2E PWA** con Playwright (smoke test: crear 3 Must, tachar, simular el cambio de día y verlas en el pool).
- CI: `dotnet build`, `dotnet test`, build de la PWA y publicación en Pages desde `main`, build de un APK de Android (debug) como artefacto.

## 8. Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| Dos implementaciones de persistencia que divergen | Tests de contrato compartidos y la lógica de consulta en Core siempre que se pueda |
| Notificaciones poco fiables en Android (fabricantes, batería) | Ventana móvil, reprogramar al abrir y al arrancar el dispositivo, y pantalla de ayuda |
| Expectativas de recordatorios en la PWA | Indicarlo claramente en la UI y en el onboarding web |
| Tamaño y arranque del WASM | Trimming, compresión Brotli y carga diferida de las vistas de calendario |
| Cambios de esquema | Migraciones de EF Core, versión de esquema en IndexedDB y backup JSON versionado |
| Safari/iOS: limpieza de datos a los 7 días sin uso si la PWA no está instalada | Pedir `navigator.storage.persist()` e invitar a instalar la PWA (ADR 0001) |
