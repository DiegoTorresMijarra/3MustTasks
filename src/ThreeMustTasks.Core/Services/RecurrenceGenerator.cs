using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <param name="ToPlan">Instancias generadas que entraron en el plan de hoy.</param>
/// <param name="ToPool">Instancias que chocaban con el máximo de su nivel y se dejaron en el pool (con aviso).</param>
public sealed record GenerationResult(IReadOnlyList<TaskItem> ToPlan, IReadOnlyList<TaskItem> ToPool)
{
    public static GenerationResult Empty { get; } = new([], []);
    public bool Any => ToPlan.Count > 0 || ToPool.Count > 0;
}

/// <summary>
/// Genera las instancias de las recurrentes para un día (spec §4.4). Es idempotente: una regla ya generada para
/// ese día (<see cref="RecurrenceRule.LastGeneratedDate"/>) no vuelve a generar. No abre transacción: lo llama
/// el cierre de día dentro de la suya.
/// </summary>
public sealed class RecurrenceGenerator(ITaskRepository tasks, IDayRepository days, IConfigRepository config)
{
    /// <summary>¿Aplica la regla en esa fecha? Función pura: no mira <see cref="RecurrenceRule.LastGeneratedDate"/>.</summary>
    public static bool Applies(RecurrenceRule rule, DateOnly date)
    {
        if (rule.IsPaused || date < rule.StartDate) return false;
        if (rule.EndDate is { } end && date > end) return false;
        var step = Math.Max(1, rule.Interval);

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
                return true;
            case RecurrenceFrequency.EveryNDays:
                return (date.DayNumber - rule.StartDate.DayNumber) % step == 0;
            case RecurrenceFrequency.Weekly:
                // DayNumber 0 (0001-01-01) fue lunes: DayNumber / 7 numera semanas de lunes a domingo.
                var weeks = date.DayNumber / 7 - rule.StartDate.DayNumber / 7;
                return rule.DaysOfWeek.Contains(date.DayOfWeek) && weeks % step == 0;
            case RecurrenceFrequency.Monthly:
                var months = (date.Year - rule.StartDate.Year) * 12 + date.Month - rule.StartDate.Month;
                if (months % step != 0) return false;
                // Un día 29-31 en un mes más corto cae en el último día del mes.
                var wanted = rule.DayOfMonth ?? rule.StartDate.Day;
                return date.Day == Math.Min(wanted, DateTime.DaysInMonth(date.Year, date.Month));
            default:
                return false;
        }
    }

    public async Task<GenerationResult> GenerateAsync(
        DateOnly date, IReadOnlyList<Level> enabledLevels, DateTimeOffset now, CancellationToken ct = default)
    {
        var due = (await config.ListRecurrencesAsync(ct))
            .Where(r => (r.LastGeneratedDate is null || r.LastGeneratedDate < date) && Applies(r, date))
            .OrderBy(r => r.StartDate).ThenBy(r => r.Id)
            .ToList();
        if (due.Count == 0) return GenerationResult.Empty;

        var assignments = (await days.GetAssignmentsAsync(date, ct)).ToList();
        var order = assignments.Count == 0 ? 0 : assignments.Max(a => a.Order) + 1;
        var countByLevel = new Dictionary<Guid, int>();
        foreach (var a in assignments.Where(a => a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed))
        {
            var t = await tasks.GetAsync(a.TaskId, ct);
            if (t is not null) countByLevel[t.LevelId] = countByLevel.GetValueOrDefault(t.LevelId) + 1;
        }

        var toPlan = new List<TaskItem>();
        var toPool = new List<TaskItem>();
        foreach (var rule in due)
        {
            // Nivel desactivado o borrado: no se genera ni se marca, para que lo haga el día que vuelva a estar activo.
            var level = enabledLevels.FirstOrDefault(l => l.Id == rule.LevelId);
            if (level is null) continue;

            var fits = QuotaPolicy.CanAdd(level, countByLevel.GetValueOrDefault(level.Id));
            var task = new TaskItem
            {
                Title = rule.TemplateTitle,
                LevelId = rule.LevelId,
                CategoryId = rule.CategoryId,
                DueTime = rule.DueTime,
                RecurrenceRuleId = rule.Id,
                // En el pool la instancia queda con la fecha de hoy: es lo que permite avisar de ella en Hoy.
                ScheduledDate = date,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await tasks.UpsertAsync(task, ct);
            if (fits)
            {
                await days.UpsertAssignmentAsync(
                    new DayAssignment(Guid.NewGuid(), task.Id, date, AssignmentOutcome.Planned, null, order++), ct);
                countByLevel[level.Id] = countByLevel.GetValueOrDefault(level.Id) + 1;
                toPlan.Add(task);
            }
            else
            {
                toPool.Add(task);
            }
            await config.UpsertRecurrenceAsync(rule with { LastGeneratedDate = date }, ct);
        }
        return new GenerationResult(toPlan, toPool);
    }
}
