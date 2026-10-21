# 22 · Historial: vista Día y métricas

Status: claimed
Blocked by: 08
Cubre: 3MT-34, 35

## What to build

El usuario consulta un día pasado en solo lectura, navega con ←/→ y ve % de cumplimiento, racha actual y mejor racha.

## Acceptance criteria

- [ ] Resumen por día para un rango (planificadas, completadas, % y si se cumplió)
- [ ] Racha actual y mejor racha de días cumplidos
- [ ] Vista Día de solo lectura con el resultado de cada tarea
- [ ] Tests de dominio y bUnit

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-2 (nota del 10): borrar una tarea deja asignaciones huérfanas en días pasados; el historial debe ignorar las que apunten a una tarea inexistente.
- pm-2: asignado a worker-10 (nuevo; rama issue/22-historial-dia-metricas).
