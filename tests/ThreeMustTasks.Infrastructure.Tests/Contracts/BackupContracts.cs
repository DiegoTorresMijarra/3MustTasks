using System.Text.Json.Nodes;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.Infrastructure.Tests.Contracts;

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class BackupSeed
{
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
    public static readonly Guid Must = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid Work = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid Task1 = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid Task2 = new("00000000-0000-0000-0000-000000000004");
    public static readonly Guid Rule = new("00000000-0000-0000-0000-000000000005");
    public static readonly Guid Fav = new("00000000-0000-0000-0000-000000000006");
    public static readonly DateOnly Day = new(2026, 10, 21);
    public static readonly AppSettings Settings = new() { DayStartsAt = new TimeOnly(5, 0), StrictMode = true, Theme = ThemeMode.Dark };

    public static BackupService Service(PersistenceSet p) =>
        new(p.Tasks, p.Days, p.Config, p.Settings, p.Unit, new FixedTime(Now));

    /// <summary>Rellena el conjunto con datos de todos los tipos, con valores opcionales presentes y ausentes.</summary>
    public static async Task FillAsync(PersistenceSet p)
    {
        var t0 = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);
        p.Settings.Save(Settings);
        await p.Config.UpsertLevelAsync(new Level(Must, "Must", "#f00", "flag", 1, 1, 3, 1, true));
        await p.Config.UpsertCategoryAsync(new Category(Work, "Trabajo", "#00f"));
        await p.Config.UpsertFavoriteAsync(new FavoriteTemplate(Fav, "Leer", Must, null, new TimeOnly(21, 0), TimeSpan.FromMinutes(15), 3));
        await p.Config.UpsertRecurrenceAsync(new RecurrenceRule(Rule, "Correo", Must, Work, new TimeOnly(9, 0),
            RecurrenceFrequency.Weekly, 1, [DayOfWeek.Monday, DayOfWeek.Friday], null, new DateOnly(2026, 10, 1), null, Day, true));
        await p.Tasks.UpsertAsync(new TaskItem
        {
            Id = Task1, Title = "Informe", Notes = "n", LevelId = Must, CategoryId = Work, DueTime = new TimeOnly(17, 30),
            ReminderOffset = TimeSpan.FromMinutes(10), ScheduledDate = Day, Status = TaskItemStatus.Completed, IsPinned = true,
            TimesPostponed = 1, RecurrenceRuleId = Rule, IcsUid = "u@3mt", IsFavoriteSource = true, CreatedAt = t0, UpdatedAt = t0.AddHours(1),
        });
        await p.Tasks.UpsertAsync(new TaskItem { Id = Task2, Title = "Otra", LevelId = Must, CreatedAt = t0.AddMinutes(5), UpdatedAt = t0.AddMinutes(5) });
        await p.Days.UpsertAssignmentAsync(new DayAssignment(new Guid("00000000-0000-0000-0000-0000000000a1"), Task1, Day, AssignmentOutcome.Completed, t0.AddHours(2), 0));
        await p.Days.UpsertAssignmentAsync(new DayAssignment(new Guid("00000000-0000-0000-0000-0000000000a2"), Task2, Day, AssignmentOutcome.NotCompleted, null, 1));
        await p.Days.UpsertLogAsync(new DayLog(Day, t0, t0.AddHours(2), true, t0.AddDays(1)));
        await p.Days.UpsertLogAsync(new DayLog(Day.AddDays(1), null, null, null, null));
    }

    public static async Task<string> SnapshotAsync(PersistenceSet p) => await Service(p).ExportAsync();
}

/// <summary>Contrato del backup: igual para memoria, SQLite e IndexedDB (cada una aporta <see cref="PersistenceContract.Create"/>).</summary>
public abstract class BackupContract : PersistenceContract
{
    private static string FixturePath => Path.Combine(AppContext.BaseDirectory, "Fixtures", "backup-v1.json");

    [Fact]
    public async Task Export_contains_every_section_with_schema_version()
    {
        var p = Create();
        await BackupSeed.FillAsync(p);

        var root = JsonNode.Parse(await BackupSeed.Service(p).ExportAsync())!.AsObject();

        Assert.Equal(1, (int)root["schemaVersion"]!);
        Assert.Equal(BackupSeed.Now, (DateTimeOffset)root["exportedAt"]!);
        Assert.Single(root["levels"]!.AsArray());
        Assert.Single(root["categories"]!.AsArray());
        Assert.Single(root["favorites"]!.AsArray());
        Assert.Single(root["recurrences"]!.AsArray());
        Assert.Equal(2, root["tasks"]!.AsArray().Count);
        Assert.Equal(2, root["assignments"]!.AsArray().Count);
        Assert.Equal(2, root["dayLogs"]!.AsArray().Count);
        Assert.Equal("dark", (string)root["settings"]!["theme"]!);
        Assert.Equal("completed", (string)root["tasks"]![0]!["status"]!);
        Assert.True((bool)root["recurrences"]![0]!["isPaused"]!);
    }

    [Fact]
    public async Task Export_then_restore_into_empty_store_roundtrips_everything()
    {
        var source = Create();
        await BackupSeed.FillAsync(source);
        var json = await BackupSeed.SnapshotAsync(source);

        var target = Create();
        await BackupSeed.Service(target).RestoreAsync(json, RestoreMode.Replace);

        Assert.Equal(json, await BackupSeed.SnapshotAsync(target));
        Assert.Equal(BackupSeed.Settings, target.Settings.Load());
        Assert.Equal(2, (await target.Tasks.ListAsync()).Count);
        Assert.NotNull(await target.Days.GetLogAsync(BackupSeed.Day));
    }

    [Fact]
    public async Task Replace_discards_everything_not_in_the_backup()
    {
        var source = Create();
        await BackupSeed.FillAsync(source);
        var json = await BackupSeed.SnapshotAsync(source);

        var target = Create();
        var extraLevel = new Level(Guid.NewGuid(), "Extra", "#000", "x", 0, 0, null, 9, true);
        await target.Config.UpsertLevelAsync(extraLevel);
        await target.Config.UpsertCategoryAsync(new Category(Guid.NewGuid(), "Otra", "#111"));
        await target.Config.UpsertFavoriteAsync(new FavoriteTemplate(Guid.NewGuid(), "Vieja", extraLevel.Id, null, null, null, 0));
        await target.Config.UpsertRecurrenceAsync(new RecurrenceRule(Guid.NewGuid(), "Vieja", extraLevel.Id, null, null,
            RecurrenceFrequency.Daily, 1, [], null, BackupSeed.Day, null, null));
        await target.Tasks.UpsertAsync(NewTask("Vieja"));
        await target.Days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), Guid.NewGuid(), BackupSeed.Day.AddDays(-5), AssignmentOutcome.Planned, null, 0));
        await target.Days.UpsertLogAsync(new DayLog(BackupSeed.Day.AddDays(-5), null, null, true, T0));
        target.Settings.Save(new AppSettings { StrictMode = false, Theme = ThemeMode.Light });

        await BackupSeed.Service(target).RestoreAsync(json, RestoreMode.Replace);

        Assert.Equal(json, await BackupSeed.SnapshotAsync(target));
    }

    [Fact]
    public async Task Merge_upserts_by_id_and_keeps_the_rest_including_settings()
    {
        var source = Create();
        await BackupSeed.FillAsync(source);
        var json = await BackupSeed.SnapshotAsync(source);

        var target = Create();
        var stale = NewTask("Antigua versión") with { Id = BackupSeed.Task1, LevelId = BackupSeed.Must };
        var local = NewTask("Solo local");
        var localFav = new FavoriteTemplate(Guid.NewGuid(), "Local", BackupSeed.Must, null, null, null, 0);
        await target.Tasks.UpsertAsync(stale);
        await target.Tasks.UpsertAsync(local);
        await target.Config.UpsertFavoriteAsync(localFav);
        await target.Days.UpsertLogAsync(new DayLog(BackupSeed.Day, null, null, false, null)); // se pisa por fecha
        var localSettings = new AppSettings { Theme = ThemeMode.Light, StrictMode = false };
        target.Settings.Save(localSettings);

        await BackupSeed.Service(target).RestoreAsync(json, RestoreMode.Merge);

        var tasks = await target.Tasks.ListAsync();
        Assert.Equal(3, tasks.Count);
        Assert.Equal("Informe", (await target.Tasks.GetAsync(BackupSeed.Task1))!.Title);
        Assert.Contains(tasks, t => t.Id == local.Id);
        Assert.Equal(2, (await target.Config.ListFavoritesAsync()).Count);
        Assert.True((await target.Days.GetLogAsync(BackupSeed.Day))!.Fulfilled);
        Assert.Equal(localSettings, target.Settings.Load());
    }

    [Fact]
    public async Task Failed_restore_leaves_existing_data_untouched()
    {
        var source = Create();
        await BackupSeed.FillAsync(source);
        var json = await BackupSeed.SnapshotAsync(source);

        var target = Create();
        var keep = NewTask("Debe sobrevivir");
        await target.Tasks.UpsertAsync(keep);
        target.Settings.Save(new AppSettings { Theme = ThemeMode.Light });
        var failing = new BackupService(new FailingTasks(target.Tasks), target.Days, target.Config, target.Settings, target.Unit, new FixedTime(BackupSeed.Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.RestoreAsync(json, RestoreMode.Replace));

        Assert.Equal([keep], await target.Tasks.ListAsync());
        Assert.Empty(await target.Config.ListLevelsAsync());
        Assert.Equal(ThemeMode.Light, target.Settings.Load().Theme);
    }

    [Fact]
    public async Task V1_fixture_restores_into_the_current_schema()
    {
        var p = Create();

        await BackupSeed.Service(p).RestoreAsync(await File.ReadAllTextAsync(FixturePath), RestoreMode.Replace);

        var settings = p.Settings.Load();
        Assert.Equal(new TimeOnly(4, 30), settings.DayStartsAt);
        Assert.True(settings.StrictMode);
        Assert.False(settings.AutoAddScheduled);
        Assert.Equal(ThemeMode.Dark, settings.Theme);
        Assert.Equal(DayOfWeek.Sunday, settings.FirstDayOfWeek);
        Assert.True(settings.ExportVTodo);

        Assert.Equal(["Must", "Nice"], (await p.Config.ListLevelsAsync()).Select(l => l.Name));
        Assert.Null((await p.Config.ListLevelsAsync())[1].Max);
        Assert.Equal("Trabajo", Assert.Single(await p.Config.ListCategoriesAsync()).Name);
        var fav = Assert.Single(await p.Config.ListFavoritesAsync());
        Assert.Equal((new TimeOnly(21, 0), TimeSpan.FromMinutes(15), 4), (fav.DueTime, fav.ReminderOffset, fav.UseCount));
        var rule = Assert.Single(await p.Config.ListRecurrencesAsync());
        Assert.Equal(RecurrenceFrequency.Weekly, rule.Frequency);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], rule.DaysOfWeek);
        Assert.True(rule.IsPaused);
        Assert.Equal(new DateOnly(2026, 10, 20), rule.LastGeneratedDate);

        var tasks = await p.Tasks.ListAsync();
        Assert.Equal(["Escribir informe", "Llamar al banco"], tasks.Select(t => t.Title));
        Assert.Equal(TaskItemStatus.Completed, tasks[0].Status);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero), tasks[0].CreatedAt);
        Assert.True(tasks[1].IsFavoriteSource);

        var assignments = await p.Days.GetAssignmentsAsync(new DateOnly(2026, 10, 21));
        Assert.Equal([AssignmentOutcome.Completed, AssignmentOutcome.NotCompleted], assignments.Select(a => a.Outcome));
        var log = await p.Days.GetLogAsync(new DateOnly(2026, 10, 21));
        Assert.Equal(true, log!.Fulfilled);
        Assert.NotNull(log.ClosedAt);
    }

    private sealed class FailingTasks(ITaskRepository inner) : ITaskRepository
    {
        public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct = default) => inner.GetAsync(id, ct);
        public Task<IReadOnlyList<TaskItem>> ListAsync(TaskItemStatus? status = null, CancellationToken ct = default) => inner.ListAsync(status, ct);
        public Task UpsertAsync(TaskItem task, CancellationToken ct = default) => throw new InvalidOperationException("fallo simulado");
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => inner.DeleteAsync(id, ct);
    }
}
