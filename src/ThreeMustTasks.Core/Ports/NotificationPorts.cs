namespace ThreeMustTasks.Core.Ports;

public enum NotificationKind
{
    /// <summary>"¡Añade tus tareas!" (PRD §5.6).</summary>
    PlanReminder = 1,
    /// <summary>Insistencia por plan incompleto (PRD §5.2).</summary>
    Nag = 2,
    /// <summary>Aviso de la tarde: falta el mínimo a completar (PRD §5.2).</summary>
    CompletionReminder = 3,
    /// <summary>Recordatorio de una tarea con hora, a su hora o con antelación (PRD §5.6).</summary>
    TaskReminder = 4,
    /// <summary>Resumen nocturno "Te quedan N" (PRD §5.6).</summary>
    EveningSummary = 5,
    /// <summary>Una tarea programada a futuro llega a su día (PRD §5.6).</summary>
    FutureTaskArrival = 6,
}

/// <summary>
/// Una notificación que debe quedar programada. <paramref name="At"/> es hora de pared local (Kind sin especificar):
/// lo que el usuario ve en el reloj. <paramref name="Route"/> es la ruta interna a abrir al tocarla (p. ej. <c>planificar</c>).
/// </summary>
public sealed record PlannedNotification(
    int Id, NotificationKind Kind, DateTime At, string Title, string Body, string Route, DateOnly LogicalDate);

/// <summary>Estado del sistema de notificaciones del dispositivo (para Ajustes y para decidir qué programar).</summary>
/// <param name="Supported">false si la plataforma no programa notificaciones (no-op, PWA por ahora).</param>
/// <param name="NotificationsEnabled">Permiso de notificaciones concedido (<c>POST_NOTIFICATIONS</c> en Android 13+).</param>
/// <param name="ExactAlarmsAllowed">Alarmas exactas concedidas; si no, los avisos se programan inexactos.</param>
public sealed record NotificationStatus(bool Supported, bool NotificationsEnabled, bool ExactAlarmsAllowed)
{
    public static NotificationStatus Unsupported { get; } = new(false, false, false);
}

public enum SystemSettingsTarget
{
    /// <summary>Ajustes de notificaciones de la app.</summary>
    Notifications,
    /// <summary>Ajuste "Alarmas y recordatorios" de la app (alarmas exactas).</summary>
    ExactAlarms,
    /// <summary>Optimización de batería del sistema.</summary>
    BatteryOptimization,
}

/// <summary>
/// Programa notificaciones locales en la plataforma (spec §2.2). El host (Android) lo implementa; el resto de la app
/// solo conoce este puerto. <see cref="ReplaceAllAsync"/> es idempotente: deja programadas exactamente las indicadas.
/// </summary>
public interface INotificationScheduler
{
    /// <summary>Deja programadas exactamente <paramref name="notifications"/> y cancela cualquier otra. Sin permiso, cancela y no programa.</summary>
    Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> notifications, CancellationToken ct = default);

    /// <summary>Pide el permiso de notificaciones al sistema. true si queda concedido.</summary>
    Task<bool> RequestPermissionAsync(CancellationToken ct = default);

    Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Abre la pantalla de ajustes del sistema indicada (si la plataforma la tiene).</summary>
    Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default);
}
