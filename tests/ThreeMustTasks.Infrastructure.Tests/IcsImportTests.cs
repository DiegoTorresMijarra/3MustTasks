using Microsoft.Extensions.DependencyInjection;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Ports;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.Infrastructure.Shared.Ics;
using ThreeMustTasks.Infrastructure.Shared.InMemory;

namespace ThreeMustTasks.Infrastructure.Tests;

/// <summary>
/// Import de .ics (issue 26, spec §4.6). El fixture google-calendar.ics está escrito a mano imitando el formato que
/// exporta Google Calendar (PRODID, VTIMEZONE, TZID, UTC, RRULE, RECURRENCE-ID, CANCELLED, líneas plegadas);
/// no es una descarga real.
/// </summary>
public sealed class IcsImportTests
{
    private sealed class MadridTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    }

    // 2026-10-22 12:00 local (10:00 UTC, CEST): hoy lógico = 2026-10-22.
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 22);

    private sealed class Env
    {
        public required IcsImportService Import { get; init; }
        public required ITaskRepository Tasks { get; init; }
        public required IDayRepository Days { get; init; }
        public required IConfigRepository Config { get; init; }
        public required TodayService TodayService { get; init; }
        public required ISettingsStore Settings { get; init; }
    }

    private static async Task<Env> CreateAsync()
    {
        var sp = new ServiceCollection()
            .AddInMemoryPersistence()
            .AddSingleton<TimeProvider>(new MadridTime(Now))
            .AddSingleton<LogicalDayCalculator>()
            .AddSingleton<PlanEvents>()
            .AddSingleton<TodayService>()
            .AddSingleton<IIcsReader, IcalNetReader>()
            .AddSingleton<IcsImportService>()
            .BuildServiceProvider();
        var env = new Env
        {
            Import = sp.GetRequiredService<IcsImportService>(),
            Tasks = sp.GetRequiredService<ITaskRepository>(),
            Days = sp.GetRequiredService<IDayRepository>(),
            Config = sp.GetRequiredService<IConfigRepository>(),
            TodayService = sp.GetRequiredService<TodayService>(),
            Settings = sp.GetRequiredService<ISettingsStore>(),
        };
        await env.TodayService.GetLevelsAsync(); // siembra los niveles por defecto
        return env;
    }

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static IcsPreviewItem Item(IcsPreview p, string uid) => p.Items.Single(i => i.Key == uid);

    // ---- Previsualización ----

    [Fact]
    public async Task Preview_of_a_google_calendar_file_lists_events_with_local_times_and_destinations()
    {
        var env = await CreateAsync();

        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        // Fuera: el cancelado y la modificación de una instancia (RECURRENCE-ID).
        Assert.Equal(7, preview.Items.Count);
        Assert.DoesNotContain(preview.Items, i => i.Event.Title.Contains("cancelada") || i.Event.Title.Contains("movida"));

        var allDay = Item(preview, "allday-future-001@google.com");
        Assert.Null(allDay.Event.Time);
        Assert.Equal(new DateOnly(2026, 11, 5), allDay.Event.Date);
        Assert.Equal(IcsImportDestination.Future, allDay.Destination);

        var tzid = Item(preview, "timed-today-tzid-002@google.com");
        Assert.Equal(new TimeOnly(17, 0), tzid.Event.Time);
        Assert.Equal(IcsImportDestination.Today, tzid.Destination);
        Assert.Contains("Llevar el portátil, el cargador y el proyector", tzid.Event.Notes);

        var utc = Item(preview, "timed-today-utc-003@google.com");
        Assert.Equal(new TimeOnly(17, 0), utc.Event.Time); // 15:00Z = 17:00 en Madrid (CEST)
        Assert.Equal(Today, utc.Event.Date);

        Assert.Equal(IcsImportDestination.PastToPool, Item(preview, "allday-past-007@google.com").Destination);
        Assert.All(preview.Items, i => Assert.Equal(IcsImportStatus.New, i.Status));
    }

    [Fact]
    public async Task Past_events_are_not_selected_by_default_and_the_rest_are()
    {
        var env = await CreateAsync();

        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        Assert.False(Item(preview, "allday-past-007@google.com").SelectedByDefault);
        Assert.True(Item(preview, "timed-today-tzid-002@google.com").SelectedByDefault);
    }

    [Fact]
    public async Task Invalid_files_are_rejected_with_a_format_error()
    {
        var env = await CreateAsync();

        await Assert.ThrowsAsync<IcsFormatException>(() => env.Import.PreviewAsync("esto no es un calendario"));
        await Assert.ThrowsAsync<IcsFormatException>(() => env.Import.PreviewAsync(""));
    }

    [Fact]
    public async Task Categories_matching_a_level_or_category_name_are_suggested()
    {
        var env = await CreateAsync();
        var work = new Category(Guid.NewGuid(), "Trabajo", "#123456");
        await env.Config.UpsertCategoryAsync(work);

        var preview = await env.Import.PreviewAsync(Fixture("export-v1.ics"));

        var timed = Item(preview, "11111111-1111-1111-1111-111111111111@3musttasks");
        Assert.Equal(work.Id, timed.CategoryId);
        Assert.Equal(DefaultLevels.MustId, timed.SuggestedLevelId); // CATEGORIES:Must,Trabajo
    }

    // ---- Importación y selección ----

    [Fact]
    public async Task Import_only_creates_the_selected_events()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        var chosen = new[] { Item(preview, "timed-today-tzid-002@google.com"), Item(preview, "allday-future-001@google.com") };

        var result = await env.Import.ImportAsync(chosen, DefaultLevels.RecommendedId);

        Assert.Equal(1, result.ToPlan);
        Assert.Equal(1, result.Scheduled);
        Assert.Equal(["Cumpleaños de Marta", "Reunión con cliente"], (await env.Tasks.ListAsync()).Select(t => t.Title).Order());
    }

    [Fact]
    public async Task Todays_event_goes_to_the_plan_with_its_time_notes_level_and_uid()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        await env.Import.ImportAsync([Item(preview, "timed-today-tzid-002@google.com")], DefaultLevels.MustId);

        var task = Assert.Single(await env.Tasks.ListAsync());
        Assert.Equal("timed-today-tzid-002@google.com", task.IcsUid);
        Assert.Equal(DefaultLevels.MustId, task.LevelId);
        Assert.Equal(new TimeOnly(17, 0), task.DueTime);
        Assert.Equal(Today, task.ScheduledDate);
        Assert.Contains("Preparar la presentación", task.Notes);
        var assignment = Assert.Single(await env.Days.GetAssignmentsAsync(Today));
        Assert.Equal(task.Id, assignment.TaskId);
        Assert.Equal(AssignmentOutcome.Planned, assignment.Outcome);
    }

    [Fact]
    public async Task Todays_event_goes_to_the_pool_when_the_level_is_full()
    {
        var env = await CreateAsync();
        var must = (await env.Config.ListLevelsAsync()).Single(l => l.Id == DefaultLevels.MustId);
        await env.Config.UpsertLevelAsync(must with { Max = 1 });
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        var result = await env.Import.ImportAsync(
            [Item(preview, "timed-today-tzid-002@google.com"), Item(preview, "timed-today-utc-003@google.com")], DefaultLevels.MustId);

        Assert.Equal(1, result.ToPlan);
        Assert.Equal(1, result.ToPool);
        Assert.Single(await env.Days.GetAssignmentsAsync(Today));
        Assert.Equal(2, (await env.Tasks.ListAsync()).Count);
    }

    [Fact]
    public async Task Future_event_is_scheduled_for_its_date_without_entering_todays_plan()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        await env.Import.ImportAsync([Item(preview, "allday-future-001@google.com")], DefaultLevels.MaybeId);

        var task = Assert.Single(await env.Tasks.ListAsync());
        Assert.Equal(new DateOnly(2026, 11, 5), task.ScheduledDate);
        Assert.Null(task.DueTime);
        Assert.Empty(await env.Days.GetAssignmentsAsync(Today));
    }

    [Fact]
    public async Task Past_event_goes_to_the_pool_without_a_date()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        await env.Import.ImportAsync([Item(preview, "allday-past-007@google.com")], DefaultLevels.MaybeId);

        var task = Assert.Single(await env.Tasks.ListAsync());
        Assert.Null(task.ScheduledDate);
        Assert.Empty(await env.Days.GetAssignmentsAsync(Today));
    }

    // ---- Deduplicado por UID ----

    [Fact]
    public async Task Importing_twice_does_not_duplicate_and_the_second_preview_flags_existing_events()
    {
        var env = await CreateAsync();
        var first = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        await env.Import.ImportAsync(first.Items.Where(i => i.SelectedByDefault && i.Destination != IcsImportDestination.Recurring).ToList(), DefaultLevels.RecommendedId);
        var count = (await env.Tasks.ListAsync()).Count;

        var second = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        var result = await env.Import.ImportAsync(second.Items.Where(i => i.Status == IcsImportStatus.Existing).ToList(), DefaultLevels.RecommendedId);

        Assert.Equal(IcsImportStatus.Existing, Item(second, "timed-today-tzid-002@google.com").Status);
        Assert.Equal(IcsImportStatus.Existing, Item(second, "allday-future-001@google.com").Status);
        Assert.Equal(IcsImportStatus.New, Item(second, "allday-past-007@google.com").Status);
        Assert.Equal(count, (await env.Tasks.ListAsync()).Count);
        Assert.Equal(count, result.Updated);
    }

    [Fact]
    public async Task Reimporting_an_existing_uid_updates_title_time_and_date_of_a_pending_task()
    {
        var env = await CreateAsync();
        var original = Fixture("google-calendar.ics");
        var p1 = await env.Import.PreviewAsync(original);
        await env.Import.ImportAsync([Item(p1, "allday-future-001@google.com")], DefaultLevels.MaybeId);

        var modified = original.Replace("Cumpleaños de Marta", "Cumpleaños de Marta (cena)")
            .Replace("DTSTART;VALUE=DATE:20261105", "DTSTART;VALUE=DATE:20261107");
        var p2 = await env.Import.PreviewAsync(modified);
        await env.Import.ImportAsync([Item(p2, "allday-future-001@google.com")], DefaultLevels.MaybeId);

        var task = Assert.Single(await env.Tasks.ListAsync());
        Assert.Equal("Cumpleaños de Marta (cena)", task.Title);
        Assert.Equal(new DateOnly(2026, 11, 7), task.ScheduledDate);
    }

    [Fact]
    public async Task Own_export_matches_existing_tasks_by_id_and_collapses_the_date_suffixed_uids()
    {
        var env = await CreateAsync();
        var timed = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var postponed = Guid.Parse("33333333-3333-3333-3333-333333333333");
        foreach (var (id, title) in new[] { (timed, "Reunión"), (postponed, "Llamar al médico") })
            await env.Tasks.UpsertAsync(new TaskItem { Id = id, Title = title, LevelId = DefaultLevels.MustId, CreatedAt = Now, UpdatedAt = Now });

        var preview = await env.Import.PreviewAsync(Fixture("export-v1.ics"));

        // 4 VEVENT, pero la tarea aplazada aparece dos veces (UID limpio y con sufijo -yyyyMMdd): 3 elementos.
        Assert.Equal(3, preview.Items.Count);
        Assert.Equal(1, preview.DuplicatesInFile);
        var post = Item(preview, "33333333-3333-3333-3333-333333333333@3musttasks");
        Assert.Equal(IcsImportStatus.Existing, post.Status);
        Assert.Equal(postponed, post.ExistingTaskId);
        Assert.Equal(new DateOnly(2026, 10, 22), post.Event.Date); // la aparición más reciente
        Assert.Equal(IcsImportStatus.Existing, Item(preview, "11111111-1111-1111-1111-111111111111@3musttasks").Status);
        Assert.Equal(IcsImportStatus.New, Item(preview, "22222222-2222-2222-2222-222222222222@3musttasks").Status);
    }

    [Fact]
    public void CanonicalUid_strips_only_the_date_suffix_of_own_uids()
    {
        Assert.Equal("33333333-3333-3333-3333-333333333333@3musttasks",
            IcsImportService.CanonicalUid("33333333-3333-3333-3333-333333333333-20261020@3musttasks"));
        Assert.Equal("abc-20261020@google.com", IcsImportService.CanonicalUid("abc-20261020@google.com"));
    }

    // ---- RRULE básicas ----

    [Fact]
    public async Task Weekly_rrule_with_byday_becomes_a_weekly_recurrence_starting_in_the_future()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        var item = Item(preview, "weekly-future-004@google.com");
        Assert.Equal(IcsImportDestination.Recurring, item.Destination);

        var result = await env.Import.ImportAsync([item], DefaultLevels.RecommendedId);

        Assert.Equal(1, result.Recurring);
        var rule = Assert.Single(await env.Config.ListRecurrencesAsync());
        Assert.Equal(RecurrenceFrequency.Weekly, rule.Frequency);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], rule.DaysOfWeek.Order());
        Assert.Equal(new DateOnly(2026, 10, 26), rule.StartDate);
        Assert.Equal(new TimeOnly(18, 0), rule.DueTime);
        Assert.Empty(await env.Tasks.ListAsync()); // la primera instancia la genera el cierre de día
    }

    [Fact]
    public async Task Daily_rrule_that_applies_today_creates_todays_instance_and_marks_the_rule_generated()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));

        await env.Import.ImportAsync([Item(preview, "daily-past-005@google.com")], DefaultLevels.RecommendedId);

        var rule = Assert.Single(await env.Config.ListRecurrencesAsync());
        Assert.Equal(RecurrenceFrequency.Daily, rule.Frequency);
        Assert.Equal(Today, rule.LastGeneratedDate);
        var task = Assert.Single(await env.Tasks.ListAsync());
        Assert.Equal(rule.Id, task.RecurrenceRuleId);
        Assert.Single(await env.Days.GetAssignmentsAsync(Today));
    }

    [Fact]
    public async Task Reimporting_a_recurring_event_does_not_duplicate_the_rule()
    {
        var env = await CreateAsync();
        var p1 = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        await env.Import.ImportAsync([Item(p1, "weekly-future-004@google.com")], DefaultLevels.RecommendedId);

        var p2 = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        var result = await env.Import.ImportAsync([Item(p2, "weekly-future-004@google.com")], DefaultLevels.RecommendedId);

        Assert.Equal(IcsImportStatus.Existing, Item(p2, "weekly-future-004@google.com").Status);
        Assert.Equal(1, result.Skipped);
        Assert.Single(await env.Config.ListRecurrencesAsync());
    }

    [Fact]
    public async Task Unsupported_rrule_is_flagged_and_imported_as_a_single_task()
    {
        var env = await CreateAsync();
        var preview = await env.Import.PreviewAsync(Fixture("google-calendar.ics"));
        var yearly = Item(preview, "yearly-006@google.com");
        Assert.True(yearly.RecurrenceIgnored);
        Assert.Equal(IcsImportDestination.Future, yearly.Destination);

        await env.Import.ImportAsync([yearly], DefaultLevels.MaybeId);

        Assert.Empty(await env.Config.ListRecurrencesAsync());
        Assert.Equal(new DateOnly(2026, 12, 1), Assert.Single(await env.Tasks.ListAsync()).ScheduledDate);
    }

    [Theory]
    [InlineData("RRULE:FREQ=DAILY;INTERVAL=3", RecurrenceFrequency.EveryNDays, 3)]
    [InlineData("RRULE:FREQ=MONTHLY;BYMONTHDAY=15", RecurrenceFrequency.Monthly, 1)]
    [InlineData("RRULE:FREQ=WEEKLY;UNTIL=20261231T000000Z", RecurrenceFrequency.Weekly, 1)]
    public async Task Basic_rrules_map_to_recurrence_frequencies(string rrule, RecurrenceFrequency frequency, int interval)
    {
        var env = await CreateAsync();
        var ics = $"BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//t//t\r\nBEGIN:VEVENT\r\nUID:r1@x\r\nDTSTAMP:20261001T000000Z\r\nDTSTART;VALUE=DATE:20261101\r\n{rrule}\r\nSUMMARY:Regla\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n";

        var preview = await env.Import.PreviewAsync(ics);
        await env.Import.ImportAsync(preview.Items, DefaultLevels.MaybeId);

        var rule = Assert.Single(await env.Config.ListRecurrencesAsync());
        Assert.Equal(frequency, rule.Frequency);
        Assert.Equal(interval, rule.Interval);
        if (rrule.Contains("UNTIL")) Assert.Equal(new DateOnly(2026, 12, 31), rule.EndDate);
        if (rrule.Contains("BYMONTHDAY")) Assert.Equal(15, rule.DayOfMonth);
    }

    // ---- Ida y vuelta con el export propio ----

    [Fact]
    public async Task Round_trip_export_then_import_recognises_every_exported_task_as_existing()
    {
        var env = await CreateAsync();
        var task = new TaskItem { Title = "Ida y vuelta", LevelId = DefaultLevels.MustId, DueTime = new TimeOnly(9, 30), CreatedAt = Now, UpdatedAt = Now };
        await env.Tasks.UpsertAsync(task);
        await env.Days.UpsertAssignmentAsync(new DayAssignment(Guid.NewGuid(), task.Id, Today, AssignmentOutcome.Planned, null, 0));
        var exporter = new IcsExportService(env.Tasks, env.Days, env.Config, env.Settings, new IcalNetWriter(), new MadridTime(Now));

        var ics = (await exporter.ExportAsync(Today, Today)).Content;
        var preview = await env.Import.PreviewAsync(ics);

        var item = Assert.Single(preview.Items);
        Assert.Equal(IcsImportStatus.Existing, item.Status);
        Assert.Equal(task.Id, item.ExistingTaskId);
        Assert.Equal(new TimeOnly(9, 30), item.Event.Time); // hora flotante: se mantiene tal cual
        Assert.Equal(DefaultLevels.MustId, item.SuggestedLevelId); // CATEGORIES trae el nombre del nivel
    }
}
