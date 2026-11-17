using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Notas.Core.Common;

public static class Validation
{
    /// <summary>Valida DataAnnotations e devolve a primeira mensagem de erro (ou null).</summary>
    public static string? FirstError(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results.FirstOrDefault()?.ErrorMessage;
    }
}

public static partial class TagRules
{
    public const int MaxLength = 30;
    public const int MaxPerNote = 10;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Normaliza: sem '#', minúsculas, espaços colapsados. Retorna false se vazio.</summary>
    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = Spaces().Replace((raw ?? "").Trim().TrimStart('#').Trim(), " ").ToLowerInvariant();
        return normalized.Length > 0;
    }
}

public static partial class Snippets
{
    [GeneratedRegex(@"[#>*_`~\[\]!]+")]
    private static partial Regex Noise();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static string From(string? content, int max = 140)
    {
        if (string.IsNullOrWhiteSpace(content)) return "";
        var text = Spaces().Replace(Noise().Replace(content, ""), " ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }
}
