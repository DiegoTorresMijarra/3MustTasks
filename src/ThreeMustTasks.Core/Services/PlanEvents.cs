namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Eventos del plan. <c>PlanChanged</c>: tras cualquier cambio del plan de un día (añadir, tachar, destachar,
/// quitar, reordenar). <c>RolloverCompleted</c>: al terminar cada cierre de día (haya cambiado algo o no), para que
/// el planificador de recordatorios desplace su ventana móvil.
/// </summary>
public sealed class PlanEvents
{
    public event Action<DateOnly>? PlanChanged;
    public event Action<DateOnly>? RolloverCompleted;

    internal void RaisePlanChanged(DateOnly logicalDate) => PlanChanged?.Invoke(logicalDate);
    internal void RaiseRolloverCompleted(DateOnly logicalDate) => RolloverCompleted?.Invoke(logicalDate);
}
