# 28 · Pulido: identidad visual, accesibilidad y rendimiento

Status: in-review
Blocked by: 07, 11
Cubre: 3MT-42, 43, 44

## What to build

Icono, splash y colores definitivos; contraste AA, etiquetas para lectores de pantalla y áreas táctiles ≥ 48 dp; arranque en frío < 2 s en Android de gama media y WASM con trimming y Brotli.

## Acceptance criteria

- [x] Iconos de la PWA (maskable) y splash
- [x] Contraste AA y nivel indicado también por icono o texto
- [x] Medición de arranque en frío documentada
- [x] Trimming y compresión Brotli activos

## Blocked by

- 07-tachar-progreso-banner
- 11-pool-ver-recuperar

## Comments
- pm-3 (2026-10-22): incorporar aquí la carga diferida (lazy assembly loading) de las vistas de calendario (Día/Semana/Mes), trasladada desde el criterio no cumplido del 24.
- usuario (2026-10-22, vía pm-3): los estilos del frontal NO se parecen lo suficiente a los mocks de design/. El 28 debe dedicar un pase serio de fidelidad visual pantalla por pantalla (comparar con cada mock, capturas lado a lado en el navegador integrado si es viable) y documentar las diferencias que queden. Revisión por el usuario el 2026-10-23.
- pm-3 (2026-10-22): asignado a worker-10. Rama issue/28-pulido-identidad-a11y-rendimiento. Se lanza en paralelo con el 21 para no perder tiempo; los conflictos se resuelven al integrar.
- worker-10 (2026-10-22): entregado en `in-review` (rama issue/28-pulido-identidad-a11y-rendimiento). Informe abajo.

### Fidelidad visual a los mocks (pantalla por pantalla)
Método: los `.dc.html` no se abren en navegador (runtime del editor de claude.ai), así que se compararon leyendo su HTML/CSS en línea con la app real renderizada con Chrome headless (Playwright, 390×844, claro y oscuro) tras sembrar datos por la UI. No hay capturas lado a lado (el mock no se puede renderizar). Tokens extraídos del mock a variables `--tmt-*` al principio de `tmt.css` (color, forma, sombras, tamaños) + tema de MudBlazor (`Services/TmtTheme.cs`) con la misma paleta (claro y oscuro derivado) y Manrope (Google Fonts, como el mock; sin red cae a system-ui). El tinte y la tinta de cada nivel se derivan con `color-mix` del `--level-color`, así que sirven para niveles personalizados y modo oscuro.
Global: título de pantalla 34/800 (`h1` real), barra de pestañas de 84 px con iconos de línea del mock y estado activo (peso 800, trazo 2,2), FAB 60×60 radio 20, selector segmentado (`.tmt-segment`) en Pool, Calendario, Planificar y "¿Cuándo?", chips-filtro de 34 px con área táctil de 48, interruptores 48×28 en Ajustes/Fijar/Favorita, `:focus-visible`, `prefers-reduced-motion`.
- **Hoy (Main)**: igualado cabecera (fecha, título, anillo 72 px con n/total y "HECHAS"), grupos con título en mayúsculas 13/800 y progreso a la derecha (rojo si falta mínimo), tarjetas de tarea 60 px radio 14, círculo 26 px con aro del nivel / relleno con check, chip "Fijada · día N" oscuro, huecos discontinuos, tachado. Diferencias: falta la racha y el banner "Día cumplido" (necesitan datos/lógica que Hoy no tiene); falta el badge de pendientes del Pool en la pestaña; la fila no muestra categoría ni hora (TodayView no las trae); se conserva el icono de nivel en la cabecera (test y criterio de nivel por icono/texto) en vez del cuadradito del mock.
- **Hoy incompleto**: banner como tarjeta roja con borde 1,5 px, texto 16/800 y botón rojo a ancho completo; huecos con borde discontinuo del color del nivel. Diferencia: no hay la segunda línea "te lo recordaremos cada 30 min…".
- **Planificar**: tarjetas de cupo tintadas por nivel, segmentado Nueva/Favoritas/Pool, lista del pool y barra de acción oscura. Diferencias: no es un bottom-sheet modal con asa (es una página con ruta); los cupos muestran `n/cupo` (dato real) y no "/ mín."; sin secciones "Esta semana/Más antiguas".
- **Pool**: título + resumen, segmentado, chips, tarjetas con casilla, borde 2 px al seleccionar, etiqueta de nivel tintada en mayúsculas, barra de acción oscura (con espaciador). Diferencias: sin secciones por antigüedad; los 3 filtros extra no están en el mock y quedan como selects discretos; "Cancelar" es el tercer botón de la barra.
- **Editar tarea (NuevaTarea)**: título grande con subrayado, nivel en rejilla de 3 con tarjeta tintada y borde del color del nivel, categoría y repetición como chips, "¿Cuándo?" segmentado, Fijar/Favorita como tarjeta de filas con interruptor. Diferencias: no existe la cabecera "Cancelar · Editar tarea · Guardar" (acciones abajo, comportamiento intacto); hora, notas y repetición salen como campos desplegados y no como filas-resumen.
- **Ajustes**: rótulos 12/800 en mayúsculas, filas en tarjeta de borde 1 px radio 16 con separadores e interruptores. Diferencias: "Niveles y mínimos" (tarjetas con steppers) vive en `ajustes/niveles` y conserva su lista (no restyled a steppers); exportar/importar/backup conservan controles nativos.
- **Onboarding**: fondo blanco, puntos de paso, título 30/800, tarjetas de preset radio 18 con chips tintados, nota gris, botón primario 56 px. Prácticamente igual (el paso 3 no tiene mock).
- **Semana**: una fila por día también en pantallas anchas (antes 7 columnas), día actual con borde 2 px, futuros discontinuos, píldora de resultado, puntos cuadrados por nivel. Diferencia: hasta 3 tareas con "+N más" (el mock 2).
- **Mes**: rejilla en tarjeta radio 18, anillos de 40 px (cumplido relleno, no cumplido arco rojo sobre pista rosa, hoy en curso), puntos de programadas, leyenda con muestras, tres métricas (la primera oscura). Diferencia menor: las celdas futuras ya no llevan círculo discontinuo (el mock no lo tiene).
Pendiente para otra pasada: racha y banner "Día cumplido" en Hoy, secciones del pool, bottom-sheet de Planificar, steppers en Niveles, cabecera de edición, autoalojar Manrope (hoy Google Fonts; el service worker no la cachea, offline usa system-ui).

### Accesibilidad
- Contraste: auditoría automática (script Playwright, WCAG 4,5:1 texto normal / 3:1 grande) sobre 15 pantallas en claro: 0 fallos salvo el botón "Guardar" deshabilitado (exento). Se oscureció el gris de tachado/deshabilitado (`#8A909C` del mock da 3,2:1, ahora `#6F7685`). Tinta del nivel = 62 % del color hacia negro (blanco en oscuro). Oscuro: revisado visualmente (Hoy y Mes), sin auditoría automática.
- Nivel nunca solo por color: nombre en texto en cabeceras, etiquetas del pool/historial, selector (nombre + hint) y cupos; icono además en las cabeceras de Hoy.
- Áreas táctiles ≥ 48 dp: filas de tarea (el enlace del título cubre la fila), botones Mud ≥ 48, pestañas/chips con `::after` hasta 48, celdas del mes ampliadas, filas de ajustes ≥ 52. La auditoría mide el elemento y no sus pseudo-elementos, por eso aún lista chips/celdas (46) y selects de 44 dentro de filas-etiqueta de ≥ 52 que activan el control.
- Lectores de pantalla: títulos como `h1`, anillo de Hoy con `role=img` y etiqueta "n de m tareas hechas", notas con `aria-label`, cierre del aviso de error con rol y nombre, splash con `role=status`, iconos decorativos `aria-hidden`. Auditoría: 0 controles sin nombre accesible tras el arreglo. No probado con TalkBack/NVDA reales.

### PWA: iconos y splash
Iconos nuevos (aro con check + tres puntos de nivel sobre #15171C) generados con Chrome headless: `icon-192/512.png` (any), `icon-maskable-192/512.png` (motivo dentro de la zona segura del 80 %), `favicon.png`. Manifest con `purpose` any/maskable, `theme_color #15171C`, `background_color #F5F6F8` (splash nativo de Android = nombre + icono sobre ese fondo); splash en página (icono + nombre) mientras arranca WASM, con modo oscuro. `PwaTests` actualizado. Los iconos de la app MAUI no se tocan aquí.

### Rendimiento (publish Release de App.Web)
- Trimming: `PublishTrimmed=true`, `TrimMode=partial` explícitos en el csproj (los mismos que por defecto; "full" rompe localización/JSON por reflexión). Brotli: `BlazorEnableCompression=true`; el publish genera `.br` y `.gz` de todo `_framework`.
- Tamaño de `_framework`: 13,1 MB sin comprimir, 3,96 MB Brotli (5,02 MB gzip). Mayores: dotnet.native.wasm 2,9 MB (0,93 br), System.Private.CoreLib 1,9 MB (0,59 br), ICU (el navegador descarga un solo .dat).
- Arranque (Chrome headless en PC, servidor local con Brotli, sin service worker, hasta ver el onboarding): sin throttling 1,5 s en frío / 1,1 s recarga; CPU x4: 8,6 s / 6,4 s; CPU x4 + 4G (9 Mbps, 170 ms): 12,1 s con 3,43 MB transferidos; CPU x6 + 1,6 Mbps: 33,8 s. Conclusión honesta: **el objetivo de < 2 s en Android de gama media NO está demostrado y, con esta emulación (pesimista), no se cumple en primera carga**. Las cargas siguientes salen del service worker (no medidas aquí con SW activo). Mono interpretado sin AOT es el factor principal.
- Guion para Android real (pendiente de dispositivo): instalar la PWA desde el publish servido por HTTPS, `chrome://inspect` → Performance, arranque en frío (cerrar la app y quitarla de recientes), medir desde el toque hasta ver Hoy/onboarding (3 repeticiones, mediana); anotar modelo, versión de Chrome y red; repetir con el APK MAUI para comparar.
- Propuestas: AOT en el publish web (más tamaño, menos CPU), ICU reducido/invariante si se acepta, precarga del service worker en la primera visita.
- Carga diferida de las vistas de calendario: **no implementada**. Requiere mover Día/Semana/Mes a un ensamblado propio (p. ej. `ThreeMustTasks.UI.Calendar`), declararlo con `BlazorWebAssemblyLazyLoad`, cargarlo en `Router.OnNavigateAsync` y añadirlo a `AdditionalAssemblies`; toca Routes, los tests bUnit (montan `Routes`) y el cliente MAUI. El ahorro esperado es marginal (esas 3 vistas son una fracción mínima frente a CoreLib/MudBlazor). Propuesta: no hacerla; medir antes con AOT.
