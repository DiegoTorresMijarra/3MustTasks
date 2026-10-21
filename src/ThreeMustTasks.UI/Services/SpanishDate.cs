namespace ThreeMustTasks.UI.Services;

/// <summary>Fecha larga en español ("Miércoles, 21 de octubre") sin depender de datos ICU del runtime.</summary>
public static class SpanishDate
{
    private static readonly string[] Days = ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    private static readonly string[] Months =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    public static string Format(DateOnly date) => $"{Days[(int)date.DayOfWeek]}, {date.Day} de {Months[date.Month - 1]}";
}
