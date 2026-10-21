# 07 · Tachar tareas, progreso por nivel y banner de mínimos

Status: claimed
Blocked by: 06
Cubre: 3MT-10, 13, parte de 48

## What to build

El usuario tacha y destacha tareas con un toque, ve el progreso 'completadas X/mín.' por nivel y un banner mientras falte el mínimo a añadir.

## Acceptance criteria

- [ ] Casos de uso: completar, destachar, quitar de hoy (va al pool) y reordenar
- [ ] Se registra `PlanMinimumMetAt` y `CompletionMinimumMetAt` la primera vez que se cumplen
- [ ] Se publica el evento `PlanChanged`
- [ ] Banner 'Te falta añadir…' que abre Planificar y desaparece al cumplir
- [ ] Tests de dominio y bUnit

## Blocked by

- 06-anadir-tarea-hoy-por-nivel

## Comments

- pm-1: asignado a worker-4 (reutilizado, ~25 % de contexto), rama issue/07-tachar-progreso-banner. Camino crítico.
