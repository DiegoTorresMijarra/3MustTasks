# 18 · Recordatorio '¡Añade tus tareas!' en Android

Status: resolved
Blocked by: 02, 04, 14
Cubre: 3MT-28, 29, 30, parte de 31

## What to build

`ReminderPlanner` puro con ventana móvil de 7 días e IDs deterministas, `INotificationScheduler` en Android, permisos, reprogramación al arrancar el dispositivo y deep link a Planificar.

## Acceptance criteria

- [x] Tests exhaustivos del planner sin dependencias de plataforma
- [x] Se reprograma tras `PlanChanged`, cambios de ajustes y cierre de día
- [x] Permiso `POST_NOTIFICATIONS`, alarmas exactas con alternativa inexacta y `BOOT_COMPLETED`
- [x] Pantalla de ayuda de ahorro de batería
- [x] Ajustes de hora y días del recordatorio
- [ ] Verificado en emulador con la app cerrada

## Blocked by

- 02-spike-notificaciones-maui
- 04-esqueleto-android
- 14-planificar-favoritas

## Comments
- pm-3 (2026-10-22): asignado a worker-4. Rama issue/18-recordatorio-planificacion-android.
- worker-4 (2026-10-22): entregado en la rama `issue/18-recordatorio-planificacion-android`. Estado: `in-review`.

### Qué se hizo
- **Core**: `ReminderPlanner` puro (ventana de 7 días lógicos, IDs deterministas `tipo*10_000_000 + DayNumber`; hoy se omite si ya pasó la hora o si `PlanMinimumMetAt` está puesto; días de la semana; `DayStartsAt` y hora anterior al inicio del día). `PlannedNotification`, `INotificationScheduler` (`ReplaceAllAsync`, `RequestPermissionAsync`, `GetStatusAsync`, `OpenSystemSettingsAsync`) y `ReminderSyncService` (se suscribe a `PlanChanged` y al nuevo `PlanEvents.RolloverCompleted`, que `DayRolloverService` lanza siempre al terminar). Ajustes nuevos `PlanReminderEnabled` y `PlanReminderDaysMask` (+ migración EF `AddPlanReminderSettings` escrita a mano porque no hay `dotnet-ef`; `HasPendingModelChanges` en verde).
- **UI**: sección "Notificaciones" en Ajustes (activar, hora, días, estado de permisos, aviso de alarmas inexactas), página `/ajustes/bateria`, `PendingNavigation` (deep link, consumido por `AppRoot` tras el primer render y mientras corre), `NoOpNotificationScheduler` por defecto (PWA y tests). Se reprograma al cambiar ajustes, al terminar el onboarding y al restaurar un backup desde Ajustes.
- **Android**: `AndroidNotificationScheduler` sobre Plugin.LocalNotification 14.1.2 (implementa a la vez `INotificationScheduler` e `INotificationPermission`: el onboarding sigue funcionando con la misma instancia). `UseLocalNotification()`, `MainActivity` `SingleTop`, permisos `POST_NOTIFICATIONS`, `RECEIVE_BOOT_COMPLETED`, `WAKE_LOCK`, `VIBRATE`, `SCHEDULE_EXACT_ALARM`; no `USE_EXACT_ALARM`. Modo `AndroidScheduleMode.Default` (exacta si hay permiso, si no inexacta). Sin permiso de notificaciones no programa y cancela todo. Tap en notificación y arranque en frío (`LaunchNotificationDetails`) llevan a `/planificar`.

### Pendiente de decidir por el usuario
- **SCHEDULE_EXACT_ALARM frente a solo inexactas**: es una opción provisional del PM (pm-3), no una decisión del usuario. Está declarado `SCHEDULE_EXACT_ALARM` con alternativa inexacta si no se concede. Un recordatorio matinal tolera minutos de desvío, así que se podría quitar el permiso y usar siempre inexactas (el ADR 0002 lo menciona). Cambiarlo es quitar una línea en `MainApplication.cs` y el aviso/botón de alarmas exactas en Ajustes.

### NO verificado (sin Android SDK ni emulador en esta máquina)
- El criterio "Verificado en emulador con la app cerrada" **no se ha ejecutado** y queda sin marcar. Tampoco se ha verificado: que el aviso salga con el proceso muerto, que el tap en frío abra Planificar (el `BlazorWebView` aún puede no estar listo cuando llega el evento; `PendingNavigation` está pensado para eso, sin probar), el comportamiento con permisos denegados ni que el receptor `BOOT_COMPLETED` del plugin reprograme tras reiniciar (inferencia del ADR). Lo comprobado: el proyecto Android compila con `-warnaserror` y el manifest fusionado contiene los permisos, `launchMode=singleTop` y `ScheduledAlarmReceiver` con `BOOT_COMPLETED`, sin `USE_EXACT_ALARM`. Los tres primeros criterios marcados lo están por lógica y UI probadas con dobles; la parte de plataforma de "Permiso/alarmas/BOOT_COMPLETED" está implementada y declarada, pero sin ejecutar.
- Guion manual (emulador API 33+ o dispositivo): 1) `dotnet build -t:Run -f net10.0-android src/ThreeMustTasks.App.Maui`; 2) Ajustes > Notificaciones: conceder permiso y poner la hora del recordatorio 2 min en el futuro; 3) cerrar la app deslizándola de recientes (no solo atrás) y esperar: debe salir "¡Añade tus tareas!"; tocarla debe abrir Planificar (repetir con la app en segundo plano); 4) repetir con el permiso de notificaciones denegado (no sale nada y Ajustes lo indica) y con "Alarmas y recordatorios" denegado (sale, con posible retraso; Ajustes muestra el aviso inexacto); 5) programar a unos minutos, reiniciar el dispositivo sin abrir la app y comprobar que sale; 6) "Forzar detención": no debe salir hasta abrir la app; 7) añadir tareas hasta el mínimo de hoy antes de la hora: no debe salir el de hoy.
- Si el paso 3 o 5 falla, el ADR 0002 manda pasar a AlarmManager nativo detrás de `INotificationScheduler`.

### Fuera de alcance / propuestas de spec (sin editar la spec)
- Solo se implementa el recordatorio de planificación. Insistencia, aviso de completar, recordatorio de tarea con hora, resumen y tarea a futuro (resto de 3MT-28) y sus ajustes (3MT-31) no están; `ReminderPlanner` está preparado para añadirlos.
- Spec §2.2: `RequestPermissionAsync` es `Task<bool>` como en la spec; el puerto añade `GetStatusAsync` y `OpenSystemSettingsAsync`. Proponer reflejarlo en §2.2 y los ajustes `PlanReminderEnabled`/`PlanReminderDaysMask` en §3 (`PlanReminder {Enabled, Time, Days}`), además de los cambios de spec ya listados en el ADR 0002.
- La PWA queda con el planificador no-op (issue 32). `INotificationPermission` se mantiene (la web lo implementa); en Android lo implementa el mismo objeto que el planificador.
- Restaurar un backup por otra vía distinta de la pantalla de Ajustes no reprograma hasta el siguiente `PlanChanged` o cierre de día.
- pm-3 (2026-10-22): resolved con reserva (integrado en main; build -warnaserror y tests en verde salvo el flaky conocido del 13, ya devuelto a worker-6). Criterio "Verificado en emulador con la app cerrada" SIN ejecutar; el guion está arriba. Si falla, pasar a AlarmManager nativo tras INotificationScheduler (ADR 0002). Pendiente del usuario: SCHEDULE_EXACT_ALARM sí/no. Propuestas de spec §2.2 y §3 sin aplicar.
