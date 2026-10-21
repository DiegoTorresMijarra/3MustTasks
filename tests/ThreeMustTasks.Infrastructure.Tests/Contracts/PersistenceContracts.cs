using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Infrastructure.Tests.Contracts;

/// <summary>Conjunto de puertos de persistencia creado limpio para cada test.</summary>
public sealed record PersistenceSet(
    ITaskRepository Tasks,
    IDayRepository Days,
    IConfigRepository Config,
    ISettingsStore Settings,
    IUnitOfWork Unit);

/// <summary>
/// Suite de contrato común a todas las implementaciones de persistencia (memoria, SQLite, IndexedDB).
/// Cada implementación hereda de las clases abstractas y solo aporta <see cref="Create"/>.
/// </summary>
public abstract class PersistenceContract
{
    protected abstract PersistenceSet Create();

    protected static readonly DateTimeOffset T0 = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    protected static TaskItem NewTask(string title, TaskItemStatus status = TaskItemStatus.Active, int minutes = 0) => new()
    {
        Title = title,
        LevelId = Guid.NewGuid(),
        Status = status,
        CreatedAt = T0.AddMinutes(minutes),
        UpdatedAt = T0.AddMinutes(minutes),
    };
}

public abstract class TaskRepositoryContract : PersistenceContract
{
    [Fact]
    public async Task Get_unknown_id_returns_null()
    {
        var p = Create();
        Assert.Null(await p.Tasks.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Upsert_then_get_roundtrips_all_fields()
    {
        var p = Create();
        var task = NewTask("Escribir informe") with
        {
            Notes = "borrador",
            CategoryId = Guid.NewGuid(),
            DueTime = new TimeOnly(17, 30),
            ReminderOffset = TimeSpan.FromMinutes(15),
            ScheduledDate = new DateOnly(2026, 10, 25),
            IsPinned = true,
            TimesPostponed = 2,
            IcsUid = "abc@3musttasks",
        };

        await p.Tasks.UpsertAsync(task);
        await p.Unit.SaveChangesAsync();

        Assert.Equal(task, await p.Tasks.GetAsync(task.Id));
    }

    [Fact]
    public async Task Upsert_existing_id_replaces_instead_of_duplicating()
    {
        var p = Create();
        var task = NewTask("Uno");
        await p.Tasks.UpsertAsync(task);
        await p.Tasks.UpsertAsync(task with { Title = "Dos" });

        var all = await p.Tasks.ListAsync();
        var single = Assert.Single(all);
        Assert.Equal("Dos", single.Title);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var p = Create();
        await p.Tasks.UpsertAsync(NewTask("a", TaskItemStatus.Active, 1));
        await p.Tasks.UpsertAsync(NewTask("b", TaskItemStatus.Archived, 2));
        await p.Tasks.UpsertAsync(NewTask("c", TaskItemStatus.Active, 3));

        Assert.Equal(["a", "c"], (await p.Tasks.ListAsync(TaskItemStatus.Active)).Select(t => t.Title));
        Assert.Equal(["b"], (await p.Tasks.ListAsync(TaskItemStatus.Archived)).Select(t => t.Title));
        Assert.Equal(3, (await p.Tasks.ListAsync()).Count);
    }

    [Fact]
    public async Task List_is_ordered_by_creation()
    {
        var p = Create();
        await p.Tasks.UpsertAsync(NewTask("tercera", minutes: 30));
        await p.Tasks.UpsertAsync(NewTask("primera", minutes: 10));
        await p.Tasks.UpsertAsync(NewTask("segunda", minutes: 20));

        Assert.Equal(["primera", "segunda", "tercera"], (await p.Tasks.ListAsync()).Select(t => t.Title));
    }

    [Fact]
    public async Task Delete_reports_whether_it_existed()
    {
        var p = Create();
        var task = NewTask("x");
        await p.Tasks.UpsertAsync(task);

        Assert.True(await p.Tasks.DeleteAsync(task.Id));
        Assert.False(await p.Tasks.DeleteAsync(task.Id));
        Assert.Null(await p.Tasks.GetAsync(task.Id));
    }
}

public abstract class DayRepositoryContract : PersistenceContract
{
    private static readonly DateOnly Day = new(2026, 10, 21);

    private static DayAssignment Assignment(DateOnly day, int order, AssignmentOutcome outcome = AssignmentOutcome.Planned) =>
        new(Guid.NewGuid(), Guid.NewGuid(), day, outcome, null, order);

    [Fact]
    public async Task Assignments_are_filtered_by_day_and_ordered()
    {
        var p = Create();
        var second = Assignment(Day, 2);
        var first = Assignment(Day, 1);
        var other = Assignment(Day.AddDays(1), 0);
        await p.Days.UpsertAssignmentAsync(second);
        await p.Days.UpsertAssignmentAsync(first);
        await p.Days.UpsertAssignmentAsync(other);

        var result = await p.Days.GetAssignmentsAsync(Day);

        Assert.Equal([first, second], result);
    }

    [Fact]
    public async Task Upsert_assignment_replaces_by_id()
    {
        var p = Create();
        var a = Assignment(Day, 0);
        await p.Days.UpsertAssignmentAsync(a);
        var done = a with { Outcome = AssignmentOutcome.Completed, CompletedAt = T0 };
        await p.Days.UpsertAssignmentAsync(done);

        Assert.Equal([done], await p.Days.GetAssignmentsAsync(Day));
    }

    [Fact]
    public async Task Delete_assignment_reports_whether_it_existed()
    {
        var p = Create();
        var a = Assignment(Day, 0);
        await p.Days.UpsertAssignmentAsync(a);

        Assert.True(await p.Days.DeleteAssignmentAsync(a.Id));
        Assert.False(await p.Days.DeleteAssignmentAsync(a.Id));
        Assert.Empty(await p.Days.GetAssignmentsAsync(Day));
    }

    [Fact]
    public async Task DayLog_roundtrips_and_is_keyed_by_date()
    {
        var p = Create();
        Assert.Null(await p.Days.GetLogAsync(Day));

        var log = new DayLog(Day, T0, null, null, null);
        await p.Days.UpsertLogAsync(log);
        Assert.Equal(log, await p.Days.GetLogAsync(Day));

        var closed = log with { Fulfilled = true, ClosedAt = T0.AddHours(20) };
        await p.Days.UpsertLogAsync(closed);
        Assert.Equal(closed, await p.Days.GetLogAsync(Day));
        Assert.Null(await p.Days.GetLogAsync(Day.AddDays(1)));
    }

    [Fact]
    public async Task Unclosed_dates_are_days_before_the_limit_with_plan_or_open_log_and_no_closed_log()
    {
        var p = Create();
        var d = (int n) => Day.AddDays(n);
        await p.Days.UpsertAssignmentAsync(Assignment(d(0), 0));                                   // Planned: abierto
        await p.Days.UpsertAssignmentAsync(Assignment(d(1), 0, AssignmentOutcome.Completed));      // Completed: abierto
        await p.Days.UpsertAssignmentAsync(Assignment(d(2), 0, AssignmentOutcome.Removed));        // solo Removed: sin plan
        await p.Days.UpsertLogAsync(new DayLog(d(3), T0, null, null, null));                       // log abierto sin asignaciones
        await p.Days.UpsertAssignmentAsync(Assignment(d(4), 0));                                   // con log cerrado: no
        await p.Days.UpsertLogAsync(new DayLog(d(4), null, null, true, T0));
        await p.Days.UpsertAssignmentAsync(Assignment(d(9), 0));                                   // no anterior al límite

        Assert.Equal([d(0), d(1), d(3)], await p.Days.ListUnclosedDatesAsync(d(5)));
    }

    [Fact]
    public async Task Closed_logs_are_listed_in_range_by_date()
    {
        var p = Create();
        await p.Days.UpsertLogAsync(new DayLog(Day.AddDays(2), null, null, false, T0));
        await p.Days.UpsertLogAsync(new DayLog(Day, null, null, true, T0));
        await p.Days.UpsertLogAsync(new DayLog(Day.AddDays(1), null, null, null, null)); // abierto: no
        await p.Days.UpsertLogAsync(new DayLog(Day.AddDays(9), null, null, true, T0));   // fuera de rango

        var result = await p.Days.ListClosedLogsAsync(Day, Day.AddDays(3));

        Assert.Equal([Day, Day.AddDays(2)], result.Select(l => l.LogicalDate));
    }
}

public abstract class UnitOfWorkContract : PersistenceContract
{
    [Fact]
    public async Task Transaction_commits_all_writes_when_work_succeeds()
    {
        var p = Create();
        var task = NewTask("A");
        var log = new DayLog(new DateOnly(2026, 10, 20), null, null, true, T0);

        await p.Unit.ExecuteInTransactionAsync(async ct =>
        {
            await p.Tasks.UpsertAsync(task, ct);
            await p.Days.UpsertLogAsync(log, ct);
            // Lo escrito dentro es visible dentro de la transacción.
            Assert.Equal(task, await p.Tasks.GetAsync(task.Id, ct));
        });

        Assert.Equal(task, await p.Tasks.GetAsync(task.Id));
        Assert.Equal(log, await p.Days.GetLogAsync(log.LogicalDate));
    }

    [Fact]
    public async Task Transaction_rolls_back_every_write_and_rethrows_when_work_fails()
    {
        var p = Create();
        var existing = NewTask("Existente");
        await p.Tasks.UpsertAsync(existing);
        var created = NewTask("Nueva");
        var day = new DateOnly(2026, 10, 20);

        await Assert.ThrowsAsync<InvalidOperationException>(() => p.Unit.ExecuteInTransactionAsync(async ct =>
        {
            await p.Tasks.UpsertAsync(created, ct);
            await p.Tasks.UpsertAsync(existing with { Title = "Cambiada" }, ct);
            await p.Days.UpsertLogAsync(new DayLog(day, null, null, true, T0), ct);
            throw new InvalidOperationException("fallo a mitad");
        }));

        Assert.Null(await p.Tasks.GetAsync(created.Id));
        Assert.Equal("Existente", (await p.Tasks.GetAsync(existing.Id))!.Title);
        Assert.Null(await p.Days.GetLogAsync(day));
    }

    [Fact]
    public async Task Writes_after_a_failed_transaction_still_work()
    {
        var p = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            p.Unit.ExecuteInTransactionAsync(_ => throw new InvalidOperationException()));

        var task = NewTask("Después");
        await p.Tasks.UpsertAsync(task);
        Assert.Equal(task, await p.Tasks.GetAsync(task.Id));
    }
}

public abstract class ConfigRepositoryContract : PersistenceContract
{
    [Fact]
    public async Task Levels_are_returned_ordered_by_Order()
    {
        var p = Create();
        var b = new Level(Guid.NewGuid(), "Should", "#ff0", "star", 1, 0, 3, 2, true);
        var a = new Level(Guid.NewGuid(), "Must", "#f00", "flag", 3, 3, null, 1, true);
        await p.Config.UpsertLevelAsync(b);
        await p.Config.UpsertLevelAsync(a);

        Assert.Equal([a, b], await p.Config.ListLevelsAsync());
    }

    [Fact]
    public async Task Upsert_level_replaces_by_id()
    {
        var p = Create();
        var level = new Level(Guid.NewGuid(), "Must", "#f00", "flag", 3, 3, null, 1, true);
        await p.Config.UpsertLevelAsync(level);
        var renamed = level with { Name = "Imprescindible", Enabled = false };
        await p.Config.UpsertLevelAsync(renamed);

        Assert.Equal([renamed], await p.Config.ListLevelsAsync());
    }

    [Fact]
    public async Task Delete_level_removes_it_and_reports_whether_it_existed()
    {
        var p = Create();
        var level = new Level(Guid.NewGuid(), "Must", "#f00", "flag", 3, 3, null, 1, true);
        await p.Config.UpsertLevelAsync(level);

        Assert.True(await p.Config.DeleteLevelAsync(level.Id));
        Assert.False(await p.Config.DeleteLevelAsync(level.Id));
        Assert.Empty(await p.Config.ListLevelsAsync());
    }

    [Fact]
    public async Task Categories_roundtrip_and_delete()
    {
        var p = Create();
        var work = new Category(Guid.NewGuid(), "Trabajo", "#00f");
        await p.Config.UpsertCategoryAsync(work);

        Assert.Equal([work], await p.Config.ListCategoriesAsync());
        Assert.True(await p.Config.DeleteCategoryAsync(work.Id));
        Assert.False(await p.Config.DeleteCategoryAsync(work.Id));
        Assert.Empty(await p.Config.ListCategoriesAsync());
    }

    [Fact]
    public async Task Favorites_roundtrip()
    {
        var p = Create();
        var fav = new FavoriteTemplate(Guid.NewGuid(), "Correr", Guid.NewGuid(), null, new TimeOnly(7, 0), TimeSpan.FromMinutes(10), 4);
        await p.Config.UpsertFavoriteAsync(fav);

        Assert.Equal([fav], await p.Config.ListFavoritesAsync());
    }

    [Fact]
    public async Task Recurrences_roundtrip_including_days_of_week()
    {
        var p = Create();
        var rule = new RecurrenceRule(
            Guid.NewGuid(), "Revisar correo", Guid.NewGuid(), null, null,
            RecurrenceFrequency.Weekly, 1, [DayOfWeek.Monday, DayOfWeek.Thursday], null,
            new DateOnly(2026, 10, 1), null, null);
        await p.Config.UpsertRecurrenceAsync(rule);

        var stored = Assert.Single(await p.Config.ListRecurrencesAsync());
        Assert.Equal(rule.Id, stored.Id);
        Assert.Equal(rule.DaysOfWeek, stored.DaysOfWeek);
        Assert.Equal(rule.Frequency, stored.Frequency);
    }
}

public abstract class SettingsStoreContract : PersistenceContract
{
    [Fact]
    public void Load_without_saved_settings_returns_defaults()
    {
        var p = Create();
        Assert.Equal(new AppSettings(), p.Settings.Load());
    }

    [Fact]
    public void Save_then_load_roundtrips()
    {
        var p = Create();
        var settings = new AppSettings
        {
            Theme = ThemeMode.Dark,
            DayStartsAt = new TimeOnly(4, 0),
            StrictMode = true,
            AutoAddScheduled = false,
            FirstDayOfWeek = DayOfWeek.Sunday,
            ExportVTodo = true,
        };

        p.Settings.Save(settings);

        Assert.Equal(settings, p.Settings.Load());
    }
}
