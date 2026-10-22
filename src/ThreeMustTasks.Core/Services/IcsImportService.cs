using System.Text.RegularExpressions;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// RRULE básica ya interpretada. <paramref name="Supported"/> es false si la regla usa algo que 3MustTasks no sabe
/// repetir (YEARLY, COUNT, BYSETPOS, semanal con intervalo > 1, varios días del mes...): el evento se importa entonces
/// como una tarea suelta en su fecha.
/// </summary>
public sealed record IcsRecurrence(
    RecurrenceFrequency? Frequency, int Interval, IReadOnlyList<DayOfWeek> Days, int? DayOfMonth, DateOnly? Until, bool Supported);

/// <summary>Un VEVENT leído. <paramref name="Time"/> está en hora local del dispositivo; null = día completo.</summary>
public sealed record IcsEvent(
    string Uid, string Title, string? Notes, DateOnly Date, TimeOnly? Time,
    IReadOnlyList<string> Categories, IcsRecurrence? Recurrence);

/// <summary>El texto no es un calendario iCalendar válido.</summary>
public sealed class IcsFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Lee un .ics (RFC 5545). Implementado con Ical.Net en Infrastructure.Shared. Lanza <see cref="IcsFormatException"/>.</summary>
public interface IIcsReader
{
    IReadOnlyList<IcsEvent> Read(string content, TimeZoneInfo localZone);
}

public enum IcsImportStatus
{
    /// <summary>No hay ninguna tarea con ese UID.</summary>
    New,
    /// <summary>Ya existe: al importar se actualiza en lugar de duplicar.</summary>
    Existing,
}

public enum IcsImportDestination
{
    /// <summary>Hoy: entra al plan si cabe en el cupo del nivel; si no, al pool.</summary>
    Today,
    /// <summary>Fecha futura: tarea programada que se añade al plan ese día (pool a futuro).</summary>
    Future,
    /// <summary>Fecha pasada: pool sin fecha.</summary>
    PastToPool,
    /// <summary>Con RRULE soportada: se crea una regla recurrente.</summary>
    Recurring,
}

/// <param name="Key">Identifica el evento dentro del fichero (UID canónico) para elegir qué importar.</param>
/// <param name="ExistingTaskId">La tarea que se actualizará (solo si <see cref="Status"/> es Existing).</param>
/// <param name="SuggestedLevelId">Nivel deducido de CATEGORIES (p. ej. un export propio); null = el nivel por defecto.</param>
/// <param name="RecurrenceIgnored">Tenía una RRULE que no se soporta y se importa como tarea suelta.</param>
public sealed record IcsPreviewItem(
    string Key, IcsEvent Event, IcsImportStatus Status, IcsImportDestination Destination,
    Guid? ExistingTaskId, Guid? SuggestedLevelId, Guid? CategoryId, bool RecurrenceIgnored)
{
    /// <summary>Selección por defecto: los eventos pasados nuevos no se marcan.</summary>
    public bool SelectedByDefault => Destination != IcsImportDestination.PastToPool || Status == IcsImportStatus.Existing;
}

public sealed record IcsPreview(IReadOnlyList<IcsPreviewItem> Items, int DuplicatesInFile);

public sealed record IcsImportResult(
    int ToPlan, int ToPool, int Scheduled, int Recurring, int Updated, int Skipped);

/// <summary>
/// Import de .ics (spec §4.6, 3MT-39). Previsualiza, deduplica por UID y crea tareas: lo de hoy al plan si cabe, lo
/// futuro como tarea programada, lo pasado al pool y las RRULE básicas como reglas recurrentes. Los VTODO se ignoran.
/// </summary>
public sealed partial class IcsImportService(
    ITaskRepository tasks, IDayRepository days, IConfigRepository config, ISettingsStore settings,
    IIcsReader reader, TodayService today, LogicalDayCalculator calendar, TimeProvider time, PlanEvents events)
{
    // {TaskId}@3musttasks o {TaskId}-yyyyMMdd@3musttasks (aplazadas, ver IcsExportService).
    [GeneratedRegex(@"^(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?<suffix>-\d{8})?@" + IcsExportService.UidDomain + "$")]
    private static partial Regex OwnUid();

    /// <summary>UID sin el sufijo de fecha de las aplazadas: todas las apariciones de una tarea comparten clave.</summary>
    public static string CanonicalUid(string uid)
    {
        var m = OwnUid().Match(uid.Trim());
        return m.Success ? $"{m.Groups["id"].Value.ToLowerInvariant()}@{IcsExportService.UidDomain}" : uid.Trim();
    }

    public async Task<IcsPreview> PreviewAsync(string content, CancellationToken ct = default)
    {
        var parsed = reader.Read(content, time.LocalTimeZone);
        var s = settings.Load();
        var day = calendar.Today(s);

        // Un mismo evento puede aparecer varias veces (aplazadas de un export propio): se queda la aparición más reciente.
        var groups = parsed.GroupBy(e => CanonicalUid(e.Uid)).ToList();
        var duplicates = parsed.Count - groups.Count;

        var existing = await tasks.ListAsync(null, ct);
        var byUid = existing.Where(t => t.IcsUid is not null).GroupBy(t => CanonicalUid(t.IcsUid!)).ToDictionary(g => g.Key, g => g.First());
        var byId = existing.ToDictionary(t => t.Id);
        var levels = (await today.GetLevelsAsync(ct)).ToList();
        var categories = await config.ListCategoriesAsync(ct);
        var rules = await config.ListRecurrencesAsync(ct);

        var items = new List<IcsPreviewItem>();
        foreach (var g in groups)
        {
            var ev = g.OrderByDescending(e => e.Date).First();
            var key = g.Key;

            TaskItem? match = byUid.GetValueOrDefault(key);
            if (match is null && OwnUid().Match(key) is { Success: true } m && Guid.TryParse(m.Groups["id"].Value, out var id))
                match = byId.GetValueOrDefault(id);

            var rec = ev.Recurrence;
            var recurring = rec is { Supported: true, Frequency: not null };
            // Las reglas no guardan el UID: una recurrente ya importada se reconoce por título, inicio, hora y frecuencia.
            var duplicateRule = recurring && rules.Any(r => SameRule(r, ev, rec!));

            var destination =
                recurring ? IcsImportDestination.Recurring :
                ev.Date == day ? IcsImportDestination.Today :
                ev.Date > day ? IcsImportDestination.Future : IcsImportDestination.PastToPool;

            var level = levels.FirstOrDefault(l => ev.Categories.Contains(l.Name, StringComparer.OrdinalIgnoreCase));
            var category = categories.FirstOrDefault(c => ev.Categories.Contains(c.Name, StringComparer.OrdinalIgnoreCase));
            items.Add(new IcsPreviewItem(
                key, ev, match is not null || duplicateRule ? IcsImportStatus.Existing : IcsImportStatus.New,
                destination, match?.Id, level?.Id, category?.Id, rec is { Supported: false }));
        }
        return new IcsPreview(items.OrderBy(i => i.Event.Date).ThenBy(i => i.Event.Time).ThenBy(i => i.Event.Title).ToList(), duplicates);
    }

    /// <summary>Importa los elementos elegidos. <paramref name="defaultLevelId"/> se usa cuando el evento no trae nivel en CATEGORIES.</summary>
    public async Task<IcsImportResult> ImportAsync(
        IReadOnlyList<IcsPreviewItem> selected, Guid defaultLevelId, CancellationToken ct = default)
    {
        var levels = (await today.GetLevelsAsync(ct)).ToDictionary(l => l.Id);
        if (!levels.ContainsKey(defaultLevelId)) throw new ArgumentException("El nivel por defecto no existe o está desactivado.", nameof(defaultLevelId));

        var day = calendar.Today(settings.Load());
        int toPlan = 0, toPool = 0, scheduled = 0, recurring = 0, updated = 0, skipped = 0;
        var planTouched = false;

        foreach (var item in selected)
        {
            var ev = item.Event;
            var levelId = item.SuggestedLevelId is { } sl && levels.ContainsKey(sl) ? sl : defaultLevelId;
            var title = Truncate(ev.Title.Trim(), TaskEditService.MaxTitleLength);
            if (title.Length == 0) { skipped++; continue; }
            var notes = string.IsNullOrWhiteSpace(ev.Notes) ? null : Truncate(ev.Notes.Trim(), TaskEditService.MaxNotesLength);
            var uid = item.Key;

            if (item.Status == IcsImportStatus.Existing)
            {
                if (item.ExistingTaskId is { } existingId && await tasks.GetAsync(existingId, ct) is { } task)
                {
                    await tasks.UpsertAsync(await UpdateAsync(task, title, notes, ev, uid, day, ct), ct);
                    updated++;
                }
                else skipped++; // regla recurrente ya presente: no se duplica
                continue;
            }

            if (item.Destination == IcsImportDestination.Recurring)
            {
                if (await CreateRuleAsync(item, title, levelId, day, uid, notes, ct) is { } r) { recurring++; planTouched |= r; }
                continue;
            }

            switch (item.Destination)
            {
                case IcsImportDestination.Today:
                {
                    var result = await today.AddAsync(new NewTaskRequest(title, levelId, AddDestination.Today,
                        CategoryId: item.CategoryId, DueTime: ev.Time), ct);
                    if (result.Outcome == AddTaskOutcome.LevelFull)
                    {
                        result = await today.AddAsync(new NewTaskRequest(title, levelId, AddDestination.Pool,
                            CategoryId: item.CategoryId, DueTime: ev.Time), ct);
                    }
                    if (!result.Succeeded) { skipped++; break; }
                    await tasks.UpsertAsync(result.Task! with { IcsUid = uid, Notes = notes }, ct);
                    if (result.Outcome == AddTaskOutcome.AddedToToday) toPlan++; else toPool++;
                    break;
                }
                case IcsImportDestination.PastToPool:
                {
                    var result = await today.AddAsync(new NewTaskRequest(title, levelId, AddDestination.Pool,
                        CategoryId: item.CategoryId, DueTime: ev.Time), ct);
                    if (!result.Succeeded) { skipped++; break; }
                    await tasks.UpsertAsync(result.Task! with { IcsUid = uid, Notes = notes }, ct);
                    toPool++;
                    break;
                }
                default: // Future
                {
                    var now = time.GetUtcNow();
                    await tasks.UpsertAsync(new TaskItem
                    {
                        Title = title, Notes = notes, LevelId = levelId, CategoryId = item.CategoryId, DueTime = ev.Time,
                        ScheduledDate = ev.Date, IcsUid = uid, CreatedAt = now, UpdatedAt = now,
                    }, ct);
                    scheduled++;
                    break;
                }
            }
        }

        if (planTouched) events.RaisePlanChanged(day);
        return new IcsImportResult(toPlan, toPool, scheduled, recurring, updated, skipped);
    }

    private async Task<TaskItem> UpdateAsync(TaskItem task, string title, string? notes, IcsEvent ev, string uid, DateOnly day, CancellationToken ct)
    {
        var updated = task with { Title = title, Notes = notes ?? task.Notes, DueTime = ev.Time, IcsUid = task.IcsUid ?? uid, UpdatedAt = time.GetUtcNow() };
        // Solo se reprograma una tarea pendiente que no está ya en el plan de hoy ni es del pasado.
        if (task.Status == TaskItemStatus.Active && ev.Date > day && ev.Recurrence is not { Supported: true })
        {
            var inPlan = (await days.GetAssignmentsAsync(day, ct)).Any(a => a.TaskId == task.Id && a.Outcome is AssignmentOutcome.Planned or AssignmentOutcome.Completed);
            if (!inPlan) updated = updated with { ScheduledDate = ev.Date };
        }
        return updated;
    }

    /// <returns>null si no se pudo crear; si no, si ha entrado una tarea al plan de hoy.</returns>
    private async Task<bool?> CreateRuleAsync(IcsPreviewItem item, string title, Guid levelId, DateOnly day, string uid, string? notes, CancellationToken ct)
    {
        var ev = item.Event;
        var rec = ev.Recurrence!;
        var rule = new RecurrenceRule(
            Guid.NewGuid(), title, levelId, item.CategoryId, ev.Time, rec.Frequency!.Value,
            rec.Interval,
            rec.Days.ToArray(), rec.DayOfMonth, ev.Date, rec.Until, null);
        var planned = false;

        if (RecurrenceGenerator.Applies(rule, day))
        {
            // La instancia de hoy ya no la generará el cierre de día: se crea ahora y la regla queda como generada hoy.
            var result = await today.AddAsync(new NewTaskRequest(title, levelId, AddDestination.Today, CategoryId: item.CategoryId, DueTime: ev.Time), ct);
            if (result.Outcome == AddTaskOutcome.LevelFull)
                result = await today.AddAsync(new NewTaskRequest(title, levelId, AddDestination.Pool, CategoryId: item.CategoryId, DueTime: ev.Time), ct);
            if (result.Succeeded)
            {
                planned = result.Outcome == AddTaskOutcome.AddedToToday;
                await tasks.UpsertAsync(result.Task! with { RecurrenceRuleId = rule.Id, Notes = notes, ScheduledDate = day }, ct);
                rule = rule with { LastGeneratedDate = day };
            }
        }
        await config.UpsertRecurrenceAsync(rule, ct);
        return planned;
    }

    private static bool SameRule(RecurrenceRule r, IcsEvent ev, IcsRecurrence rec) =>
        r.StartDate == ev.Date && r.DueTime == ev.Time && r.Frequency == rec.Frequency
        && string.Equals(r.TemplateTitle, ev.Title.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
