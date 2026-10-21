# Olas de trabajo

Agrupa los 30 issues de [`.scratch/v1/issues/`](../.scratch/v1/issues/) en **olas**. Los issues de una misma ola no dependen entre sí y se pueden hacer en paralelo; una ola solo arranca cuando las dependencias de sus issues están `resolved` (no hace falta que toda la ola anterior haya terminado).

> **Revisión:** el PM revisa y marca `resolved` al terminar cada ola o bloque (ver [workflow.md](workflow.md)), así que en la práctica una ola siguiente arranca tras esa revisión.
>
> **Fuente de verdad:** la línea `Status:` y `Blocked by:` de cada issue. Este documento es una vista derivada de esas dependencias; si cambian, hay que regenerarlo. El flujo de ejecución está en [workflow.md](workflow.md).

Los números son los de fichero (`NN-slug.md`). 🧑 = `ready-for-human`: lo decide el usuario, el PM no lo asigna a un worker.

## Resumen

| Ola | Issues | Paralelismo | Qué se desbloquea |
|---|---|---|---|
| **0** | 01 🧑, 02 🧑, 03 | 3 | Esqueleto web y las dos decisiones técnicas |
| **1** | 04, 05, 06 | 3 | Android, persistencia web y primer recorrido de Hoy |
| **2** | 07, 30 | 2 | Tachar y deploy de la PWA |
| **3** | 08, 10, 15 | 3 | Cierre de día, edición y ajustes |
| **4** | 09, 11, 16, 17, 22, 27 | 6 | Fijadas, Pool, recurrentes, onboarding, historial, backup |
| **5** | 12, 13, 14, 23, 24, 25, 28 | 7 | Resto del pool, planificar, calendario, export y pulido |
| **6** | 18, 26 | 2 | Notificaciones Android e importación |
| **7** | 19, 20, 21, 29 | 4 | Resto de notificaciones y APK firmado |

Camino crítico (8 olas): `03 → 06 → 07 → 08 → 11 → 14 → 18 → 19/20/21/29`.

## Detalle

### Ola 0 · Fundaciones
| Issue | Bloqueado por | Notas |
|---|---|---|
| 01 spike-persistencia-web 🧑 | — | Su decisión fija el alcance de 05 y 30. Cuanto antes, mejor. |
| 02 spike-notificaciones-maui 🧑 | — | Solo bloquea a 18; puede esperar hasta la ola 5 sin afectar al camino crítico. |
| 03 esqueleto-web | — | **Prioridad.** Desbloquea toda la ola 1. |

### Ola 1
| Issue | Bloqueado por | Notas |
|---|---|---|
| 04 esqueleto-android | 03 | Necesita emulador Android. |
| 05 persistencia-web | 01, 03 | Espera la decisión del spike 01. |
| 06 anadir-tarea-hoy-por-nivel | 03 | **Camino crítico.** Define dominio y página Hoy. |

### Ola 2
| Issue | Bloqueado por | Notas |
|---|---|---|
| 07 tachar-progreso-banner | 06 | **Camino crítico.** Toca la página Hoy. |
| 30 despliegue-pwa | 05 | Independiente del resto; puede hacerse cuando haya hueco. |

### Ola 3
| Issue | Bloqueado por | Notas |
|---|---|---|
| 08 cierre-de-dia | 07 | **Camino crítico.** |
| 10 editar-tareas-categorias | 07 | Comparte formulario con 06; coordinar si 06 sigue abierto. |
| 15 ajustes-niveles-cupos | 07 | Toca la página de Ajustes y el modelo de niveles. |

### Ola 4
| Issue | Bloqueado por | Notas |
|---|---|---|
| 09 tareas-fijadas | 08 | Extiende el cierre de día. |
| 11 pool-ver-recuperar | 08 | **Camino crítico.** |
| 16 recurrentes | 08, 10 | También extiende el cierre de día: **no en paralelo con 09** si ambos tocan el mismo servicio. |
| 17 onboarding | 15 | |
| 22 historial-dia-metricas | 08 | |
| 27 backup-json | 08 | |

### Ola 5
| Issue | Bloqueado por | Notas |
|---|---|---|
| 12 archivar-eliminar | 11 | Misma pantalla Pool que 13: coordinar. |
| 13 tareas-a-futuro | 11 | |
| 14 planificar-favoritas | 11 | **Camino crítico** (es el destino del deep link de 18). |
| 23 vista-semana | 22 | |
| 24 vista-mes | 22 | |
| 25 exportar-ics | 22 | |
| 28 pulido-identidad-a11y-rendimiento | 07, 11 | Mejor al final de la ola: toca estilos de muchas pantallas. |

### Ola 6
| Issue | Bloqueado por | Notas |
|---|---|---|
| 18 recordatorio-planificacion-android | 02, 04, 14 | **Camino crítico.** Necesita la decisión del spike 02. |
| 26 importar-ics | 11, 25 | |

### Ola 7
| Issue | Bloqueado por | Notas |
|---|---|---|
| 19 insistencia-aviso-completar | 18 | |
| 20 recordatorio-tarea-hora | 18 | |
| 21 notificaciones-pwa | 05, 18 | |
| 29 apk-firmado | 18, 28 | |

## Riesgos de paralelizar
- **Mismos ficheros:** 09/16 (cierre de día), 12/13 (pantalla Pool) y 06/10 (formulario de tarea). El PM no los lanza a la vez; usa el orden de la tabla.
- **Decisiones HITL:** 01 y 02 pueden retrasar 05, 30 y 18. Pedir esas decisiones al usuario nada más empezar.
- **Cuello de botella:** las olas 0 a 3 tienen poco paralelismo real (2 a 3 issues y la mayoría en el camino crítico). Las olas 4 y 5 son las anchas, pero el límite de 2 a 3 workers simultáneos manda.
