# 3MustTasks — Backlog

> Formato listo para convertir en issues de GitHub. Cada issue: **ID · título**, etiquetas, tamaño (S ≤ 1 día · M 2–3 días · L ≥ 4 días), dependencias y criterios de aceptación.
>
> Etiquetas: `area:core|ui|android|web|infra|docs` · `type:feature|chore|test` · `prio:P0|P1|P2`

## Resumen por hitos

| Hito | Objetivo | Issues | Entregable |
|---|---|---|---|
| **M0 Fundaciones** | Esqueleto que compila y corre en Android y web | 3MT-01 … 3MT-07 | App vacía con navegación en ambos hosts + CI |
| **M1 Bucle diario** | Planificar, tachar, cerrar el día | 3MT-08 … 3MT-15 | Se puede usar a diario (sin notificaciones) |
| **M2 Pool** | Lo no hecho no se pierde | 3MT-16 … 3MT-20 | Pool, recuperar, archivar, a futuro |
| **M3 Personalización** | Todo configurable con valores por defecto | 3MT-21 … 3MT-27 | Niveles, categorías, favoritas, recurrentes, onboarding |
| **M4 Notificaciones** | Recordatorios | 3MT-28 … 3MT-33 | Planificación, insistencia, recordatorio de tarea |
| **M5 Calendario** | Historial visual | 3MT-34 … 3MT-37 | Vistas día, semana y mes + métricas |
| **M6 Import/Export** | Interoperabilidad | 3MT-38 … 3MT-41 | `.ics` y backup JSON |
| **M7 Release v1** | Pulido y publicación | 3MT-42 … 3MT-46 | APK firmado + PWA desplegada |

**Camino crítico:** 01 → 02 → 04 → 08 → 09 → 11 → 14 → 16 → 29 → 30 → 45

---

## M0 · Fundaciones

### 3MT-01 · Crear la solución y la estructura de proyectos
`area:infra` `type:chore` `prio:P0` · **M** · deps: —
- [ ] `ThreeMustTasks.sln` con los proyectos de [02-spec-tecnica §2.1](02-spec-tecnica.md#21-proyectos), apuntando a .NET 10.
- [ ] `Directory.Build.props` (nullable, warnings as errors, versión centralizada) y `Directory.Packages.props`.
- [ ] `.editorconfig`, `.gitignore` y `global.json`.
- [ ] `dotnet build` en verde.

### 3MT-02 · CI en GitHub Actions
`area:infra` `type:chore` `prio:P0` · **M** · deps: 01
- [ ] Workflow para PRs y `main`: restore, build y test con informe de cobertura.
- [ ] Job de build de la PWA (`dotnet publish App.Web`) como artefacto.
- [ ] Job de build del APK Android (debug) como artefacto.

### 3MT-03 · Shell de UI compartida con MudBlazor
`area:ui` `type:feature` `prio:P0` · **M** · deps: 01
- [ ] MudBlazor configurado en la RCL `ThreeMustTasks.UI`.
- [ ] Layout con navegación inferior: Hoy · Pool · Calendario · Ajustes (páginas vacías).
- [ ] Tema claro/oscuro/sistema.
- [ ] Recursos `.resx` en español.

### 3MT-04 · Puertos de persistencia y tests de contrato
`area:core` `type:feature` `prio:P0` · **M** · deps: 01
- [ ] Interfaces `ITaskRepository`, `IDayRepository`, `IConfigRepository`, `ISettingsStore` e `IUnitOfWork` en Core.
- [ ] Suite abstracta `RepositoryContractTests<T>` (CRUD + consultas de pool y plan) que cada implementación hereda.

### 3MT-05 · Implementación SQLite (EF Core)
`area:infra` `type:feature` `prio:P0` · **M** · deps: 04
- [ ] `DbContext` + migración inicial con el modelo de [§3](02-spec-tecnica.md#3-modelo-de-datos).
- [ ] Pasa la suite de contrato (SQLite en memoria).
- [ ] Ruta de la BD en `FileSystem.AppDataDirectory` en MAUI.

### 3MT-06 · Implementación IndexedDB (navegador)
`area:web` `type:feature` `prio:P0` · **L** · deps: 04
- [ ] Módulo JS `idb` (object stores por entidad, índices por `LogicalDate` y `Status`).
- [ ] Repositorios C# vía `IJSRuntime`.
- [ ] Versionado de esquema (`onupgradeneeded`).
- [ ] Pasa la suite de contrato (con fake o con Playwright).
- [ ] Se llama a `navigator.storage.persist()`.

### 3MT-07 · Hosts MAUI (Android) y Blazor WASM (PWA) funcionando
`area:android` `area:web` `type:chore` `prio:P0` · **M** · deps: 03, 05, 06
- [ ] La app Android arranca en el emulador y muestra el shell.
- [ ] La PWA es instalable (manifest, iconos provisionales, service worker offline).
- [ ] Registro de DI por host (repositorio correcto y `TimeProvider.System`).

## M1 · Bucle diario

### 3MT-08 · Entidades de dominio y valores por defecto
`area:core` `type:feature` `prio:P0` · **M** · deps: 04
- [ ] `Level`, `Category`, `TaskItem`, `DayAssignment`, `DayLog` y `AppSettings`.
- [ ] Seed inicial: Must (3/3, rojo), Should (0/2, ámbar), Maybe (0/2, azul) y los ajustes por defecto del PRD §5.6/5.7.
- [ ] `LogicalDayCalculator` con tests (incluye `DayStartsAt` = 04:00 y cambio de horario).

### 3MT-09 · `QuotaPolicy`
`area:core` `type:feature` `prio:P0` · **S** · deps: 08
- [ ] `CanAdd` respeta el `Max` (null = ilimitado).
- [ ] `Evaluate` devuelve `IsMinimumMet` y lo que falta por nivel.
- [ ] Ignora los niveles desactivados.
- [ ] 100 % de cobertura.

### 3MT-10 · Casos de uso del plan diario
`area:core` `type:feature` `prio:P0` · **M** · deps: 09
- [ ] `AddTaskToToday`, `CompleteTask`, `UncompleteTask`, `RemoveFromToday` (→ pool) y `ReorderToday`.
- [ ] Al cumplir el mínimo por primera vez se rellena `DayLog.MinimumMetAt`.
- [ ] Se publica un evento `PlanChanged` (para la UI y los recordatorios).

### 3MT-11 · Pantalla Hoy
`area:ui` `type:feature` `prio:P0` · **L** · deps: 10, 07
- [ ] Tareas agrupadas por nivel en su orden, con contador "Must 2/3" y huecos vacíos visibles.
- [ ] Tachar con un toque o deslizando; destachar.
- [ ] Muestra la categoría (chip de color), la hora y el icono del nivel (no solo color).
- [ ] Estado vacío que invita a planificar.
- [ ] Tests bUnit.

### 3MT-12 · Formulario crear/editar tarea
`area:ui` `type:feature` `prio:P0` · **M** · deps: 10
- [ ] Campos: título*, nivel*, categoría, hora, recordatorio, notas, fecha (hoy / futura / sin fecha).
- [ ] Bloquea el guardado en hoy si el nivel está lleno, y ofrece guardarla en el pool.
- [ ] Validaciones y tests bUnit.

### 3MT-13 · Banner de mínimo no cumplido
`area:ui` `type:feature` `prio:P0` · **S** · deps: 11
- [ ] Banner persistente "Te faltan N Must…" mientras `IsMinimumMet == false`.
- [ ] Al pulsarlo se abre Planificar.
- [ ] Desaparece al cumplir el mínimo.

### 3MT-14 · `DayRolloverService`
`area:core` `type:feature` `prio:P0` · **M** · deps: 10
- [ ] Algoritmo de [spec §4.1](02-spec-tecnica.md#41-dayrolloverservice-idempotente) en una transacción.
- [ ] Tests: un día, varios días saltados, idempotencia, inicio de día ≠ 00:00 y día sin plan.
- [ ] Se invoca al iniciar y al reanudar en ambos hosts.

### 3MT-15 · Pantalla Planificar (selector rápido)
`area:ui` `type:feature` `prio:P1` · **M** · deps: 11, 12
- [ ] Pestañas Nueva · Favoritas · Pool (Favoritas y Pool se habilitan cuando existan 3MT-16 y 3MT-23).
- [ ] Muestra los cupos en tiempo real.
- [ ] Destino del deep link de las notificaciones.

## M2 · Pool

### 3MT-16 · `PoolService` y consultas de pool
`area:core` `type:feature` `prio:P0` · **M** · deps: 14
- [ ] `Recover(ids)` con validación de cupos (devuelve los rechazados con motivo), `Archive`, `Unarchive` y `Delete`.
- [ ] Consultas: pendientes, a futuro y archivadas, con filtros (nivel, categoría, antigüedad, `TimesPostponed`).

### 3MT-17 · Pantalla Pool
`area:ui` `type:feature` `prio:P0` · **L** · deps: 16
- [ ] Lista con filtros y orden (más antigua / más pospuesta).
- [ ] Selección múltiple con acciones Recuperar a hoy y Archivar.
- [ ] Sección "Programadas a futuro" agrupada por fecha.
- [ ] Indicador "pospuesta N veces".

### 3MT-18 · Pantalla Archivadas
`area:ui` `type:feature` `prio:P1` · **S** · deps: 16
- [ ] Lista con desarchivar y eliminar definitivamente (con confirmación).

### 3MT-19 · Pool a futuro: auto-añadir el día que toca
`area:core` `type:feature` `prio:P1` · **S** · deps: 16, 14
- [ ] En el cierre de día, las tareas con `ScheduledDate == hoy` entran al plan si `AutoAddScheduled` y si caben en el cupo; si no, quedan en el pool con un aviso.

### 3MT-20 · Tests de integración del ciclo de vida completo
`area:core` `type:test` `prio:P1` · **S** · deps: 16, 19
- [ ] Escenario: crear → no completar → cierre → pool → recuperar → completar. El historial del día 1 sigue mostrándola como no completada.

## M3 · Personalización

### 3MT-21 · Ajustes: niveles y cupos
`area:ui` `area:core` `type:feature` `prio:P0` · **M** · deps: 09
- [ ] CRUD de niveles: nombre, color, icono, mín., máx./ilimitado, orden (arrastrar) y activo.
- [ ] Siempre queda al menos uno activo; mín. ≤ máx.
- [ ] Botón "restaurar valores por defecto".

### 3MT-22 · Ajustes: categorías
`area:ui` `type:feature` `prio:P1` · **S** · deps: 08
- [ ] CRUD con color. Al borrar una categoría en uso, las tareas quedan sin categoría.

### 3MT-23 · Favoritas
`area:core` `area:ui` `type:feature` `prio:P1` · **M** · deps: 12
- [ ] Marcar como favorita desde el formulario o desde una tarea existente.
- [ ] Lista en la pestaña Favoritas de Planificar, ordenada por uso. Añadir con un toque.

### 3MT-24 · Recurrentes: modelo y generador
`area:core` `type:feature` `prio:P1` · **M** · deps: 14
- [ ] `RecurrenceRule` (diaria, laborables, días de la semana, cada N días, mensual) y `RecurrenceGenerator`.
- [ ] Se integra en el cierre de día; desborde de cupo → pool + aviso.
- [ ] Tests con calendario (meses de 28–31 días).

### 3MT-25 · Recurrentes: UI
`area:ui` `type:feature` `prio:P1` · **M** · deps: 24, 12
- [ ] Sección de recurrencia en el formulario y lista de recurrentes en los ajustes (pausar, editar y borrar).

### 3MT-26 · Ajustes generales
`area:ui` `type:feature` `prio:P1` · **S** · deps: 08
- [ ] Inicio del día lógico, tema, primer día de la semana y modo estricto.
- [ ] Opciones de comportamiento (auto-añadir tareas a futuro, VTODO).

### 3MT-27 · Onboarding
`area:ui` `type:feature` `prio:P1` · **M** · deps: 21, 26
- [ ] 3 pasos: concepto, cupos (aceptar 3 Must o ajustar) y hora de planificación + permiso de notificaciones.
- [ ] En la PWA, aviso de las limitaciones de las notificaciones.
- [ ] Solo aparece en el primer arranque; se puede repetir desde Ajustes.

## M4 · Notificaciones

### 3MT-28 · `ReminderPlanner` (dominio puro)
`area:core` `type:feature` `prio:P0` · **M** · deps: 10, 16
- [ ] Calcula las `PlannedNotification` en una ventana de 7 días: planificación, insistencia (solo hoy y mientras falte el mínimo), recordatorio de tarea, resumen y tarea a futuro.
- [ ] IDs deterministas.
- [ ] Tests exhaustivos (sin dependencias de plataforma).

### 3MT-29 · Notificaciones locales en Android
`area:android` `type:feature` `prio:P0` · **L** · deps: 28
- [ ] `INotificationScheduler` con Plugin.LocalNotification (o AlarmManager).
- [ ] Se reprograma tras `PlanChanged`, el cambio de ajustes y el cierre de día.
- [ ] Al tocar una notificación se abre la pantalla correspondiente (Planificar / Hoy).
- [ ] Verificado en el emulador con la app cerrada.

### 3MT-30 · Permisos y alarmas exactas en Android
`area:android` `type:feature` `prio:P0` · **M** · deps: 29
- [ ] Solicitud de `POST_NOTIFICATIONS` (13+).
- [ ] Gestión de `SCHEDULE_EXACT_ALARM`, con alternativa inexacta y aviso.
- [ ] `BOOT_COMPLETED` → reprogramar.
- [ ] Pantalla de ayuda sobre el ahorro de batería.

### 3MT-31 · Ajustes de notificaciones
`area:ui` `type:feature` `prio:P0` · **S** · deps: 28
- [ ] Hora y días del recordatorio de planificación, insistencia (intervalo y tope), resumen nocturno y antelación por defecto del recordatorio de tarea.

### 3MT-32 · Notificaciones en la PWA (con la app abierta)
`area:web` `type:feature` `prio:P2` · **M** · deps: 28
- [ ] `INotificationScheduler` web: pide el permiso de la Notification API y usa temporizadores para las notificaciones del día en curso.
- [ ] La UI indica que solo funciona con la app abierta.

### 3MT-33 · Insistencia: cancelar al cumplir el mínimo
`area:core` `type:test` `prio:P1` · **S** · deps: 29
- [ ] Test de integración: al añadir la tercera Must, las insistencias pendientes de hoy desaparecen.

## M5 · Calendario e historial

### 3MT-34 · Consultas de historial y métricas
`area:core` `type:feature` `prio:P1` · **M** · deps: 14
- [ ] Resumen por día (planificadas, completadas, % y si se cumplieron los Must) para un rango.
- [ ] Racha actual y mejor racha.

### 3MT-35 · Vista Día (histórico)
`area:ui` `type:feature` `prio:P1` · **S** · deps: 34
- [ ] Detalle de un día pasado con el resultado de cada tarea (solo lectura) y navegación ←/→.

### 3MT-36 · Vista Semana
`area:ui` `type:feature` `prio:P1` · **M** · deps: 34
- [ ] 7 columnas (o filas en un móvil estrecho) con las tareas y su estado, respetando el primer día de la semana.

### 3MT-37 · Vista Mes
`area:ui` `type:feature` `prio:P1` · **M** · deps: 34
- [ ] Cuadrícula con un anillo de cumplimiento por día y la racha visible; al tocar un día se abre la vista Día.
- [ ] Los días futuros muestran las tareas programadas.

## M6 · Import/Export

### 3MT-38 · Exportar `.ics`
`area:core` `area:ui` `type:feature` `prio:P1` · **M** · deps: 34
- [ ] Selector de rango y genera un `.ics` según [spec §4.6](02-spec-tecnica.md#46-importexport) (VTODO opcional).
- [ ] Se valida importándolo en Google Calendar (manual) y con un test de ida y vuelta con Ical.Net.
- [ ] Android: compartir/guardar. Web: descarga.

### 3MT-39 · Importar `.ics`
`area:core` `area:ui` `type:feature` `prio:P1` · **L** · deps: 38, 16
- [ ] Elegir archivo, previsualizar y seleccionar eventos, y asignar el nivel por defecto.
- [ ] Hoy → plan (si cabe en el cupo); futuro → pool a futuro.
- [ ] Deduplicado por `UID`.
- [ ] Soporta eventos de día completo y con hora, y RRULE básicas (se expanden en el rango o se convierten a recurrente).

### 3MT-40 · Backup JSON
`area:core` `area:ui` `type:feature` `prio:P1` · **M** · deps: 08
- [ ] Exportar todo con `schemaVersion`.
- [ ] Restaurar en modo reemplazar o fusionar, con confirmación.
- [ ] Ida y vuelta Android ↔ PWA verificada.

### 3MT-41 · Migración de versiones de backup
`area:core` `type:test` `prio:P2` · **S** · deps: 40
- [ ] Tests con un fixture de backup v1 que se sigue pudiendo restaurar tras cambios de esquema.

## M7 · Release v1

### 3MT-42 · Identidad visual
`area:ui` `type:chore` `prio:P1` · **S** · deps: 07
- [ ] Icono, splash, colores definitivos e iconos de la PWA (maskable).

### 3MT-43 · Accesibilidad
`area:ui` `type:chore` `prio:P1` · **S** · deps: 11, 17
- [ ] Contraste AA, etiquetas para lectores de pantalla y áreas táctiles ≥ 48 dp.
- [ ] Nivel indicado con icono o texto además del color.

### 3MT-44 · Rendimiento
`area:infra` `type:chore` `prio:P2` · **S** · deps: 07
- [ ] Arranque en frío < 2 s en Android de gama media.
- [ ] WASM con trimming + Brotli; vistas de calendario con carga diferida.

### 3MT-45 · APK firmado
`area:android` `type:chore` `prio:P0` · **S** · deps: 30
- [ ] Keystore guardado como secreto en CI, versión desde un tag y APK publicado en GitHub Releases.

### 3MT-46 · Despliegue de la PWA
`area:web` `type:chore` `prio:P0` · **S** · deps: 07
- [ ] Publicación automática en GitHub Pages desde `main` (base href correcto), con HTTPS y funcionamiento offline verificado.
