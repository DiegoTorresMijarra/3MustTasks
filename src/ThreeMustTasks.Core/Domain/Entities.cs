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
    /// <summary>Si ya se completó (o saltó) el onboarding del primer arranque. Se puede repetir desde Ajustes.</summary>
    public bool OnboardingCompleted { get; init; }
    /// <summary>Hora del recordatorio "¡Añade tus tareas!" (PRD §5.6); por defecto 07:30.</summary>
    public TimeOnly PlanningReminderTime { get; init; } = new(7, 30);
    /// <summary>Si el recordatorio "¡Añade tus tareas!" está activo (spec §3 <c>PlanReminder.Enabled</c>).</summary>
    public bool PlanReminderEnabled { get; init; } = true;
    /// <summary>
    /// Días de la semana en que suena el recordatorio, como máscara de bits (bit <c>(int)DayOfWeek</c>); por defecto
    /// todos (127). Se guarda como entero para persistirlo en una sola columna. Ver <see cref="PlanReminderOnDay"/>.
    /// </summary>
    public int PlanReminderDaysMask { get; init; } = AllDaysMask;

    public const int AllDaysMask = 0b111_1111;

    /// <summary>true si el recordatorio de planificación está activo el día de la semana indicado.</summary>
    public bool PlanReminderOnDay(DayOfWeek day) => (PlanReminderDaysMask & (1 << (int)day)) != 0;

    /// <summary>Copia con el día de la semana activado o desactivado en la máscara.</summary>
    public AppSettings WithPlanReminderDay(DayOfWeek day, bool on) => this with
    {
        PlanReminderDaysMask = on ? PlanReminderDaysMask | (1 << (int)day) : PlanReminderDaysMask & ~(1 << (int)day),
    };
    public int SchemaVersion { get; init; } = 1;
}
