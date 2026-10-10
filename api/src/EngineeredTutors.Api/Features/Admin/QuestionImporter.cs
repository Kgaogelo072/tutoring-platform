using System.Globalization;
using System.Text.RegularExpressions;
using EngineeredTutors.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EngineeredTutors.Api.Features.Admin;

public sealed record ImportError(int Row, string Column, string Message);

public sealed record ImportResult(Guid AssessmentId, Subject Subject, int Version, int QuestionCount, int TotalMarks);

/// <summary>
/// AD-06: validates a whole question file and, only if every row is valid, creates a new draft assessment version
/// with fresh question rows. Published versions are never touched (SC-06).
/// </summary>
public sealed partial class QuestionImporter(AppDbContext db, TimeProvider clock)
{
    public static readonly string[] Columns =
    [
        "code", "section", "topic_code", "type", "marks", "difficulty", "stem",
        "option_a", "option_b", "option_c", "option_d", "correct_option",
        "answer_value", "tolerance", "unit", "image_ref", "explanation",
    ];

    private static readonly string[] OptionColumns = ["option_a", "option_b", "option_c", "option_d"];

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+\.(png|jpe?g|webp|svg)$", RegexOptions.IgnoreCase)]
    private static partial Regex ImageName();

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex PlainDecimal();

    public async Task<(ImportResult? Result, List<ImportError> Errors)> ImportAsync(
        Subject subject, string csvText, int timeLimitMinutes, CancellationToken ct)
    {
        var topics = await db.Topics.AsNoTracking().ToDictionaryAsync(t => t.Code, ct);
        var (questions, errors) = Validate(subject, csvText, topics);
        if (errors.Count > 0) return (null, errors);

        var now = clock.GetUtcNow().UtcDateTime;
        var nextVersion = (await db.Assessments.Where(a => a.Subject == subject).MaxAsync(a => (int?)a.Version, ct) ?? 0) + 1;
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Version = nextVersion,
            TimeLimitMinutes = timeLimitMinutes,
            CreatedAt = now,
        };
        db.Assessments.Add(assessment);

        var order = 0;
        foreach (var (q, section) in questions)
        {
            db.Questions.Add(q);
            db.AssessmentQuestions.Add(new AssessmentQuestion
            {
                AssessmentId = assessment.Id,
                QuestionId = q.Id,
                Order = ++order,
                Section = section,
            });
        }

        await db.SaveChangesAsync(ct);
        return (new ImportResult(assessment.Id, subject, nextVersion, questions.Count, questions.Sum(q => q.Question.Marks)), []);
    }

    /// <summary>Pure validation; every problem is reported with its row number and column.</summary>
    public static (List<(Question Question, string Section)> Questions, List<ImportError> Errors) Validate(
        Subject subject, string csvText, IReadOnlyDictionary<string, Topic> topics)
    {
        var errors = new List<ImportError>();
        var questions = new List<(Question, string)>();

        List<Csv.Record> records;
        try { records = Csv.Parse(csvText); }
        catch (FormatException ex) { return ([], [new ImportError(0, "", ex.Message)]); }

        if (records.Count == 0) return ([], [new ImportError(0, "", "The file is empty.")]);

        var header = records[0].Fields.Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var missing = Columns.Where(c => !header.Contains(c)).ToList();
        if (missing.Count > 0)
            return ([], [new ImportError(1, string.Join(", ", missing), "Missing column(s) in the header row.")]);
        var col = Columns.ToDictionary(c => c, c => Array.IndexOf(header, c));

        if (records.Count == 1) return ([], [new ImportError(0, "", "The file has a header but no questions.")]);

        var codes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var rec in records.Skip(1))
        {
            var row = rec.RowNumber;
            string Get(string c) => col[c] < rec.Fields.Length ? rec.Fields[col[c]].Trim() : "";
            void Err(string c, string msg) => errors.Add(new ImportError(row, c, msg));

            if (rec.Fields.Length != header.Length)
                Err("", $"Expected {header.Length} fields but found {rec.Fields.Length}. Check the quotes on this row.");

            var code = Get("code");
            if (code.Length is 0 or > 16) Err("code", "Code is required (up to 16 characters).");
            else if (codes.TryGetValue(code, out var firstRow)) Err("code", $"Duplicate code; first used on row {firstRow}.");
            else codes[code] = row;

            var section = Get("section");
            if (section.Length is 0 or > 64) Err("section", "Section is required (up to 64 characters).");

            var topicCode = Get("topic_code");
            Topic? topic = null;
            if (!topics.TryGetValue(topicCode, out topic)) Err("topic_code", $"Unknown topic code '{topicCode}'.");
            else if (topic.Subject != subject)
                Err("topic_code", $"Topic {topicCode} belongs to {Display.SubjectName(topic.Subject)}, not {Display.SubjectName(subject)}.");

            QuestionType? type = Get("type").ToUpperInvariant() switch
            {
                "MCQ" => QuestionType.MCQ,
                "NUM" => QuestionType.NUM,
                _ => null,
            };
            if (type is null) Err("type", "Type must be MCQ or NUM.");

            if (!int.TryParse(Get("marks"), NumberStyles.None, CultureInfo.InvariantCulture, out var marks) || marks is < 1 or > 100)
                Err("marks", "Marks must be a whole number from 1 to 100.");

            if (!int.TryParse(Get("difficulty"), NumberStyles.None, CultureInfo.InvariantCulture, out var difficulty) || difficulty is < 1 or > 3)
                Err("difficulty", "Difficulty must be 1, 2 or 3.");

            var stem = Get("stem");
            if (stem.Length == 0) Err("stem", "Stem is required.");
            CheckLatex(stem, "stem", Err);

            var explanation = Get("explanation");
            if (explanation.Length == 0) Err("explanation", "Explanation is required.");
            CheckLatex(explanation, "explanation", Err);

            var imageRef = Get("image_ref");
            if (imageRef.Length > 0 && (imageRef.Length > 100 || !ImageName().IsMatch(imageRef)))
                Err("image_ref", "Image must be a plain file name ending in .png, .jpg, .webp or .svg.");

            var q = new Question
            {
                Id = Guid.NewGuid(),
                Code = code,
                TopicId = topic?.Id ?? 0,
                Type = type ?? QuestionType.MCQ,
                StemLatex = stem,
                ImageUrl = imageRef.Length > 0 ? $"question-images/{imageRef}" : null,
                Marks = marks,
                Difficulty = difficulty,
                Explanation = explanation,
            };

            if (type == QuestionType.MCQ)
            {
                var correct = Get("correct_option").ToUpperInvariant();
                if (correct is not ("A" or "B" or "C" or "D")) Err("correct_option", "Correct option must be A, B, C or D.");
                for (var i = 0; i < 4; i++)
                {
                    var text = Get(OptionColumns[i]);
                    if (text.Length == 0) Err(OptionColumns[i], "MCQ questions need four options.");
                    CheckLatex(text, OptionColumns[i], Err);
                    var label = ((char)('A' + i)).ToString();
                    q.Options.Add(new QuestionOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = q.Id,
                        Label = label,
                        TextLatex = text,
                        IsCorrect = label == correct,
                    });
                }
                if (Get("answer_value").Length > 0) Err("answer_value", "Leave answer_value empty for MCQ questions.");
            }
            else if (type == QuestionType.NUM)
            {
                var answer = Get("answer_value");
                if (!PlainDecimal().IsMatch(answer))
                    Err("answer_value", "NUM questions need a numeric answer with a full stop as the decimal sign, e.g. 5.05.");
                else q.AnswerValue = decimal.Parse(answer, CultureInfo.InvariantCulture);

                var tolerance = Get("tolerance");
                if (!PlainDecimal().IsMatch(tolerance) || tolerance.StartsWith('-'))
                    Err("tolerance", "NUM questions need a tolerance of 0 or more, e.g. 0 or 0.01.");
                else q.Tolerance = decimal.Parse(tolerance, CultureInfo.InvariantCulture);

                var unit = Get("unit");
                if (unit.Length > 16) Err("unit", "Unit must be 16 characters or fewer.");
                q.Unit = unit.Length > 0 ? unit : null;

                foreach (var c in OptionColumns.Append("correct_option"))
                    if (Get(c).Length > 0) Err(c, "Leave the options and correct_option empty for NUM questions.");
            }

            questions.Add((q, section));
        }

        return (questions, errors);
    }

    /// <summary>
    /// Structural LaTeX check: $ delimiters must pair up, and braces must balance inside each maths span. Full
    /// rendering is checked by the admin preview, which uses KaTeX like the learner page.
    /// </summary>
    internal static void CheckLatex(string text, string column, Action<string, string> err)
    {
        var inMath = false;
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length) { i++; continue; } // \$, \{, \} and commands
            if (c == '$')
            {
                if (inMath && depth != 0) { err(column, "Unbalanced { } inside $…$."); return; }
                inMath = !inMath;
                depth = 0;
                continue;
            }
            if (!inMath) continue;
            if (c == '{') depth++;
            else if (c == '}' && --depth < 0) { err(column, "Unbalanced { } inside $…$."); return; }
        }
        if (inMath) err(column, "A $ is not closed. Maths must be written between a pair of $ signs.");
    }
}
