# 02 · Spike: notificaciones locales programadas en MAUI (.NET 10)

Status: ready-for-agent
Blocked by: ninguno
Cubre: 3MT-29

## What to build

Validar que Plugin.LocalNotification (o AlarmManager nativo) funciona con .NET 10 y MAUI: notificaciones programadas que salen con la app cerrada y que al tocarlas abren una ruta interna.

## Acceptance criteria

- [ ] Notificación programada a 2 min que llega con la app cerrada en emulador Android
- [ ] Al tocarla se abre una página concreta de la app
- [ ] Se comprueba el comportamiento con el permiso `POST_NOTIFICATIONS` denegado y con alarmas exactas denegadas
- [ ] Decisión (plugin o nativo) registrada como ADR

## Blocked by

None - can start immediately

## Comments

- usuario (2026-10-22): delega la decisión en el PM, igual que el 01. Criterio: fiabilidad con la app cerrada (reinicio del dispositivo incluido), menos código nativo y mantenimiento, comportamiento con permisos denegados. Plugin.LocalNotification por defecto; AlarmManager solo si falla en el emulador. ADR provisional (`docs/adr/0002-*`) a revisar por el usuario. Si el entorno no tiene emulador Android, el worker prepara el prototipo y lo documenta sin dar la verificación por hecha.
