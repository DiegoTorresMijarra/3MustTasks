namespace ThreeMustTasks.Core.Domain;

public sealed record Level(
    Guid Id,
    string Name,
    string Color,
    string Icon,
    int MinToAdd,
    int MinToComplete,
    int? Max,
    int Order,
    bool Enabled);

public sealed record Category(Guid Id, string Name, string Color);

public sealed record TaskItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public string? Notes { get; init; }
    public Guid LevelId { get; init; }
    public Guid? CategoryId { get; init; }
    public TimeOnly? DueTime { get; init; }
    public TimeSpan? ReminderOffset { get; init; }
    public DateOnly? ScheduledDate { get; init; }
    public TaskItemStatus Status { get; init; } = TaskItemStatus.Active;
    public bool IsPinned { get; init; }
    public int TimesPostponed { get; init; }
    public Guid? RecurrenceRuleId { get; init; }
    public string? IcsUid { get; init; }
    public bool IsFavoriteSource { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record DayAssignment(
    Guid Id,
    Guid TaskId,
    DateOnly LogicalDate,
    AssignmentOutcome Outcome,
    DateTimeOffset? CompletedAt,
    int Order);

public sealed record DayLog(
    DateOnly LogicalDate,
    DateTimeOffset? PlanMinimumMetAt,
    DateTimeOffset? CompletionMinimumMetAt,
    bool? Fulfilled,
    DateTimeOffset? ClosedAt);

public sealed record FavoriteTemplate(
    Guid Id,
    string Title,
    Guid LevelId,
    Guid? CategoryId,
    TimeOnly? DueTime,
    TimeSpan? ReminderOffset,
    int UseCount);

public sealed record RecurrenceRule(
    Guid Id,
    string TemplateTitle,
    Guid LevelId,
    Guid? CategoryId,
    TimeOnly? DueTime,
    RecurrenceFrequency Frequency,
    int Interval,
    DayOfWeek[] DaysOfWeek,
    int? DayOfMonth,
    DateOnly StartDate,
    DateOnly? EndDate,
    DateOnly? LastGeneratedDate,
    bool IsPaused = false);

/// <summary>Ajustes de la app. Solo lo necesario hasta ahora; el resto se añade con cada issue.</summary>
public sealed record AppSettings
{
    public TimeOnly DayStartsAt { get; init; } = new(0, 0);
    public bool StrictMode { get; init; }
    public bool AutoAddScheduled { get; init; } = true;
    public ThemeMode Theme { get; init; } = ThemeMode.System;
    public DayOfWeek FirstDayOfWeek { get; init; } = DayOfWeek.Monday;
    /// <summary>Si el export .ics añade además un VTODO por tarea (spec §10). Desactivado por defecto.</summary>
    public bool ExportVTodo { get; init; }
    public int SchemaVersion { get; init; } = 1;
}
