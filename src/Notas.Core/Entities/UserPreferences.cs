using System.Text.RegularExpressions;

namespace Notas.Core.Entities;

public sealed class UserPreferences
{
    private static readonly Regex AccentPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);

    public string UserId { get; set; } = "";
    public string EditorFont { get; set; } = Defaults.EditorFont;
    public double FontSize { get; set; } = Defaults.FontSize;
    public double LineHeight { get; set; } = Defaults.LineHeight;
    public int ColumnWidth { get; set; } = Defaults.ColumnWidth;
    public string Accent { get; set; } = Defaults.Accent;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static UserPreferences Normalize(UserPreferences value, string? userId = null) => new()
    {
        UserId = userId ?? value.UserId,
        EditorFont = IsAllowedFont(value.EditorFont) ? value.EditorFont : Defaults.EditorFont,
        FontSize = double.IsFinite(value.FontSize)
            ? Math.Clamp(value.FontSize, Defaults.MinFontSize, Defaults.MaxFontSize)
            : Defaults.FontSize,
        LineHeight = double.IsFinite(value.LineHeight)
            ? Math.Clamp(value.LineHeight, Defaults.MinLineHeight, Defaults.MaxLineHeight)
            : Defaults.LineHeight,
        ColumnWidth = Math.Clamp(value.ColumnWidth, Defaults.MinColumnWidth, Defaults.MaxColumnWidth),
        Accent = value.Accent is not null && AccentPattern.IsMatch(value.Accent) ? value.Accent : Defaults.Accent,
        UpdatedAt = value.UpdatedAt
    };

    public static bool IsAllowedFont(string? value) =>
        value is "serif" or "sans" or "mono" or "legivel";

    public static class Defaults
    {
        public const string EditorFont = "serif";
        public const double FontSize = 1.08;
        public const double LineHeight = 1.75;
        public const int ColumnWidth = 880;
        public const string Accent = "#8b5cf6";
        public const double MinFontSize = 0.9;
        public const double MaxFontSize = 1.5;
        public const double MinLineHeight = 1.4;
        public const double MaxLineHeight = 2.1;
        public const int MinColumnWidth = 640;
        public const int MaxColumnWidth = 1100;

        public static UserPreferences Create(string? userId = null) => new()
        {
            UserId = userId ?? "",
            EditorFont = EditorFont,
            FontSize = FontSize,
            LineHeight = LineHeight,
            ColumnWidth = ColumnWidth,
            Accent = Accent
        };
    }
}
