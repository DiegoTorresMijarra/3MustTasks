# Preguntas abiertas

Ninguna bloquea el arranque: cada una tiene un **valor asumido** con el que se trabaja hasta que decidas otra cosa.

## Decididas
| # | Tema | Decisión |
|---|---|---|
| D1 | Dónde viven las specs | Archivos markdown en este repo |
| D2 | Plataformas v1 | Android + PWA web |
| D3 | Datos | Solo local, sin backend ni cuentas |
| D4 | Forzar el mínimo | Banner + insistencia; modo estricto opcional (off) |
| D5 | Formato de calendario | `.ics` (el seed decía "ico") |
| D6 | Niveles por defecto | Must, Recommended, Maybe; por defecto 1 Must + 2 Recommended (total 3) |
| D7 | Mínimos | Por nivel: mínimo a añadir y mínimo a completar |
| D8 | Fin de día | Lo no completado va al pool; las tareas **fijadas** se mantienen en el plan |
| D9 | Tipo vs. categoría | Distintos: tipo = nivel de exigencia; categoría = etiqueta con color |
| D10 | Edición | Todas las tareas son editables salvo el historial pasado |

## Pendientes
| # | Pregunta | Valor asumido |
|---|---|---|
| P1 | Mínimo a completar por defecto de Recommended | 0 (solo Must es obligatorio de completar) |
| P2 | ¿Máximo por nivel por defecto? | Sin máximo |
| P3 | ¿Una tarea fijada cuenta para el mínimo a añadir del día siguiente? | Sí, ocupa su hueco |
| P4 | Hora de inicio del día lógico | 00:00 (configurable) |
| P5 | ¿Las tareas a futuro se añaden solas al plan el día que toca? | Sí, si caben en el cupo; si no, se avisa |
| P6 | ¿Una recurrente que supera el cupo va al pool? | Sí, con aviso |
| P7 | ¿Las archivadas se borran solas alguna vez? | No |
| P8 | ¿Incluir VTODO al exportar `.ics`? | Off por defecto (Google Calendar los ignora) |
| P9 | ¿Qué métricas mostrar? | % de cumplimiento diario y racha de días cumplidos |
| P10 | Idioma | Español en v1, preparado para inglés |
| P11 | Distribución en Android | APK firmado primero; Play Store después |
| P12 | Nombre del paquete | `com.threemusttasks.app` |
| P13 | ¿Tachar una tarea a futuro o del pool directamente (sin pasar por el plan)? | Sí: crea una asignación del día actual marcada como completada |
| P14 | ¿Se puede editar el plan de días pasados? | No; solo consultar (el historial es inmutable) |
