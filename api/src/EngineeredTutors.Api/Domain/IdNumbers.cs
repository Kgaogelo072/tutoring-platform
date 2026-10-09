using System.Globalization;
using EngineeredTutors.Api.Data;

namespace EngineeredTutors.Api.Domain;

/// <summary>Validation for the optional learner ID number (REG-02, REG-09).</summary>
public static class IdNumbers
{
    public sealed record Result(bool IsValid, string? Normalised, string? Error, DateOnly? DateOfBirth);

    /// <summary>Removes spaces and dashes and upper-cases, so "8001 0150 0908 7" and "800101-5009087" match.</summary>
    public static string Normalise(string raw) =>
        new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    public static Result Validate(IdType type, string raw, DateOnly today)
    {
        var id = Normalise(raw);
        return type switch
        {
            IdType.SA_ID => ValidateSaId(id, today),
            IdType.PASSPORT => ValidatePassport(id),
            _ => new(false, null, "Choose SA ID or passport.", null),
        };
    }

    private static Result ValidateSaId(string id, DateOnly today)
    {
        if (id.Length != 13 || !id.All(char.IsAsciiDigit))
            return new(false, null, "An SA ID number has 13 digits.", null);

        var dob = DateOfBirth(id, today);
        if (dob is null)
            return new(false, null, "The first six digits must be a real date of birth (YYMMDD).", null);

        if (!LuhnValid(id))
            return new(false, null, "This ID number isn't valid. Please check the digits.", null);

        return new(true, id, null, dob);
    }

    private static Result ValidatePassport(string id)
    {
        if (id.Length is < 6 or > 20 || !id.All(char.IsAsciiLetterOrDigit))
            return new(false, null, "A passport number has 6 to 20 letters and digits.", null);
        return new(true, id, null, null);
    }

    /// <summary>
    /// YYMMDD from the first six digits. Two-digit years later than this year are taken as 1900s; a date in the
    /// future is invalid.
    /// </summary>
    internal static DateOnly? DateOfBirth(string id, DateOnly today)
    {
        var yy = int.Parse(id[..2], CultureInfo.InvariantCulture);
        var century = yy <= today.Year % 100 ? today.Year / 100 * 100 : (today.Year / 100 - 1) * 100;
        if (!DateOnly.TryParseExact($"{century + yy:D4}{id[2..6]}", "yyyyMMdd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dob))
            return null;
        return dob > today ? null : dob;
    }

    /// <summary>Luhn check over all 13 digits; the last digit is the check digit.</summary>
    internal static bool LuhnValid(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }
}
