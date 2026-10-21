using Microsoft.Extensions.Time.Testing;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.Core.Tests;

public class LogicalDayCalculatorTests
{
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private static readonly AppSettings Default = new();
    private static readonly AppSettings Dawn = new() { DayStartsAt = new TimeOnly(4, 0) };

    private static DateOnly TodayAt(DateTimeOffset utc, AppSettings settings, TimeZoneInfo? tz = null)
    {
        var time = new FakeTimeProvider(utc);
        time.SetLocalTimeZone(tz ?? TimeZoneInfo.Utc);
        return new LogicalDayCalculator(time).Today(settings);
    }

    [Fact]
    public void Default_start_00_00_uses_the_calendar_date()
    {
        Assert.Equal(new DateOnly(2026, 10, 21), TodayAt(new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero), Default));
        Assert.Equal(new DateOnly(2026, 10, 21), TodayAt(new(2026, 10, 21, 23, 59, 59, TimeSpan.Zero), Default));
    }

    [Theory]
    [InlineData(0, 0, 20)]
    [InlineData(3, 59, 20)]
    [InlineData(4, 0, 21)]
    [InlineData(12, 0, 21)]
    [InlineData(23, 59, 21)]
    public void Start_04_00_moves_the_boundary_to_four_in_the_morning(int hour, int minute, int expectedDay) =>
        Assert.Equal(new DateOnly(2026, 10, expectedDay), TodayAt(new(2026, 10, 21, hour, minute, 0, TimeSpan.Zero), Dawn));

    [Fact]
    public void Month_and_year_boundaries_roll_back_correctly() =>
        Assert.Equal(new DateOnly(2025, 12, 31), TodayAt(new(2026, 1, 1, 2, 0, 0, TimeSpan.Zero), Dawn));

    [Fact]
    public void Uses_local_time_not_utc()
    {
        // 02:30 UTC del 21 = 04:30 en Madrid (UTC+2): ya es el día 21 con inicio 04:00.
        Assert.Equal(new DateOnly(2026, 10, 21), TodayAt(new(2026, 10, 21, 2, 30, 0, TimeSpan.Zero), Dawn, Madrid));
        // 01:30 UTC = 03:30 Madrid: todavía el día 20.
        Assert.Equal(new DateOnly(2026, 10, 20), TodayAt(new(2026, 10, 21, 1, 30, 0, TimeSpan.Zero), Dawn, Madrid));
    }

    [Fact]
    public void Spring_forward_keeps_the_04_00_boundary()
    {
        // 29-mar-2026: a las 02:00 locales los relojes saltan a las 03:00 (UTC+1 -> UTC+2).
        // 01:30 UTC = 03:30 locales (ya con horario de verano): antes de las 04:00, sigue siendo el día 28.
        Assert.Equal(new DateOnly(2026, 3, 28), TodayAt(new(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), Dawn, Madrid));
        // 02:00 UTC = 04:00 locales: cambia el día.
        Assert.Equal(new DateOnly(2026, 3, 29), TodayAt(new(2026, 3, 29, 2, 0, 0, TimeSpan.Zero), Dawn, Madrid));
    }

    [Fact]
    public void Fall_back_repeats_an_hour_without_changing_the_day_twice()
    {
        // 25-oct-2026: a las 03:00 locales los relojes vuelven a las 02:00 (UTC+2 -> UTC+1).
        // 00:30 UTC = 02:30 (primera pasada) y 01:30 UTC = 02:30 (segunda): ambas el día 24.
        Assert.Equal(new DateOnly(2026, 10, 24), TodayAt(new(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), Dawn, Madrid));
        Assert.Equal(new DateOnly(2026, 10, 24), TodayAt(new(2026, 10, 25, 1, 30, 0, TimeSpan.Zero), Dawn, Madrid));
        // 03:00 UTC = 04:00 locales (ya UTC+1): cambia el día.
        Assert.Equal(new DateOnly(2026, 10, 25), TodayAt(new(2026, 10, 25, 3, 0, 0, TimeSpan.Zero), Dawn, Madrid));
    }

    [Fact]
    public void Static_overload_matches_instance()
    {
        var local = new DateTimeOffset(2026, 10, 21, 3, 59, 0, TimeSpan.FromHours(2));
        Assert.Equal(new DateOnly(2026, 10, 20), LogicalDayCalculator.For(local, new TimeOnly(4, 0)));
    }
}
