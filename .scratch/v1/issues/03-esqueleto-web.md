# 03 · Esqueleto andante: solución, CI y PWA con navegación vacía

Status: claimed
Blocked by: ninguno
Cubre: 3MT-01, 02, 03, parte de 04 y 07

## What to build

Solución .NET 10 con los proyectos de la spec, puertos de persistencia con una implementación en memoria, shell de UI con MudBlazor y navegación inferior (Hoy · Pool · Calendario · Ajustes) corriendo como PWA instalable. CI que compila, testea y publica la PWA como artefacto.

## Acceptance criteria

- [ ] `dotnet build` y `dotnet test` en verde con warnings como errores
- [ ] Shell con las cuatro pestañas vacías, tema claro/oscuro/sistema y textos en `.resx` (español)
- [ ] La PWA es instalable y arranca offline
- [ ] Puertos de persistencia con suite de contrato abstracta y su implementación en memoria
- [ ] Workflow de GitHub Actions en PRs y `main` con informe de cobertura

## Blocked by

None - can start immediately

## Comments

- pm-1: asignado a worker-1 (rama issue/03-esqueleto-web, worktree propio). 2026-10-21.
