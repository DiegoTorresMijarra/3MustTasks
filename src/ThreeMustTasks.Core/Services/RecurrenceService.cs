using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>Formas de repetición que ofrece la UI (PRD §5.5). Los laborables son una semanal de lunes a viernes.</summary>
public enum RecurrencePreset { Daily, Weekdays, Weekly, EveryNDays, Monthly }

/// <summary>Alcance al editar una instancia de una recurrente (PRD §6.2).</summary>
public enum RecurrenceScope { OnlyThis, ThisAndFollowing }

public enum RecurrenceOutcome
{
    Saved, NotFound, NotRecurrent, AlreadyRecurrent, InvalidTitle, TitleTooLong, UnknownLevel, UnknownCategory,
    InvalidInterval, NoDays, InvalidDayOfMonth, InvalidEndDate,
}

/// <param name="Interval">Solo para <see cref="RecurrencePreset.EveryNDays"/> (cada N días); en el resto se ignora (1).</param>
/// <param name="Days">Solo para <see cref="RecurrencePreset.Weekly"/>.</param>
/// <param name="DayOfMonth">Solo para <see cref="RecurrencePreset.Monthly"/>; null = el día de inicio. Un 29-31 en un mes corto cae en su último día.</param>
public sealed record RecurrenceSpec(
    RecurrencePreset Preset,
    int Interval = 1,
    IReadOnlyList<DayOfWeek>? Days = null,
    int? DayOfMonth = null,
    DateOnly? EndDate = null)
{
    public const int MaxInterval = 365;

    public static readonly DayOfWeek[] WeekdayDays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public static RecurrenceSpec FromRule(RecurrenceRule rule)
    {
        var days = rule.DaysOfWeek.Distinct().OrderBy(d => ((int)d + 6) % 7).ToArray();
        return rule.Frequency switch
        {
            RecurrenceFrequency.Daily => new(RecurrencePreset.Daily, EndDate: rule.EndDate),
            RecurrenceFrequency.EveryNDays => new(RecurrencePreset.EveryNDays, rule.Interval, EndDate: rule.EndDate),
            RecurrenceFrequency.Monthly => new(RecurrencePreset.Monthly, DayOfMonth: rule.DayOfMonth ?? rule.StartDate.Day, EndDate: rule.EndDate),
            _ when rule.Interval == 1 && days.SequenceEqual(WeekdayDays) => new(RecurrencePreset.Weekdays, EndDate: rule.EndDate),
            _ => new(RecurrencePreset.Weekly, Days: days, EndDate: rule.EndDate),
        };
    }

    /// <summary>Null si es válida; si no, el motivo. <paramref name="start"/> es el inicio de la regla (la fecha de fin no puede ser anterior).</summary>
    public RecurrenceOutcome? Validate(DateOnly start)
    {
        if (Preset == RecurrencePreset.EveryNDays && (Interval < 1 || Interval > MaxInterval)) return RecurrenceOutcome.InvalidInterval;
        if (Preset == RecurrencePreset.Weekly && (Days is null || Days.Count == 0)) return RecurrenceOutcome.NoDays;
        if (Preset == RecurrencePreset.Monthly && DayOfMonth is { } d && (d < 1 || d > 31)) return RecurrenceOutcome.InvalidDayOfMonth;
        if (EndDate is { } end && end < start) return RecurrenceOutcome.InvalidEndDate;
        return null;
    }

    internal RecurrenceRule ApplyTo(RecurrenceRule rule) => Preset switch
    {
        RecurrencePreset.Daily => rule with { Frequency = RecurrenceFrequency.Daily, Interval = 1, DaysOfWeek = [], DayOfMonth = null, EndDate = EndDate },
        RecurrencePreset.Weekdays => rule with { Frequency = RecurrenceFrequency.Weekly, Interval = 1, DaysOfWeek = WeekdayDays.ToArray(), DayOfMonth = null, EndDate = EndDate },
        RecurrencePreset.Weekly => rule with { Frequency = RecurrenceFrequency.Weekly, Interval = 1, DaysOfWeek = Days!.Distinct().ToArray(), DayOfMonth = null, EndDate = EndDate },
        RecurrencePreset.EveryNDays => rule with { Frequency = RecurrenceFrequency.EveryNDays, Interval = Interval, DaysOfWeek = [], DayOfMonth = null, EndDate = EndDate },
        _ => rule with { Frequency = RecurrenceFrequency.Monthly, Interval = 1, DaysOfWeek = [], DayOfMonth = DayOfMonth ?? rule.StartDate.Day, EndDate = EndDate },
    };
}

/// <summary>Edición de una tarea más lo que afecta a su repetición: lo que envía el formulario de edición.</summary>
public sealed record RecurringEdit(EditTaskRequest Request, RecurrenceScope Scope, RecurrenceSpec? Spec);

/// <summary>Datos de la plantilla de una regla editados desde Ajustes.</summary>
public sealed record RuleEdit(string Title, Guid LevelId, Guid? CategoryId, TimeOnly? DueTime, RecurrenceSpec Spec);

public sealed record RecurrenceResult(RecurrenceOutcome Outcome, RecurrenceRule? Rule)
{
    public bool Succeeded => Outcome == RecurrenceOutcome.Saved;
}

/// <param name="Edit">Resultado de editar la tarea; null si no se llegó a intentar porque la repetición era inválida.</param>
/// <param name="Recurrence">Estado de la parte de repetición (Saved si no había nada que hacer).</param>
public sealed record TaskRecurrenceResult(EditTaskResult? Edit, RecurrenceOutcome Recurrence)
{
    public bool Succeeded => Edit is { Succeeded: true } && Recurrence == RecurrenceOutcome.Saved;
}

/// <summary>
/// Alta, pausa, edición y borrado de reglas recurrentes (PRD §5.5), y edición de una instancia con el alcance
/// "solo esta" / "esta y las siguientes". La generación diaria la hace <see cref="RecurrenceGenerator"/> en el cierre.
/// </summary>
public sealed class RecurrenceService(
    ITaskRepository tasks,
    IConfigRepository config,
    ISettingsStore settings,
    IUnitOfWork unit,
    LogicalDayCalculator calendar,
    TimeProvider time,
    TaskEditService editor)
{
    public async Task<IReadOnlyList<RecurrenceRule>> ListAsync(CancellationToken ct = default) =>
        (await config.ListRecurrencesAsync(ct)).OrderBy(r => r.TemplateTitle, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.Id).ToList();

    public async Task<RecurrenceRule?> GetAsync(Guid ruleId, CancellationToken ct = default) =>
        (await config.ListRecurrencesAsync(ct)).FirstOrDefault(r => r.Id == ruleId);

    /// <summary>
    /// Convierte una tarea en la primera instancia de una regla nueva que empieza hoy: la tarea queda vinculada y
    /// la regla marcada como generada hoy, así que el cierre de día no duplica la de hoy.
    /// </summary>
    public async Task<RecurrenceResult> CreateFromTaskAsync(Guid taskId, RecurrenceSpec spec, CancellationToken ct = default)
    {
        var task = await tasks.GetAsync(taskId, ct);
        if (task is null) return new(RecurrenceOutcome.NotFound, null);
        if (task.RecurrenceRuleId is not null) return new(RecurrenceOutcome.AlreadyRecurrent, null);

        var today = calendar.Today(settings.Load());
        if (spec.Validate(today) is { } error) return new(error, null);

        var rule = spec.ApplyTo(new RecurrenceRule(
            Guid.NewGuid(), task.Title, task.LevelId, task.CategoryId, task.DueTime,
            RecurrenceFrequency.Daily, 1, [], null, today, null, today));
        await config.UpsertRecurrenceAsync(rule, ct);
        await tasks.UpsertAsync(task with { RecurrenceRuleId = rule.Id, UpdatedAt = time.GetUtcNow() }, ct);
        await unit.SaveChangesAsync(ct);
        return new(RecurrenceOutcome.Saved, rule);
    }

    public async Task<RecurrenceResult> SetPausedAsync(Guid ruleId, bool paused, CancellationToken ct = default)
    {
        var rule = await GetAsync(ruleId, ct);
        if (rule is null) return new(RecurrenceOutcome.NotFound, null);
        if (rule.IsPaused != paused)
        {
            rule = rule with { IsPaused = paused };
            await config.UpsertRecurrenceAsync(rule, ct);
            await unit.SaveChangesAsync(ct);
        }
        return new(RecurrenceOutcome.Saved, rule);
    }

    /// <summary>Edita la plantilla y la frecuencia de una regla. No toca las instancias ya generadas.</summary>
    public async Task<RecurrenceResult> UpdateRuleAsync(Guid ruleId, RuleEdit edit, CancellationToken ct = default)
    {
        var rule = await GetAsync(ruleId, ct);
        if (rule is null) return new(RecurrenceOutcome.NotFound, null);
        return await SaveRuleAsync(rule, edit.Title, edit.LevelId, edit.CategoryId, edit.DueTime, edit.Spec, ct);
    }

    /// <summary>Borra la regla; las instancias ya generadas siguen siendo tareas normales (se desvinculan).</summary>
    public async Task<bool> DeleteAsync(Guid ruleId, CancellationToken ct = default)
    {
        var deleted = false;
        await unit.ExecuteInTransactionAsync(async token =>
        {
            deleted = await config.DeleteRecurrenceAsync(ruleId, token);
            if (!deleted) return;
            foreach (var t in (await tasks.ListAsync(null, token)).Where(t => t.RecurrenceRuleId == ruleId))
                await tasks.UpsertAsync(t with { RecurrenceRuleId = null, UpdatedAt = time.GetUtcNow() }, token);
        }, ct);
        return deleted;
    }

    /// <summary>
    /// Guarda la edición de una tarea (<see cref="TaskEditService"/>) aplicando el alcance si es una instancia:
    /// "solo esta" no toca la regla; "esta y las siguientes" actualiza la plantilla (y la frecuencia, si se da
    /// <paramref name="spec"/>) con lo guardado. En una tarea que aún no repite, un <paramref name="spec"/> la
    /// convierte en recurrente.
    /// </summary>
    public async Task<TaskRecurrenceResult> UpdateTaskAsync(
        EditTaskRequest request, RecurrenceScope scope, RecurrenceSpec? spec, CancellationToken ct = default)
    {
        var before = await tasks.GetAsync(request.TaskId, ct);
        var rule = before?.RecurrenceRuleId is { } rid ? await GetAsync(rid, ct) : null;

        // Se valida antes de guardar nada para no dejar la edición a medias.
        var affectsRule = rule is null ? spec is not null && before is not null : scope == RecurrenceScope.ThisAndFollowing;
        if (affectsRule && spec is not null
            && spec.Validate(rule?.StartDate ?? calendar.Today(settings.Load())) is { } invalid)
            return new(null, invalid);

        var result = await editor.UpdateAsync(request, ct);
        if (!result.Succeeded || result.Task is not { } saved) return new(result, RecurrenceOutcome.Saved);

        if (rule is null)
        {
            if (spec is null || saved.RecurrenceRuleId is not null) return new(result, RecurrenceOutcome.Saved);
            return new(result, (await CreateFromTaskAsync(saved.Id, spec, ct)).Outcome);
        }

        if (scope == RecurrenceScope.OnlyThis) return new(result, RecurrenceOutcome.Saved);
        var updated = await SaveRuleAsync(
            rule, saved.Title, saved.LevelId, saved.CategoryId, saved.DueTime, spec ?? RecurrenceSpec.FromRule(rule), ct);
        return new(result, updated.Outcome);
    }

    private async Task<RecurrenceResult> SaveRuleAsync(
        RecurrenceRule rule, string? rawTitle, Guid levelId, Guid? categoryId, TimeOnly? dueTime, RecurrenceSpec spec, CancellationToken ct)
    {
        var title = rawTitle?.Trim();
        if (string.IsNullOrEmpty(title)) return new(RecurrenceOutcome.InvalidTitle, null);
        if (title.Length > TaskEditService.MaxTitleLength) return new(RecurrenceOutcome.TitleTooLong, null);
        if ((await config.ListLevelsAsync(ct)).All(l => l.Id != levelId)) return new(RecurrenceOutcome.UnknownLevel, null);
        if (categoryId is { } cid && (await config.ListCategoriesAsync(ct)).All(c => c.Id != cid))
            return new(RecurrenceOutcome.UnknownCategory, null);
        if (spec.Validate(rule.StartDate) is { } error) return new(error, null);

        var updated = spec.ApplyTo(rule with
        {
            TemplateTitle = title, LevelId = levelId, CategoryId = categoryId, DueTime = dueTime,
        });
        await config.UpsertRecurrenceAsync(updated, ct);
        await unit.SaveChangesAsync(ct);
        return new(RecurrenceOutcome.Saved, updated);
    }
}
