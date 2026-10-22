using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

public class ReminderSyncTests
{
    private sealed class FakeScheduler : INotificationScheduler
    {
        public List<IReadOnlyList<PlannedNotification>> Calls { get; } = [];
        public bool Throw { get; set; }

        public Task ReplaceAllAsync(IReadOnlyList<PlannedNotification> notifications, CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("boom");
            lock (Calls) Calls.Add(notifications);
            return Task.CompletedTask;
        }

        public Task<bool> RequestPermissionAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<NotificationStatus> GetStatusAsync(CancellationToken ct = default) => Task.FromResult(new NotificationStatus(true, true, true));
        public Task OpenSystemSettingsAsync(SystemSettingsTarget target, CancellationToken ct = default) => Task.CompletedTask;
    }

    // Miércoles 21 octubre 2026, 06:00 UTC (zona local del test = UTC).
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 6, 0, 0, TimeSpan.Zero));
    private readonly FakeScheduler _scheduler = new();
    private readonly ServiceProvider _sp;

    public ReminderSyncTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        _sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<INotificationScheduler>(_scheduler)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<DayRolloverService>()
            .AddSingleton<TodayService>()
            .AddSingleton<ReminderSyncService>()
            .BuildServiceProvider();
        foreach (var l in DefaultLevels.Create()) _sp.GetRequiredService<IConfigRepository>().UpsertLevelAsync(l).GetAwaiter().GetResult();
    }

    private ReminderSyncService Sync => _sp.GetRequiredService<ReminderSyncService>();
    private ISettingsStore Store => _sp.GetRequiredService<ISettingsStore>();
    private PlanEvents Events => _sp.GetRequiredService<PlanEvents>();

    private async Task WaitForCallsAsync(int count)
    {
        for (var i = 0; i < 200; i++)
        {
            lock (_scheduler.Calls) if (_scheduler.Calls.Count >= count) return;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Se esperaban {count} llamadas al planificador y hay {_scheduler.Calls.Count}.");
    }

    [Fact]
    public async Task Rebuild_applies_the_planner_result_to_the_scheduler()
    {
        await Sync.RebuildAsync();

        var call = Assert.Single(_scheduler.Calls);
        Assert.Equal(7, call.Count);
        Assert.Equal(new DateTime(2026, 10, 21, 7, 30, 0), call[0].At);
    }

    [Fact]
    public async Task Rebuild_uses_the_current_settings()
    {
        Store.Save(Store.Load() with { PlanReminderEnabled = false });
        await Sync.RebuildAsync();
        Store.Save(Store.Load() with { PlanReminderEnabled = true, PlanningReminderTime = new TimeOnly(9, 0) });
        await Sync.RebuildAsync();

        Assert.Empty(_scheduler.Calls[0]);
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(_scheduler.Calls[1][0].At));
    }

    [Fact]
    public async Task Rebuild_skips_today_when_the_plan_minimum_was_already_met()
    {
        var days = _sp.GetRequiredService<IDayRepository>();
        await days.UpsertLogAsync(new DayLog(new DateOnly(2026, 10, 21), _time.GetUtcNow(), null, null, null));

        await Sync.RebuildAsync();

        Assert.Equal(6, _scheduler.Calls[0].Count);
        Assert.DoesNotContain(_scheduler.Calls[0], n => n.LogicalDate == new DateOnly(2026, 10, 21));
    }

    [Fact]
    public async Task Rebuild_is_idempotent()
    {
        await Sync.RebuildAsync();
        await Sync.RebuildAsync();

        Assert.Equal(_scheduler.Calls[0].Select(n => (n.Id, n.At)), _scheduler.Calls[1].Select(n => (n.Id, n.At)));
    }

    [Fact]
    public async Task Plan_changed_triggers_a_rebuild_once_started()
    {
        Sync.Start();

        await _sp.GetRequiredService<TodayService>().AddAsync(new NewTaskRequest("Una", DefaultLevels.MustId));
        await WaitForCallsAsync(1);

        Assert.Equal(7, _scheduler.Calls[0].Count);
    }

    [Fact]
    public async Task Without_Start_events_do_not_rebuild()
    {
        _ = Sync;
        await _sp.GetRequiredService<DayRolloverService>().RunAsync();
        await Task.Delay(50);

        Assert.Empty(_scheduler.Calls);
    }

    [Fact]
    public async Task Day_rollover_triggers_a_rebuild_even_when_nothing_changed()
    {
        Sync.Start();

        await _sp.GetRequiredService<DayRolloverService>().RunAsync();
        await WaitForCallsAsync(1);

        Assert.Equal(7, _scheduler.Calls[0].Count);
    }

    [Fact]
    public async Task Rollover_to_the_next_day_slides_the_window()
    {
        Sync.Start();
        await _sp.GetRequiredService<DayRolloverService>().RunAsync();
        await WaitForCallsAsync(1);

        _time.Advance(TimeSpan.FromDays(1));
        await _sp.GetRequiredService<DayRolloverService>().RunAsync();
        await WaitForCallsAsync(2);

        Assert.Equal(new DateOnly(2026, 10, 21), _scheduler.Calls[0][0].LogicalDate);
        Assert.Equal(new DateOnly(2026, 10, 22), _scheduler.Calls[1][0].LogicalDate);
        Assert.Equal(new DateOnly(2026, 10, 28), _scheduler.Calls[1][^1].LogicalDate);
    }

    [Fact]
    public async Task Start_is_idempotent_and_does_not_duplicate_rebuilds()
    {
        Sync.Start();
        Sync.Start();

        await _sp.GetRequiredService<DayRolloverService>().RunAsync();
        await WaitForCallsAsync(1);
        await Task.Delay(100);

        Assert.Single(_scheduler.Calls);
    }

    [Fact]
    public async Task A_failing_scheduler_does_not_throw_and_is_reported_in_LastError()
    {
        _scheduler.Throw = true;

        await Sync.RebuildAsync();

        Assert.IsType<InvalidOperationException>(Sync.LastError);

        _scheduler.Throw = false;
        await Sync.RebuildAsync();
        Assert.Null(Sync.LastError);
    }

    [Fact]
    public async Task Concurrent_rebuilds_are_serialized()
    {
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Sync.RebuildAsync()));

        Assert.Equal(10, _scheduler.Calls.Count);
    }
}
