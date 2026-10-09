namespace EngineeredTutors.Api.Tests;

public static class TestIds
{
    /// <summary>A valid SA ID number (real date of birth, Luhn check digit) for the given or a random date.</summary>
    public static string SaId(DateOnly? dob = null)
    {
        var d = dob ?? new DateOnly(2009, 1, 1).AddDays(Random.Shared.Next(0, 1500));
        var body = $"{d:yyMMdd}{Random.Shared.Next(0, 10000):D4}08";
        for (var check = 0; check < 10; check++)
        {
            var id = body + check;
            if (Luhn(id)) return id;
        }
        throw new InvalidOperationException();
    }

    private static bool Luhn(string digits)
    {
        var sum = 0;
        var dbl = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var n = digits[i] - '0';
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum += n;
            dbl = !dbl;
        }
        return sum % 10 == 0;
    }

    /// <summary>A unique SA mobile number, local format.</summary>
    public static string Phone() => "08" + Random.Shared.Next(10_000_000, 99_999_999);
}

/// <summary>Small question files with known answers, in the section 5.2 format.</summary>
public static class TestCsv
{
    public const string Header =
        "\"code\",\"section\",\"topic_code\",\"type\",\"marks\",\"difficulty\",\"stem\",\"option_a\",\"option_b\",\"option_c\",\"option_d\",\"correct_option\",\"answer_value\",\"tolerance\",\"unit\",\"image_ref\",\"explanation\"\n";

    public static string Row(string code, string topic, string type, string stem,
        string a, string b, string c, string d, string correct, string section = "Algebra") =>
        Q(code, section, topic, type, "2", "1", stem, a, b, c, d, correct, "", "", "", "", "Because.");

    public static string NumRow(string code, string topic, string stem, string answer, string tolerance,
        string unit = "", string section = "Algebra") =>
        Q(code, section, topic, "NUM", "2", "2", stem, "", "", "", "", "", answer, tolerance, unit, "", "Worked answer.");

    private static string Q(params string[] f) => string.Join(',', f.Select(x => "\"" + x.Replace("\"", "\"\"") + "\"")) + "\n";

    /// <summary>Q1 MCQ (correct B), Q2 NUM 5.05 ± 0.01 N, Q3 NUM −4 exact. 6 marks over three topics.</summary>
    public static readonly string Maths = Header +
        Row("T01", "MATH-11-ALG", "MCQ", "Simplify $2x + 3x$, with commas, in the stem.", "$6x$", "$5x$", "$5x^2$", "$x$", "B") +
        NumRow("T02", "MATH-11-TRIG", "Find $F$ to two decimals.", "5.05", "0.01", "N", "Trigonometry") +
        NumRow("T03", "MATH-11-FUN", "Solve $3^{x+1} = \\frac{1}{27}$", "-4", "0", "", "Functions");

    public static readonly string Physics = Header +
        Row("P01", "PHYS-11-NEW", "MCQ", "Which law is $F = ma$?", "First", "Second", "Third", "Zeroth", "B", "Newton");
}
