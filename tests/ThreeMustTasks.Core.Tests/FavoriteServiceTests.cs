using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class FavoriteServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 21);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IServiceProvider _sp;
    private readonly IConfigRepository _config;
    private readonly ITaskRepository _tasks;
    private readonly IDayRepository _days;
    private readonly TodayService _today;
    private readonly FavoriteService _favorites;
    private readonly List<DateOnly> _events = [];

    public FavoriteServiceTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<FavoriteService>()
            .BuildServiceProvider();
        _config = _sp.GetRequiredService<IConfigRepository>();
        _tasks = _sp.GetRequiredService<ITaskRepository>();
        _days = _sp.GetRequiredService<IDayRepository>();
        _today = _sp.GetRequiredService<TodayService>();
        _favorites = _sp.GetRequiredService<FavoriteService>();
        _sp.GetRequiredService<PlanEvents>().PlanChanged += _events.Add;
        _today.GetLevelsAsync().GetAwaiter().GetResult();
    }

    private async Task<FavoriteTemplate> Fav(string title, Guid level, int uses = 0, Guid? category = null,
        TimeOnly? time = null, TimeSpan? reminder = null)
    {
        var f = new FavoriteTemplate(Guid.NewGuid(), title, level, category, time, reminder, uses);
        await _config.UpsertFavoriteAsync(f);
        return f;
    }

    private async Task<Category> NewCategory(string name)
    {
        var cat = new Category(Guid.NewGuid(), name, "#ffffff");
        await _config.UpsertCategoryAsync(cat);
        return cat;
    }

    [Fact]
    public async Task List_orders_by_use_count_desc_then_title()
    {
        await Fav("b", DefaultLevels.MustId, 2);
        await Fav("c", DefaultLevels.MustId, 9);
        await Fav("a", DefaultLevels.MustId, 2);
        await Fav("z", DefaultLevels.MaybeId, 0);

        var items = await _favorites.ListAsync();

        Assert.Equal(["c", "a", "b", "z"], items.Select(i => i.Favorite.Title));
    }

    [Fact]
    public async Task List_skips_favorites_of_disabled_levels()
    {
        await Fav("visible", DefaultLevels.MustId);
        await Fav("oculta", DefaultLevels.MaybeId);
        var maybe = (await _config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MaybeId);
        await _config.UpsertLevelAsync(maybe with { Enabled = false });

        Assert.Equal(["visible"], (await _favorites.ListAsync()).Select(i => i.Favorite.Title));
    }

    [Fact]
    public async Task Mark_creates_a_template_from_an_existing_task_and_flags_the_source()
    {
        var cat = await NewCategory("Casa");
        var task = (await _today.AddAsync(new NewTaskRequest("Regar", DefaultLevels.RecommendedId, CategoryId: cat.Id,
            DueTime: new TimeOnly(8, 30), ReminderOffset: TimeSpan.FromMinutes(15)))).Task!;

        var outcome = await _favorites.MarkAsync(task.Id);

        Assert.Equal(MarkFavoriteOutcome.Marked, outcome);
        var fav = Assert.Single(await _config.ListFavoritesAsync());
        Assert.Equal(("Regar", DefaultLevels.RecommendedId, cat.Id, new TimeOnly(8, 30), TimeSpan.FromMinutes(15), 0),
            (fav.Title, fav.LevelId, fav.CategoryId, fav.DueTime, fav.ReminderOffset, fav.UseCount));
        Assert.True((await _tasks.GetAsync(task.Id))!.IsFavoriteSource);
    }

    [Fact]
    public async Task Mark_twice_does_not_duplicate_and_unknown_task_is_not_found()
    {
        var task = (await _today.AddAsync(new NewTaskRequest("Leer", DefaultLevels.MustId))).Task!;

        Assert.Equal(MarkFavoriteOutcome.Marked, await _favorites.MarkAsync(task.Id));
        Assert.Equal(MarkFavoriteOutcome.AlreadyFavorite, await _favorites.MarkAsync(task.Id));
        Assert.Single(await _config.ListFavoritesAsync());
        Assert.Equal(MarkFavoriteOutcome.NotFound, await _favorites.MarkAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Mark_reuses_an_existing_template_with_same_title_and_level()
    {
        await Fav("leer", DefaultLevels.MustId, 3);
        var task = (await _today.AddAsync(new NewTaskRequest("Leer", DefaultLevels.MustId))).Task!;

        Assert.Equal(MarkFavoriteOutcome.AlreadyFavorite, await _favorites.MarkAsync(task.Id));
        Assert.Single(await _config.ListFavoritesAsync());
        Assert.True((await _tasks.GetAsync(task.Id))!.IsFavoriteSource);
    }

    [Fact]
    public async Task AddToToday_creates_the_task_in_the_plan_copying_the_template_and_counts_a_use()
    {
        var cat = await NewCategory("Salud");
        var fav = await Fav("Correr", DefaultLevels.MustId, 1, cat.Id, new TimeOnly(7, 0), TimeSpan.FromMinutes(10));
        _events.Clear();

        var result = await _favorites.AddToTodayAsync(fav.Id);

        Assert.True(result.Succeeded);
        var task = (await _tasks.GetAsync(result.Task!.Id))!;
        Assert.Equal(("Correr", cat.Id, new TimeOnly(7, 0), TimeSpan.FromMinutes(10), Today),
            (task.Title, task.CategoryId, task.DueTime, task.ReminderOffset, task.ScheduledDate));
        Assert.True(task.IsFavoriteSource);
        var assignment = Assert.Single(await _days.GetAssignmentsAsync(Today));
        Assert.Equal(task.Id, assignment.TaskId);
        Assert.Equal(2, Assert.Single(await _config.ListFavoritesAsync()).UseCount);
        Assert.Contains(Today, _events);
    }

    [Fact]
    public async Task Use_count_moves_the_favorite_up_the_list()
    {
        await Fav("a", DefaultLevels.MaybeId, 0);
        var b = await Fav("b", DefaultLevels.MaybeId, 0);

        await _favorites.AddToTodayAsync(b.Id);

        Assert.Equal(["b", "a"], (await _favorites.ListAsync()).Select(i => i.Favorite.Title));
    }

    [Fact]
    public async Task AddToToday_when_level_is_full_adds_nothing_and_does_not_count_a_use()
    {
        var must = (await _config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MustId);
        await _config.UpsertLevelAsync(must with { Max = 1 });
        await _today.AddAsync(new NewTaskRequest("ocupa", DefaultLevels.MustId));
        var fav = await Fav("extra", DefaultLevels.MustId, 5);

        var result = await _favorites.AddToTodayAsync(fav.Id);

        Assert.Equal(AddFavoriteOutcome.LevelFull, result.Outcome);
        Assert.Single(await _days.GetAssignmentsAsync(Today));
        Assert.Equal(5, Assert.Single(await _config.ListFavoritesAsync()).UseCount);
    }

    [Fact]
    public async Task AddToToday_unknown_favorite_is_not_found()
    {
        Assert.Equal(AddFavoriteOutcome.NotFound, (await _favorites.AddToTodayAsync(Guid.NewGuid())).Outcome);
    }
}
