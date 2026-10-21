# 10 · Editar cualquier tarea y categorías con color

Status: claimed
Blocked by: 07
Cubre: 3MT-12 (edición), 22

## What to build

Tocar el texto de una tarea abre su edición completa desde Hoy (después también desde Pool, A futuro y Archivadas): título, nivel, categoría, hora, recordatorio, notas y fecha. Incluye CRUD de categorías.

## Acceptance criteria

- [ ] Formulario con todos los campos del PRD §6.2 y validaciones
- [ ] Cambiar nivel o fecha revalida el cupo
- [ ] Eliminar desde el formulario con confirmación
- [ ] El historial pasado es de solo lectura
- [ ] CRUD de categorías con color; borrar una en uso deja las tareas sin categoría
- [ ] Tests bUnit

## Blocked by

- 07-tachar-progreso-banner

## Comments
- pm-1: asignado a worker-4 (reutilizado, ~40 %; rama issue/10-editar-tareas-categorias). Comparte formulario con 06.
