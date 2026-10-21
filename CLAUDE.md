# 3MustTasks

Ver [README.md](README.md) y `docs/` para producto, spec técnica y backlog.

## Cómo trabajar en este proyecto

Antes de planificar o implementar nada, lee:

- `docs/workflow.md`: roles (`pm-[n]` revisa y asigna, `worker-[n]` implementa), concurrencia, reutilización de agentes y límite de cuota. Se aplica siempre salvo que el usuario diga lo contrario.
- `docs/waves.md`: olas de issues que se pueden paralelizar. El estado real de cada issue está en su línea `Status:` de `.scratch/v1/issues/`.

## Agent skills

### Issue tracker

Los issues viven como markdown local en `.scratch/<feature>/` (sin GitHub Issues ni PRs externos). Ver `docs/agents/issue-tracker.md`.

### Triage labels

Vocabulario por defecto: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`, guardado en la línea `Status:` de cada issue. Ver `docs/agents/triage-labels.md`.

### Domain docs

Single-context: un `CONTEXT.md` y `docs/adr/` en la raíz. Ver `docs/agents/domain.md`.
