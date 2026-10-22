# 02 · Spike: notificaciones locales programadas en MAUI (.NET 10)

Status: in-review
Blocked by: ninguno
Cubre: 3MT-29

## What to build

Validar que Plugin.LocalNotification (o AlarmManager nativo) funciona con .NET 10 y MAUI: notificaciones programadas que salen con la app cerrada y que al tocarlas abren una ruta interna.

## Acceptance criteria

- [ ] Notificación programada a 2 min que llega con la app cerrada en emulador Android
- [ ] Al tocarla se abre una página concreta de la app
- [ ] Se comprueba el comportamiento con el permiso `POST_NOTIFICATIONS` denegado y con alarmas exactas denegadas
- [x] Decisión (plugin o nativo) registrada como ADR (provisional, `docs/adr/0002-notificaciones-maui.md`)

## Blocked by

None - can start immediately

## Comments

- usuario (2026-10-22): delega la decisión en el PM, igual que el 01. Criterio: fiabilidad con la app cerrada (reinicio del dispositivo incluido), menos código nativo y mantenimiento, comportamiento con permisos denegados. Plugin.LocalNotification por defecto; AlarmManager solo si falla en el emulador. ADR provisional (`docs/adr/0002-*`) a revisar por el usuario. Si el entorno no tiene emulador Android, el worker prepara el prototipo y lo documenta sin dar la verificación por hecha.
- pm-3 (2026-10-22): asignado a worker-1. Rama issue/02-spike-notificaciones-maui. Decisión delegada en el PM: ADR 0002 provisional "revisar por el usuario".
- worker-1 (2026-10-22): entregado, `in-review`. Prototipo en `spikes/notificaciones-maui/` (compila con `dotnet build -warnaserror`, workload `maui-android` presente) y ADR provisional `docs/adr/0002-notificaciones-maui.md`: Plugin.LocalNotification 14.1.2.
  - **Criterios NO verificados** (sin Android SDK ni emulador en la máquina; se dejan sin marcar): notificación a 2 min con la app cerrada; tap que abre la página Planificar; comportamiento con `POST_NOTIFICATIONS` denegado y con alarmas exactas denegadas. Tampoco el reinicio del dispositivo. El guion manual para ejecutarlos está en el ADR ("Prototipo").
  - Verificado sin emulador: el plugin compila contra `net10.0-android` (trae `net10.0-android36.0`) y su `ScheduledAlarmReceiver` con `BOOT_COMPLETED`/`MY_PACKAGE_REPLACED` aparece en el manifest fusionado. Que reprograme todo tras reiniciar es una inferencia.
  - Propuesta al PM: el issue 18 debe empezar ejecutando ese guion en emulador o dispositivo; si falla, pasar a AlarmManager nativo tras `INotificationScheduler`. El criterio del ADR está cumplido solo como provisional. Cambios de spec propuestos (no aplicados) al final del ADR.
