# 21 · Notificaciones en la PWA con la app abierta

Status: in-review
Blocked by: 05, 18
Cubre: 3MT-32

## What to build

Implementación web de `INotificationScheduler` con la Notification API y temporizadores para el día en curso. La UI indica claramente que solo funciona con la app abierta.

## Acceptance criteria

- [x] Solicita el permiso de notificaciones
- [ ] Las notificaciones del día salen con la PWA abierta (implementado y probado con dobles; falta verificar en navegador real, ver Comments)
- [x] Aviso visible en Ajustes y en el onboarding sobre la limitación

## Blocked by

- 05-persistencia-web
- 18-recordatorio-planificacion-android

## Comments
- pm-3 (2026-10-22): asignado a worker-9 (reutilizado, ~30% de contexto; mismo ámbito). Rama issue/21-notificaciones-pwa.
- worker-9 (2026-10-24): entregado, `in-review`.

### Qué se hizo
- `WebNotificationScheduler` (`ThreeMustTasks.UI/Services`): `INotificationScheduler` de la PWA sobre un puente `IWebNotificationBridge` (permiso, petición, mostrar). Programa con `TimeProvider.CreateTimer` solo lo que cae en las próximas 24 h (día en curso); cada `ReplaceAllAsync` cancela los temporizadores anteriores (idempotente); sin permiso `granted` cancela y no programa. `GetStatusAsync`: soportado salvo `unsupported`, alarmas exactas "concedidas" (no existen en web, evita el aviso de Android).
- `JsWebNotificationBridge` (App.Web) + `tmtNotifications.permission/requestPermission/show` en `index.html` (usa `registration.showNotification` si hay service worker —necesario en Chrome Android— y `new Notification` si no). Handler `notificationclick` en `service-worker.js` y `service-worker.published.js`: enfoca la app y, si la notificación trae ruta, navega.
- `WebNotificationPermission` (onboarding 17) ahora usa el mismo puente: un solo punto de acceso a la Notification API; el onboarding no cambia.
- `Program.cs` de App.Web registra puente y planificador (ganan al no-op) y arranca `ReminderSyncService.Start()`; reutiliza `ReminderPlanner`/`ReminderSyncService` sin tocarlos.
- Ajustes: aviso visible (`MudAlert`, mismo texto que el onboarding) cuando `INotificationPermission.OnlyWorksWhileOpen`; en web se ocultan "Abrir ajustes del sistema" y la ayuda de batería (no aplican). El botón "Activar notificaciones" sí funciona.

### Tests
13 nuevos en `WebNotificationTests` (UI.Tests, puente doble + `FakeTimeProvider`): permiso, estado, disparo a su hora, reemplazo sin duplicados, ventana de 24 h, sin permiso, fallo del navegador, integración con `ReminderSyncService` real y aviso en Ajustes.

### No verificado (criterio 2 sin marcar)
- Ninguna ejecución en un navegador real: ni la Notification API, ni `showNotification` vía service worker, ni `notificationclick`, ni los temporizadores tras suspender la pestaña. Guion manual (Chrome de escritorio y Chrome Android, PWA publicada por HTTPS o `localhost`): (1) Ajustes > Activar notificaciones: el navegador pide permiso y el estado pasa a "activadas"; (2) crear una tarea de hoy con hora a +2 min y "A la hora", dejar la pestaña abierta en segundo plano: debe salir una notificación a su hora; (3) tocarla: enfoca la app (con ruta `planificar`, navega); (4) activar el resumen nocturno a +2 min con una tarea sin tachar: sale "Te queda 1 tarea"; (5) cerrar la pestaña: no sale nada (limitación esperada); (6) permiso denegado: no sale nada y Ajustes muestra el aviso.
- Limitaciones conocidas: los navegadores suspenden temporizadores de pestañas en segundo plano (pueden retrasarse); al volver a primer plano se reprograma (cierre de día), pero un aviso vencido durante la suspensión se pierde. iOS Safari solo permite notificaciones en PWA instalada.
