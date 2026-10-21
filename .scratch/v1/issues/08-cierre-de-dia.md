# 08 · Cierre de día idempotente: lo no hecho pasa al pool

Status: claimed
Blocked by: 07
Cubre: 3MT-14, 48

## What to build

Al abrir la app o volver a primer plano, los días pendientes se cierran: las tareas no completadas pasan al pool sumando `TimesPostponed`, el día queda en el historial como cumplido o no cumplido y el historial no se reescribe.

## Acceptance criteria

- [ ] Algoritmo de la spec §4.1 en una sola transacción
- [ ] Se invoca al iniciar y al reanudar en ambos hosts
- [ ] Tests: un día, varios días saltados, idempotencia, inicio de día distinto de 00:00 y día sin plan
- [ ] `DayLog.Fulfilled` se calcula al cerrar; la racha cuenta solo días cumplidos
- [ ] Asunción a confirmar: pasar un día sin completar suma `TimesPostponed` también a las fijadas

## Blocked by

- 07-tachar-progreso-banner

## Comments
- pm-1: asignado a worker-6 (rama issue/08-cierre-de-dia). Camino crítico.
