using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Función pura (spec §4.5): ajustes + ahora → notificaciones que deben estar programadas en una ventana móvil de
/// <see cref="WindowDays"/> días lógicos (hoy y los 6 siguientes). Sin dependencias de plataforma ni de tiempo real.
/// Hoy solo calcula el recordatorio de planificación; insistencia, tarea con hora y resumen se añaden con sus issues.
/// </summary>
public static class ReminderPlanner
{
    public const int WindowDays = 7;
    public const string PlanReminderTitle = "¡Añade tus tareas!";
    public const string PlanReminderBody = "Planifica tu día en 3MustTasks.";
    public const string PlanRoute = "planificar";

    /// <summary>Un ID por tipo y día: <c>tipo × 10 000 000 + DayNumber</c>. Estable entre ejecuciones, así reemplazar es idempotente.</summary>
    public static int PlanReminderId(DateOnly logicalDate) => (int)NotificationKind.PlanReminder * 10_000_000 + logicalDate.DayNumber;

    /// <param name="settings">Ajustes vigentes.</param>
    /// <param name="now">Hora de pared local actual.</param>
    /// <param name="planMinimumMetToday">Si hoy ya se alcanzó el mínimo a añadir: el recordatorio de hoy sobra.</param>
    public static IReadOnlyList<PlannedNotification> Plan(AppSettings settings, DateTime now, bool planMinimumMetToday)
    {
        var result = new List<PlannedNotification>();
        if (!settings.PlanReminderEnabled) return result;

        var today = DateOnly.FromDateTime(now - settings.DayStartsAt.ToTimeSpan());
        // Una hora anterior al inicio del día lógico pertenece al día lógico anterior: suena al día natural siguiente.
        var nextCalendarDay = settings.PlanningReminderTime < settings.DayStartsAt ? 1 : 0;

        for (var i = 0; i < WindowDays; i++)
        {
            var logical = today.AddDays(i);
            if (!settings.PlanReminderOnDay(logical.DayOfWeek)) continue;
            if (i == 0 && planMinimumMetToday) continue;

            var at = logical.AddDays(nextCalendarDay).ToDateTime(settings.PlanningReminderTime);
            if (at <= now) continue;

            result.Add(new PlannedNotification(
                PlanReminderId(logical), NotificationKind.PlanReminder, at,
                PlanReminderTitle, PlanReminderBody, PlanRoute, logical));
        }
        return result;
    }
}
