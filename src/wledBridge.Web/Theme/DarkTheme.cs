using MudBlazor;

namespace wledBridge.Web.Theme;

public static class DarkTheme
{
    private static readonly string[] FontFamily = ["Oswald", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#01FF95",
            Secondary = "#01FF95",
            Background = "#121212",
            Surface = "#1e1e1e",
            AppbarBackground = "#181818",
            DrawerBackground = "#181818",
            TextPrimary = "#FFFFFF",
            TextSecondary = "#B3B3B3",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = FontFamily },
            H1 = new H1Typography { FontFamily = FontFamily },
            H2 = new H2Typography { FontFamily = FontFamily },
            H3 = new H3Typography { FontFamily = FontFamily },
            H4 = new H4Typography { FontFamily = FontFamily },
            H5 = new H5Typography { FontFamily = FontFamily },
            H6 = new H6Typography { FontFamily = FontFamily },
            Button = new ButtonTypography { FontFamily = FontFamily },
        }
    };
}
