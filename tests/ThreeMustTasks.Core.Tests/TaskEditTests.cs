using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class TaskEditTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly IServiceProvider _sp;
    private readonly TodayService _today;
    private readonly TaskEditService _edit;
    private readonly CategoryService _cats;
    private readonly List<DateOnly> _events = [];

    public TaskEditTests()
    {
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero)))
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<TaskEditService>()
            .AddSingleton<CategoryService>()
            .BuildServiceProvider();
        _today = _sp.GetRequiredService<TodayService>();
        _edit = _sp.GetRequiredService<TaskEditService>();
        _cats = _sp.GetRequiredService<CategoryService>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += _events.Add;
    }

    private async Task<TaskItem> AddToday(string title, Guid level) =>
        (await _today.AddAsync(new NewTaskRequest(title, level))).Task!;

    private async Task<TaskItem> AddPool(string title, Guid level) =>
        (await _today.AddAsync(new NewTaskRequest(title, level, AddDestination.Pool))).Task!;

    private static EditTaskRequest Req(TaskItem t, TaskWhen when = TaskWhen.Today, DateOnly? date = null) =>
        new(t.Id, t.Title, t.LevelId, t.CategoryId, t.DueTime, t.ReminderOffset, t.Notes, when, date);

    private async Task SetMax(Guid levelId, int? max)
    {
        await _today.GetLevelsAsync();
        var config = _sp.GetRequiredService<IConfigRepository>();
        var l = (await config.ListLevelsAsync()).Single(x => x.Id == levelId);
        await config.UpsertLevelAsync(l with { Max = max });
    }

    [Fact]
    public async Task Get_reports_where_the_task_lives_and_unknown_is_null()
    {
        var plan = await AddToday("hoy", DefaultLevels.MustId);
        var pool = await AddPool("pool", DefaultLevels.MustId);

        Assert.Null(await _edit.GetAsync(Guid.NewGuid()));
        Assert.Equal(TaskWhen.Today, (await _edit.GetAsync(plan.Id))!.When);
        Assert.Equal(TaskWhen.Pool, (await _edit.GetAsync(pool.Id))!.When);
        Assert.False((await _edit.GetAsync(plan.Id))!.ReadOnly);
    }

    [Fact]
    public async Task Update_saves_all_fields_trimmed()
    {
        var cat = (await _cats.SaveAsync(null, "Trabajo", "#2559c7")).Category!;
        var t = await AddToday("viejo", DefaultLevels.RecommendedId);

        var r = await _edit.UpdateAsync(Req(t) with
        {
            Title = "  Nuevo  ", CategoryId = cat.Id, DueTime = new TimeOnly(10, 30),
            ReminderOffset = TimeSpan.FromMinutes(15), Notes = "  notas  ",
        });

        Assert.True(r.Succeeded);
        var stored = (await _sp.GetRequiredService<ITaskRepository>().GetAsync(t.Id))!;
        Assert.Equal("Nuevo", stored.Title);
        Assert.Equal(cat.Id, stored.CategoryId);
        Assert.Equal(new TimeOnly(10, 30), stored.DueTime);
        Assert.Equal(TimeSpan.FromMinutes(15), stored.ReminderOffset);
        Assert.Equal("notas", stored.Notes);
        Assert.Equal(Today, stored.ScheduledDate);
    }

    [Fact]
    public async Task Blank_notes_become_null_and_reminder_is_dropped_without_time_validation_first()
    {
        var t = await AddToday("a", DefaultLevels.MustId);

        var withReminderNoTime = await _edit.UpdateAsync(Req(t) with { ReminderOffset = TimeSpan.FromMinutes(5) });
        Assert.Equal(EditTaskOutcome.ReminderWithoutTime, withReminderNoTime.Outcome);

        var ok = await _edit.UpdateAsync(Req(t) with { Notes = "   " });
        Assert.Null(ok.Task!.Notes);
    }

    [Theory]
    [InlineData("", EditTaskOutcome.InvalidTitle)]
    [InlineData("   ", EditTaskOutcome.InvalidTitle)]
    public async Task Invalid_title_is_rejected(string title, EditTaskOutcome expected)
    {
        var t = await AddToday("a", DefaultLevels.MustId);

        Assert.Equal(expected, (await _edit.UpdateAsync(Req(t) with { Title = title })).Outcome);
    }

    [Fact]
    public async Task Length_limits_unknown_references_and_dates_are_validated()
    {
        var t = await AddToday("a", DefaultLevels.MustId);

        Assert.Equal(EditTaskOutcome.TitleTooLong, (await _edit.UpdateAsync(Req(t) with { Title = new string('x', 121) })).Outcome);
        Assert.Equal(EditTaskOutcome.NotesTooLong, (await _edit.UpdateAsync(Req(t) with { Notes = new string('x', 1001) })).Outcome);
        Assert.Equal(EditTaskOutcome.UnknownLevel, (await _edit.UpdateAsync(Req(t) with { LevelId = Guid.NewGuid() })).Outcome);
        Assert.Equal(EditTaskOutcome.UnknownCategory, (await _edit.UpdateAsync(Req(t) with { CategoryId = Guid.NewGuid() })).Outcome);
        Assert.Equal(EditTaskOutcome.NotFound, (await _edit.UpdateAsync(Req(t) with { TaskId = Guid.NewGuid() })).Outcome);
        Assert.Equal(EditTaskOutcome.InvalidDate, (await _edit.UpdateAsync(Req(t, TaskWhen.OtherDay))).Outcome);
        Assert.Equal(EditTaskOutcome.InvalidDate, (await _edit.UpdateAsync(Req(t, TaskWhen.OtherDay, Today))).Outcome);
        Assert.Equal(EditTaskOutcome.ReminderWithoutTime,
            (await _edit.UpdateAsync(Req(t) with { DueTime = new TimeOnly(8, 0), ReminderOffset = TimeSpan.FromMinutes(-5) })).Outcome);
    }

    [Fact]
    public async Task Changing_level_revalidates_the_quota_and_keeps_the_task_in_place_when_full()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await AddToday("must", DefaultLevels.MustId);
        var other = await AddToday("rec", DefaultLevels.RecommendedId);

        var r = await _edit.UpdateAsync(Req(other) with { LevelId = DefaultLevels.MustId });

        Assert.Equal(EditTaskOutcome.LevelFull, r.Outcome);
        Assert.Equal(DefaultLevels.RecommendedId, (await _sp.GetRequiredService<ITaskRepository>().GetAsync(other.Id))!.LevelId);
    }

    [Fact]
    public async Task Editing_a_task_in_a_full_level_without_changing_it_is_allowed()
    {
        await SetMax(DefaultLevels.MustId, 1);
        var must = await AddToday("must", DefaultLevels.MustId);

        Assert.True((await _edit.UpdateAsync(Req(must) with { Title = "must 2" })).Succeeded);
    }

    [Fact]
    public async Task Changing_level_moves_the_task_group_and_updates_the_minimums()
    {
        var t = await AddToday("a", DefaultLevels.RecommendedId);
        _events.Clear();

        Assert.True((await _edit.UpdateAsync(Req(t) with { LevelId = DefaultLevels.MustId })).Succeeded);

        var view = await _today.GetAsync();
        Assert.Equal(["a"], view.Groups.Single(g => g.Level.Id == DefaultLevels.MustId).Tasks.Select(x => x.Task.Title));
        Assert.Single(_events);
    }

    [Fact]
    public async Task Moving_a_pool_task_to_today_checks_quota_and_creates_the_assignment()
    {
        await SetMax(DefaultLevels.MustId, 1);
        await AddToday("must", DefaultLevels.MustId);
        var pool = await AddPool("p", DefaultLevels.MustId);

        Assert.Equal(EditTaskOutcome.LevelFull, (await _edit.UpdateAsync(Req(pool))).Outcome);

        var rec = await _edit.UpdateAsync(Req(pool) with { LevelId = DefaultLevels.RecommendedId });
        Assert.True(rec.Succeeded);
        Assert.Equal(["p"], (await _today.GetAsync()).Groups.Single(g => g.Level.Id == DefaultLevels.RecommendedId).Tasks.Select(x => x.Task.Title));
        Assert.Equal(Today, rec.Task!.ScheduledDate);
    }

    [Fact]
    public async Task Moving_to_another_day_or_pool_removes_it_from_today_and_sets_the_date()
    {
        var a = await AddToday("a", DefaultLevels.MustId);
        var b = await AddToday("b", DefaultLevels.RecommendedId);

        var future = await _edit.UpdateAsync(Req(a, TaskWhen.OtherDay, Today.AddDays(3)));
        var pool = await _edit.UpdateAsync(Req(b, TaskWhen.Pool));

        Assert.Equal(Today.AddDays(3), future.Task!.ScheduledDate);
        Assert.Null(pool.Task!.ScheduledDate);
        Assert.All((await _today.GetAsync()).Groups, g => Assert.Empty(g.Tasks));
        Assert.Equal(TaskWhen.OtherDay, (await _edit.GetAsync(a.Id))!.When);
        Assert.Equal(TaskWhen.Pool, (await _edit.GetAsync(b.Id))!.When);
    }

    [Fact]
    public async Task Completed_task_today_is_editable_but_cannot_leave_today()
    {
        var t = await AddToday("a", DefaultLevels.MustId);
        var aid = (await _today.GetAsync()).Groups[0].Tasks.Single().AssignmentId;
        await _today.CompleteAsync(aid);

        Assert.True((await _edit.UpdateAsync(Req(t) with { Title = "b" })).Succeeded);
        Assert.Equal(EditTaskOutcome.CannotMoveCompleted, (await _edit.UpdateAsync(Req(t, TaskWhen.Pool))).Outcome);
        Assert.Equal(EditTaskOutcome.CannotMoveCompleted, (await _edit.UpdateAsync(Req(t, TaskWhen.OtherDay, Today.AddDays(1)))).Outcome);
    }

    [Fact]
    public async Task Past_history_and_archived_tasks_are_read_only()
    {
        var repo = _sp.GetRequiredService<ITaskRepository>();
        var days = _sp.GetRequiredService<IDayRepository>();
        var past = new TaskItem { Title = "ayer", LevelId = DefaultLevels.MustId, Status = TaskItemStatus.Completed };
        await repo.UpsertAsync(past);
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), past.Id, Today.AddDays(-1), AssignmentOutcome.Completed, null, 0));
        var archived = new TaskItem { Title = "arch", LevelId = DefaultLevels.MustId, Status = TaskItemStatus.Archived };
        await repo.UpsertAsync(archived);

        Assert.True((await _edit.GetAsync(past.Id))!.ReadOnly);
        Assert.Equal(EditTaskOutcome.ReadOnly, (await _edit.UpdateAsync(Req(past) with { Title = "x" })).Outcome);
        Assert.Equal(EditTaskOutcome.ReadOnly, (await _edit.UpdateAsync(Req(archived))).Outcome);
        Assert.Equal("ayer", (await repo.GetAsync(past.Id))!.Title);
    }

    [Fact]
    public async Task Delete_removes_task_and_todays_assignment_and_publishes_the_change()
    {
        var t = await AddToday("a", DefaultLevels.MustId);
        _events.Clear();

        Assert.True(await _edit.DeleteAsync(t.Id));

        Assert.Null(await _sp.GetRequiredService<ITaskRepository>().GetAsync(t.Id));
        Assert.Empty(await _sp.GetRequiredService<IDayRepository>().GetAssignmentsAsync(Today));
        Assert.Single(_events);
        Assert.False(await _edit.DeleteAsync(t.Id));
    }

    [Fact]
    public async Task Delete_of_a_pool_task_does_not_touch_the_plan()
    {
        var t = await AddPool("p", DefaultLevels.MustId);
        _events.Clear();

        Assert.True(await _edit.DeleteAsync(t.Id));
        Assert.Empty(_events);
    }

    // --- Categorías ---

    [Fact]
    public async Task Category_create_update_list_sorted()
    {
        var b = await _cats.SaveAsync(null, " Casa ", "#8a4fd1");
        var a = await _cats.SaveAsync(null, "Alfa", "#C9372C");
        Assert.True(b.Succeeded);
        Assert.Equal("Casa", b.Category!.Name);
        Assert.Equal("#8A4FD1", b.Category.Color);

        var renamed = await _cats.SaveAsync(b.Category.Id, "Hogar", "#112233");
        Assert.True(renamed.Succeeded);
        Assert.Equal(["Alfa", "Hogar"], (await _cats.ListAsync()).Select(c => c.Name));
        Assert.Equal(a.Category!.Id, (await _cats.ListAsync())[0].Id);
    }

    [Fact]
    public async Task Category_validation()
    {
        await _cats.SaveAsync(null, "Casa", "#000000");

        Assert.Equal(SaveCategoryOutcome.InvalidName, (await _cats.SaveAsync(null, "  ", "#000000")).Outcome);
        Assert.Equal(SaveCategoryOutcome.NameTooLong, (await _cats.SaveAsync(null, new string('x', 31), "#000000")).Outcome);
        Assert.Equal(SaveCategoryOutcome.InvalidColor, (await _cats.SaveAsync(null, "X", "rojo")).Outcome);
        Assert.Equal(SaveCategoryOutcome.InvalidColor, (await _cats.SaveAsync(null, "X", "#12345")).Outcome);
        Assert.Equal(SaveCategoryOutcome.DuplicateName, (await _cats.SaveAsync(null, "casa", "#000000")).Outcome);
        Assert.Equal(SaveCategoryOutcome.NotFound, (await _cats.SaveAsync(Guid.NewGuid(), "Y", "#000000")).Outcome);
    }

    [Fact]
    public async Task Renaming_a_category_to_its_own_name_is_not_a_duplicate()
    {
        var c = (await _cats.SaveAsync(null, "Casa", "#000000")).Category!;

        Assert.True((await _cats.SaveAsync(c.Id, "Casa", "#FFFFFF")).Succeeded);
    }

    [Fact]
    public async Task Deleting_a_category_in_use_leaves_tasks_favorites_and_recurrences_without_category()
    {
        var cat = (await _cats.SaveAsync(null, "Casa", "#000000")).Category!;
        var other = (await _cats.SaveAsync(null, "Otra", "#111111")).Category!;
        var t = await AddToday("a", DefaultLevels.MustId);
        await _edit.UpdateAsync(Req(t) with { CategoryId = cat.Id });
        var keep = await AddToday("b", DefaultLevels.RecommendedId);
        await _edit.UpdateAsync(Req(keep) with { CategoryId = other.Id });
        var config = _sp.GetRequiredService<IConfigRepository>();
        await config.UpsertFavoriteAsync(new FavoriteTemplate(Guid.NewGuid(), "f", DefaultLevels.MustId, cat.Id, null, null, 0));
        await config.UpsertRecurrenceAsync(new RecurrenceRule(Guid.NewGuid(), "r", DefaultLevels.MustId, cat.Id, null,
            RecurrenceFrequency.Daily, 1, [], null, Today, null, null));

        Assert.True(await _cats.DeleteAsync(cat.Id));

        var repo = _sp.GetRequiredService<ITaskRepository>();
        Assert.Null((await repo.GetAsync(t.Id))!.CategoryId);
        Assert.Equal(other.Id, (await repo.GetAsync(keep.Id))!.CategoryId);
        Assert.Null((await config.ListFavoritesAsync()).Single().CategoryId);
        Assert.Null((await config.ListRecurrencesAsync()).Single().CategoryId);
        Assert.Equal(["Otra"], (await _cats.ListAsync()).Select(c => c.Name));
        Assert.False(await _cats.DeleteAsync(cat.Id));
    }
}
