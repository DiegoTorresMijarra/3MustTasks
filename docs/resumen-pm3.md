# Resumen de la sesión pm-3 (2026-10-22 → 23)

Estado: **los 30 issues de v1 están `resolved`** y `main` está pusheado. Build `-warnaserror` limpio (solución y `ThreeMustTasks.App.Maui` net10.0-android). Tests: Core 323, Infrastructure 160, UI 235.

"Resolved" significa revisado, integrado y con build/tests en verde, **no** probado en dispositivo. No hay emulador Android ni soporte de service workers en el entorno de los agentes.

## Hecho en esta sesión
02 spike notificaciones (ADR 0002 provisional), 12 archivar/eliminar, 13 tareas a futuro, 17 onboarding, 18 recordatorio de planificación Android, 19 insistencia y modo estricto, 20 recordatorio de tarea con hora, 21 notificaciones PWA, 23 semana, 24 mes, 25 exportar .ics, 26 importar .ics, 27 backup JSON, 28 pulido (+ segundo pase de fidelidad visual, rama `issue/28b-fidelidad-visual`, capturas en `docs/capturas/`), 29 APK firmado, 30 despliegue PWA.

## Qué debes probar o hacer tú
1. **Android real/emulador (lo más importante).** Guion manual en los Comments de los issues 02 y 18: notificación a 2 min con la app cerrada, tap que abre Planificar, tras reiniciar el dispositivo, con `POST_NOTIFICATIONS` denegado y con alarmas exactas denegadas. Si falla, pasar a AlarmManager nativo tras `INotificationScheduler` (ADR 0002). Tu SDK Android existe pero sin emulador (instalarlo son varios GB; no lo he hecho).
2. **Descarga/selección de ficheros en el WebView de MAUI** (backup JSON y .ics): puede requerir Share/FilePicker nativo. Ida y vuelta Android ↔ PWA sin verificar (27).
3. **PWA**: activar GitHub Pages (Settings > Pages > Source: GitHub Actions); checklist de `docs/despliegue-pwa.md` (service worker, offline, instalación). Hasta entonces el workflow `deploy-pages.yml` falla en el job de despliegue en cada push a `main`.
4. **APK**: crear keystore y los 4 secretos (`docs/release-android.md`) y probar con un tag tipo `v0.0.1-rc.1`.
5. **Revisar el aspecto frente a los mocks** (capturas en `docs/capturas/`). No se han podido hacer capturas lado a lado porque los `.dc.html` solo se renderizan en el editor de claude.ai.

## Decisiones/dudas pendientes para ti
- **ADR 0002** (Plugin.LocalNotification) es provisional, pendiente de tu revisión.
- **`SCHEDULE_EXACT_ALARM`**: declarado con alternativa inexacta; un recordatorio matinal tolera minutos de desvío, así que se podría quitar (una línea en `MainApplication.cs` + aviso en Ajustes).
- **Arranque en frío < 2 s no demostrado** (28): emulación pesimista 8,6 s (CPU x4); propuestas AOT / ICU reducido / precarga del service worker. Carga diferida del calendario no hecha (medir antes con AOT).
- **Pool**: ¿secciones como el mock ("Esta semana" arriba) o por spec (más antiguas primero)? Cambiar es una línea en `PoolPanel.Sections()`.
- **Ajustes > Recordatorios**: la tarjeta no se reorganizó como el mock (habría que ajustar tests).
- **"Restaurar valores por defecto"** en Ajustes llama a `RestoreDefaultsAsync` sin confirmación (igual que en la página Niveles). Probablemente conviene confirmar.
- **Recordatorios (20)**: `ReminderOffset == null` = sin recordatorio (las tareas con hora importadas de .ics no avisan); el aviso de tarea a futuro suena a la hora de planificación (07:30); el resumen nocturno solo cuenta el plan de hoy.
- **Insistencia (19)** depende del recordatorio de planificación (si está desactivado o el día no está marcado, no suena).
- **Spec sin actualizar** (propuestas de los workers en los Comments de 02, 18, 19, 20): §1 (BackupService vive en Core), §2.1, §2.2, §3 (ajustes planos nuevos), §4.5, §5.1, §8.
- **Menores**: string sin uso `Archived_BackToPool`; fechas programadas ya pasadas no se autoañaden (spec `== hoy`); selector "cuándo" visible en tareas archivadas; Manrope se carga de Google Fonts (autoalojar para offline); falta "siguiente aviso" y "Buenos días" en Hoy; steppers +/− de Niveles no llegan a 48 dp.
- **Importación .ics (26)** no es transaccional (el dedupe permite reintentar); fixture de Google es de imitación.

## Incidencias del proceso
- `git add -A` coló `.idea/` en dos commits; ya está fuera del índice y en `.gitignore`.
- Un test intermitente del 13 (orden por `CreatedAt` + Guid) se detectó al integrar y se corrigió antes de pushear.
- Un brief mío al worker del 18 atribuía al usuario la decisión de `SCHEDULE_EXACT_ALARM`; se corrigió de inmediato.
- Worktrees: eliminados los de issues cerrados (ramas intactas). Quedan: el del 01 (ficheros sin commitear en `spikes/persistencia-web`, distintos de los de `main`; decide si se borra), el del 30 (bloqueado por su agente) y restos de worker-7 (archivo en uso).
- Se reutilizaron workers por debajo del 50 % de contexto (5→26, 6→13, 7→24, 4→19, 9→21).
