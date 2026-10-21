# 18 · Recordatorio '¡Añade tus tareas!' en Android

Status: ready-for-agent
Blocked by: 02, 04, 14
Cubre: 3MT-28, 29, 30, parte de 31

## What to build

`ReminderPlanner` puro con ventana móvil de 7 días e IDs deterministas, `INotificationScheduler` en Android, permisos, reprogramación al arrancar el dispositivo y deep link a Planificar.

## Acceptance criteria

- [ ] Tests exhaustivos del planner sin dependencias de plataforma
- [ ] Se reprograma tras `PlanChanged`, cambios de ajustes y cierre de día
- [ ] Permiso `POST_NOTIFICATIONS`, alarmas exactas con alternativa inexacta y `BOOT_COMPLETED`
- [ ] Pantalla de ayuda de ahorro de batería
- [ ] Ajustes de hora y días del recordatorio
- [ ] Verificado en emulador con la app cerrada

## Blocked by

- 02-spike-notificaciones-maui
- 04-esqueleto-android
- 14-planificar-favoritas

## Comments
