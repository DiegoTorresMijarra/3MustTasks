# 19 · Insistencia por plan incompleto y aviso de la tarde

Status: claimed
Blocked by: 18
Cubre: 3MT-31, 33

## What to build

Mientras falte el mínimo a añadir, recordatorios cada X min con tope diario; por la tarde, aviso si falta el mínimo a completar. Ambos se cancelan al cumplirlos.

## Acceptance criteria

- [ ] Intervalo y tope configurables (por defecto 30 min y 4)
- [ ] Aviso de la tarde a las 18:00 configurable
- [ ] Test de integración: al añadir la última tarea requerida desaparecen las insistencias de hoy
- [ ] Modo estricto opcional que bloquea salir de Planificar

## Blocked by

- 18-recordatorio-planificacion-android

## Comments
- pm-3 (2026-10-22): asignado a worker-4 (reutilizado, ~30% de contexto; mismo ámbito). Rama issue/19-insistencia-aviso-completar.
