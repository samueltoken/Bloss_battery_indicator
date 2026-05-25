using BluetoothBatteryWidget.Core.Models;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace BluetoothBatteryWidget.App.Services;

public sealed record ColorPreset(
    string Id,
    string Label,
    WpfColor PrimaryText,
    WpfColor SecondaryText,
    WpfColor BatteryText,
    WpfColor CardTint,
    WpfColor CardBorder,
    WpfColor Track,
    WpfColor IconBack,
    WpfColor IconBorder,
    WpfColor FooterTop,
    WpfColor FooterBottom,
    WpfColor ListTop,
    WpfColor ListBottom,
    WpfColor ActionButtonBack,
    WpfColor ActionButtonBorder)
{
    public WpfBrush PreviewBrush => new(CardTint);

    public WpfBrush AccentBrush => new(BatteryText);

    public WpfBrush TextBrush => new(PrimaryText);

    public override string ToString() => Label;
}

public static class ColorPresetCatalog
{
    public static IReadOnlyList<ColorPreset> Presets { get; } =
    [
        new(
            WidgetSettings.WhiteBluePreset,
            "White Blue",
            ColorFrom("#10243C"),
            ColorFrom("#314E6A"),
            ColorFrom("#132F4D"),
            ColorFrom("#C2FFFFFF"),
            ColorFrom("#93FFFFFF"),
            ColorFrom("#7AB8C8D7"),
            ColorFrom("#E7F2F8FC"),
            ColorFrom("#A3CBDDEE"),
            ColorFrom("#C8FFFFFF"),
            ColorFrom("#ADECF7FF"),
            ColorFrom("#A7FFFFFF"),
            ColorFrom("#88ECF5FF"),
            ColorFrom("#B5F5FAFF"),
            ColorFrom("#9ECAE0EF")),
        new(
            WidgetSettings.CloudDancerPreset,
            "Cloud Dancer",
            ColorFrom("#26312E"),
            ColorFrom("#596C67"),
            ColorFrom("#B45D3E"),
            ColorFrom("#EAF8F4EE"),
            ColorFrom("#D7D3CDC0"),
            ColorFrom("#9BBAC6C1"),
            ColorFrom("#F5FBF8F2"),
            ColorFrom("#D1D0C8B9"),
            ColorFrom("#EEFDF6EC"),
            ColorFrom("#DADDE5D4"),
            ColorFrom("#E7F8F4EF"),
            ColorFrom("#CFE2EEF2"),
            ColorFrom("#EAF7EFE8"),
            ColorFrom("#C8C5BEB2")),
        new(
            WidgetSettings.MoonLavenderPreset,
            "Orchid Glass",
            ColorFrom("#251D3B"),
            ColorFrom("#62577D"),
            ColorFrom("#7A5CFF"),
            ColorFrom("#E0F2EDFF"),
            ColorFrom("#C3B9B1DF"),
            ColorFrom("#988DA0D2"),
            ColorFrom("#F1F8F3FF"),
            ColorFrom("#C9BEB3E2"),
            ColorFrom("#DEEAE3FF"),
            ColorFrom("#C9D1C4EC"),
            ColorFrom("#E2F3EEFF"),
            ColorFrom("#C8CED4F4"),
            ColorFrom("#E5ECE6FB"),
            ColorFrom("#BDB2A8D7")),
        new(
            WidgetSettings.MistSagePreset,
            "Jade Vapor",
            ColorFrom("#11372F"),
            ColorFrom("#416F63"),
            ColorFrom("#008C73"),
            ColorFrom("#DCEEF8EF"),
            ColorFrom("#BCA3CBB8"),
            ColorFrom("#948EBA9D"),
            ColorFrom("#F2FAF8F4"),
            ColorFrom("#BDB6D1BE"),
            ColorFrom("#DAEEF9E9"),
            ColorFrom("#BCDAD8C8"),
            ColorFrom("#DFF3F7EA"),
            ColorFrom("#B4D4D8C4"),
            ColorFrom("#DBEBF4E5"),
            ColorFrom("#AEB9CDB5")),
        new(
            WidgetSettings.AuroraTealPreset,
            "Aurora Lake",
            ColorFrom("#07313B"),
            ColorFrom("#2E6D7D"),
            ColorFrom("#007FA4"),
            ColorFrom("#D9EAFBFA"),
            ColorFrom("#BEA2D1D7"),
            ColorFrom("#937FB9C6"),
            ColorFrom("#EFF6FCFB"),
            ColorFrom("#B8A9D5DA"),
            ColorFrom("#D2E4FBFA"),
            ColorFrom("#B8C4DDF4"),
            ColorFrom("#D8ECFCFA"),
            ColorFrom("#B1C8DCF4"),
            ColorFrom("#D5E9FAF8"),
            ColorFrom("#A8A8CFD5")),
        new(
            WidgetSettings.RoseDuskPreset,
            "Quartz Bloom",
            ColorFrom("#3B1B2D"),
            ColorFrom("#765167"),
            ColorFrom("#B83E70"),
            ColorFrom("#E6F8EEF4"),
            ColorFrom("#CDD5A8B9"),
            ColorFrom("#9DAD8E9E"),
            ColorFrom("#F5FBF2F5"),
            ColorFrom("#CDDDB6C3"),
            ColorFrom("#E2F0DAE4"),
            ColorFrom("#C8D4C4D8"),
            ColorFrom("#E7F4DEE8"),
            ColorFrom("#C2D5C5D8"),
            ColorFrom("#E7E9D7E0"),
            ColorFrom("#BFAEA0AF")),
        new(
            WidgetSettings.DeepNavyPreset,
            "Midnight Tide",
            ColorFrom("#CFEAFF"),
            ColorFrom("#8FB7D7"),
            ColorFrom("#8BD7FF"),
            ColorFrom("#D316243B"),
            ColorFrom("#B34E6EA0"),
            ColorFrom("#8B3E5A78"),
            ColorFrom("#E6233550"),
            ColorFrom("#A95C83AF"),
            ColorFrom("#C8182B45"),
            ColorFrom("#A0264066"),
            ColorFrom("#C0203655"),
            ColorFrom("#92253E62"),
            ColorFrom("#C85C7FA7"),
            ColorFrom("#A957759A")),
        new(
            WidgetSettings.GraphiteBloomPreset,
            "Graphite Gold",
            ColorFrom("#F3D98A"),
            ColorFrom("#BDA066"),
            ColorFrom("#F0C86A"),
            ColorFrom("#D11D2028"),
            ColorFrom("#A8697188"),
            ColorFrom("#86505B70"),
            ColorFrom("#E02A2E38"),
            ColorFrom("#AA758198"),
            ColorFrom("#C2252934"),
            ColorFrom("#983A4252"),
            ColorFrom("#C52E3442"),
            ColorFrom("#963E4658"),
            ColorFrom("#C96D778D"),
            ColorFrom("#9C70798C"))
    ];

    public static ColorPreset GetById(string? presetId)
    {
        var normalized = WidgetSettings.NormalizeColorPresetId(presetId);
        return Presets.First(preset => string.Equals(preset.Id, normalized, StringComparison.Ordinal));
    }

    public static double GetContrastRatio(WpfColor foreground, WpfColor background)
    {
        var foregroundOpaque = BlendOnWhite(foreground);
        var backgroundOpaque = BlendOnWhite(background);
        var light = Math.Max(GetRelativeLuminance(foregroundOpaque), GetRelativeLuminance(backgroundOpaque));
        var dark = Math.Min(GetRelativeLuminance(foregroundOpaque), GetRelativeLuminance(backgroundOpaque));
        return (light + 0.05d) / (dark + 0.05d);
    }

    private static WpfColor BlendOnWhite(WpfColor color)
    {
        if (color.A == byte.MaxValue)
        {
            return color;
        }

        var alpha = color.A / 255d;
        return WpfColor.FromRgb(
            (byte)Math.Round((color.R * alpha) + (255d * (1d - alpha))),
            (byte)Math.Round((color.G * alpha) + (255d * (1d - alpha))),
            (byte)Math.Round((color.B * alpha) + (255d * (1d - alpha))));
    }

    private static double GetRelativeLuminance(WpfColor color)
    {
        static double Convert(byte channel)
        {
            var normalized = channel / 255d;
            return normalized <= 0.03928d
                ? normalized / 12.92d
                : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Convert(color.R)) +
               (0.7152d * Convert(color.G)) +
               (0.0722d * Convert(color.B));
    }

    private static WpfColor ColorFrom(string value)
    {
        return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(value);
    }
}
