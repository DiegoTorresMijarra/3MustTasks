# 11 · Pool: ver pendientes y recuperarlas a hoy

Status: claimed
Blocked by: 08
Cubre: 3MT-16, 17

## What to build

El usuario abre el Pool, filtra y ordena las tareas no completadas, selecciona varias y las recupera al día de hoy respetando los cupos.

## Acceptance criteria

- [ ] `PoolService.Recover` valida cupos y devuelve los rechazados con motivo
- [ ] Filtros por nivel, categoría, antigüedad y veces pospuesta; orden por más antigua o más pospuesta
- [ ] Selección múltiple con acciones Recuperar a hoy y Archivar
- [ ] Indicador 'pospuesta N veces'
- [ ] Tests de dominio y bUnit

## Blocked by

- 08-cierre-de-dia

## Comments
- pm-1: asignado a worker-6 (reutilizado, ~25 %; rama issue/11-pool-ver-recuperar). Camino crítico.
