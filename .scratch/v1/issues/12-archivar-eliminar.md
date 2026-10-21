# 12 · Archivar, desarchivar y eliminar tareas

Status: claimed
Blocked by: 11
Cubre: 3MT-18

## What to build

El usuario archiva tareas del pool sin darlas por completadas, las consulta en Archivadas y puede desarchivarlas o eliminarlas definitivamente.

## Acceptance criteria

- [ ] Archivar saca la tarea del pool sin marcarla completada
- [ ] Pantalla Archivadas con desarchivar y eliminar (con confirmación)
- [ ] Las archivadas siguen en el historial
- [ ] Las archivadas no se borran solas (P7)

## Blocked by

- 11-pool-ver-recuperar

## Comments
- pm-2 (nota del 10): Archivadas debe enlazar a `tarea/{id}`; hoy `TaskEditService.GetAsync` trata las Archived como solo lectura: relajar esa regla aquí.
- pm-2: asignado a worker-12 (nuevo; rama issue/12-archivar-eliminar).
