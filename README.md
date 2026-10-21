# 3MustTasks

App de **foco diario**: cada día eliges unas pocas tareas importantes (por defecto **3 Must**), las tachas, y lo que no hagas pasa a un **pool** del que puedes recuperarlo o archivarlo.

- **Stack:** .NET 10 · Blazor · MudBlazor · MAUI Blazor Hybrid (Android) · Blazor WASM PWA (web)
- **Datos:** solo locales (SQLite / IndexedDB), sin cuentas
- **Estado:** fase de definición

## Documentos

| Doc | Contenido |
|---|---|
| [docs/01-producto.md](docs/01-producto.md) | PRD: visión, glosario, reglas de negocio, pantallas, alcance |
| [docs/02-spec-tecnica.md](docs/02-spec-tecnica.md) | Arquitectura, proyectos, modelo de datos, servicios, plataforma, calidad |
| [docs/03-preguntas-abiertas.md](docs/03-preguntas-abiertas.md) | Decisiones tomadas y preguntas con su valor asumido |
| [docs/04-backlog.md](docs/04-backlog.md) | 46 issues en 8 hitos con criterios de aceptación |

## Trazabilidad seed → specs

| Punto del seed | PRD | Issues |
|---|---|---|
| N tareas de M tipos, personalizable | §3, §5.1 | 08, 09, 21 |
| Mínimo para forzar al usuario | §5.2 | 09, 13, 28–33 |
| Tachar tareas | §5.3 | 10, 11 |
| Historial por día | §5.4 | 14, 34–37 |
| Pool acumulativo + consultar no completadas | §5.3 | 14, 16, 17 |
| Archivar (ni completada ni en pool) | §5.3 | 16, 18 |
| Recuperar X tareas del pool para hoy | §5.3 | 16, 17 |
| Recordatorio de tarea a una hora | §5.6 | 28, 29 |
| "7:30 ¡Añade tus tareas!" | §5.6 | 28, 29, 31 |
| Configurable: horas, categorías, colores, mín./máx. | §5.7 | 21, 22, 26, 31 |
| Favoritas | §3, §6 | 23 |
| Recurrentes | §5.5 | 24, 25 |
| Pool a futuro con recordatorio | §5.3, §5.6 | 12, 17, 19, 28 |
| Valores por defecto (3 Must, Must/Should/Maybe) | §5.1 | 08, 21 |
| Vistas calendario: diario en inicio, semanal… | §6 | 11, 35–37 |
| Import/export `.ics` (Google Calendar) | §7 | 38, 39, 40 |
| Móvil con .NET + Blazor | §8, spec §1 | 01, 07 |

## Próximos pasos
1. Revisar las [preguntas pendientes](docs/03-preguntas-abiertas.md#pendientes).
2. Subir este repo a GitHub y crear los issues desde el backlog.
3. Empezar por M0 (3MT-01 → 3MT-07).
