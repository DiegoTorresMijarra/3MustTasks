# 12 · Archivar, desarchivar y eliminar tareas

Status: in-review
Blocked by: 11
Cubre: 3MT-18

## What to build

El usuario archiva tareas del pool sin darlas por completadas, las consulta en Archivadas y puede desarchivarlas o eliminarlas definitivamente.

## Acceptance criteria

- [x] Archivar saca la tarea del pool sin marcarla completada
- [x] Pantalla Archivadas con desarchivar y eliminar (con confirmación)
- [x] Las archivadas siguen en el historial
- [x] Las archivadas no se borran solas (P7)

## Blocked by

- 11-pool-ver-recuperar

## Comments
- pm-2 (nota del 10): Archivadas debe enlazar a `tarea/{id}`; hoy `TaskEditService.GetAsync` trata las Archived como solo lectura: relajar esa regla aquí.
- pm-3 (2026-10-22): asignado a worker-6. Rama issue/12-archivar-eliminar.
- worker-6 (2026-10-22): entregado. Decisiones: (1) Archivadas es la ruta `pool/archivadas` (enlace desde Pool; el mock lo plantea como pestaña, se deja para 13 si quiere unificar pestañas). (2) `PoolService.DeleteAsync` solo elimina Archived y borra todas sus asignaciones (evita huérfanas); favoritas y reglas recurrentes no referencian tareas, no se tocan, y una recurrente no regenera la instancia (`LastGeneratedDate`). (3) `TaskEditService`: las archivadas ya no son solo lectura; se editan en su sitio, siguen Archived, sin plan y conservan su fecha (se ignora "cuándo"; el selector de la TaskForm sigue visible pero no aplica; mejora de UI pendiente). (4) Desarchivar vuelve a Active conservando fecha: pool, o pool a futuro si la fecha es posterior a hoy. (5) Las archivadas no se tocan en el cierre de día (solo se tratan asignaciones Planned); test con 30 días. Eliminar solo se ofrece desde Archivadas.
