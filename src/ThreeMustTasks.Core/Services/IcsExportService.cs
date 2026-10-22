using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Una asignación lista para escribir como VEVENT (y opcionalmente VTODO). <paramref name="Uid"/> ya incluye el
/// dominio (<c>@3musttasks</c>). <paramref name="Time"/> es hora de pared local (flotante): null = día completo.
/// </summary>
public sealed record IcsEntry(
    string Uid, Guid TaskId, string Title, string? Notes, DateOnly Date, TimeOnly? Time,
    IReadOnlyList<string> Categories, bool Completed, DateTimeOffset? CompletedAt);

/// <summary>Resultado de exportar: el texto .ics, el nombre de fichero sugerido y cuántas asignaciones contiene.</summary>
public sealed record IcsExportResult(string Content, string FileName, int EventCount);

/// <summary>Serializa entradas a iCalendar (RFC 5545). Implementado con Ical.Net en Infrastructure.Shared.</summary>
public interface IIcsWriter
{
    string Write(IReadOnlyList<IcsEntry> entries, bool includeVTodo, DateTimeOffset stamp);
}

/// <summary>
/// Export a .ics (spec §4.6, 3MT-38): un VEVENT por asignación del rango (excepto las quitadas del plan), más un VTODO
/// por asignación si <see cref="AppSettings.ExportVTodo"/> está activo.
/// </summary>
public sealed class IcsExportService(
    ITaskRepository tasks, IDayRepository days, IConfigRepository config, ISettingsStore settings,
    IIcsWriter writer, TimeProvider time)
{
    public const string UidDomain = "3musttasks";

    /// <summary>Exporta las asignaciones con <c>LogicalDate</c> entre <paramref name="from"/> y <paramref name="to"/> (ambas incluidas).</summary>
    public async Task<IcsExportResult> ExportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) throw new ArgumentException("El rango es inválido: la fecha final es anterior a la inicial.", nameof(to));

        var taskMap = (await tasks.ListAsync(null, ct)).ToDictionary(t => t.Id);
        var levels = (await config.ListLevelsAsync(ct)).ToDictionary(l => l.Id);
        var categories = (await config.ListCategoriesAsync(ct)).ToDictionary(c => c.Id);

        var inRange = (await days.ListAssignmentsAsync(ct))
            .Where(a => a.LogicalDate >= from && a.LogicalDate <= to
                        && a.Outcome != AssignmentOutcome.Removed && taskMap.ContainsKey(a.TaskId))
            .OrderBy(a => a.LogicalDate).ThenBy(a => a.Order)
            .ToList();

        // Una tarea aplazada tiene varias asignaciones: la más reciente conserva el UID "limpio" {TaskId}@3musttasks
        // y las anteriores llevan la fecha como sufijo, para que ningún UID se repita en el fichero.
        var latestDate = inRange.GroupBy(a => a.TaskId).ToDictionary(g => g.Key, g => g.Max(a => a.LogicalDate));

        var entries = inRange.Select(a =>
        {
            var task = taskMap[a.TaskId];
            var names = new List<string>();
            if (levels.TryGetValue(task.LevelId, out var level)) names.Add(level.Name);
            if (task.CategoryId is { } cid && categories.TryGetValue(cid, out var cat)) names.Add(cat.Name);
            var uid = a.LogicalDate == latestDate[a.TaskId] ? $"{a.TaskId:D}@{UidDomain}" : $"{a.TaskId:D}-{a.LogicalDate:yyyyMMdd}@{UidDomain}";
            return new IcsEntry(uid, a.TaskId, task.Title, task.Notes, a.LogicalDate, task.DueTime, names,
                a.Outcome == AssignmentOutcome.Completed, a.CompletedAt);
        }).ToList();

        var content = writer.Write(entries, settings.Load().ExportVTodo, time.GetUtcNow());
        return new IcsExportResult(content, $"3musttasks-{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.ics", entries.Count);
    }
}
