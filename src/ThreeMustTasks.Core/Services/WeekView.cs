using ThreeMustTasks.Core.Domain;

namespace ThreeMustTasks.Core.Services;

public enum WeekDayKind { Past, Today, Future }

/// <summary>
/// Un día de la vista Semana. En días pasados <paramref name="Items"/> son las asignaciones con su resultado y
/// <paramref name="Fulfilled"/> viene del cierre; hoy son las asignaciones actuales (<paramref name="Fulfilled"/> null);
/// en días futuros son las tareas activas programadas para esa fecha (resultado Planned).
/// <paramref name="Planned"/> y <paramref name="Completed"/> cuentan las asignaciones no quitadas (en futuro, las programadas).
/// <paramref name="HasPlan"/> es false para un día pasado sin plan cerrado.
/// </summary>
public sealed record WeekDay(
    DateOnly Date,
    WeekDayKind Kind,
    IReadOnlyList<HistoryItem> Items,
    int Planned,
    int Completed,
    bool? Fulfilled,
    bool HasPlan);

/// <param name="Start">Primer día de la semana según Ajustes.</param>
public sealed record WeekView(DateOnly Start, IReadOnlyList<WeekDay> Days)
{
    public DateOnly End => Start.AddDays(6);
    public int TotalPlanned => Days.Where(d => d.Kind != WeekDayKind.Future).Sum(d => d.Planned);
    public int TotalCompleted => Days.Where(d => d.Kind != WeekDayKind.Future).Sum(d => d.Completed);
}

/// <summary>
/// Vista Mes (3MT-37): cuadrícula de semanas completas (desde el primer día de la semana de Ajustes) que cubre el mes.
/// Los días de <see cref="Weeks"/> fuera del mes se muestran vacíos en la UI (<see cref="Contains"/>).
/// </summary>
/// <param name="First">Primer día del mes.</param>
/// <param name="FulfilledDays">Días cerrados del mes cumplidos.</param>
/// <param name="DaysWithPlan">Días cerrados del mes con plan.</param>
public sealed record MonthView(DateOnly First, IReadOnlyList<WeekView> Weeks, int FulfilledDays, int DaysWithPlan, int CurrentStreak, int BestStreak)
{
    public bool Contains(DateOnly d) => d.Year == First.Year && d.Month == First.Month;
}
