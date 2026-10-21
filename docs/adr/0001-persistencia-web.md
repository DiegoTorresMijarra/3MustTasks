# ADR 0001 · Persistencia en la web: IndexedDB frente a SQLite WASM (OPFS)

- **Estado:** aceptado (provisional, ratificado por pm-1 por delegación del usuario; revisar por el usuario)
- **Fecha:** 2026-10-22
- **Issue:** 01 (spike persistencia web) · cubre 3MT-06
- **Autor:** worker-5

> **Decisión provisional, revisar por el usuario.** El usuario delegó la decisión en el PM con este criterio: mejor encaje en dispositivos y gamas distintas (móviles modestos, navegadores variados incluido Safari/iOS) y mayor flexibilidad/mantenibilidad (evitar dos implementaciones de persistencia divergentes). Este ADR aporta evidencia y una recomendación; el PM ratifica.

## Contexto

La PWA (Blazor WASM) necesita persistencia local sin backend. Android ya usa EF Core + SQLite (issue 04, en `main`, con una suite de contrato abstracta compartida en `tests/ThreeMustTasks.Infrastructure.Tests/Contracts`). La spec (§1, §2.1) asume en la web repositorios propios sobre IndexedDB (`Infrastructure.Browser`). La pregunta: ¿conviene reutilizar EF Core + SQLite compilado a WASM con almacenamiento en OPFS, para tener una sola implementación?

Volumen de datos esperado: pequeño (cientos o pocos miles de filas: tareas, asignaciones diarias, un documento de ajustes). Las consultas derivadas de la spec §3.1 son filtros simples sobre `Status`, `ScheduledDate` y `LogicalDate`.

## Opciones

- **A. EF Core + SQLite WASM.** `Microsoft.EntityFrameworkCore.Sqlite` en Blazor WASM (e_sqlite3 enlazado en `dotnet.native.wasm`), fichero `.db` en el sistema de ficheros en memoria de Emscripten, volcado a OPFS en cada `SaveChanges` (con IndexedDB como respaldo si el navegador no soporta `createWritable` en OPFS desde el hilo principal).
- **B. Repositorios propios sobre IndexedDB.** Módulo JS mínimo (~60 líneas, sin dependencias) con un object store por entidad, JSON por documento y un índice por `status`; repositorios C# que implementan los puertos de Core y acumulan escrituras hasta `SaveChangesAsync` (una transacción IndexedDB).

Descartada por inviable en este contexto una tercera variante (sqlite-wasm oficial de sqlite.org con VFS OPFS en un Worker): no la consume EF Core, obligaría a escribir un proveedor ADO.NET propio, y el VFS `opfs` exige cabeceras COOP/COEP (GitHub Pages no permite configurarlas; el VFS `opfs-sahpool` no las necesita pero no admite varias pestañas con la misma base).

## Prototipo y mediciones

Prototipo en `spikes/persistencia-web/` (fuera de `ThreeMustTasks.sln`; `node_modules/`, `bin/`, `obj/` y `publish/` ignorados). Ambas opciones ejecutan el mismo escenario (`Spike.Common/Scenario.cs`) a través de los puertos de Core: guardar 3 tareas de ejemplo, 1.000 tareas en lote, leer todas y recargar la página comprobando que persisten (con verificación de ida y vuelta de campos `TimeOnly`, `TimeSpan`, `DateOnly`, `DateTimeOffset`). El script `measure.mjs` publica con servidor estático con Brotli y mide con Chrome 155 (headless, Playwright).

Parte del prototipo (estructura de `Spike.Common`, `Spike.Idb`, `Spike.Sqlite`, `measure.mjs`) la dejó iniciada worker-2 en un worktree sin entregar; worker-5 la ha revisado, reconstruido desde cero y medido de nuevo.

**Medido** (Windows 11, Chrome 155 headless, publicación Release, `dotnet 10.0.402`, runtime 10.0.13, 5 repeticiones, mediana):

| Métrica | A · EF Core + SQLite | B · IndexedDB |
|---|---:|---:|
| Descarga en frío (transferido, Brotli) | 5,3 MB (80 peticiones) | 2,2 MB (44 peticiones) |
| Publicado sin comprimir | 19,7 MB | 9,1 MB |
| Arranque en frío hasta primer resultado de datos | ~2,0 s | ~0,76 s |
| Recarga con caché caliente | ~1,9 s (hasta 4,6 s en una repetición) | ~0,85 s |
| Abrir almacén (inicialización) | ~0,8 a 1,0 s | ~10 ms |
| Escribir 1.000 tareas en lote | ~2,9 s (*) | ~0,42 s |
| Leer 1.004 tareas | ~0,47 a 0,88 s | ~0,41 s |
| Arranque en frío con CPU ×4 más lenta (3 rep.) | ~18,7 s | ~3,3 s |
| Abrir almacén con CPU ×4 | ~10,5 a 11 s | ~35 ms |
| Persistencia tras recargar | OK (1.004 filas) | OK (1.004 filas) |

(*) La escritura de SQLite en el prototipo es ingenua (un `AnyAsync` por fila); con `AddRange` bajaría, pero el coste fijo de la apertura (JIT/interpretación de EF Core y SQLite) y de volcar el fichero entero en cada guardado se mantiene. El estrangulamiento de CPU ×4 (emulación de Chrome, `Emulation.setCPUThrottlingRate`) es una aproximación a un móvil modesto, no un móvil real; las cifras de ×4 son ruidosas (3 repeticiones) y se dan como orden de magnitud.

Hallazgos del prototipo A:

1. **No hay OPFS "nativo" para EF Core.** El `e_sqlite3` que usa `Microsoft.Data.Sqlite` en browser-wasm trabaja sobre MEMFS. La persistencia es un volcado del fichero completo a OPFS (o IndexedDB como blob) tras cada `SaveChanges`: el coste de guardar crece con el tamaño de la base y la durabilidad ante un cierre brusco entre escritura y volcado es peor que en IndexedDB.
2. El volcado solo es válido con `journal_mode=DELETE`; EF Core abre en WAL por defecto y hay que forzarlo (si no, el `.db` queda a medias). Es una trampa de mantenimiento.
3. `FileSystemFileHandle.createWritable` en el hilo principal no está disponible en todos los navegadores; el prototipo detecta y cae a IndexedDB (blob). Es decir, **la opción A necesita IndexedDB igualmente como respaldo**.

**No medido / estimado** (fundado en documentación y conocimiento general; verificar antes de cerrar):

- Comportamiento en Safari/iOS y Firefox reales (no hay en esta máquina). Según la documentación de sqlite-wasm (consultada con ctx7: `sqlite.org/wasm/doc/trunk/persistence.md`): OPFS solo en Worker; Chromium 108+, Firefox 111+, Safari 16.4+; Safari < 17 con un fallo de almacenamiento conocido en el VFS OPFS; el VFS `opfs` requiere COOP/COEP; `opfs-sahpool` no los requiere pero no admite múltiples conexiones. IndexedDB está disponible en todos los navegadores objetivo desde hace años.
- Evicción: OPFS e IndexedDB comparten el mismo "bucket" de almacenamiento del origen y la misma política de evicción; `navigator.storage.persist()` (la spec ya lo pide, §5.2) protege a ambos por igual. En Safari/iOS, el almacenamiento escribible por script de sitios web no instalados puede borrarse tras ~7 días sin uso (ITP); instalar la PWA en la pantalla de inicio lo evita. Esto **no diferencia** a A de B. En el prototipo `persisted` salió `false` (Chrome headless no concede persistencia); no se pudo medir la concesión real.
- Memoria: A mantiene la base entera en memoria WASM (además del volcado); para el volumen esperado (<10 MB) es asumible, pero en móviles modestos suma al coste del runtime EF Core.
- Entorno: los dos proyectos del spike publicaron en esta máquina en Release (el publish ejecutó `wasm-ld` con los paquetes Emscripten 3.1.56 ya presentes, es decir, con enlazado nativo). A lo necesita sí o sí para enlazar `e_sqlite3.a`; para B no se ha comprobado si el enlazado nativo es evitable. Si en CI o en otra máquina falta la carga de trabajo `wasm-tools` (NETSDK1147), A fallaría seguro y B probablemente no (estimación).

## Comparativa

| Criterio | A · EF Core + SQLite WASM | B · IndexedDB propio |
|---|---|---|
| Tamaño de descarga | 5,3 MB (+2,4× frente a B); además exige enlazado nativo (`wasm-tools`) | 2,2 MB |
| Arranque / móvil modesto | ~2,6× más lento en escritorio; ~5× en CPU ×4 (estimación) | Rápido; apertura despreciable |
| Soporte de navegadores | Depende de OPFS + `createWritable`; fallback a IDB necesario; Safari < 17 con fallos | IndexedDB universal (incl. Safari/iOS) |
| Persistencia / evicción | Igual que B (mismo origen); guardado = volcado completo de fichero | Igual; transacciones nativas por lotes |
| Reutilización con Android | Alta: mismo `DbContext`, migraciones y suite de contrato | Media: reutiliza Core, serialización y suite de contrato; repositorios nuevos |
| Esfuerzo | Bajo en código (reutilizar `Infrastructure.Sqlite`), alto en infraestructura (volcado, WAL, fallback, CI con `wasm-tools`, migraciones en cliente) | Medio: módulo JS (~60 líneas) + repositorios C# (~180 líneas por analogía con `SqliteStores.cs`, 178 líneas) |
| Riesgo de divergencia | Bajo en semántica de consultas | Contenible: suite de contrato abstracta ya existente (`PersistenceContracts.cs`) corre contra InMemory y SQLite; se añade `Browser` |
| Migraciones de esquema | Migraciones EF (ya hechas) | `onupgradeneeded` + versión de esquema (spec §8) |
| Coste de un cambio de modelo | Una migración EF para ambos | Migración EF + migración IDB (pequeña, documentos JSON tolerantes a campos nuevos) |

## Decisión (provisional)

**Se recomienda la opción B: repositorios propios sobre IndexedDB** (`ThreeMustTasks.Infrastructure.Browser`), manteniendo lo previsto en la spec. Razones, según el criterio del usuario:

1. **Encaje en dispositivos y gamas:** menos de la mitad de descarga y de 3 a 5 veces menos tiempo de arranque, justo en el hardware modesto donde importa; sin dependencia de OPFS/`createWritable`, ausentes o con fallos en Safari/iOS antiguo (A necesitaría IndexedDB de respaldo, es decir, ambas implementaciones).
2. **Flexibilidad/mantenibilidad:** el temor a la divergencia se resuelve con lo que ya existe: los puertos de Core, la lógica de consulta en Core cuando sea posible y la suite de contrato abstracta compartida. El código "extra" es pequeño y acotado, y no obliga a depender del enlazado nativo de `e_sqlite3` (`wasm-tools`) en el CI de la PWA.
3. **Modelo de guardado más sano:** transacciones IndexedDB por lote frente a volcar el fichero completo en cada guardado.

La opción A queda como alternativa de reserva si el volumen de datos o la complejidad de las consultas crecieran mucho (p. ej. informes pesados); entonces convendría evaluar sqlite-wasm oficial en un Worker, no EF Core.

## Consecuencias

- Hay dos implementaciones de persistencia reales (Sqlite y Browser); se mitigan con la suite de contrato, que **debe** incluir `Browser` (spec §7 ya lo contempla con Playwright contra la PWA real).
- Las consultas derivadas (spec §3.1) se resuelven filtrando en C# sobre `getAll` con índices por `status`/`logicalDate`; si crecen, añadir índices IDB.
- El `IUnitOfWork` de Browser debe agrupar las escrituras en una única transacción IndexedDB (patrón del prototipo) para cumplir la atomicidad exigida a `DayRolloverService` (spec §4.1). Una lectura-modificación-escritura entre pestañas distintas no queda cubierta por la transacción: asumir una sola pestaña activa o usar `navigator.locks`.
- Migraciones de esquema web: versión de IndexedDB + `SchemaVersion` de ajustes (spec §8), con tests.
- `navigator.storage.persist()` al guardar los primeros datos (ya en spec §5.2). Informar al usuario de iOS de instalar la PWA para evitar la limpieza de 7 días.
- Riesgo residual: la validación en Safari/iOS y Firefox reales queda pendiente (no medido aquí). Recomendado un smoke test manual en iOS antes de la release web (issue 05/30).

## Cambios propuestos en la spec (no aplicados)

Por orden de aparición. Como gana B, la spec casi no cambia; solo se precisan los puntos de IndexedDB:

- **§1, fila Persistencia:** mantener tal cual y añadir "IndexedDB (web): módulo JS propio mínimo, sin EF Core ni SQLite WASM; ver ADR 0001".
- **§2.1, fila `Infrastructure.Browser`:** precisar "un object store por entidad, documentos JSON con el mismo serializador que el backup, índices por `status` y `logicalDate`, `IUnitOfWork` = una transacción IndexedDB por `SaveChangesAsync`; `localStorage` para ajustes; incluye la migración por `onupgradeneeded`".
- **§3 (Modelo de datos):** añadir una nota: "La web guarda cada entidad como documento JSON (claves Guid en cadena, `DateOnly`/`TimeOnly`/`TimeSpan` en formato ISO). Un cambio de modelo requiere migración EF (Android) **y** migración IndexedDB, ambas validadas por la suite de contrato".
- **§7:** el test de contrato contra "un fake del comportamiento de IndexedDB" se sustituye por el repositorio real de Browser ejecutado en navegador (Playwright), con el fake solo para ejecución rápida en CI sin navegador.
- **§8 (Riesgos):** añadir fila "Safari/iOS: limpieza de datos a los 7 días sin uso si la PWA no está instalada → pedir persistencia e invitar a instalar".
