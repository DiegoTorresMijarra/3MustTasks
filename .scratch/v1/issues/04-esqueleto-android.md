# 04 · Esqueleto Android: host MAUI con SQLite

Status: resolved
Blocked by: 03
Cubre: 3MT-05, parte de 07

## What to build

Host MAUI Blazor Hybrid que arranca en emulador Android mostrando el mismo shell, con la persistencia SQLite (EF Core) detrás de los puertos.

## Acceptance criteria

- [ ] La app arranca en emulador y muestra el shell
- [ ] Migración inicial con el modelo de datos de la spec; base de datos en el directorio de datos de la app
- [x] La implementación SQLite pasa la suite de contrato (SQLite en memoria)
- [ ] Job de CI que genera el APK debug como artefacto

## Blocked by

- 03-esqueleto-web

## Comments

- pm-1: asignado a worker-3 (rama issue/04-esqueleto-android).
- worker-3 (entrega): hecho en rama `issue/04-esqueleto-android`. Estado de criterios:
  - Criterio 3 (contrato SQLite en memoria): cumplido; los tests de contrato de 03 se ejecutan contra SQLite en memoria con las migraciones reales aplicadas, más 5 tests de migración (modelo sin cambios pendientes, fichero y tablas, idempotencia, persistencia al reabrir, ticks).
  - Criterio 1 (arranca en emulador y muestra el shell): NO verificado. No hay emulador ni imagen de sistema (`Sdk/system-images` no existe; solo platforms/android-36, build-tools 36.0.0 y platform-tools) y `adb devices` está vacío. Sí verificado: compila, el APK debug se genera (96 MB, minSdk 26, targetSdk 36, contiene `index.html`, MudBlazor, `tmt.css`, `blazor.webview.js` y `libe_sqlite3.so` arm64) y hay tests estáticos del host. Pendiente de prueba manual en emulador o dispositivo.
  - Criterio 2 (migración inicial + BD en el directorio de datos): migración `InitialCreate` generada y probada (todas las tablas de spec §3, sin cambios de modelo pendientes). NO verificado en dispositivo que el fichero acabe en `FileSystem.AppDataDirectory` (solo test estático de `MauiProgram`). Sin marcar hasta decidir.
  - Criterio 4 (job de CI del APK): `.github/workflows/android.yml` creado, pero no ejecutado en GitHub Actions. El mismo comando (`dotnet publish ... -c Debug -p:AndroidPackageFormat=apk -p:EmbedAssembliesIntoApk=true`) sí produjo el APK en local (Windows). Sin marcar.
- worker-3, decisiones y dudas para pm-1:
  - `App.Maui` NO está en `ThreeMustTasks.sln`: necesita el workload `maui-android` y rompería el job `build-test` (ubuntu, sin workload). Se compila solo en `android.yml`. Si se prefiere en la sln, hay que instalar el workload en `ci.yml`.
  - El job de CI está en un workflow aparte (`android.yml`) y no en `ci.yml`, para no chocar con otros issues que editen `ci.yml`.
  - Sin claves foráneas en SQLite: la suite de contrato usa `LevelId`/`TaskId` sin fila asociada, así que el esquema no puede imponerlas.
  - `IUnitOfWork` en SQLite es no-op: cada operación de repositorio confirma al instante (como la implementación en memoria).
  - `DateTimeOffset` se guarda como ticks UTC (long) porque SQLite no puede ordenar ese tipo en TEXT; al leer, el offset es +00:00 (mismo instante). `DaysOfWeek` como flags (spec §3). Enums como texto.
  - Sin niveles ni ajustes sembrados: la migración solo crea el esquema.
  - `ApplicationId` = `com.threemusttasks.app` (a confirmar). Iconos y splash son los de la plantilla MAUI con color #3f51b5; sin permisos de notificaciones (issues posteriores).
  - `dotnet-ef` se usó desde una ruta temporal; no hay `dotnet-tools.json`.
- pm-1: revisado e integrado en main por instrucción del coordinador (el usuario duerme). Core 49, Infrastructure 41 y UI 44 tests en verde con -warnaserror. Pendientes anotados que NO bloquean, sin marcar como cumplidos: (1) arranque en emulador/dispositivo, (2) comprobar en dispositivo que la BD queda en AppDataDirectory, (3) primera ejecución real de android.yml en GitHub. App.Maui queda fuera de la .sln y con workflow aparte (aceptado). Queda trazabilidad para una prueba manual.
