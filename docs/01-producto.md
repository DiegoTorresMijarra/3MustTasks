# 3MustTasks — Definición de producto (PRD)

> Estado: borrador v0.1 · Fuente: seed inicial · Las decisiones marcadas con **[D]** son propuestas mías a validar; las dudas abiertas están en [03-preguntas-abiertas.md](03-preguntas-abiertas.md).

## 1. Visión

Una app móvil de **foco diario**: cada día te pide que elijas un número pequeño de tareas importantes (por defecto **3 "Must"**), te recuerda que las hagas y no deja que lo no hecho desaparezca: lo acumula en un *pool* del que puedes rescatarlo otro día o archivarlo conscientemente.

No es un gestor de tareas genérico. La gracia es la **restricción**: pocas tareas, clasificadas por importancia, con un mínimo obligatorio.

## 2. Usuario objetivo

- Uso personal, una persona por dispositivo (v1 sin cuentas ni colaboración). ✅ Decidido.
- Alguien que se dispersa con listas largas y quiere un ritual diario: *planifica por la mañana → ejecuta → revisa*.

## 3. Glosario

| Término | Definición |
|---|---|
| **Tarea** | Algo a hacer: título, nivel, categoría, notas opcionales, hora/recordatorio opcional. |
| **Nivel** (antes "tipo") | Importancia de la tarea. Por defecto: **Must**, **Should**, **Maybe**. Cada nivel tiene un cupo diario (mín./máx.). Personalizable (nombre, color, cupos, orden). |
| **Categoría** | Etiqueta libre con color (Trabajo, Casa, Salud…). Independiente del nivel. **[D]** |
| **Plan del día** | Las tareas asignadas a un día concreto. |
| **Día lógico** | El "día" de la app. Por defecto empieza a las 00:00; configurable (p. ej. 04:00 para quien trasnocha). **[D]** |
| **Cierre de día** | Al empezar un día nuevo, las tareas no completadas del anterior pasan al pool. |
| **Pool** | Acumulado de tareas no completadas de días anteriores **más** las tareas planificadas a futuro / sin fecha. Se pueden recuperar o archivar. |
| **Archivada** | Tarea no completada que el usuario descarta del pool. No cuenta como completada ni sigue en el pool, pero sigue en el historial. |
| **Favorita** | Plantilla de tarea para añadirla rápido. |
| **Recurrente** | Tarea que se genera sola según una regla (diaria, días laborables, semanal, mensual). |
| **Historial** | Registro inmutable de qué se planificó cada día y qué pasó con cada tarea. |

## 4. Bucle principal

```
 07:30 🔔 "¡Añade tus tareas!"
   │
   ▼
 PLANIFICAR ── añadir nuevas / desde favoritas / recuperar del pool / recurrentes ya generadas
   │           (hasta cumplir el mínimo; si no, recordatorios insistentes)
   ▼
 EJECUTAR ──── tachar tareas · recordatorios a la hora fijada
   │
   ▼
 CIERRE (cambio de día lógico) ── no completadas → pool · el día queda en el historial
```

## 5. Reglas de negocio

### 5.1 Cupos por nivel
- Cada nivel tiene **mínimo** y **máximo** diarios.
- Valores por defecto **[D]**:

| Nivel | Color | Mín. | Máx. |
|---|---|---|---|
| Must | rojo | **3** | 3 |
| Should | ámbar | 0 | 2 |
| Maybe | azul | 0 | 2 |

- Ejemplo personalizado del seed: *2 Must + 1 Should ("recommended")*.
- Se pueden renombrar, recolorear, reordenar, añadir y desactivar niveles. Siempre tiene que quedar al menos uno activo.
- No se pueden añadir más tareas de las que permite el máximo de un nivel. El usuario puede desactivar el máximo (= ilimitado).

### 5.2 Mínimo obligatorio ("forzar")
- El plan está **incompleto** mientras no se cumpla el mínimo de algún nivel.
- Con el plan incompleto (✅ decidido):
  1. Un banner persistente en Inicio indica lo que falta ("Te faltan 2 Must").
  2. Recordatorios repetidos cada *X* min (por defecto 30) desde la hora de planificación hasta cumplirlo, con un tope diario (por defecto 4).
  3. **Modo estricto** opcional (desactivado por defecto): la app no deja salir de la pantalla de planificación hasta cumplir el mínimo.

### 5.3 Ciclo de vida de una tarea

```
            ┌──────────── recuperar ────────────┐
            ▼                                   │
 [Nueva] → Planificada(día D) ── tachar ──→ Completada
            │        ▲  │
            │ quitar │  └── cierre de día ──→ En pool ── archivar ──→ Archivada
            ▼        │                          │  ▲
         En pool ────┘                          │  └── desarchivar
         (sin fecha / futura)                   └── eliminar (definitivo, con confirmación)
```

- **Tachar** es reversible el mismo día (destachar).
- Al **cerrar el día**, cada tarea no completada queda registrada en el historial de ese día como *"no completada"* y vuelve al pool, conservando cuántas veces se ha arrastrado (`vecesPospuesta`).
- **Recuperar** asigna una tarea del pool al día actual, respetando los cupos.
- **Archivar** la saca del pool sin marcarla como completada. Se puede consultar en "Archivadas" y desarchivar.
- **Pool a futuro**: una tarea con fecha futura espera en el pool. Ese día se ofrece para añadirla al plan (o se añade sola, configurable) y puede llevar su recordatorio.

### 5.4 Historial
- Un registro por día con cada tarea planificada y su resultado (completada / no completada / quitada del plan).
- Se muestran métricas sencillas: % de cumplimiento, racha de días con todos los Must completados.
- El historial **no se reescribe**: si recuperas una tarea y la completas otro día, el día original sigue mostrándola como no completada.

### 5.5 Recurrentes
- Reglas soportadas en v1: diaria, días laborables, días concretos de la semana, cada N días, mensual (día X).
- Al empezar el día se generan las instancias, que entran en el plan y cuentan para el cupo.
- Si una recurrente choca con el máximo de su nivel, se avisa y se deja en el pool en lugar de superar el cupo. **[D]**

### 5.6 Recordatorios y notificaciones
| Notificación | Por defecto | Configurable |
|---|---|---|
| "¡Añade tus tareas!" | 07:30, todos los días | hora, días de la semana, on/off |
| Insistencia por plan incompleto | cada 30 min, máx. 4 | intervalo, tope, on/off |
| Recordatorio de tarea con hora | a la hora de la tarea | antelación (0/5/15/30/60 min) |
| Resumen nocturno "Te quedan N" | 21:00, off | hora, on/off |
| Tarea a futuro llega a su día | al inicio del día | on/off |

### 5.7 Configuración (todo con valores por defecto)
- Niveles: nombre, color, mín., máx., orden y activo.
- Categorías: nombre y color.
- Horas: inicio del día lógico, recordatorio de planificación, resumen.
- Comportamiento: modo estricto, insistencia, si las tareas a futuro se añaden solas.
- Apariencia: tema claro/oscuro/sistema y primer día de la semana.
- Idioma: español (v1). Inglés más adelante.

## 6. Pantallas

1. **Inicio / Hoy**: las tareas del día agrupadas por nivel con huecos visibles ("Must 2/3"), tachar con un toque o deslizando, banner de mínimo y botón para añadir.
2. **Añadir/editar tarea**: título, nivel, categoría, hora y recordatorio, notas, recurrencia y fecha (hoy / futura / sin fecha → pool), además de marcar como favorita.
3. **Planificar**: selector rápido con pestañas *Nueva · Favoritas · Pool*.
4. **Pool**: lista filtrable (nivel, categoría, antigüedad, veces pospuesta), selección múltiple para *recuperar a hoy* o *archivar*. Con sección "Programadas a futuro".
5. **Archivadas**.
6. **Historial / Calendario** con vistas:
   - **Día**: detalle de un día pasado.
   - **Semana**: 7 columnas con las tareas y su estado.
   - **Mes**: cuadrícula con indicador de cumplimiento (anillo o color) por día.
7. **Ajustes**.
8. **Importar / Exportar**.
9. **Onboarding** (primer arranque): explica el concepto, deja elegir los cupos (o aceptar 3 Must) y la hora de planificación, y pide permiso de notificaciones.

## 7. Importar / Exportar (calendarios)

- Formato **iCalendar `.ics`** (RFC 5545), el que usan Google Calendar, Apple y Outlook. *(El seed decía "ico"; entiendo que se refiere a `.ics`.)*
- **Exportar**: rango de fechas → `.ics` que se comparte o guarda.
  - Tareas con hora → `VEVENT` con esa hora.
  - Tareas sin hora → `VEVENT` de día completo.
  - Además se incluye `VTODO` opcional para apps que lo soportan (Google Calendar ignora los VTODO).
  - Nivel y categoría van en `CATEGORIES`, y el estado en `X-3MT-STATUS`.
- **Importar** `.ics`:
  - Los eventos de hoy se proponen para el plan y los futuros van al pool a futuro.
  - Se muestra una previsualización para elegir qué importar y con qué nivel por defecto.
  - Los duplicados se detectan por `UID`.
- **Backup completo** en JSON (configuración + todas las tareas + historial) para migrar o restaurar. Es distinto del `.ics`, que pierde información. **[D]**
- *Fuera de v1*: sincronización bidireccional con la API de Google Calendar.

## 8. Alcance

### MVP (v1)
- Plan diario con niveles y cupos configurables, y mínimo obligatorio (banner + insistencia).
- Tachar tareas, cierre de día automático, pool, recuperar, archivar.
- Historial con vistas día, semana y mes.
- Notificaciones: planificación, insistencia, recordatorio de tarea.
- Categorías, favoritas y recurrentes básicas.
- Exportar e importar `.ics`, y backup JSON.
- **Android** (app nativa MAUI Blazor Hybrid) + **PWA web** (Blazor WebAssembly). ✅ Decidido.
- Todo en local, sin cuentas ni backend. ✅ Decidido.

### Después (v1.x / v2)
- iOS (el código de UI y dominio ya es compartido; requiere Mac y cuenta de Apple Developer).
- Modo estricto avanzado, widgets de pantalla de inicio, estadísticas detalladas.
- Sincronización en la nube / multi-dispositivo.
- Integración directa con la API de Google Calendar.
- Inglés.

### Fuera de alcance
- Colaboración, tareas compartidas, subtareas y proyectos.

## 9. Limitaciones de la PWA (asumidas)

| Tema | Android | PWA web |
|---|---|---|
| Notificaciones con la app cerrada | ✅ programadas en el sistema | ❌ no hay API estándar para programar notificaciones locales sin servidor (Notification Triggers se abandonó) |
| Notificaciones con la app abierta | ✅ | ✅ (Notification API + temporizadores) |
| Banner de mínimo / insistencia en pantalla | ✅ | ✅ |
| Almacenamiento | SQLite | IndexedDB (el navegador puede borrarlo si falta espacio → se pide `navigator.storage.persist()`) |
| Datos compartidos entre dispositivos | ❌ cada dispositivo tiene sus datos | ❌ |

- Para mover datos entre el móvil y la web se usa el **backup JSON** (exportar en uno, importar en otro).
- Si en el futuro se quieren recordatorios reales en la web o sincronización, hará falta un backend (Web Push + API). Queda fuera de v1.

## 10. Requisitos no funcionales
- **Offline total**: funciona sin conexión y los datos se guardan en el dispositivo.
- **Arranque** en frío < 2 s en un Android de gama media. **[D]**
- **Fiabilidad de recordatorios**: las notificaciones programadas salen aunque la app esté cerrada.
- **Integridad**: el cierre de día es idempotente; abrir la app tras varios días sin usarla procesa todos los días pendientes.
- **Privacidad**: sin telemetría en v1; los datos solo salen del dispositivo al exportar.
- **Accesibilidad**: contraste AA y el nivel no se distingue solo por color (también por icono o texto).
