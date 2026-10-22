# ADR 0002 · Notificaciones locales programadas en MAUI: Plugin.LocalNotification frente a AlarmManager nativo

- **Estado:** provisional – revisar por el usuario (decisión basada en documentación y análisis del paquete; **sin verificación en emulador ni dispositivo**, ver "Qué no se ha verificado")
- **Fecha:** 2026-10-22
- **Issue:** 02 (spike notificaciones MAUI) · cubre 3MT-29
- **Autor:** worker-1

> **Decisión provisional, revisar por el usuario.** El usuario delegó la decisión en el PM con este criterio: fiabilidad con la app cerrada (incluido el reinicio del dispositivo), menos código nativo y menos mantenimiento, y comportamiento con permisos denegados (`POST_NOTIFICATIONS` y alarmas exactas). Por defecto, Plugin.LocalNotification; AlarmManager nativo solo si el plugin falla. En esta máquina no hay Android SDK ni emulador, así que no se ha podido ver ninguna notificación salir. El ADR deja claro qué es verificado y qué es inferido.

## Contexto

Android es la única plataforma de la app nativa en v1 (spec §1, §5.1: .NET 10, MAUI Blazor Hybrid, `minSdk 26`). La app necesita avisos locales programados (recordatorio diario, avisos de las 3 tareas) que salgan **con la app cerrada** y que, al tocarlos, abran una página interna (p. ej. `/planificar`). No hay backend ni push. El issue 18 implementa el servicio de notificaciones y depende de esta decisión.

## Opciones

- **A. Plugin.LocalNotification** (paquete NuGet `Plugin.LocalNotification` 14.1.2, mantenido por thudugala; ctx7 `/thudugala/plugin.localnotification`, reputación alta). Envuelve `AlarmManager` + `NotificationManager` y aporta un receptor de alarma/arranque, canales, permisos y eventos de tap.
- **B. Nativo.** `AlarmManager.SetExactAndAllowWhileIdle` (o inexacta) + `BroadcastReceiver` propio para disparar la notificación + segundo receptor para `BOOT_COMPLETED` que reprograma desde la base de datos + `NotificationChannel`, permisos en manifest y petición de permisos en tiempo de ejecución, todo con código específico de `Platforms/Android` y `PendingIntent` hacia `MainActivity`.

## Prototipo

`spikes/notificaciones-maui/` (proyecto MAUI mínimo `net10.0-android`, fuera de `ThreeMustTasks.sln`; mismo `minSdk 26`). Pantalla de pruebas con botones: pedir `POST_NOTIFICATIONS`, ver estado (notificaciones + alarmas exactas), abrir el ajuste de alarmas exactas, programar a 2 min y a 20 s, listar pendientes, cancelar. La notificación lleva `ReturningData = "planificar"` y la app navega a la ruta `//planificar` (Shell) tanto por el evento `NotificationActionTapped` (app en memoria) como por `LocalNotificationCenter.LaunchNotificationDetails` (arranque en frío), con navegación idempotente. Se programa con `AndroidScheduleMode.Default` (exacta si hay permiso, inexacta con `AllowWhileIdle` si no) y, antes de programar, se consulta `GetNotificationPermissionStatus()`.

Cómo probarlo cuando haya emulador o dispositivo (API 33+ para ver el permiso de ejecución):

1. `dotnet build -t:Run -f net10.0-android spikes/notificaciones-maui` (o desplegar desde Visual Studio / VS Code).
2. Botón 1 (conceder permiso), botón 4 (programar a 2 min), **cerrar la app deslizándola de recientes** (no solo atrás), esperar 2 min, comprobar la notificación y tocarla: debe abrirse "PÁGINA PLANIFICAR".
3. Repetir con el permiso denegado (Ajustes > Apps > Notificaciones) y con las alarmas exactas denegadas (Ajustes > Apps > Alarmas y recordatorios). Repetir con un reinicio del dispositivo entre programar y la hora prevista.

## Qué se ha verificado y qué no

**Verificado en esta máquina (Windows 11, SDK 10.0.402, workload `maui-android` 10.0.110 instalado):**

- `dotnet build -warnaserror` del prototipo en Debug: 0 advertencias, 0 errores. Esto confirma que el plugin 14.1.2 resuelve y compila contra `net10.0-android` con MAUI de .NET 10 (el paquete trae `lib/net10.0-android36.0`).
- Del manifest fusionado generado en `obj/Debug/net10.0-android/android/AndroidManifest.xml`: el plugin añade **por sí solo** el `ScheduledAlarmReceiver` registrado para `BOOT_COMPLETED`, `MY_PACKAGE_REPLACED` y `QUICKBOOT_POWERON` (más `NotificationActionReceiver` y `NotificationForegroundService`), y los permisos declarados en `MainApplication.cs` (`POST_NOTIFICATIONS`, `RECEIVE_BOOT_COMPLETED`, `WAKE_LOCK`, `VIBRATE`, `SCHEDULE_EXACT_ALARM`) llegan al manifest. `targetSdkVersion` resultante: 36.
- De la documentación del plugin (ctx7 y XML del paquete): la API de programación (`NotificationRequest.Schedule.NotifyTime`, `RepeatType`), `AndroidScheduleMode` (`Default`, `Inexact`, `InexactAllowWhileIdle`, `Exact`, `ExactAllowWhileIdle`, `AlarmClock`; `Default` = exacta si hay permiso, inexacta en caso contrario), `GetNotificationPermissionStatus()` con `IsEnabled` y `CanScheduleExactAlarms`, `RequestNotificationPermission()`, `AndroidService.RequestExactAlarmsPermission()`, `NotificationActionTapped` y `LaunchNotificationDetails` para el arranque en frío.

**NO verificado (requiere emulador o dispositivo; ningún criterio de ejecución se da por cumplido):**

1. Que la notificación programada a 2 min llegue con la app cerrada (proceso muerto).
2. Que, al tocarla, se abra la página `Planificar`, tanto en arranque en frío como con la app en segundo plano; el prototipo está escrito según la documentación pero nunca se ha ejecutado.
3. El comportamiento real con `POST_NOTIFICATIONS` denegado y con alarmas exactas denegadas (qué hace `Show` y si la alarma inexacta llega y con cuánto retraso).
4. Que el receptor de arranque del plugin reprograme efectivamente las notificaciones tras un reinicio. Lo que sí se sabe es que el receptor existe y está registrado para `BOOT_COMPLETED`; **que reprograme todas las pendientes es una inferencia** (el plugin guarda sus peticiones y su documentación lo ofrece como característica, pero no se ha leído su código fuente ni probado).
5. Fabricantes con ahorro de batería agresivo (Xiaomi, Huawei, Samsung, etc.), Doze prolongado y "forzar detención" (tras forzar detención Android cancela las alarmas y no entrega `BOOT_COMPLETED` hasta que el usuario abre la app; esto afecta igual a A y B).
6. La integración con el WebView de Blazor (el prototipo usa Shell/ContentPage, no `BlazorWebView`); ver consecuencias.

Conocimiento de la plataforma Android que se da por cierto sin haberlo re-consultado hoy (verificar en la documentación oficial al ejecutar el issue 18): desde Android 13 `POST_NOTIFICATIONS` es permiso de ejecución y, si está denegado, el sistema descarta la notificación sin error; desde Android 12 las alarmas exactas exigen `SCHEDULE_EXACT_ALARM` (denegado por defecto en instalaciones nuevas dirigidas a Android 14+) o `USE_EXACT_ALARM` (concedido sin prompt, pero Google Play solo lo permite a apps de alarma o calendario); las alarmas de `AlarmManager` no sobreviven a un reinicio y hay que reprogramarlas en `BOOT_COMPLETED`.

## Comparativa

| Criterio | A · Plugin.LocalNotification | B · AlarmManager nativo |
|---|---|---|
| Fiabilidad con la app cerrada | Usa `AlarmManager` con `AllowWhileIdle`; mismo mecanismo que B (documentado, **no probado aquí**) | Igual, bajo control total |
| Reinicio del dispositivo | Receptor `BOOT_COMPLETED` incluido y fusionado en el manifest (**verificado**); reprogramación efectiva **inferida** | Hay que escribir y probar el receptor y la persistencia de lo programado |
| Permisos denegados | `GetNotificationPermissionStatus()` y `AndroidScheduleMode.Default` (degrada de exacta a inexacta sin lanzar) (**documentado**) | Hay que implementar la detección (`CanScheduleExactAlarms`, `AreNotificationsEnabled`) y la degradación |
| Código nativo propio | ~0 líneas nativas; atributos de permisos y `UseLocalNotification()` | Receptor de alarma, receptor de arranque, canal, `PendingIntent`, petición de permisos: del orden de 200 a 300 líneas en `Platforms/Android` (estimación) |
| Mantenimiento | Dependencia externa de un único mantenedor; actualizaciones con cada versión de .NET/Android (14.1.2 ya apunta a `net10.0-android36.0`) | Sin dependencia, pero hay que seguir los cambios de Android (alarmas exactas, Doze, restricciones de segundo plano) |
| Tap y ruta interna | `NotificationActionTapped` + `LaunchNotificationDetails` + `ReturningData` | `PendingIntent` con extras hacia `MainActivity` y `OnNewIntent` |
| Compilación con .NET 10 | Compila con `-warnaserror` (**verificado**) | No aplica (API de Android) |
| Portabilidad | iOS/Windows disponibles si algún día se amplía | Solo Android (aceptable en v1) |

## Decisión (provisional)

**Se adopta la opción A: Plugin.LocalNotification 14.1.2.** Razones, según el criterio del usuario:

1. **Menos código nativo y mantenimiento:** el receptor de alarma, el receptor de arranque y el canal vienen resueltos y se fusionan solos en el manifest; la aplicación solo declara permisos y llama a `UseLocalNotification()`.
2. **Permisos denegados:** el plugin expone el estado de ambos permisos y `AndroidScheduleMode.Default` degrada de exacta a inexacta, que es justo lo que se quiere (un aviso con unos minutos de retraso es aceptable; ninguno, no).
3. **Reinicio:** la infraestructura existe (receptor `BOOT_COMPLETED`); escribirla a mano replicaría lo mismo.

**Salvaguarda:** como la fiabilidad real con la app cerrada no se ha comprobado, el issue 18 debe empezar ejecutando el guion de pruebas del prototipo en un emulador o dispositivo. **Si falla** (la notificación no llega con el proceso muerto, o no sobrevive a un reinicio), se pasa a la opción B detrás de la misma interfaz `INotificationScheduler`, de modo que el cambio no afecte al resto. Esta decisión se invalida solo con evidencia de ese fallo.

## Consecuencias para el issue 18

- **Paquete:** `Plugin.LocalNotification` (14.1.2 o la vigente) solo en `ThreeMustTasks.App.Maui`; el puerto `INotificationScheduler` ya existe en la spec (§2.1, `ReplaceAllAsync` con ventana móvil de 7 días, §4) y la implementación con el plugin vive en `ThreeMustTasks.App.Maui`. La web no programa notificaciones en v1.
- **Inicialización:** `builder.UseLocalNotification()` en `MauiProgram.CreateMauiApp()`.
- **Permisos en `MainApplication.cs`:** `POST_NOTIFICATIONS`, `RECEIVE_BOOT_COMPLETED`, `WAKE_LOCK`, `VIBRATE` y `SCHEDULE_EXACT_ALARM`. El resto de componentes (`ScheduledAlarmReceiver` con `BOOT_COMPLETED`/`MY_PACKAGE_REPLACED`, `NotificationActionReceiver`, `NotificationForegroundService`) los aporta el paquete. No se declara `USE_EXACT_ALARM`: la app no es de alarmas ni calendario y Google Play lo restringe. Con avisos que toleren minutos de desviación, `SCHEDULE_EXACT_ALARM` puede incluso omitirse y quedarse en inexacta; decisión del issue 18 según el requisito de puntualidad.
- **Programar:** `NotificationRequest` con `Schedule.NotifyTime` y `Schedule.Android.ScheduleMode = AndroidScheduleMode.Default`; `RepeatType` según el aviso; `ReturningData` con la ruta interna (`planificar`). Reprogramar (cancelar + `Show`) al cambiar ajustes de recordatorios y al arrancar la app.
- **Permisos en ejecución:** consultar `GetNotificationPermissionStatus()` antes de programar. Si `IsEnabled` es falso: no programar, mostrar en Ajustes un aviso con enlace a los ajustes del sistema y no insistir en cada arranque. Si `CanScheduleExactAlarms` es falso: programar igualmente en modo `Default` (inexacta), sin bloquear, y ofrecer opcionalmente `AndroidService.RequestExactAlarmsPermission()`.
- **Reinicio:** confiar en el receptor del plugin, y **además** reprogramar al abrir la app (idempotente por `NotificationId`) como red de seguridad; documentar que tras "forzar detención" los avisos no llegan hasta que se abra la app.
- **Deep link a Planificar (Blazor Hybrid):** el prototipo usa Shell; en la app real la página es una ruta Blazor (`/planificar`, `PlanPage`). Patrón recomendado: un servicio singleton `PendingNavigation` al que se llega desde `NotificationActionTapped` (app en memoria) y desde `LaunchNotificationDetails` (arranque en frío, consultado en `Window.Created`, como ya hace `App.xaml.cs` con el cierre de día); la UI Blazor lo consume tras el primer render y llama a `NavigationManager.NavigateTo("/planificar")`. Hay que probar el arranque en frío, donde el `BlazorWebView` aún no está listo cuando llega el evento.
- **Actividad:** `MainActivity` con `LaunchMode = LaunchMode.SingleTop` para reutilizar la actividad en el tap.
- **Pruebas:** la lógica de qué notificar y cuándo va en Core con tests unitarios; la parte de plataforma solo admite prueba manual o de instrumentación. Dejar en la descripción del issue 18 el guion manual del apartado "Prototipo".
- **Riesgo residual:** fabricantes con ahorro agresivo de batería pueden retrasar o suprimir alarmas; informar en la documentación de usuario y considerar el aviso de "excluir de optimización de batería" solo si las pruebas reales lo exigen.

## Cambios propuestos en la spec (no aplicados)

La spec ya prevé Plugin.LocalNotification (§1), `INotificationScheduler` (§2.1) y reprogramar al abrir y al arrancar (§8), por lo que apenas cambia:

- **§1, fila Notificaciones Android:** sustituir "(o API nativa ... si da problemas)" por "Plugin.LocalNotification 14.x; AlarmManager nativo solo si falla en pruebas reales; ver ADR 0002".
- **§2.1, fila `App.Maui`:** precisar que el receptor de arranque lo aporta el plugin (no se escribe uno propio salvo que se pase a la opción B).
- **§5.1:** listar los permisos Android (`POST_NOTIFICATIONS`, `RECEIVE_BOOT_COMPLETED`, `WAKE_LOCK`, `VIBRATE`, `SCHEDULE_EXACT_ALARM`) y la política de degradación: sin permiso de notificaciones no se programa y se avisa; sin alarmas exactas se programa inexacta.
- **§8, fila de notificaciones poco fiables:** añadir "tras forzar detención no hay avisos hasta abrir la app".

