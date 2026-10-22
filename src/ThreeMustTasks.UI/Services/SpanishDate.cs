namespace ThreeMustTasks.UI.Services;

/// <summary>Fecha larga en español ("Miércoles, 21 de octubre") sin depender de datos ICU del runtime.</summary>
public static class SpanishDate
{
    private static readonly string[] Days = ["Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado"];

    private static readonly string[] Months =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    private static readonly string[] DayAbbreviations = ["DOM", "LUN", "MAR", "MIÉ", "JUE", "VIE", "SÁB"];

    /// <summary>"19 oct".</summary>
    public static string FormatShort(DateOnly date) => $"{date.Day} {Months[date.Month - 1][..3]}";

    /// <summary>"LUN", "MIÉ"…</summary>
    public static string DayAbbreviation(DateOnly date) => DayAbbreviations[(int)date.DayOfWeek];

    private static readonly string[] DayInitials = ["D", "L", "M", "X", "J", "V", "S"];

    /// <summary>"Octubre 2026".</summary>
    public static string FormatMonth(DateOnly date) => $"{char.ToUpperInvariant(Months[date.Month - 1][0])}{Months[date.Month - 1][1..]} {date.Year}";

    /// <summary>"L", "M", "X"…</summary>
    public static string DayInitial(DateOnly date) => DayInitials[(int)date.DayOfWeek];

    /// <summary>"7 de octubre".</summary>
    public static string FormatShortLong(DateOnly date) => $"{date.Day} de {Months[date.Month - 1]}";

    public static string Format(DateOnly date) => $"{Days[(int)date.DayOfWeek]}, {date.Day} de {Months[date.Month - 1]}";
}
