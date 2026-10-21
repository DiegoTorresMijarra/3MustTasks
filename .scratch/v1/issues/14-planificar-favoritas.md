# 14 · Planificar con selector rápido y favoritas

Status: resolved
Blocked by: 11
Cubre: 3MT-15, 23

## What to build

Pantalla Planificar con pestañas Nueva · Favoritas · Pool y cupos en tiempo real. El usuario marca tareas como favoritas y las añade con un toque.

## Acceptance criteria

- [x] Pestañas Nueva, Favoritas y Pool con los cupos actualizados al instante
- [x] Marcar favorita desde el formulario o una tarea existente
- [x] Favoritas ordenadas por uso
- [x] Es el destino de los deep links de notificaciones y del banner

## Blocked by

- 11-pool-ver-recuperar

## Comments
- pm-2: asignado a worker-9 (nuevo; rama issue/14-planificar-favoritas).
- pm-2: integrado en main (22, 14, 16) y revisado en bloque. Conflictos semánticos 14/16 resueltos a mano: NewTaskRequest (campos de ambos, Recurrence al final), TaskForm, EditTaskPage (favorita + recurrente) y la lógica de recurrencia de NewTaskPage portada a NewTaskPanel (con TodayDate/Error). build -warnaserror 0 avisos; tests Core 186, Infra 90, UI 116 en verde. Resolved.
- pm-2 (decisión 14): el FAB/huecos de Hoy siguen yendo a tarea/nueva; reevaluar con 28 (pulido). Quitar/editar favoritas queda sin issue: proponer al usuario. Notificaciones (18/20) deben abrir planificar.
