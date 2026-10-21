using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>Dónde queda la tarea: en Hoy, en otro día (a futuro) o en el pool sin fecha.</summary>
public enum TaskWhen { Today, OtherDay, Pool }

/// <param name="Date">Solo para <see cref="TaskWhen.OtherDay"/>: debe ser posterior a hoy.</param>
/// <param name="IsPinned">Fijar o desfijar; null deja la marca como está.</param>
public sealed record EditTaskRequest(
    Guid TaskId,
    string Title,
    Guid LevelId,
    Guid? CategoryId,
    TimeOnly? DueTime,
    TimeSpan? ReminderOffset,
    string? Notes,
    TaskWhen When,
    DateOnly? Date = null,
    bool? IsPinned = null);

public enum EditTaskOutcome
{
    Saved, NotFound, ReadOnly, InvalidTitle, TitleTooLong, NotesTooLong, UnknownLevel, UnknownCategory,
    ReminderWithoutTime, InvalidDate, LevelFull, CannotMoveCompleted,
}

public sealed record EditTaskResult(EditTaskOutcome Outcome, TaskItem? Task)
{
    public bool Succeeded => Outcome == EditTaskOutcome.Saved;
}

/// <param name="InTodayPlan">La tarea tiene asignación planificada o completada en el día lógico actual.</param>
/// <param name="ReadOnly">Historial pasado (completada en otro día) o archivada: se puede ver, no editar.</param>
public sealed record EditableTask(TaskItem Task, bool InTodayPlan, TaskWhen When, bool ReadOnly);

/// <summary>Edición completa de una tarea (spec PRD §6.2) revalidando el cupo, y eliminación definitiva.</summary>
public sealed class TaskEditService(
    ITaskRepository tasks,
    IDayRepository days,
    IConfigRepository config,
    ISettingsStore settings,
    IUnitOfWork unit,
    LogicalDayCalculator calendar,
    TimeProvider time,
    PlanEvents events,
    TodayService today)
{
    public const int MaxTitleLength = 120;
    public const int MaxNotesLength = 1000;

    public async Task<EditableTask?> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await tasks.GetAsync(taskId, ct);
        if (task is null) return null;
        var day = calendar.Today(settings.Load());
        var inPlan = await FindTodayAssignmentAsync(task.Id, day, ct) is not null;
        var readOnly = task.Status == TaskItemStatus.Archived || (task.Status == TaskItemStatus.Completed && !inPlan);
        var when = inPlan ? TaskWhen.Today : task.ScheduledDate is { } d && d > day ? TaskWhen.OtherDay : TaskWhen.Pool;
        return new EditableTask(task, inPlan, when, readOnly);
    }

    public async Task<EditTaskResult> UpdateAsync(EditTaskRequest r, CancellationToken ct = default)
    {
        var current = await GetAsync(r.TaskId, ct);
        if (current is null) return Fail(EditTaskOutcome.NotFound);
        if (current.ReadOnly) return Fail(EditTaskOutcome.ReadOnly);

        var title = r.Title?.Trim();
        if (string.IsNullOrEmpty(title)) return Fail(EditTaskOutcome.InvalidTitle);
        if (title.Length > MaxTitleLength) return Fail(EditTaskOutcome.TitleTooLong);
        var notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        if (notes is not null && notes.Length > MaxNotesLength) return Fail(EditTaskOutcome.NotesTooLong);

        var level = (await today.GetLevelsAsync(ct)).FirstOrDefault(l => l.Id == r.LevelId);
        if (level is null) return Fail(EditTaskOutcome.UnknownLevel);
        if (r.CategoryId is { } cid && (await config.ListCategoriesAsync(ct)).All(c => c.Id != cid))
            return Fail(EditTaskOutcome.UnknownCategory);
        if (r.ReminderOffset is { } offset && (r.DueTime is null || offset < TimeSpan.Zero))
            return Fail(EditTaskOutcome.ReminderWithoutTime);

        var day = calendar.Today(settings.Load());
        if (r.When == TaskWhen.OtherDay && (r.Date is null || r.Date <= day)) return Fail(EditTaskOutcome.InvalidDate);
        var completed = current.Task.Status == TaskItemStatus.Completed;
        if (completed && r.When != TaskWhen.Today) return Fail(EditTaskOutcome.CannotMoveCompleted);

        var assignment = await FindTodayAssignmentAsync(r.TaskId, day, ct);
        var otherInLevel = (await today.LoadPlanAsync(day, ct)).Count(p => p.Task.LevelId == level.Id && p.Task.Id != r.TaskId);
        if (r.When == TaskWhen.Today && !QuotaPolicy.CanAdd(level, otherInLevel)) return Fail(EditTaskOutcome.LevelFull);

        var scheduled = r.When switch { TaskWhen.Today => day, TaskWhen.OtherDay => r.Date, _ => (DateOnly?)null };
        var updated = current.Task with
        {
            Title = title,
            LevelId = level.Id,
            CategoryId = r.CategoryId,
            DueTime = r.DueTime,
            ReminderOffset = r.DueTime is null ? null : r.ReminderOffset,
            Notes = notes,
            ScheduledDate = scheduled,
            IsPinned = r.IsPinned ?? current.Task.IsPinned,
            UpdatedAt = time.GetUtcNow(),
        };
        await tasks.UpsertAsync(updated, ct);

        var planTouched = false;
        if (r.When == TaskWhen.Today && assignment is null)
        {
            var existing = await days.GetAssignmentsAsync(day, ct);
            var order = existing.Count == 0 ? 0 : existing.Max(a => a.Order) + 1;
            await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), r.TaskId, day, AssignmentOutcome.Planned, null, order), ct);
            planTouched = true;
        }
        else if (r.When != TaskWhen.Today && assignment is not null)
        {
            await days.UpsertAssignmentAsync(assignment with { Outcome = AssignmentOutcome.Removed }, ct);
            planTouched = true;
        }

        // El nivel de una tarea ya planificada puede haber cambiado: también afecta a los mínimos.
        planTouched |= assignment is not null && current.Task.LevelId != level.Id;
        if (planTouched) await today.RecordMilestonesAsync(day, ct);
        await unit.SaveChangesAsync(ct);
        if (planTouched) events.RaisePlanChanged(day);
        return new(EditTaskOutcome.Saved, updated);
    }

    /// <summary>
    /// Elimina la tarea de forma definitiva y su asignación de hoy. Las asignaciones de días pasados quedan
    /// huérfanas (el historial debe ignorar las que apunten a una tarea que ya no existe).
    /// </summary>
    public async Task<bool> DeleteAsync(Guid taskId, CancellationToken ct = default)
    {
        var day = calendar.Today(settings.Load());
        var assignment = await FindTodayAssignmentAsync(taskId, day, ct);
        if (!await tasks.DeleteAsync(taskId, ct)) return false;
        if (assignment is not null) await days.DeleteAssignmentAsync(assignment.Id, ct);
        if (assignment is not null) await today.RecordMilestonesAsync(day, ct);
        await unit.SaveChangesAsync(ct);
        if (assignment is not null) events.RaisePlanChanged(day);
        return true;
    }

    private static EditTaskResult Fail(EditTaskOutcome outcome) => new(outcome, null);

    private async Task<DayAssignment?> FindTodayAssignmentAsync(Guid taskId, DateOnly day, CancellationToken ct) =>
        (await days.GetAssignmentsAsync(day, ct)).FirstOrDefault(a =>
            a.TaskId == taskId && a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed);
}
