using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using ThreeMustTasks.Core.Services;

namespace ThreeMustTasks.Infrastructure.Shared.Ics;

/// <summary>
/// Escribe el .ics con Ical.Net (spec §4.6). Formato v1, que el importador (issue 26) debe leer:
/// <list type="bullet">
/// <item>VEVENT por asignación: <c>UID</c>, <c>SUMMARY</c>, <c>DESCRIPTION</c> (notas), <c>CATEGORIES</c> (nivel y categoría),
/// <c>STATUS:CONFIRMED</c>, <c>X-3MT-STATUS:COMPLETED|PENDING</c>.</item>
/// <item>Sin hora: <c>DTSTART;VALUE=DATE</c> (día completo, <c>DTEND</c> = día siguiente). Con hora: hora flotante
/// (sin zona, como hora de pared local) y duración fija de <see cref="EventMinutes"/> minutos.</item>
/// <item>VTODO opcional: UID <c>{TaskId}-todo@3musttasks</c>, <c>DUE</c>, <c>STATUS</c>
/// (<c>COMPLETED</c>|<c>NEEDS-ACTION</c>), <c>COMPLETED</c> en UTC si se conoce, y los mismos CATEGORIES y X-3MT-STATUS.</item>
/// </list>
/// </summary>
public sealed class IcalNetWriter : IIcsWriter
{
    public const int EventMinutes = 30;
    public const string StatusProperty = "X-3MT-STATUS";

    public string Write(IReadOnlyList<IcsEntry> entries, bool includeVTodo, DateTimeOffset stamp)
    {
        var calendar = new Calendar { ProductId = "-//3MustTasks//ES" };
        var dtStamp = new CalDateTime(stamp.UtcDateTime, "UTC");

        foreach (var e in entries)
        {
            var ev = new CalendarEvent
            {
                Uid = e.Uid,
                Summary = e.Title,
                Description = string.IsNullOrWhiteSpace(e.Notes) ? null : e.Notes,
                DtStamp = dtStamp,
                Status = EventStatus.Confirmed,
            };
            if (e.Time is { } t)
            {
                var start = e.Date.ToDateTime(t);
                ev.DtStart = new CalDateTime(start);
                ev.DtEnd = new CalDateTime(start.AddMinutes(EventMinutes));
            }
            else
            {
                ev.DtStart = new CalDateTime(e.Date.Year, e.Date.Month, e.Date.Day);
                var next = e.Date.AddDays(1);
                ev.DtEnd = new CalDateTime(next.Year, next.Month, next.Day);
            }
            ev.Categories = [.. e.Categories];
            ev.Properties.Add(new CalendarProperty(StatusProperty, e.Completed ? "COMPLETED" : "PENDING"));
            calendar.Events.Add(ev);

            if (!includeVTodo) continue;
            var todo = new Todo
            {
                Uid = e.Uid.Replace($"@{IcsExportService.UidDomain}", $"-todo@{IcsExportService.UidDomain}"),
                Summary = e.Title,
                Description = ev.Description,
                DtStamp = dtStamp,
                Due = e.Time is { } tt ? new CalDateTime(e.Date.ToDateTime(tt)) : new CalDateTime(e.Date.Year, e.Date.Month, e.Date.Day),
                Status = e.Completed ? TodoStatus.Completed : TodoStatus.NeedsAction,
            };
            if (e.Completed && e.CompletedAt is { } done) todo.Completed = new CalDateTime(done.UtcDateTime, "UTC");
            todo.Categories = [.. e.Categories];
            todo.Properties.Add(new CalendarProperty(StatusProperty, e.Completed ? "COMPLETED" : "PENDING"));
            calendar.Todos.Add(todo);
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? "";
    }
}
