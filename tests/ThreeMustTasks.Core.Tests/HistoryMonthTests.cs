using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Core.Tests;

/// <summary>Vista Mes (issue 24).</summary>
public class HistoryMonthTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero));
    private readonly IDayRepository _days;
    private readonly ISettingsStore _settings;
    private readonly HistoryService _history;

    public HistoryMonthTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<StreakCalculator>()
            .AddSingleton<HistoryService>()
            .BuildServiceProvider();
        _days = sp.GetRequiredService<IDayRepository>();
        _settings = sp.GetRequiredService<ISettingsStore>();
        _history = sp.GetRequiredService<HistoryService>();
    }

    private Task Close(int day, bool fulfilled) =>
        _days.UpsertLogAsync(new DayLog(new DateOnly(2026, 10, day), null, null, fulfilled, _time.GetUtcNow()));

    [Theory]
    [InlineData(DayOfWeek.Monday, "2026-09-28", 5)]
    [InlineData(DayOfWeek.Sunday, "2026-09-27", 5)]
    public async Task Month_covers_whole_weeks_from_the_first_day_of_week(DayOfWeek first, string start, int weeks)
    {
        _settings.Save(_settings.Load() with { FirstDayOfWeek = first });

        var month = await _history.GetMonthAsync(new DateOnly(2026, 10, 15));

        Assert.Equal(new DateOnly(2026, 10, 1), month.First);
        Assert.Equal(DateOnly.Parse(start), month.Weeks[0].Start);
        Assert.Equal(weeks, month.Weeks.Count);
        Assert.Contains(new DateOnly(2026, 10, 31), month.Weeks[^1].Days.Select(d => d.Date));
        Assert.True(month.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(month.Contains(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public async Task Month_counts_fulfilled_days_and_streaks()
    {
        await Close(14, true); await Close(15, true); await Close(16, false);
        await Close(17, true); await Close(18, true); await Close(19, true); await Close(20, true);

        var month = await _history.GetMonthAsync(new DateOnly(2026, 10, 21));

        Assert.Equal((6, 7), (month.FulfilledDays, month.DaysWithPlan));
        Assert.Equal(4, month.BestStreak);
        Assert.True(month.CurrentStreak >= 0);
    }

    [Fact]
    public async Task Other_month_only_counts_its_own_days()
    {
        await Close(20, true);
        var month = await _history.GetMonthAsync(new DateOnly(2026, 9, 10));
        Assert.Equal((0, 0), (month.FulfilledDays, month.DaysWithPlan));
    }
}
