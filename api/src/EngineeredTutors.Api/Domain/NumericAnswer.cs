using System.Globalization;
using System.Text.RegularExpressions;

namespace EngineeredTutors.Api.Domain;

/// <summary>Parsing and marking of numeric (NUM) answers, DX-11 and SC-02.</summary>
public static partial class NumericAnswer
{
    /// <summary>Digits, an optional leading minus and at most one decimal separator (already turned into '.').</summary>
    [GeneratedRegex(@"^-?(\d+(\.\d*)?|\.\d+)$")]
    private static partial Regex Shape();

    /// <summary>
    /// Normalises learner input: whitespace removed (including non-breaking spaces), comma becomes a full stop,
    /// and a typographic minus (U+2212) becomes '-'. Returns null if the result is not a plain decimal number.
    /// </summary>
    public static decimal? Parse(string? raw)
    {
        if (raw is null) return null;
        var s = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray())
            .Replace(',', '.')
            .Replace('−', '-');
        if (s.Length == 0 || s.Length > 30 || !Shape().IsMatch(s)) return null;
        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>Correct if |given − answer| ≤ tolerance, in decimal arithmetic so edge values are accepted exactly.</summary>
    public static bool IsCorrect(decimal given, decimal answer, decimal tolerance) =>
        Math.Abs(given - answer) <= tolerance;
}
