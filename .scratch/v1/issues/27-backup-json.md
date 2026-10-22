# 27 · Backup JSON versionado

Status: in-review
Blocked by: 08
Cubre: 3MT-40, 41

## What to build

Exportar toda la información a JSON con `schemaVersion` y restaurarla reemplazando o fusionando por Id; sirve para mover datos entre Android y la PWA.

## Acceptance criteria

- [x] Exportar configuración, tareas, asignaciones e historial
- [x] Restaurar con confirmación, en modo reemplazar o fusionar
- [ ] Ida y vuelta Android ↔ PWA verificada (parcial: solo por tests de serialización; falta prueba en dispositivo/navegador real, ver Comments)
- [x] Fixture de backup v1 que sigue restaurándose tras cambios de esquema

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-3 (2026-10-22): asignado a worker-3. Rama issue/27-backup-json.
- worker-3 (entrega a pm-3):
  - Hecho: `BackupService` (Core/Services; export, `Parse`, `Summarize`, `RestoreAsync` Replace/Merge atómico con `ExecuteInTransactionAsync`), formato v1 camelCase, enums como texto, fechas ISO, sin dependencia de cultura. UI: `Components/BackupPanel.razor` (+ `wwwroot/backup.js`) montado con una línea en `SettingsPage`. Fixture `tests/ThreeMustTasks.Infrastructure.Tests/Fixtures/backup-v1.json` escrito a mano (no generado por el código).
  - Criterio "Ida y vuelta Android ↔ PWA" **NO marcado**. Verificado solo por tests: SQLite, IndexedDB (con el almacén falso de los tests) y memoria exportan JSON idéntico byte a byte, y un backup de SQLite se restaura en la implementación del navegador y vuelve igual. **No verificado en dispositivo/navegador real**: descarga del `Blob` y selector de archivo en el WebView de Android (MAUI; las descargas por blob/`<a download>` pueden no funcionar y quizá haga falta Share/FilePicker nativo), ni en la PWA real (IndexedDB real, `InputFile` real). Falta prueba manual en ambos.
  - Desviación de la spec: `BackupService` vive en Core, no en Infrastructure.Shared (§1), para que la UI (solo referencia Core) lo use sin depender de infraestructura. Mantener o mover lo decide el PM.
  - Puertos ampliados (3 implementaciones + contratos): `IDayRepository.ListAssignmentsAsync/ListLogsAsync/DeleteLogAsync` y `IConfigRepository.DeleteFavoriteAsync` (cubre el pendiente DeleteFavorite).
  - Semántica: Reemplazar borra todo y aplica también los ajustes del backup; Fusionar hace upsert por Id (logs por fecha) y conserva los ajustes locales. Los ajustes se guardan fuera de la transacción (puerto síncrono), al final.
  - Limitaciones: tras restaurar, `ThemeService` no notifica el cambio de tema hasta el siguiente render/recarga; no se reprograman recordatorios (aún no existe ReminderPlanner). No se ha ejecutado `tests/browser` (e2e node).
