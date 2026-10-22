using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.Infrastructure.Shared.Ics;

/// <summary>
/// Lee un .ics con Ical.Net (spec §4.6). Solo lee VEVENT; ignora los VTODO, los eventos cancelados y las
/// modificaciones de una instancia (RECURRENCE-ID). Una hora con zona (TZID) o en UTC se convierte a la hora local del
/// dispositivo; una hora flotante (la que escribe <see cref="IcalNetWriter"/>) se toma tal cual. Un evento de varios
/// días se importa en su primer día. Se entienden las RRULE DAILY (con INTERVAL), WEEKLY (INTERVAL=1, con BYDAY) y
/// MONTHLY (con BYMONTHDAY) con UNTIL opcional; el resto (COUNT, YEARLY, BYSETPOS...) se marca como no soportado.
/// </summary>
public sealed class IcalNetReader : IIcsReader
{
    public IReadOnlyList<IcsEvent> Read(string content, TimeZoneInfo localZone)
    {
        CalendarCollection calendars;
        try
        {
            if (string.IsNullOrWhiteSpace(content) || !content.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
                throw new IcsFormatException("El archivo no parece un calendario .ics.");
            calendars = CalendarCollection.Load(content);
        }
        catch (IcsFormatException) { throw; }
        catch (Exception e)
        {
            throw new IcsFormatException("El archivo .ics está dañado o tiene un formato inesperado.", e);
        }

        var result = new List<IcsEvent>();
        foreach (var calendar in calendars)
        foreach (var ev in calendar.Events)
        {
            if (ev.DtStart is null || ev.RecurrenceIdentifier is not null) continue;
            if (string.Equals(ev.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;

            var (date, time) = Resolve(ev.DtStart, localZone);
            var title = string.IsNullOrWhiteSpace(ev.Summary) ? "(Sin título)" : ev.Summary.Trim();
            var uid = string.IsNullOrWhiteSpace(ev.Uid) ? $"sin-uid-{title}-{date:yyyyMMdd}-{time:HHmm}" : ev.Uid.Trim();
            result.Add(new IcsEvent(uid, title, ev.Description, date, time,
                [.. ev.Categories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())],
                ev.RecurrenceRule is { } rule ? ParseRule(rule, date) : null));
        }
        return result;
    }

    private static (DateOnly Date, TimeOnly? Time) Resolve(CalDateTime start, TimeZoneInfo local)
    {
        if (!start.HasTime) return (DateOnly.FromDateTime(start.Value), null);
        var moment = start.Value;
        if (start.IsUtc || !string.IsNullOrEmpty(start.TzId))
            moment = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(start.AsUtc, DateTimeKind.Utc), local);
        return (DateOnly.FromDateTime(moment), TimeOnly.FromDateTime(moment));
    }

    private static IcsRecurrence ParseRule(Ical.Net.DataTypes.RecurrenceRule rule, DateOnly start)
    {
        var interval = Math.Max(1, rule.Interval);
        DateOnly? until = rule.Until is { } u ? DateOnly.FromDateTime(u.Value) : null;
        var unsupported = new IcsRecurrence(null, interval, [], null, until, false);
        // COUNT, BYSETPOS, BYYEARDAY... exceden lo que sabe repetir la app.
        if (rule.Count > 0 || rule.BySetPosition.Count > 0 || rule.ByYearDay.Count > 0 || rule.ByWeekNo.Count > 0 || rule.ByMonth.Count > 0)
            return unsupported;
        var byDay = rule.ByDay;

        switch (rule.Frequency)
        {
            case FrequencyType.Daily when byDay.Count == 0 && rule.ByMonthDay.Count == 0:
                return new(interval == 1 ? RecurrenceFrequency.Daily : RecurrenceFrequency.EveryNDays, interval, [], null, until, true);
            case FrequencyType.Weekly when interval == 1 && rule.ByMonthDay.Count == 0 && byDay.All(d => d.Offset is null or 0): // null = sin ordinal (MO frente a 1MO)
                var days = byDay.Count == 0 ? [start.DayOfWeek] : byDay.Select(d => d.DayOfWeek).Distinct().ToList();
                return new(RecurrenceFrequency.Weekly, 1, days, null, until, true);
            case FrequencyType.Monthly when byDay.Count == 0 && rule.ByMonthDay.Count <= 1 && rule.ByMonthDay.All(d => d is >= 1 and <= 31):
                return new(RecurrenceFrequency.Monthly, interval, [], rule.ByMonthDay.Count == 1 ? rule.ByMonthDay[0] : start.Day, until, true);
            default:
                return unsupported;
        }
    }
}
