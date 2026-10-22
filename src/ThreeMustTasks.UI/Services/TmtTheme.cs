using MudBlazor;

namespace ThreeMustTasks.UI.Services;

/// <summary>
/// Tema de MudBlazor con los tokens del sistema visual de los mocks (design/README.md). Los mismos valores viven
/// como variables CSS <c>--tmt-*</c> en tmt.css; aquí solo se mapean a la paleta de Mud para sus componentes.
/// </summary>
public static class TmtTheme
{
    public static readonly string[] FontFamily = ["Manrope", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#15171C",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#5B6170",
            SecondaryContrastText = "#FFFFFF",
            Background = "#F5F6F8",
            BackgroundGray = "#F0F2F5",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#15171C",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#15171C",
            TextPrimary = "#15171C",
            TextSecondary = "#5B6170",
            TextDisabled = "#6F7685",
            ActionDefault = "#15171C",
            ActionDisabled = "#6F7685",
            ActionDisabledBackground = "#E3E6EB",
            LinesDefault = "#E3E6EB",
            LinesInputs = "#C4C9D2",
            TableLines = "#E3E6EB",
            Divider = "#E3E6EB",
            DividerLight = "#F0F2F5",
            Error = "#C9372C",
            ErrorContrastText = "#FFFFFF",
            Warning = "#E0A100",
            WarningContrastText = "#15171C",
            Info = "#2559C7",
            InfoContrastText = "#FFFFFF",
            Success = "#1F8A5B",
            SuccessContrastText = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#F2F3F5",
            PrimaryContrastText = "#15171C",
            Secondary = "#A3A9B7",
            SecondaryContrastText = "#15171C",
            Background = "#0F1115",
            BackgroundGray = "#22262E",
            Surface = "#1A1D23",
            AppbarBackground = "#1A1D23",
            AppbarText = "#F2F3F5",
            DrawerBackground = "#1A1D23",
            DrawerText = "#F2F3F5",
            TextPrimary = "#F2F3F5",
            TextSecondary = "#A3A9B7",
            TextDisabled = "#7B8190",
            ActionDefault = "#F2F3F5",
            ActionDisabled = "#7B8190",
            ActionDisabledBackground = "#2E333D",
            LinesDefault = "#2E333D",
            LinesInputs = "#4A505C",
            TableLines = "#2E333D",
            Divider = "#2E333D",
            DividerLight = "#22262E",
            Error = "#FF7A6E",
            ErrorContrastText = "#15171C",
            Warning = "#F2B91F",
            WarningContrastText = "#15171C",
            Info = "#7FA3F5",
            InfoContrastText = "#15171C",
            Success = "#4CC38A",
            SuccessContrastText = "#15171C",
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "12px" },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontFamily },
            H5 = new H5Typography { FontFamily = FontFamily, FontSize = "2.125rem", FontWeight = "800", LetterSpacing = "-0.02em", LineHeight = "1.15" },
            H6 = new H6Typography { FontFamily = FontFamily, FontSize = "0.75rem", FontWeight = "800", LetterSpacing = "0.08em", LineHeight = "1.4" },
            Subtitle1 = new Subtitle1Typography { FontFamily = FontFamily, FontSize = "1rem", FontWeight = "800", LineHeight = "1.4" },
            Body1 = new Body1Typography { FontFamily = FontFamily, FontSize = "0.9375rem", FontWeight = "500", LineHeight = "1.5" },
            Body2 = new Body2Typography { FontFamily = FontFamily, FontSize = "0.8125rem", FontWeight = "500", LineHeight = "1.45" },
            Caption = new CaptionTypography { FontFamily = FontFamily, FontSize = "0.75rem", FontWeight = "600", LineHeight = "1.4" },
            Button = new ButtonTypography { FontFamily = FontFamily, FontSize = "0.9375rem", FontWeight = "800", LineHeight = "1.2", TextTransform = "none" },
        },
    };
}
