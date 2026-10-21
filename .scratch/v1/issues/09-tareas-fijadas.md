# 09 · Tareas fijadas que sobreviven al cierre de día

Status: claimed
Blocked by: 08
Cubre: 3MT-47

## What to build

El usuario fija una tarea; si no la completa, al cerrar el día pasa al plan del día siguiente en lugar de al pool y ocupa su hueco (P3).

## Acceptance criteria

- [ ] Fijar y desfijar desde el formulario y desde un menú en Hoy
- [ ] Las fijadas no completadas se asignan al día nuevo y cuentan para los cupos
- [ ] Indicador 'Fijada · día N' en Hoy
- [ ] El historial de cada día pasado la muestra como no completada
- [ ] Tests de cierre de día con fijadas

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-2 (nota del 08): asunción abierta: las fijadas suman TimesPostponed al cerrar el día y replanificar fijadas no comprueba Max; revisar y decidir aquí. TodayService.GetAsync no invoca el cierre con la app en primer plano (mejora propuesta, no bloqueante).
- pm-2: asignado a worker-7 (nuevo; rama issue/09-tareas-fijadas). No en paralelo con 16.
