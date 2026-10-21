# 06 · Añadir una tarea a Hoy y verla agrupada por nivel

Status: claimed
Blocked by: 03
Cubre: 3MT-08, 09, parte de 10, 11 y 12

## What to build

Primer recorrido completo: el usuario abre Hoy, añade una tarea con título y nivel, y la ve agrupada bajo su nivel con los huecos de cupo visibles. Incluye entidades, niveles por defecto (Must 1/1, Recommended 2/0, Maybe 0/0), `QuotaPolicy`, cálculo del día lógico y formulario mínimo.

## Acceptance criteria

- [ ] Niveles por defecto sembrados según el PRD §5.1
- [ ] `QuotaPolicy` respeta máximos y ignora niveles desactivados (100 % de cobertura)
- [ ] `LogicalDayCalculator` con tests, incl. inicio de día 04:00 y cambio de horario
- [ ] Hoy agrupa por nivel con huecos vacíos visibles; el nivel se distingue también por icono o texto
- [ ] No se puede guardar en Hoy si el nivel está lleno; se ofrece mandarla al pool
- [ ] Tests de dominio y bUnit

## Blocked by

- 03-esqueleto-web

## Comments

- pm-1: asignado a worker-4 (rama issue/06-anadir-tarea-hoy-por-nivel). Camino crítico.
