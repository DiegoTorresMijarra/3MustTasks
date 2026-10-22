using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>Cómo se aplica un backup sobre los datos actuales.</summary>
public enum RestoreMode
{
    /// <summary>Borra todo lo actual y deja exactamente el contenido del backup (incluidos los ajustes).</summary>
    Replace,
    /// <summary>Inserta o reemplaza por Id lo que trae el backup y conserva el resto. Los ajustes actuales se mantienen.</summary>
    Merge,
}

/// <summary>Contenido de un backup (spec §4.6). Las propiedades siguen el orden del formato v1.</summary>
public sealed record BackupDocument
{
    public int SchemaVersion { get; init; } = BackupService.CurrentSchemaVersion;
    public DateTimeOffset ExportedAt { get; init; }
    public AppSettings Settings { get; init; } = new();
    public List<Level> Levels { get; init; } = [];
    public List<Category> Categories { get; init; } = [];
    public List<FavoriteTemplate> Favorites { get; init; } = [];
    public List<RecurrenceRule> Recurrences { get; init; } = [];
    public List<TaskItem> Tasks { get; init; } = [];
    public List<DayAssignment> Assignments { get; init; } = [];
    public List<DayLog> DayLogs { get; init; } = [];
}

/// <summary>Recuento de lo que contiene un backup (para la confirmación previa a restaurar).</summary>
public sealed record BackupSummary(
    int SchemaVersion, DateTimeOffset ExportedAt, int Levels, int Categories, int Favorites,
    int Recurrences, int Tasks, int Assignments, int DayLogs)
{
    public static BackupSummary Of(BackupDocument d) => new(
        d.SchemaVersion, d.ExportedAt, d.Levels.Count, d.Categories.Count, d.Favorites.Count,
        d.Recurrences.Count, d.Tasks.Count, d.Assignments.Count, d.DayLogs.Count);
}

/// <summary>El texto no es un backup válido o su versión no se entiende.</summary>
public sealed class BackupFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Backup completo en JSON versionado. El formato es independiente de la plataforma (mismo texto desde SQLite o
/// IndexedDB): camelCase, enums como texto, fechas/horas ISO 8601 y Guid en formato "D".
/// </summary>
public sealed class BackupService(
    ITaskRepository tasks, IDayRepository days, IConfigRepository config, ISettingsStore settings,
    IUnitOfWork unit, TimeProvider time)
{
    /// <summary>Versión del formato de backup que escribe esta versión de la app. Subirla exige un paso de migración.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Opciones del formato. Estables: forman parte del contrato de los ficheros ya exportados.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    public async Task<string> ExportAsync(CancellationToken ct = default)
    {
        var document = new BackupDocument
        {
            ExportedAt = time.GetUtcNow(),
            Settings = settings.Load(),
            Levels = [.. await config.ListLevelsAsync(ct)],
            Categories = [.. await config.ListCategoriesAsync(ct)],
            Favorites = [.. await config.ListFavoritesAsync(ct)],
            Recurrences = [.. await config.ListRecurrencesAsync(ct)],
            Tasks = [.. await tasks.ListAsync(null, ct)],
            Assignments = [.. await days.ListAssignmentsAsync(ct)],
            DayLogs = [.. await days.ListLogsAsync(ct)],
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    /// <summary>Lee y valida un backup. No toca los datos. Lanza <see cref="BackupFormatException"/> si no es válido.</summary>
    public static BackupDocument Parse(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException e) { throw new BackupFormatException("El archivo no es un JSON válido.", e); }

        if (root is not JsonObject obj || obj["schemaVersion"] is not JsonValue v || !v.TryGetValue<int>(out var version))
            throw new BackupFormatException("El archivo no parece un backup de 3MustTasks (falta schemaVersion).");
        if (version < 1)
            throw new BackupFormatException($"Versión de backup no válida: {version}.");
        if (version > CurrentSchemaVersion)
            throw new BackupFormatException(
                $"El backup es de una versión más nueva ({version}) que esta app (hasta la {CurrentSchemaVersion}). Actualiza la app.");

        // Aquí irán los pasos de migración JsonObject vN -> vN+1 cuando cambie el formato.

        try
        {
            return obj.Deserialize<BackupDocument>(JsonOptions)
                ?? throw new BackupFormatException("El backup está vacío.");
        }
        catch (JsonException e)
        {
            throw new BackupFormatException("El backup está dañado o tiene un formato inesperado.", e);
        }
    }

    public static BackupSummary Summarize(BackupDocument document) => BackupSummary.Of(document);

    /// <summary>Aplica el backup de forma atómica: si algo falla, los datos quedan como estaban.</summary>
    public async Task RestoreAsync(BackupDocument document, RestoreMode mode, CancellationToken ct = default)
    {
        await unit.ExecuteInTransactionAsync(async token =>
        {
            if (mode == RestoreMode.Replace) await ClearAsync(token);

            foreach (var x in document.Levels) await config.UpsertLevelAsync(x, token);
            foreach (var x in document.Categories) await config.UpsertCategoryAsync(x, token);
            foreach (var x in document.Favorites) await config.UpsertFavoriteAsync(x, token);
            foreach (var x in document.Recurrences) await config.UpsertRecurrenceAsync(x, token);
            foreach (var x in document.Tasks) await tasks.UpsertAsync(x, token);
            foreach (var x in document.Assignments) await days.UpsertAssignmentAsync(x, token);
            foreach (var x in document.DayLogs) await days.UpsertLogAsync(x, token);
        }, ct);

        // Los ajustes no forman parte de la transacción (puerto síncrono): se guardan al final, ya aplicado lo demás.
        if (mode == RestoreMode.Replace) settings.Save(document.Settings);
    }

    public async Task RestoreAsync(string json, RestoreMode mode, CancellationToken ct = default) =>
        await RestoreAsync(Parse(json), mode, ct);

    private async Task ClearAsync(CancellationToken ct)
    {
        foreach (var x in await tasks.ListAsync(null, ct)) await tasks.DeleteAsync(x.Id, ct);
        foreach (var x in await days.ListAssignmentsAsync(ct)) await days.DeleteAssignmentAsync(x.Id, ct);
        foreach (var x in await days.ListLogsAsync(ct)) await days.DeleteLogAsync(x.LogicalDate, ct);
        foreach (var x in await config.ListRecurrencesAsync(ct)) await config.DeleteRecurrenceAsync(x.Id, ct);
        foreach (var x in await config.ListFavoritesAsync(ct)) await config.DeleteFavoriteAsync(x.Id, ct);
        foreach (var x in await config.ListCategoriesAsync(ct)) await config.DeleteCategoryAsync(x.Id, ct);
        foreach (var x in await config.ListLevelsAsync(ct)) await config.DeleteLevelAsync(x.Id, ct);
    }
}
