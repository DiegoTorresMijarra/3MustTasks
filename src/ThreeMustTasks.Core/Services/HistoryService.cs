using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Resumen de un día cerrado. <paramref name="Planned"/> cuenta las tareas completadas y no completadas (las quitadas
/// del plan no cuentan). <paramref name="Percent"/> es completadas/planificadas (0-100) y es null si no hubo planificadas.
/// <paramref name="HasPlan"/> es false para un día sin <see cref="DayLog"/> cerrado ("sin plan").
/// </summary>
public sealed record DaySummary(DateOnly Date, int Planned, int Completed, int? Percent, bool? Fulfilled, bool HasPlan);

/// <summary>Una tarea en la vista Día del historial.</summary>
public sealed record HistoryItem(TaskItem Task, Level? Level, AssignmentOutcome Outcome, int Order);

/// <param name="IsReadOnly">Siempre true: los días pasados del historial no se editan.</param>
public sealed record DayDetail(DateOnly Date, IReadOnlyList<HistoryItem> Items, DaySummary Summary, bool IsReadOnly = true);

/// <param name="DaysWithPlan">Días cerrados con plan dentro del rango.</param>
/// <param name="FulfilledDays">De ellos, los cumplidos.</param>
/// <param name="CompliancePercent">Cumplidos/con plan (0-100); null si no hay días con plan en el rango.</param>
public sealed record HistoryMetrics(int DaysWithPlan, int FulfilledDays, int? CompliancePercent, int CurrentStreak, int BestStreak);

/// <summary>Casos de uso del historial: resumen por día, vista Día (solo lectura) y métricas (3MT-34, 35).</summary>
public sealed class HistoryService(
    ITaskRepository tasks,
    IDayRepository days,
    IConfigRepository config,
    ISettingsStore settings,
    LogicalDayCalculator calendar,
    StreakCalculator streak)
{
    private const int MaxLookbackDays = 3650;

    /// <summary>Hoy lógico: el historial termina ayer.</summary>
    public DateOnly Today => calendar.Today(settings.Load());

    /// <summary>Un resumen por cada día del rango (ambos incluidos), en orden ascendente. Ignora asignaciones huérfanas.</summary>
    public async Task<IReadOnlyList<DaySummary>> GetSummariesAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var taskIds = (await tasks.ListAsync(null, ct)).Select(t => t.Id).ToHashSet();
        var logs = (await days.ListClosedLogsAsync(from, to, ct)).ToDictionary(l => l.LogicalDate);
        var result = new List<DaySummary>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            logs.TryGetValue(d, out var log);
            var assignments = log is null ? [] : (await days.GetAssignmentsAsync(d, ct)).Where(a => taskIds.Contains(a.TaskId)).ToList();
            result.Add(Summarize(d, log, assignments));
        }
        return result;
    }

    /// <summary>Detalle de un día pasado: cada tarea con su resultado, sin las asignaciones de tareas borradas.</summary>
    public async Task<DayDetail> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var taskMap = (await tasks.ListAsync(null, ct)).ToDictionary(t => t.Id);
        var levels = (await config.ListLevelsAsync(ct)).ToDictionary(l => l.Id);
        var log = await days.GetLogAsync(date, ct);
        var assignments = (await days.GetAssignmentsAsync(date, ct)).Where(a => taskMap.ContainsKey(a.TaskId)).OrderBy(a => a.Order).ToList();
        var items = assignments
            .Select(a => new HistoryItem(taskMap[a.TaskId], levels.GetValueOrDefault(taskMap[a.TaskId].LevelId), a.Outcome, a.Order))
            .ToList();
        var closedLog = log?.ClosedAt is null ? null : log;
        return new DayDetail(date, items, Summarize(date, closedLog, assignments));
    }

    /// <summary>Cumplimiento del rango (% de días con plan cumplidos), racha actual y mejor racha.</summary>
    public async Task<HistoryMetrics> GetMetricsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var inRange = (await days.ListClosedLogsAsync(from, to, ct)).ToList();
        var fulfilled = inRange.Count(l => l.Fulfilled == true);
        int? percent = inRange.Count == 0 ? null : (int)Math.Round(100.0 * fulfilled / inRange.Count, MidpointRounding.AwayFromZero);

        var current = await streak.GetAsync(ct);
        var today = Today;
        var all = (await days.ListClosedLogsAsync(today.AddDays(-MaxLookbackDays), today.AddDays(-1), ct)).OrderBy(l => l.LogicalDate);
        int best = 0, run = 0;
        DateOnly? previous = null;
        foreach (var log in all)
        {
            if (log.Fulfilled != true) { run = 0; previous = null; continue; }
            run = previous is { } p && log.LogicalDate == p.AddDays(1) ? run + 1 : 1;
            previous = log.LogicalDate;
            best = Math.Max(best, run);
        }
        return new HistoryMetrics(inRange.Count, fulfilled, percent, current, Math.Max(best, current));
    }

    private static DaySummary Summarize(DateOnly date, DayLog? log, IReadOnlyList<DayAssignment> assignments)
    {
        if (log?.ClosedAt is null) return new DaySummary(date, 0, 0, null, null, false);
        var planned = assignments.Count(a => a.Outcome is AssignmentOutcome.Completed or AssignmentOutcome.NotCompleted);
        var completed = assignments.Count(a => a.Outcome == AssignmentOutcome.Completed);
        int? percent = planned == 0 ? null : (int)Math.Round(100.0 * completed / planned, MidpointRounding.AwayFromZero);
        return new DaySummary(date, planned, completed, percent, log.Fulfilled, true);
    }
}
