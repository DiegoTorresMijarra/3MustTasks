# 17 · Onboarding del primer arranque

Status: in-review
Blocked by: 15
Cubre: 3MT-27

## What to build

Tres pasos en el primer arranque: concepto, cupos (aceptar 3 Must o ajustar) y hora de planificación con permiso de notificaciones.

## Acceptance criteria

- [x] Solo aparece la primera vez y se puede repetir desde Ajustes
- [x] En la PWA avisa de las limitaciones de las notificaciones
- [x] Respeta el mock de Onboarding

## Blocked by

- 15-ajustes-niveles-cupos

## Comments

### worker-2 (entrega a pm-3)
- Rama `issue/17-onboarding`. Hecho: `OnboardingService` (Core), página `/onboarding` (3 pasos), `AppRoot` (redirige al onboarding solo en el arranque si está pendiente), enlace "Repetir la introducción" en Ajustes, `AppSettings.OnboardingCompleted` y `PlanningReminderTime` (07:30) con migración SQLite `AddOnboardingSettings` (escrita a mano: no hay `dotnet-ef` en el entorno; `HasPendingModelChanges` sigue en verde).
- Puerto nuevo `INotificationPermission` (`OnlyWorksWhileOpen`, `RequestAsync`). Implementaciones: `WebNotificationPermission` (PWA, JS `Notification.requestPermission`) y `NoOpNotificationPermission` por defecto (Android hasta el spike 02: devuelve `Unsupported`). La spec §2.2 lo tiene como `INotificationScheduler.RequestPermissionAsync`; lo separé a la espera del 02. Hay que decidir si el 02 lo absorbe.
- Cambio de host: web y MAUI arrancan con `AppRoot` (envuelve `Routes`) en vez de `Routes`; actualizado `AndroidHostTests`. Motivo: no romper los tests que renderizan `Routes` con ajustes por defecto.
- Ambigüedades resueltas por mi cuenta: (1) el mock solo muestra el paso 2; el 1 (concepto) y el 3 (hora + permiso) siguen el mismo estilo. (2) Presets: Equilibrado 1 Must + 2 Recommended, Exigente 2+1, Bestia 3 Must (Recommended 0); Must con mín. completar = mín. añadir, Recommended solo añadir. (3) "Personalizado" no aplica nada y al terminar lleva a Ajustes > Niveles. (4) "Saltar" cuenta como hecho sin cambios. (5) El permiso se pide con el botón del paso 3 y, si no se había pedido, también al pulsar "Empezar". (6) Instalaciones existentes con datos verán el onboarding una vez (flag por defecto false).
- Fuera: aviso de limitaciones PWA en Ajustes (spec §5.2) y ajuste de la hora de planificación en Ajustes: no los pide este issue.
