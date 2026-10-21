# 10 · Editar cualquier tarea y categorías con color

Status: resolved
Blocked by: 07
Cubre: 3MT-12 (edición), 22

## What to build

Tocar el texto de una tarea abre su edición completa desde Hoy (después también desde Pool, A futuro y Archivadas): título, nivel, categoría, hora, recordatorio, notas y fecha. Incluye CRUD de categorías.

## Acceptance criteria

- [x] Formulario con todos los campos del PRD §6.2 y validaciones
- [x] Cambiar nivel o fecha revalida el cupo
- [x] Eliminar desde el formulario con confirmación
- [x] El historial pasado es de solo lectura
- [x] CRUD de categorías con color; borrar una en uso deja las tareas sin categoría
- [x] Tests bUnit

## Blocked by

- 07-tachar-progreso-banner

## Comments
- pm-1: asignado a worker-4 (reutilizado, ~40 %; rama issue/10-editar-tareas-categorias). Comparte formulario con 06.
- worker-4 (entrega), notas para pm-1:
  - **Alcance del formulario**: `TaskForm` tiene dos modos. Nueva tarea (06) sigue con título y nivel; edición (`/tarea/{id}`) añade categoría, cuándo (Hoy / Otro día + fecha / Al pool), hora, recordatorio (a la hora, 5, 15, 30, 60 min), notas y eliminar. Fijar, repetir y favorita no están (issues 09, 16, 14). Propuesta: llevar los mismos campos al alta cuando se necesite (14/16); no lo hice por no ampliar `NewTaskRequest`/`AddAsync`.
  - **Solo lectura**: se considera historial pasado una tarea `Completed` sin asignación hoy, y también las `Archived` (cuando 12 las haga editables habrá que relajar esa regla en `TaskEditService.GetAsync`). Se muestra un resumen sin formulario.
  - Completada hoy: se puede editar, pero no moverla a otro día ni al pool (`CannotMoveCompleted`); hay que destacharla antes.
  - **Eliminar**: borra la tarea y su asignación de hoy. Las asignaciones de días pasados quedan huérfanas, así que el historial (22) debe ignorar las que apunten a una tarea inexistente. Confirmación inline en el formulario (sin diálogo modal).
  - **Categorías**: `/ajustes/categorias` (enlazada desde Ajustes y desde el formulario). Nombre único sin distinguir mayúsculas, máx. 30, color `#RRGGBB`. Borrar una pone a null la categoría de tareas, favoritas y recurrencias que la usaban.
  - Validaciones de edición: título obligatorio y máx. 120, notas máx. 1000, recordatorio solo con hora, fecha posterior a hoy. Cambiar nivel o pasar a Hoy revalida el cupo (excluyendo a la propia tarea); si está lleno se ofrece "Mandar al pool".
  - Pendiente para otros issues: abrir la edición desde Pool, A futuro y Archivadas (11/12/13) enlazando a `tarea/{id}`; el "+ Nueva" categoría inline del mock se sustituyó por el enlace "Gestionar categorías".
- pm-2: revisado e integrado en main (merge de issue/10-editar-tareas-categorias): criterios contra código/tests OK; build -warnaserror 0 avisos; tests 85+51+59 en verde. Resolved.
