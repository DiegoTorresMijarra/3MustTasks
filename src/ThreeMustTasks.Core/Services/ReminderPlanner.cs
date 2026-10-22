using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>Estado de hoy que necesita el planificador: qué mínimos están cumplidos y qué falta por completar (nombre del nivel, cuántas).</summary>
public sealed record DayProgress(bool PlanMinimumMet, bool CompletionMinimumMet, IReadOnlyList<(string Level, int Missing)> MissingToComplete)
{
    /// <summary>Todo cumplido: no hay insistencia ni aviso de la tarde.</summary>
    public static DayProgress AllMet { get; } = new(true, true, []);
}

/// <summary>
/// Función pura (spec §4.5): ajustes + ahora → notificaciones que deben estar programadas en una ventana móvil de
/// <see cref="WindowDays"/> días lógicos (hoy y los 6 siguientes). Sin dependencias de plataforma ni de tiempo real.
/// Planificación, insistencia (solo hoy, mientras falte el mínimo a añadir) y aviso de la tarde (solo hoy, mientras falte
/// el mínimo a completar). Tarea con hora, resumen y tarea a futuro llegan con sus issues.
/// </summary>
public static class ReminderPlanner
{
    public const int WindowDays = 7;
    /// <summary>Tope duro de insistencias al día (cabe en el esquema de IDs).</summary>
    public const int MaxNagsPerDay = 9;
    public const string PlanReminderTitle = "¡Añade tus tareas!";
    public const string PlanReminderBody = "Planifica tu día en 3MustTasks.";
    public const string NagTitle = "Te falta planificar tu día";
    public const string NagBody = "Añade las tareas que faltan para cumplir tu mínimo.";
    public const string CompletionTitle = "Aún te queda por completar";
    public const string PlanRoute = "planificar";
    public const string TodayRoute = "";

    /// <summary>Un ID por tipo y día: <c>tipo × 10 000 000 + DayNumber</c>. Estable entre ejecuciones, así reemplazar es idempotente.</summary>
    public static int PlanReminderId(DateOnly logicalDate) => (int)NotificationKind.PlanReminder * 10_000_000 + logicalDate.DayNumber;

    /// <summary>Insistencia nº <paramref name="index"/> (1..9) del día: <c>2 × 10 000 000 + DayNumber × 10 + índice</c>.</summary>
    public static int NagId(DateOnly logicalDate, int index) => (int)NotificationKind.Nag * 10_000_000 + logicalDate.DayNumber * 10 + index;

    public static int CompletionReminderId(DateOnly logicalDate) => (int)NotificationKind.CompletionReminder * 10_000_000 + logicalDate.DayNumber;

    /// <summary>Compatibilidad: solo el recordatorio de planificación (sin insistencia ni aviso de la tarde).</summary>
    public static IReadOnlyList<PlannedNotification> Plan(AppSettings settings, DateTime now, bool planMinimumMetToday) =>
        Plan(settings with { NagEnabled = false, CompletionReminderEnabled = false }, now,
            new DayProgress(planMinimumMetToday, CompletionMinimumMet: true, []));

    /// <param name="settings">Ajustes vigentes.</param>
    /// <param name="now">Hora de pared local actual.</param>
    /// <param name="today">Progreso de hoy: si el mínimo a añadir ya se cumplió, sobran el recordatorio de hoy y las insistencias.</param>
    public static IReadOnlyList<PlannedNotification> Plan(AppSettings settings, DateTime now, DayProgress today)
    {
        var result = new List<PlannedNotification>();

        var todayLogical = DateOnly.FromDateTime(now - settings.DayStartsAt.ToTimeSpan());
        // Una hora anterior al inicio del día lógico pertenece al día lógico anterior: suena al día natural siguiente.
        var planShift = settings.PlanningReminderTime < settings.DayStartsAt ? 1 : 0;

        if (settings.PlanReminderEnabled)
        {
            for (var i = 0; i < WindowDays; i++)
            {
                var logical = todayLogical.AddDays(i);
                if (!settings.PlanReminderOnDay(logical.DayOfWeek)) continue;
                if (i == 0 && today.PlanMinimumMet) continue;

                var at = logical.AddDays(planShift).ToDateTime(settings.PlanningReminderTime);
                if (at <= now) continue;

                result.Add(new PlannedNotification(
                    PlanReminderId(logical), NotificationKind.PlanReminder, at,
                    PlanReminderTitle, PlanReminderBody, PlanRoute, logical));
            }
        }

        AddNags(result, settings, now, todayLogical, planShift, today);
        AddCompletionReminder(result, settings, now, todayLogical, today);

        result.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Id.CompareTo(b.Id));
        return result;
    }

    // Insistencia: solo hoy, mientras falte el mínimo a añadir. Cuelga del recordatorio de planificación (activo y ese día
    // seleccionado). Rejilla fija desde la hora de planificación: k-ésima a hora + k × intervalo, k = 1..tope. Como la rejilla
    // no depende de "ahora", el tope diario se respeta aunque se recalcule varias veces. No cruza el fin del día lógico.
    private static void AddNags(
        List<PlannedNotification> result, AppSettings s, DateTime now, DateOnly todayLogical, int planShift, DayProgress today)
    {
        if (!s.NagEnabled || !s.PlanReminderEnabled || today.PlanMinimumMet || !s.PlanReminderOnDay(todayLogical.DayOfWeek)) return;

        var interval = TimeSpan.FromMinutes(Math.Max(1, s.NagIntervalMin));
        var cap = Math.Clamp(s.NagMaxPerDay, 0, MaxNagsPerDay);
        var anchor = todayLogical.AddDays(planShift).ToDateTime(s.PlanningReminderTime);
        var dayEnd = todayLogical.AddDays(1).ToDateTime(s.DayStartsAt);

        for (var k = 1; k <= cap; k++)
        {
            var at = anchor + k * interval;
            if (at >= dayEnd) break;
            if (at <= now) continue;
            result.Add(new PlannedNotification(
                NagId(todayLogical, k), NotificationKind.Nag, at, NagTitle, NagBody, PlanRoute, todayLogical));
        }
    }

    // Aviso de la tarde: solo hoy (de los demás días aún no se sabe qué faltará), mientras falte el mínimo a completar.
    private static void AddCompletionReminder(
        List<PlannedNotification> result, AppSettings s, DateTime now, DateOnly todayLogical, DayProgress today)
    {
        if (!s.CompletionReminderEnabled || today.CompletionMinimumMet) return;

        var shift = s.CompletionReminderTime < s.DayStartsAt ? 1 : 0;
        var at = todayLogical.AddDays(shift).ToDateTime(s.CompletionReminderTime);
        if (at <= now) return;

        result.Add(new PlannedNotification(
            CompletionReminderId(todayLogical), NotificationKind.CompletionReminder, at,
            CompletionTitle, CompletionBody(today.MissingToComplete), TodayRoute, todayLogical));
    }

    /// <summary>"Te queda 1 Must por completar" / "Te quedan 1 Must y 2 Recommended por completar".</summary>
    public static string CompletionBody(IReadOnlyList<(string Level, int Missing)> missing)
    {
        var parts = missing.Where(m => m.Missing > 0).Select(m => $"{m.Missing} {m.Level}").ToList();
        if (parts.Count == 0) return "Te quedan tareas por completar.";
        var total = missing.Where(m => m.Missing > 0).Sum(m => m.Missing);
        var list = parts.Count == 1 ? parts[0] : string.Join(", ", parts[..^1]) + " y " + parts[^1];
        return (total == 1 ? "Te queda " : "Te quedan ") + list + " por completar";
    }
}
