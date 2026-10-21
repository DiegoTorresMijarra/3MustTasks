namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Evento <c>PlanChanged</c>: se publica tras cualquier cambio del plan de un día (añadir, tachar, destachar,
/// quitar, reordenar). Lo consumen la UI y, más adelante, el planificador de recordatorios.
/// </summary>
public sealed class PlanEvents
{
    public event Action<DateOnly>? PlanChanged;

    internal void RaisePlanChanged(DateOnly logicalDate) => PlanChanged?.Invoke(logicalDate);
}
