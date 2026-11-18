using System.Globalization;
using Notas.Core.Entities;

namespace Notas.Web.Services;

public static class AppearanceStyleBuilder
{
    private static readonly IReadOnlyDictionary<string, string> FontStacks = new Dictionary<string, string>
    {
        ["serif"] = "\"Source Serif 4\", \"Iowan Old Style\", \"Palatino Linotype\", Georgia, serif",
        ["sans"] = "\"Inter\", ui-sans-serif, system-ui, sans-serif",
        ["mono"] = "ui-monospace, \"SF Mono\", Menlo, Consolas, monospace",
        ["legivel"] = "\"Atkinson Hyperlegible\", system-ui, sans-serif"
    };

    public static string Build(UserPreferences preferences)
    {
        var value = UserPreferences.Normalize(preferences);
        var declarations = new List<string>(9);

        if (value.EditorFont != UserPreferences.Defaults.EditorFont)
            declarations.Add($"--font-doc:{FontStacks[value.EditorFont]}");
        if (value.FontSize != UserPreferences.Defaults.FontSize)
            declarations.Add($"--font-size-editor-body:{value.FontSize.ToString("0.##", CultureInfo.InvariantCulture)}rem");
        if (value.LineHeight != UserPreferences.Defaults.LineHeight)
            declarations.Add($"--line-height-editor:{value.LineHeight.ToString("0.##", CultureInfo.InvariantCulture)}");
        if (value.ColumnWidth != UserPreferences.Defaults.ColumnWidth)
            declarations.Add($"--editor-max-w:{value.ColumnWidth.ToString(CultureInfo.InvariantCulture)}px");
        if (!string.Equals(value.Accent, UserPreferences.Defaults.Accent, StringComparison.OrdinalIgnoreCase))
        {
            declarations.Add($"--accent:{value.Accent}");
            declarations.Add($"--accent-solid:{value.Accent}");
            declarations.Add("--accent-hover:color-mix(in srgb, var(--accent) 85%, white)");
            declarations.Add("--accent-active:color-mix(in srgb, var(--accent) 85%, black)");
            declarations.Add("--accent-soft:color-mix(in srgb, var(--accent) 14%, transparent)");
            declarations.Add("--accent-glow:color-mix(in srgb, var(--accent) 14%, transparent)");
            declarations.Add($"--on-accent:{GetOnAccent(value.Accent)}");
        }

        return string.Join(';', declarations);
    }

    private static string GetOnAccent(string accent)
    {
        var red = ToLinear(Convert.ToInt32(accent.Substring(1, 2), 16));
        var green = ToLinear(Convert.ToInt32(accent.Substring(3, 2), 16));
        var blue = ToLinear(Convert.ToInt32(accent.Substring(5, 2), 16));
        var luminance = 0.2126 * red + 0.7152 * green + 0.0722 * blue;
        return luminance > 0.179 ? "#18181b" : "#fff";
    }

    private static double ToLinear(int channel)
    {
        var value = channel / 255d;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
