# 15 · Ajustes: niveles, cupos y opciones generales

Status: claimed
Blocked by: 07
Cubre: 3MT-21, 26

## What to build

El usuario personaliza niveles (nombre, color, icono, mínimos, máximo, orden, activo) y ajustes generales (inicio del día lógico, tema, primer día de la semana, modo estricto, auto-añadir a futuro, VTODO).

## Acceptance criteria

- [ ] CRUD de niveles; siempre queda al menos uno activo; mín. completar ≤ mín. añadir ≤ máx.
- [ ] Reordenar arrastrando
- [ ] Botón 'restaurar valores por defecto'
- [ ] Ajustes generales persistidos con los valores por defecto del PRD §5.7
- [ ] Tests de dominio y bUnit

## Blocked by

- 07-tachar-progreso-banner

## Comments
- pm-2: asignado a worker-8 (nuevo; rama issue/15-ajustes-niveles-cupos). Toca Ajustes y modelo de niveles; el 10 ya añadió el enlace a categorías en SettingsPage.
