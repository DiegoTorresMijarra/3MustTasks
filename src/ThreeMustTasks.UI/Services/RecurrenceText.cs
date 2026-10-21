using Microsoft.Extensions.Localization;
using ThreeMustTasks.Core.Domain;
using ThreeMustTasks.Core.Services;
using ThreeMustTasks.UI.Resources;

namespace ThreeMustTasks.UI.Services;

/// <summary>Textos en español de las reglas recurrentes (resumen y errores).</summary>
public static class RecurrenceText
{
    public static string Describe(IStringLocalizer<Strings> l, RecurrenceRule rule) => Describe(l, RecurrenceSpec.FromRule(rule));

    public static string Describe(IStringLocalizer<Strings> l, RecurrenceSpec spec) => spec.Preset switch
    {
        RecurrencePreset.Daily => l["Rec_DescDaily"],
        RecurrencePreset.Weekdays => l["Rec_DescWeekdays"],
        RecurrencePreset.EveryNDays => l["Rec_DescEveryN", spec.Interval],
        RecurrencePreset.Monthly => l["Rec_DescMonthly", spec.DayOfMonth ?? 1],
        _ => l["Rec_DescWeekly", string.Join(", ", (spec.Days ?? []).OrderBy(d => ((int)d + 6) % 7).Select(d => l[$"Day_{d}"].Value.ToLowerInvariant()))],
    };

    public static string Error(IStringLocalizer<Strings> l, RecurrenceOutcome outcome) => outcome switch
    {
        RecurrenceOutcome.InvalidInterval => l["Rec_ErrInterval", RecurrenceSpec.MaxInterval],
        RecurrenceOutcome.NoDays => l["Rec_ErrNoDays"],
        RecurrenceOutcome.InvalidDayOfMonth => l["Rec_ErrDayOfMonth"],
        RecurrenceOutcome.InvalidEndDate => l["Rec_ErrEndDate"],
        RecurrenceOutcome.InvalidTitle => l["Err_InvalidTitle"],
        RecurrenceOutcome.TitleTooLong => l["Err_TitleTooLong", TaskEditService.MaxTitleLength],
        RecurrenceOutcome.AlreadyRecurrent => l["Rec_ErrAlready"],
        _ => l["Err_Generic"],
    };
}
