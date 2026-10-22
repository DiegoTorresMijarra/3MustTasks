using Ical.Net;
using Ical.Net.CalendarComponents;
using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.Ics;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Infrastructure.Tests;

/// <summary>Export .ics (issue 25, spec §4.6): formato, ida y vuelta con Ical.Net y fixture del formato v1 (para el import, issue 26).</summary>
public sealed class IcsExportTests
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid MustLevel = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid WorkCategory = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Timed = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AllDay = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Postponed = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Removed = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static TaskItem Task(Guid id, string title, TimeOnly? time = null, string? notes = null, Guid? category = null) =>
        new() { Id = id, Title = title, Notes = notes, LevelId = MustLevel, CategoryId = category, DueTime = time, CreatedAt = Now, UpdatedAt = Now };

    private static async Task<(IcsExportService Service, ISettingsStore Settings)> SeedAsync()
    {
        var sp = new ServiceCollection().AddInMemoryPersistence().BuildServiceProvider();
        var tasks = sp.GetRequiredService<ITaskRepository>();
        var days = sp.GetRequiredService<IDayRepository>();
        var config = sp.GetRequiredService<IConfigRepository>();
        var settings = sp.GetRequiredService<ISettingsStore>();

        await config.UpsertLevelAsync(new Level(MustLevel, "Must", "#e53935", "flag", 1, 1, 3, 0, true));
        await config.UpsertCategoryAsync(new Category(WorkCategory, "Trabajo", "#1e88e5"));
        await tasks.UpsertAsync(Task(Timed, "Reunión; con, comas", new TimeOnly(9, 30), "Llevar el informe", WorkCategory));
        await tasks.UpsertAsync(Task(AllDay, "Hacer la compra"));
        await tasks.UpsertAsync(Task(Postponed, "Llamar al médico"));
        await tasks.UpsertAsync(Task(Removed, "Quitada del plan"));

        var done = new DateTimeOffset(2026, 10, 20, 10, 15, 0, TimeSpan.Zero);
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), Timed, new DateOnly(2026, 10, 20), AssignmentOutcome.Completed, done, 0));
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), AllDay, new DateOnly(2026, 10, 21), AssignmentOutcome.Planned, null, 0));
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), Postponed, new DateOnly(2026, 10, 20), AssignmentOutcome.NotCompleted, null, 1));
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), Postponed, new DateOnly(2026, 10, 22), AssignmentOutcome.Planned, null, 0));
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), Removed, new DateOnly(2026, 10, 21), AssignmentOutcome.Removed, null, 1));
        // Fuera del rango que piden los tests.
        await days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), AllDay, new DateOnly(2026, 11, 5), AssignmentOutcome.Planned, null, 0));

        var service = new IcsExportService(tasks, days, config, settings, new IcalNetWriter(), new FixedTime(Now));
        return (service, settings);
    }

    private static readonly DateOnly From = new(2026, 10, 20);
    private static readonly DateOnly To = new(2026, 10, 31);

    [Fact]
    public async Task Exports_one_vevent_per_assignment_in_range_without_the_removed_ones()
    {
        var (service, _) = await SeedAsync();

        var result = await service.ExportAsync(From, To);

        var calendar = Calendar.Load(result.Content)!;
        Assert.Equal(4, result.EventCount);
        Assert.Equal(4, calendar.Events.Count);
        Assert.Empty(calendar.Todos);
        Assert.DoesNotContain(calendar.Events, e => e.Summary == "Quitada del plan");
        Assert.Equal("3musttasks-2026-10-20_2026-10-31.ics", result.FileName);
    }

    [Fact]
    public async Task Range_is_inclusive_and_excludes_other_days()
    {
        var (service, _) = await SeedAsync();

        var result = await service.ExportAsync(new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21));

        var ev = Assert.Single(Calendar.Load(result.Content)!.Events);
        Assert.Equal("Hacer la compra", ev.Summary);
    }

    [Fact]
    public async Task Event_has_uid_categories_and_x_3mt_status_and_round_trips_through_ical_net()
    {
        var (service, _) = await SeedAsync();

        var calendar = Calendar.Load((await service.ExportAsync(From, To)).Content)!;

        var timed = calendar.Events.Single(e => e.Uid == $"{Timed}@3musttasks");
        Assert.Equal("Reunión; con, comas", timed.Summary);
        Assert.Equal("Llevar el informe", timed.Description);
        Assert.Equal(["Must", "Trabajo"], timed.Categories);
        Assert.Equal("CONFIRMED", timed.Status);
        Assert.Equal("COMPLETED", timed.Properties.Get<string>("X-3MT-STATUS"));
        Assert.True(timed.DtStart!.HasTime);
        Assert.Equal(new DateTime(2026, 10, 20, 9, 30, 0), timed.DtStart.Value);
        Assert.Equal(new DateTime(2026, 10, 20, 10, 0, 0), timed.DtEnd!.Value);

        var allDay = calendar.Events.Single(e => e.Uid == $"{AllDay}@3musttasks");
        Assert.Equal(["Must"], allDay.Categories);
        Assert.Equal("PENDING", allDay.Properties.Get<string>("X-3MT-STATUS"));
    }

    [Fact]
    public async Task Task_without_time_is_an_all_day_event_with_value_date()
    {
        var (service, _) = await SeedAsync();

        var content = (await service.ExportAsync(From, To)).Content;
        var allDay = Calendar.Load(content)!.Events.Single(e => e.Uid == $"{AllDay}@3musttasks");

        Assert.False(allDay.DtStart!.HasTime);
        Assert.Equal(new DateTime(2026, 10, 21), allDay.DtStart.Value);
        Assert.Equal(new DateTime(2026, 10, 22), allDay.DtEnd!.Value);
        Assert.Contains("DTSTART;VALUE=DATE:20261021", content);
    }

    [Fact]
    public async Task Postponed_task_keeps_a_clean_uid_only_on_its_latest_assignment_so_uids_are_unique()
    {
        var (service, _) = await SeedAsync();

        var events = Calendar.Load((await service.ExportAsync(From, To)).Content)!.Events;

        Assert.Equal(events.Count, events.Select(e => e.Uid).Distinct().Count());
        var late = events.Single(e => e.Uid == $"{Postponed}@3musttasks");
        Assert.Equal(new DateTime(2026, 10, 22), late.DtStart!.Value);
        var early = events.Single(e => e.Uid == $"{Postponed}-20261020@3musttasks");
        Assert.Equal("PENDING", early.Properties.Get<string>("X-3MT-STATUS"));
    }

    [Fact]
    public async Task Vtodo_is_included_only_when_enabled_in_settings()
    {
        var (service, settings) = await SeedAsync();
        settings.Save(settings.Load() with { ExportVTodo = true });

        var calendar = Calendar.Load((await service.ExportAsync(From, To)).Content)!;

        Assert.Equal(4, calendar.Events.Count);
        Assert.Equal(4, calendar.Todos.Count);
        var done = calendar.Todos.Single(t => t.Uid == $"{Timed}-todo@3musttasks");
        Assert.Equal("COMPLETED", done.Status);
        Assert.Equal("COMPLETED", done.Properties.Get<string>("X-3MT-STATUS"));
        Assert.Equal(new DateTime(2026, 10, 20, 10, 15, 0), done.Completed!.Value);
        Assert.Equal("NEEDS-ACTION", calendar.Todos.Single(t => t.Uid == $"{AllDay}-todo@3musttasks").Status);
        Assert.Equal(calendar.Events.Count + calendar.Todos.Count,
            calendar.Events.Select(e => e.Uid).Concat(calendar.Todos.Select(t => t.Uid)).Distinct().Count());
    }

    [Fact]
    public async Task Inverted_range_is_rejected_and_empty_range_yields_a_valid_empty_calendar()
    {
        var (service, _) = await SeedAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExportAsync(To, From));
        var empty = await service.ExportAsync(new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 31));
        Assert.Equal(0, empty.EventCount);
        Assert.Empty(Calendar.Load(empty.Content)!.Events);
    }

    [Fact]
    public async Task Output_matches_the_v1_fixture()
    {
        var (service, settings) = await SeedAsync();
        settings.Save(settings.Load() with { ExportVTodo = true });

        var actual = (await service.ExportAsync(From, To)).Content;

        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "export-v1.ics");
        var expected = await File.ReadAllTextAsync(path);
        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    [Fact]
    public void Fixture_can_be_read_back_by_ical_net()
    {
        // Es la entrada de ejemplo para el import (issue 26): lo que export-v1.ics declara tiene que poder leerse.
        var calendar = Calendar.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "export-v1.ics")))!;

        Assert.Equal(4, calendar.Events.Count);
        Assert.All(calendar.Events, e => Assert.EndsWith("@3musttasks", e.Uid));
        Assert.All(calendar.Events, e => Assert.NotNull(e.Properties.Get<string>("X-3MT-STATUS")));
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").TrimEnd();
}
