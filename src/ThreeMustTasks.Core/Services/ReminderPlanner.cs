using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>Estado de hoy que necesita el planificador: qué mínimos están cumplidos y qué falta por completar (nombre del nivel, cuántas).</summary>
/// <param name="PendingToday">Tareas del plan de hoy aún sin completar (para el resumen nocturno "Te quedan N").</param>
public sealed record DayProgress(
    bool PlanMinimumMet, bool CompletionMinimumMet, IReadOnlyList<(string Level, int Missing)> MissingToComplete, int PendingToday = 0)
{
    /// <summary>Todo cumplido: no hay insistencia ni aviso de la tarde.</summary>
    public static DayProgress AllMet { get; } = new(true, true, []);
}

/// <summary>
/// Una tarea activa con día lógico concreto dentro de la ventana (hoy, ya en el plan, o programada a futuro).
/// <paramref name="DueTime"/> y <paramref name="ReminderOffset"/> son los de la tarea; sin antelación no hay recordatorio de hora.
/// </summary>
public sealed record ReminderTask(Guid TaskId, string Title, DateOnly LogicalDate, TimeOnly? DueTime, TimeSpan? ReminderOffset);

/// <summary>
/// Función pura (spec §4.5): ajustes + ahora → notificaciones que deben estar programadas en una ventana móvil de
/// <see cref="WindowDays"/> días lógicos (hoy y los 6 siguientes). Sin dependencias de plataforma ni de tiempo real.
/// Planificación, insistencia (solo hoy, mientras falte el mínimo a añadir) y aviso de la tarde (solo hoy, mientras falte
/// el mínimo a completar). Además: recordatorio de tarea con hora, resumen nocturno y llegada de tareas a futuro (issue 20).
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

    // Rangos de IDs (sin colisión; DayNumber ≈ 740 000 en 2026):
    //   1×10M+día PlanReminder · 2×10M+día×10+i Nag · 3×10M+día Completion · 5×10M+día EveningSummary
    //   100M + día×100 + ranura  TaskReminder · 300M + día×100 + ranura  FutureTaskArrival (ranura 0..99)
    // Las dos últimas llevan 100 ranuras por día porque hay varias tareas por día; caben hasta el año 2700.
    public const int TaskReminderBase = 100_000_000;
    public const int FutureArrivalBase = 300_000_000;
    /// <summary>Tareas con recordatorio (o con llegada) por día y tipo; las demás se ignoran.</summary>
    public const int MaxTaskSlotsPerDay = 100;

    public static int EveningSummaryId(DateOnly logicalDate) => (int)NotificationKind.EveningSummary * 10_000_000 + logicalDate.DayNumber;
    public static int TaskReminderId(DateOnly logicalDate, int slot) => TaskReminderBase + logicalDate.DayNumber * 100 + slot;
    public static int FutureArrivalId(DateOnly logicalDate, int slot) => FutureArrivalBase + logicalDate.DayNumber * 100 + slot;

    public const string SummaryTitle = "Resumen del día";
    public const string FutureArrivalTitle = "Hoy toca una tarea";

    /// <summary>Compatibilidad: solo el recordatorio de planificación (sin insistencia ni aviso de la tarde).</summary>
    public static IReadOnlyList<PlannedNotification> Plan(AppSettings settings, DateTime now, bool planMinimumMetToday) =>
        Plan(settings with { NagEnabled = false, CompletionReminderEnabled = false }, now,
            new DayProgress(planMinimumMetToday, CompletionMinimumMet: true, []));

    /// <param name="settings">Ajustes vigentes.</param>
    /// <param name="now">Hora de pared local actual.</param>
    /// <param name="today">Progreso de hoy: si el mínimo a añadir ya se cumplió, sobran el recordatorio de hoy y las insistencias.</param>
    /// <param name="tasks">Tareas con día lógico dentro de la ventana (plan de hoy y programadas a futuro); null = ninguna.</param>
    public static IReadOnlyList<PlannedNotification> Plan(
        AppSettings settings, DateTime now, DayProgress today, IReadOnlyList<ReminderTask>? tasks = null)
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
        AddEveningSummary(result, settings, now, todayLogical, today);
        if (tasks is { Count: > 0 })
        {
            AddTaskReminders(result, settings, now, todayLogical, tasks);
            AddFutureArrivals(result, settings, now, todayLogical, planShift, tasks);
        }

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

    // Resumen nocturno: solo hoy, opcional, y solo si queda algo por completar.
    private static void AddEveningSummary(
        List<PlannedNotification> result, AppSettings s, DateTime now, DateOnly todayLogical, DayProgress today)
    {
        if (!s.EveningSummaryEnabled || today.PendingToday <= 0) return;

        var shift = s.EveningSummaryTime < s.DayStartsAt ? 1 : 0;
        var at = todayLogical.AddDays(shift).ToDateTime(s.EveningSummaryTime);
        if (at <= now) return;

        var n = today.PendingToday;
        result.Add(new PlannedNotification(
            EveningSummaryId(todayLogical), NotificationKind.EveningSummary, at,
            n == 1 ? "Te queda 1 tarea" : $"Te quedan {n} tareas", "Aún puedes cerrar el día con todo hecho.", TodayRoute, todayLogical));
    }

    // Tarea con hora: a su hora menos la antelación. Una hora anterior al inicio del día lógico cae en el día natural
    // siguiente. Sin antelación (null) no hay recordatorio. Las ranuras se asignan por día antes de descartar las pasadas,
    // así el ID de una tarea no cambia al avanzar el reloj.
    private static void AddTaskReminders(
        List<PlannedNotification> result, AppSettings s, DateTime now, DateOnly todayLogical, IReadOnlyList<ReminderTask> tasks)
    {
        foreach (var day in tasks
                     .Where(t => t.DueTime is not null && t.ReminderOffset is { } o && o >= TimeSpan.Zero && InWindow(t.LogicalDate, todayLogical))
                     .GroupBy(t => t.LogicalDate))
        {
            var slot = 0;
            foreach (var t in day.OrderBy(t => t.DueTime).ThenBy(t => t.TaskId).Take(MaxTaskSlotsPerDay))
            {
                var due = DueAt(t.LogicalDate, t.DueTime!.Value, s.DayStartsAt);
                var at = due - t.ReminderOffset!.Value;
                var id = TaskReminderId(day.Key, slot++);
                if (at <= now) continue;
                result.Add(new PlannedNotification(
                    id, NotificationKind.TaskReminder, at, t.Title, TaskReminderBody(due, t.ReminderOffset.Value), TodayRoute, day.Key));
            }
        }
    }

    // Tarea a futuro: aviso el día en que llega, a la hora de planificación (no a medianoche). Solo días posteriores a hoy:
    // lo de hoy ya está en el plan o en el pool.
    private static void AddFutureArrivals(
        List<PlannedNotification> result, AppSettings s, DateTime now, DateOnly todayLogical, int planShift, IReadOnlyList<ReminderTask> tasks)
    {
        if (!s.FutureTaskAlertEnabled) return;

        foreach (var day in tasks.Where(t => t.LogicalDate > todayLogical && InWindow(t.LogicalDate, todayLogical)).GroupBy(t => t.LogicalDate))
        {
            var at = day.Key.AddDays(planShift).ToDateTime(s.PlanningReminderTime);
            var slot = 0;
            foreach (var t in day.OrderBy(t => t.DueTime).ThenBy(t => t.TaskId).Take(MaxTaskSlotsPerDay))
            {
                var id = FutureArrivalId(day.Key, slot++);
                if (at <= now) continue;
                var body = t.DueTime is { } due ? $"{t.Title} · {due:HH:mm}" : t.Title;
                result.Add(new PlannedNotification(id, NotificationKind.FutureTaskArrival, at, FutureArrivalTitle, body, TodayRoute, day.Key));
            }
        }
    }

    private static bool InWindow(DateOnly date, DateOnly todayLogical) =>
        date >= todayLogical && date < todayLogical.AddDays(WindowDays);

    /// <summary>Instante de pared de la hora de una tarea del día lógico dado (las horas previas al inicio del día caen en el día natural siguiente).</summary>
    public static DateTime DueAt(DateOnly logicalDate, TimeOnly dueTime, TimeOnly dayStartsAt) =>
        logicalDate.AddDays(dueTime < dayStartsAt ? 1 : 0).ToDateTime(dueTime);

    /// <summary>"Es la hora de tu tarea (14:00)" / "Empieza a las 14:00 (en 15 min)".</summary>
    public static string TaskReminderBody(DateTime due, TimeSpan offset) =>
        offset == TimeSpan.Zero ? $"Es la hora: {due:HH:mm}" : $"Empieza a las {due:HH:mm} (en {(int)offset.TotalMinutes} min)";

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
