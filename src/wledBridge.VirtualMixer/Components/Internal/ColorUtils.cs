using System.Globalization;

namespace wledBridge.VirtualMixer.Components.Internal;

/// <summary>
/// WCAG-based color helpers: relative luminance per WCAG 2.x (sRGB linearization) to pick
/// automatically contrasting text colors, plus alpha overlays so stored colors stay untouched
/// while rendering stays subtle.
/// </summary>
internal static class ColorUtils
{
    public static (byte R, byte G, byte B)? ParseHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        var value = hex.TrimStart('#');
        if (value.Length == 8)
        {
            value = value[..6]; // ignore alpha channel from pickers
        }

        if (value.Length != 6 ||
            !byte.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return null;
        }

        return (r, g, b);
    }

    /// <summary>WCAG relative luminance (0 = black, 1 = white).</summary>
    public static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Linearize(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearize(r)) + (0.7152 * Linearize(g)) + (0.0722 * Linearize(b));
    }

    /// <summary>Black or white, whichever has the higher WCAG contrast against the given color.</summary>
    public static string ContrastingTextColor(string? backgroundHex)
    {
        if (ParseHex(backgroundHex) is not { } rgb)
        {
            return "var(--mud-palette-text-primary, #fff)";
        }

        var luminance = RelativeLuminance(rgb.R, rgb.G, rgb.B);
        // Contrast vs white: (1.05)/(L+0.05); vs black: (L+0.05)/0.05 - white wins below ~0.179.
        return luminance < 0.179 ? "#ffffff" : "#000000";
    }

    /// <summary>CSS rgba() from a stored hex color - the stored value itself stays unchanged.</summary>
    public static string ToRgba(string hex, double alpha)
    {
        if (ParseHex(hex) is not { } rgb)
        {
            return "transparent";
        }

        return $"rgba({rgb.R},{rgb.G},{rgb.B},{alpha.ToString("0.###", CultureInfo.InvariantCulture)})";
    }

    public const string AmberHex = "#FFBF00";

    /// <summary>LED color at a given brightness (0..127): mixes the color towards dark.</summary>
    public static string LedColor(string? colorHex, int brightness)
    {
        var rgb = ParseHex(colorHex) ?? ParseHex(AmberHex)!.Value;
        var factor = 0.12 + (Math.Clamp(brightness, 0, 127) / 127.0 * 0.88);
        return $"rgb({(int)(rgb.R * factor)},{(int)(rgb.G * factor)},{(int)(rgb.B * factor)})";
    }
}
