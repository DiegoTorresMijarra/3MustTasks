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

## Pendientes
| # | Pregunta | Valor asumido |
|---|---|---|
| P1 | ¿"Recommended" del seed equivale a "Should"? | Sí. Niveles por defecto: Must / Should / Maybe |
| P2 | Cupos por defecto de Should y Maybe | mín. 0, máx. 2 cada uno |
| P3 | "N tareas": ¿hay un máximo **total** diario además del de cada nivel? | No, solo el máximo por nivel |
| P4 | Hora de inicio del día lógico | 00:00 (configurable) |
| P5 | ¿Las tareas a futuro se añaden solas al plan el día que toca? | Sí, si caben en el cupo; si no, se avisa |
| P6 | ¿Una recurrente que supera el cupo va al pool? | Sí, con aviso |
| P7 | ¿Las archivadas se borran solas alguna vez? | No |
| P8 | ¿Incluir VTODO al exportar `.ics`? | Off por defecto (Google Calendar los ignora) |
| P9 | ¿Qué métricas mostrar? | % de cumplimiento diario y racha de días con todos los Must |
| P10 | Idioma | Español en v1, preparado para inglés |
| P11 | Distribución en Android | APK firmado primero; Play Store después |
| P12 | Nombre del paquete | `com.threemusttasks.app` |
| P13 | ¿Tachar una tarea a futuro o del pool directamente (sin pasar por el plan)? | Sí: crea una asignación del día actual marcada como completada |
| P14 | ¿Se puede editar el plan de días pasados? | No; solo consultar (el historial es inmutable) |
