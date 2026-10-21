# Flujo de trabajo

Directrices para trabajar en este proyecto con varios agentes. Son las vigentes por ahora y **se aplican siempre, salvo que el usuario diga lo contrario**.

Documentos relacionados: [waves.md](waves.md) (qué issues se pueden hacer a la vez) · [docs/agents/issue-tracker.md](agents/issue-tracker.md) (formato de los issues) · [04-backlog.md](04-backlog.md) y [02-spec-tecnica.md](02-spec-tecnica.md) (contexto).

## Roles

| Rol | Nombre | Qué hace |
|---|---|---|
| **Reviewer / coordinador** | `pm-[n]` | Elige qué issues toca, los asigna a workers, revisa lo entregado y decide si se acepta. No implementa. |
| **Implementador** | `worker-[n]` | Hace un issue de principio a fin (código, tests, criterios de aceptación) y entrega para revisión. |
| **Usuario** | — | Decide los issues `ready-for-human` (los spikes), puede cambiar estas reglas y es quien tiene la última palabra. |

`[n]` es un contador que solo crece: `pm-1`, `pm-2`… y `worker-1`, `worker-2`… No se reutiliza un número.

## Fuente de verdad
- El estado de cada issue es su línea `Status:` en `.scratch/v1/issues/NN-slug.md`. Las olas de [waves.md](waves.md) se derivan de los `Blocked by:`.
- Un issue está **desbloqueado** cuando todos los issues de su `Blocked by:` están `resolved`.
- Para no meter vocabulario nuevo, los estados de trabajo reutilizan los del tracker local:

| `Status:` | Significado | Lo cambia |
|---|---|---|
| `ready-for-agent` | Listo, nadie lo ha cogido | — |
| `ready-for-human` | Lo decide o hace el usuario; el PM no lo asigna | — |
| `claimed` | Un worker lo está haciendo (se anota quién en `## Comments`) | PM, al asignar |
| `in-review` | El worker ha entregado y espera revisión | worker, al entregar |
| `resolved` | Revisado (al cerrar la ola o el bloque) y aceptado | PM |

Si la revisión del bloque pide cambios, el PM devuelve el issue a `claimed` con los comentarios en `## Comments`.

## Ciclo de un issue

1. **Selección.** `pm-[n]` lista los issues `ready-for-agent` desbloqueados, sigue el orden de [waves.md](waves.md) (primero el camino crítico) y evita lanzar a la vez dos issues marcados como conflictivos.
2. **Asignación.** Pone `Status: claimed`, anota en `## Comments` el worker asignado y le manda el issue. El brief al worker incluye:
   - la ruta del issue y que lea [CLAUDE.md](../CLAUDE.md), el PRD y la spec técnica relevantes;
   - los issues de los que depende y qué ofrecen ya hechos;
   - la rama a usar y que debe trabajar en un worktree propio.
3. **Implementación.** `worker-[n]` crea la rama `issue/NN-slug` desde la rama de integración, implementa, escribe los tests y marca los criterios de aceptación (`- [x]`). Si algo del issue es ambiguo o se contradice con la spec, lo anota en `## Comments` y se lo pregunta al PM; no improvisa.
4. **Entrega.** El worker deja build y tests en verde, hace commit, pone `Status: in-review` y responde al PM con: resumen de lo hecho, criterios cumplidos, tests ejecutados, cosas que quedaron fuera o dudas, y **una estimación de su ocupación de contexto**.
5. **Revisión por bloques.** El PM **no revisa issue a issue** ni supervisa a los workers mientras trabajan. Al recibir una entrega solo toma nota (el issue queda `in-review`) y asigna el siguiente si hay plaza. La revisión se hace **al terminar la ola** o al cerrar un **bloque** que el PM haya fijado de antemano (p. ej. 2 o 3 issues relacionados, o los que desbloquean el camino crítico). En la revisión, `pm-[n]` comprueba, como mínimo:
   - cada criterio de aceptación contra el código y los tests (no solo contra lo que dice el worker);
   - que compila y que los tests pasan, también con las ramas del bloque integradas entre sí;
   - que respeta el PRD, la spec técnica y los ADR, y que no introduce alcance extra.
   
   Si todo está bien: integra las ramas, pone `Status: resolved` a los issues del bloque y reevalúa qué issues se han desbloqueado. Si no: devuelve ese issue al worker con comentarios concretos. La revisión es proporcionada: se centra en criterios, tests y encaje con la spec, no en estilo línea a línea.
6. **Siguiente.** Los issues de la ola siguiente se lanzan cuando sus bloqueantes están `resolved`, es decir, tras la revisión del bloque o ola que los contiene. Si esperar al final de la ola frenaría el camino crítico, el PM cierra antes un bloque más pequeño. Después sigue con el siguiente issue disponible según las reglas de concurrencia y de cuota.

## Concurrencia
- **Por defecto, 2 o 3 workers a la vez** (2 si los issues tocan zonas parecidas del código).
- Se tiene en cuenta que **las sesiones no acumulen mucho contexto**: preferir issues pequeños y bien acotados, y que cada worker no cargue más de lo necesario.
- Los workers trabajan en worktrees separados para no pisarse.
- Los issues `ready-for-human` no cuentan como plazas de worker: el PM se los pide al usuario cuanto antes, sobre todo 01 y 02, porque bloquean a otros.

## Reutilización de agentes
- Un worker se puede **reutilizar para otro issue** (continuando su sesión) solo si su contexto **no supera el 50 %**.
- Con más del 50 %, se da por terminado y se spawnea uno nuevo (`worker-[n+1]`) con un brief limpio.
- El worker informa de su ocupación al entregar. Si no puede medirla con precisión, informa una estimación honesta y el PM, ante la duda, no lo reutiliza.
- Conviene reasignar a un worker un issue **del mismo ámbito** que el anterior (aprovecha lo que ya conoce).
- **Traspaso del PM.** Si `pm-[n]` alcanza el 50 % de contexto, **no corta a mitad de fase**: termina la ola o el bloque en curso (incluida su revisión) y entonces levanta a `pm-[n+1]`. Al spawnearlo le pasa un mensaje con:
  - el **estado actual**: qué issues están `resolved`, `in-review`, `claimed` o bloqueados, qué workers siguen vivos y su ocupación de contexto, y decisiones o incidencias abiertas;
  - los **siguientes pasos**: la próxima ola o bloque, qué issues lanzar y en qué orden, y qué esperar del usuario (spikes, decisiones);
  - el consumo de cuota que lleva la sesión.

  Deja además el `Status:` y los `## Comments` de los issues al día, para que `pm-[n+1]` pueda verificar el mensaje contra los ficheros. Tras el traspaso, `pm-[n]` no vuelve a asignar ni a revisar.

## Cuota de la sesión
- El PM vigila el uso de cuota de la sesión.
- **Al llegar al 70 % no se envían más tareas a workers.** Los que estén en curso terminan su issue actual y se revisa y cierra lo ya entregado.
- Después, el PM resume al usuario: qué quedó `resolved`, qué está `in-review` o `claimed`, y cuál es el siguiente issue disponible.
- Si el PM no puede leer la cuota, se la pide al usuario al empezar y cada vez que vaya a lanzar un nuevo lote.

## Reglas generales
- Un worker, un issue. No se mezclan issues en un mismo commit ni en una misma rama.
- Mensajes de commit en español, convencionales (`feat:`, `fix:`, `docs:`, `test:`, `chore:`), con la referencia al issue (p. ej. `feat(06): añade tarea a Hoy`).
- Código y namespaces con el prefijo `ThreeMustTasks` (ver [spec §1](02-spec-tecnica.md)).
- Las decisiones técnicas que cambien algo de la spec se registran como ADR en `docs/adr/` y se actualiza la spec.
- Un worker no cambia la spec, el PRD ni otros issues: lo propone al PM.
- No se hacen operaciones destructivas (borrado de ramas ajenas, `reset --hard`, force push) ni se matan procesos que no sean de este proyecto.
- Todo lo que decida el usuario prevalece sobre este documento.

## Antes de empezar
- La rama de integración es `main`. Hoy las specs y los issues viven en `claude/specs-y-mocks`: hay que **integrarla en `main` antes de que los workers creen sus ramas**.
- Arrancar por la **ola 0** de [waves.md](waves.md): 03 para el PM/worker y 01 y 02 para que decida el usuario.
