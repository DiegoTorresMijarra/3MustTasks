using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class LevelServiceTests
{
    private readonly IServiceProvider _sp;
    private readonly LevelService _levels;
    private readonly TodayService _today;

    public LevelServiceTests()
    {
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero)))
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<LevelService>()
            .BuildServiceProvider();
        _levels = _sp.GetRequiredService<LevelService>();
        _today = _sp.GetRequiredService<TodayService>();
    }

    private static LevelInput Input(string name = "Nuevo", int add = 0, int complete = 0, int? max = null, bool enabled = true) =>
        new(name, "#112233", "bolt", add, complete, max, enabled);

    [Fact]
    public async Task List_seeds_the_default_levels_when_empty()
    {
        var all = await _levels.ListAsync();

        Assert.Equal(["Must", "Recommended", "Maybe"], all.Select(l => l.Name));
    }

    [Fact]
    public async Task Create_appends_at_the_end_and_normalizes_the_input()
    {
        var result = await _levels.SaveAsync(null, Input("  Extra  ", add: 1, complete: 1, max: 3));

        Assert.True(result.Succeeded);
        Assert.Equal("Extra", result.Level!.Name);
        Assert.Equal(3, result.Level.Order);
        Assert.Equal(4, (await _levels.ListAsync()).Count);
    }

    [Fact]
    public async Task Update_keeps_id_and_order_and_changes_the_values()
    {
        var must = (await _levels.ListAsync())[0];

        var result = await _levels.SaveAsync(must.Id, Input("Crítico", add: 2, complete: 1, max: 4));

        Assert.True(result.Succeeded);
        var stored = (await _levels.ListAsync()).Single(l => l.Id == must.Id);
        Assert.Equal(("Crítico", 2, 1, 4, 0), (stored.Name, stored.MinToAdd, stored.MinToComplete, stored.Max, stored.Order));
    }

    [Theory]
    [InlineData(1, 2, null)]   // completar > añadir
    [InlineData(3, 1, 2)]      // añadir > máx.
    [InlineData(-1, 0, null)]  // añadir negativo
    [InlineData(1, -1, null)]  // completar negativo
    [InlineData(0, 0, 0)]      // máx. 0
    public async Task Invalid_quotas_are_rejected(int add, int complete, int? max)
    {
        var result = await _levels.SaveAsync(null, Input(add: add, complete: complete, max: max));

        Assert.Equal(SaveLevelOutcome.InvalidQuota, result.Outcome);
        Assert.Equal(3, (await _levels.ListAsync()).Count);
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(2, 2, 2)]
    [InlineData(3, 1, 5)]
    public void Valid_quotas_are_accepted(int add, int complete, int? max) =>
        Assert.True(LevelService.IsValidQuota(add, complete, max));

    [Fact]
    public async Task Invalid_name_color_icon_and_duplicates_are_rejected()
    {
        Assert.Equal(SaveLevelOutcome.InvalidName, (await _levels.SaveAsync(null, Input("  "))).Outcome);
        Assert.Equal(SaveLevelOutcome.NameTooLong, (await _levels.SaveAsync(null, Input(new string('x', 31)))).Outcome);
        Assert.Equal(SaveLevelOutcome.InvalidColor, (await _levels.SaveAsync(null, Input() with { Color = "rojo" })).Outcome);
        Assert.Equal(SaveLevelOutcome.InvalidIcon, (await _levels.SaveAsync(null, Input() with { Icon = " " })).Outcome);
        Assert.Equal(SaveLevelOutcome.DuplicateName, (await _levels.SaveAsync(null, Input("must"))).Outcome);
        Assert.Equal(SaveLevelOutcome.NotFound, (await _levels.SaveAsync(Guid.NewGuid(), Input())).Outcome);
    }

    [Fact]
    public async Task Renaming_a_level_to_its_own_name_is_not_a_duplicate()
    {
        var must = (await _levels.ListAsync())[0];

        Assert.True((await _levels.SaveAsync(must.Id, Input("MUST"))).Succeeded);
    }

    [Fact]
    public async Task At_least_one_level_always_stays_active()
    {
        var all = await _levels.ListAsync();
        Assert.True((await _levels.SaveAsync(all[0].Id, Input("Must", enabled: false))).Succeeded);
        Assert.True((await _levels.SaveAsync(all[1].Id, Input("Recommended", enabled: false))).Succeeded);

        var result = await _levels.SaveAsync(all[2].Id, Input("Maybe", enabled: false));

        Assert.Equal(SaveLevelOutcome.NoActiveLevel, result.Outcome);
        Assert.True((await _levels.ListAsync()).Single(l => l.Id == all[2].Id).Enabled);
    }

    [Fact]
    public async Task Delete_removes_an_unused_level_and_renumbers_the_rest()
    {
        var extra = (await _levels.SaveAsync(null, Input("Extra"))).Level!;

        Assert.Equal(DeleteLevelOutcome.Deleted, await _levels.DeleteAsync(DefaultLevels.MaybeId));

        var after = await _levels.ListAsync();
        Assert.Equal(["Must", "Recommended", "Extra"], after.Select(l => l.Name));
        Assert.Equal([0, 1, 2], after.Select(l => l.Order));
        Assert.Equal(extra.Id, after[2].Id);
        Assert.Equal(DeleteLevelOutcome.NotFound, await _levels.DeleteAsync(DefaultLevels.MaybeId));
    }

    [Fact]
    public async Task Delete_is_refused_when_tasks_use_the_level()
    {
        await _levels.ListAsync();
        await _today.AddAsync(new NewTaskRequest("Algo", DefaultLevels.MaybeId));

        Assert.Equal(DeleteLevelOutcome.InUse, await _levels.DeleteAsync(DefaultLevels.MaybeId));
        Assert.Equal(3, (await _levels.ListAsync()).Count);
    }

    [Fact]
    public async Task Delete_is_refused_for_the_only_active_level()
    {
        var all = await _levels.ListAsync();
        await _levels.SaveAsync(all[1].Id, Input("Recommended", enabled: false));
        await _levels.SaveAsync(all[2].Id, Input("Maybe", enabled: false));

        Assert.Equal(DeleteLevelOutcome.LastActive, await _levels.DeleteAsync(all[0].Id));
    }

    [Fact]
    public async Task Move_reorders_and_persists_contiguous_orders()
    {
        await _levels.ListAsync();
        await _levels.MoveAsync(DefaultLevels.MaybeId, 0);

        var after = await _levels.ListAsync();
        Assert.Equal(["Maybe", "Must", "Recommended"], after.Select(l => l.Name));
        Assert.Equal([0, 1, 2], after.Select(l => l.Order));

        await _levels.MoveAsync(DefaultLevels.MaybeId, 99);
        Assert.Equal(["Must", "Recommended", "Maybe"], (await _levels.ListAsync()).Select(l => l.Name));
        Assert.False(await _levels.MoveAsync(Guid.NewGuid(), 0));
    }

    [Fact]
    public async Task Reordering_changes_the_order_of_the_levels_shown_in_Today()
    {
        await _levels.ListAsync();
        await _levels.MoveAsync(DefaultLevels.MaybeId, 0);

        var view = await _today.GetAsync();

        Assert.Equal(DefaultLevels.MaybeId, view.Groups[0].Level.Id);
    }

    [Fact]
    public async Task Restore_defaults_resets_the_default_levels_and_disables_custom_ones()
    {
        var all = await _levels.ListAsync();
        await _levels.SaveAsync(all[0].Id, Input("Crítico", add: 3, complete: 2, max: 5));
        await _levels.SaveAsync(all[1].Id, Input("Recommended", enabled: false));
        var extra = (await _levels.SaveAsync(null, Input("Extra"))).Level!;
        await _levels.MoveAsync(DefaultLevels.MaybeId, 0);

        await _levels.RestoreDefaultsAsync();

        var after = await _levels.ListAsync();
        Assert.Equal(DefaultLevels.Create(), after.Where(l => l.Id != extra.Id).ToList());
        var custom = after.Single(l => l.Id == extra.Id);
        Assert.False(custom.Enabled);
        Assert.Equal(3, custom.Order);
    }

    [Fact]
    public async Task Quota_policy_uses_the_edited_quotas()
    {
        var must = (await _levels.ListAsync())[0];
        await _levels.SaveAsync(must.Id, Input("Must", add: 2, complete: 1, max: 2));
        var levels = await _levels.ListAsync();

        var status = QuotaPolicy.Evaluate(levels, [new PlanEntry(must.Id, false)]);

        Assert.Contains(status.MissingToAdd, s => s.Level.Id == must.Id && s.Missing == 1);
        Assert.False(QuotaPolicy.CanAdd(levels[0], 2));
    }
}
