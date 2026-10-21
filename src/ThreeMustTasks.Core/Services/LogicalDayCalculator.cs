using ThreeMustTasks.Core.Domain;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Único sitio donde se calcula el día lógico: <c>DateOnly.FromDateTime(ahora_local - DayStartsAt)</c>.
/// Se resta sobre la hora de pared local, así los cambios de horario no desplazan el límite del día.
/// </summary>
public sealed class LogicalDayCalculator(TimeProvider time)
{
    public DateOnly Today(AppSettings settings) => For(time.GetLocalNow(), settings.DayStartsAt);

    public static DateOnly For(DateTimeOffset localNow, TimeOnly dayStartsAt) =>
        DateOnly.FromDateTime(localNow.DateTime - dayStartsAt.ToTimeSpan());
}
