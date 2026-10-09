using System.Globalization;
using System.Text;

namespace EngineeredTutors.Api.Domain;

public static class Normalise
{
    /// <summary>
    /// SA mobile number to E.164 (+27XXXXXXXXX), REG-01. Accepts 0821234567, 082 123 4567, +27 82 123 4567,
    /// 27821234567. Returns null if it is not a 9-digit SA mobile number after the country code.
    /// </summary>
    public static string? SaMobile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        var trimmed = raw.Trim();

        string national;
        if (trimmed.StartsWith('+'))
        {
            if (!digits.StartsWith("27")) return null;
            national = digits[2..];
        }
        else if (digits.StartsWith("27") && digits.Length == 11) national = digits[2..];
        else if (digits.StartsWith('0') && digits.Length == 10) national = digits[1..];
        else return null;

        // SA mobile numbers start with 6, 7 or 8 after the leading 0.
        if (national.Length != 9 || national[0] is not ('6' or '7' or '8')) return null;
        return "+27" + national;
    }

    /// <summary>
    /// Duplicate-check key (REG-07): first name + surname, lower-cased, with spaces, punctuation and accents
    /// removed. "Thabo Mokoena", "thabo  mokoena" and "Thábo Mokoena" all give "thabomokoena".
    /// </summary>
    public static string NameKey(string firstName, string surname)
    {
        var decomposed = (firstName + surname).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Trims and collapses internal whitespace in free-text names.</summary>
    public static string Text(string? raw) =>
        string.Join(' ', (raw ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
