using ThreeMustTasks.Core.Ports;

namespace ThreeMustTasks.Core.Services;

/// <summary>
/// Racha de días cumplidos: solo cuentan los días cerrados con <c>Fulfilled == true</c>. Un día no cumplido
/// o sin plan (sin <c>DayLog</c>) la corta. Hoy suma si ya ha alcanzado el mínimo a completar, y si aún
/// no lo ha hecho no la corta (el día sigue abierto).
/// </summary>
public sealed class StreakCalculator(IDayRepository days, ISettingsStore settings, LogicalDayCalculator calendar)
{
    private const int MaxLookbackDays = 3650;

    public async Task<int> GetAsync(CancellationToken ct = default)
    {
        var today = calendar.Today(settings.Load());
        var streak = (await days.GetLogAsync(today, ct))?.CompletionMinimumMetAt is not null ? 1 : 0;

        var from = today.AddDays(-MaxLookbackDays);
        var closed = (await days.ListClosedLogsAsync(from, today.AddDays(-1), ct)).ToDictionary(l => l.LogicalDate);
        for (var d = today.AddDays(-1); d >= from; d = d.AddDays(-1))
        {
            if (!closed.TryGetValue(d, out var log) || log.Fulfilled != true) break;
            streak++;
        }
        return streak;
    }
}
