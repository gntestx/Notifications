using MudBlazor;

namespace Notifications.Client.Layout;

public static class AzureMudTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0078D4",
            PrimaryDarken = "#005A9E",
            PrimaryLighten = "#50A7E8",
            Secondary = "#00A4EF",
            Tertiary = "#7FBA00",
            Background = "#F5F8FC",
            Surface = "#FFFFFF",
            AppbarBackground = "#0F2A44",
            AppbarText = "#FFFFFF",
            Success = "#107C10",
            Warning = "#FFB900",
            Error = "#D13438"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "14px"
        }
    };
}
