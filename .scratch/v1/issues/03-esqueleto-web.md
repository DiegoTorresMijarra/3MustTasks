# 03 · Esqueleto andante: solución, CI y PWA con navegación vacía

Status: in-review
Blocked by: ninguno
Cubre: 3MT-01, 02, 03, parte de 04 y 07

## What to build

Solución .NET 10 con los proyectos de la spec, puertos de persistencia con una implementación en memoria, shell de UI con MudBlazor y navegación inferior (Hoy · Pool · Calendario · Ajustes) corriendo como PWA instalable. CI que compila, testea y publica la PWA como artefacto.

## Acceptance criteria

- [x] `dotnet build` y `dotnet test` en verde con warnings como errores
- [x] Shell con las cuatro pestañas vacías, tema claro/oscuro/sistema y textos en `.resx` (español)
- [x] La PWA es instalable y arranca offline
- [x] Puertos de persistencia con suite de contrato abstracta y su implementación en memoria
- [x] Workflow de GitHub Actions en PRs y `main` con informe de cobertura

## Blocked by

None - can start immediately

## Comments

- pm-1: asignado a worker-1 (rama issue/03-esqueleto-web, worktree propio). 2026-10-21.
- worker-1 (entrega, rama issue/03-esqueleto-web):
  - Proyectos creados: Core, Infrastructure.Shared, UI (RCL), App.Web y `tests/{Core,Infrastructure,UI}.Tests`. Sqlite, Browser y Maui quedan fuera (issues 04/05).
  - Duda/propuesta al PM: la spec no dice dónde vive la implementación en memoria de los puertos. La he puesto en `Infrastructure.Shared` (namespace `...Shared.InMemory`, `AddInMemoryPersistence()`), sin crear un proyecto nuevo. Si se prefiere un proyecto aparte, es un movimiento trivial.
  - Los puertos de Core usan métodos CRUD mínimos (no hay consultas de pool aún); las entidades siguen la spec §3. Cada issue posterior ampliará los puertos y la suite de contrato. El enum de estado se llama `TaskItemStatus` para no chocar con `System.Threading.Tasks.TaskStatus`.
  - "Arranca offline": verificado estáticamente (tests de manifest/service worker) y con `dotnet publish` (el `service-worker-assets.js` incluye index, manifest, iconos, MudBlazor y todo `_framework`). No se ha probado en un navegador real con la red cortada; conviene una comprobación manual o el E2E de Playwright (issue 28/30).
  - CI: `.github/workflows/ci.yml` (PRs y main): build con warnaserror, tests con cobertura (coverlet + ReportGenerator en el resumen del job y artefacto) y publicación de la PWA como artefacto. No se ha podido ejecutar en GitHub desde aquí.
  - Sin fuente Roboto externa (MudBlazor la pide a Google Fonts por defecto): se usa la tipografía del sistema para que funcione offline.
