using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Domain;
using EngineeredTutors.Api.Features.Admin;

namespace EngineeredTutors.Api.Tests.Unit;

public class IdNumberTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    [Theory]
    [InlineData("8001015009087")]
    [InlineData("8001 0150 0908 7")]
    [InlineData("800101-5009087")]
    public void Valid_SA_ID_is_accepted_and_normalised(string raw)
    {
        var r = IdNumbers.Validate(IdType.SA_ID, raw, Today);
        Assert.True(r.IsValid, r.Error);
        Assert.Equal("8001015009087", r.Normalised);
        Assert.Equal(new DateOnly(1980, 1, 1), r.DateOfBirth);
    }

    [Fact]
    public void REG09_wrong_check_digit_is_rejected()
    {
        var r = IdNumbers.Validate(IdType.SA_ID, "8001015009088", Today);
        Assert.False(r.IsValid);
        Assert.Contains("isn't valid", r.Error);
    }

    [Theory]
    [InlineData("8013015009087")] // month 13
    [InlineData("8002305009087")] // 30 February
    public void REG09_impossible_date_of_birth_is_rejected(string id)
    {
        var r = IdNumbers.Validate(IdType.SA_ID, id, Today);
        Assert.False(r.IsValid);
        Assert.Contains("date of birth", r.Error);
    }

    [Theory]
    [InlineData("800101500908")]
    [InlineData("80010150090877")]
    [InlineData("80010150090A7")]
    public void SA_ID_must_be_13_digits(string id) =>
        Assert.False(IdNumbers.Validate(IdType.SA_ID, id, Today).IsValid);

    [Fact]
    public void Two_digit_year_up_to_this_year_is_2000s()
    {
        var id = TestIds.SaId(new DateOnly(2010, 5, 17));
        Assert.Equal(new DateOnly(2010, 5, 17), IdNumbers.Validate(IdType.SA_ID, id, Today).DateOfBirth);
    }

    [Theory]
    [InlineData("A1234567", true)]
    [InlineData("a12 345", true)]
    [InlineData("AB12", false)]
    [InlineData("A1234567890123456789X", false)]
    [InlineData("A12/4567", false)]
    public void Passport_is_6_to_20_letters_and_digits(string raw, bool ok) =>
        Assert.Equal(ok, IdNumbers.Validate(IdType.PASSPORT, raw, Today).IsValid);

    [Fact]
    public void Test_id_generator_makes_valid_numbers()
    {
        for (var i = 0; i < 50; i++)
            Assert.True(IdNumbers.Validate(IdType.SA_ID, TestIds.SaId(), Today).IsValid);
    }
}

public class NormaliseTests
{
    [Theory]
    [InlineData("082 123 4567", "+27821234567")]
    [InlineData("0821234567", "+27821234567")]
    [InlineData("+27 82 123 4567", "+27821234567")]
    [InlineData("27821234567", "+27821234567")]
    [InlineData("(071) 555-0000", "+27715550000")]
    public void REG01_SA_mobile_is_normalised_to_plus27(string raw, string expected) =>
        Assert.Equal(expected, Normalise.SaMobile(raw));

    [Theory]
    [InlineData("011 123 4567")] // landline
    [InlineData("082 123 456")]
    [InlineData("+44 7700 900123")]
    [InlineData("")]
    public void Non_mobile_numbers_are_rejected(string raw) => Assert.Null(Normalise.SaMobile(raw));

    [Theory]
    [InlineData("thabo", "Mokoena")]
    [InlineData("Thabo", "Mokoena")]
    [InlineData(" THABO ", "mokoena")]
    [InlineData("Thábo", "Mokoena")]
    [InlineData("Tha bo", "Mo-koena")]
    public void REG07_name_key_ignores_case_spaces_and_accents(string first, string surname) =>
        Assert.Equal("thabomokoena", Normalise.NameKey(first, surname));
}

public class NumericAnswerTests
{
    [Theory]
    [InlineData("5,05", true)]
    [InlineData("5.05", true)]
    [InlineData(" 5.05 ", true)]
    [InlineData("5,06", true)]
    [InlineData("5,04", true)]
    [InlineData("5,07", false)]
    [InlineData("5,03", false)]
    public void Acceptance_numeric_input_with_tolerance(string raw, bool correct)
    {
        var v = NumericAnswer.Parse(raw);
        Assert.NotNull(v);
        Assert.Equal(correct, NumericAnswer.IsCorrect(v!.Value, 5.05m, 0.01m));
    }

    [Theory]
    [InlineData("5.05m")]
    [InlineData("5,0,5")]
    [InlineData("--4")]
    [InlineData("4-")]
    [InlineData("1e3")]
    [InlineData("½")]
    [InlineData("")]
    [InlineData("   ")]
    public void DX11_anything_but_a_plain_number_is_flagged(string raw) => Assert.Null(NumericAnswer.Parse(raw));

    [Theory]
    [InlineData("-4", -4)]
    [InlineData("−4", -4)]
    [InlineData("1 250", 1250)]
    [InlineData("0,58", 0.58)]
    [InlineData(".5", 0.5)]
    public void Parses_learner_formats(string raw, double expected) =>
        Assert.Equal((decimal)expected, NumericAnswer.Parse(raw));

    [Fact]
    public void SC02_edge_of_tolerance_is_accepted_exactly()
    {
        // 0.1 + 0.2 style floating point error would reject this; decimal must not.
        Assert.True(NumericAnswer.IsCorrect(0.3m, 0.1m + 0.1m, 0.1m));
        Assert.True(NumericAnswer.IsCorrect(1.26m, 1.25m, 0.01m));
        Assert.False(NumericAnswer.IsCorrect(1.2601m, 1.25m, 0.01m));
    }
}

public class ScoringTests
{
    private static readonly ScoringOptions Bands = new();

    private static Question Mcq(int topic, out Guid correct, out Guid wrong)
    {
        correct = Guid.NewGuid();
        wrong = Guid.NewGuid();
        return new Question
        {
            Id = Guid.NewGuid(), TopicId = topic, Type = QuestionType.MCQ, Marks = 2,
            Options = [new() { Id = correct, IsCorrect = true }, new() { Id = wrong }],
        };
    }

    private static Question Num(int topic, decimal answer, decimal tol) => new()
    {
        Id = Guid.NewGuid(), TopicId = topic, Type = QuestionType.NUM, Marks = 2, AnswerValue = answer, Tolerance = tol,
    };

    [Fact]
    public void SC01_SC03_grades_all_or_nothing_and_groups_by_topic()
    {
        var q1 = Mcq(1, out var right, out _);
        var q2 = Mcq(1, out _, out var wrong);
        var q3 = Num(2, 5.05m, 0.01m);
        var q4 = Num(2, -4, 0);
        var items = new[]
        {
            new Scoring.Item(q1, new Response { SelectedOptionId = right }),
            new Scoring.Item(q2, new Response { SelectedOptionId = wrong }),
            new Scoring.Item(q3, new Response { NumericValue = 5.06m }),
            new Scoring.Item(q4, new Response()), // unanswered
        };

        var (topics, overall) = Scoring.Score(Guid.NewGuid(), items, Bands);

        Assert.Equal(50m, overall);
        Assert.Collection(topics,
            t => { Assert.Equal(1, t.TopicId); Assert.Equal(2, t.MarksEarned); Assert.Equal(4, t.MarksAvailable); Assert.Equal(50m, t.Pct); },
            t => { Assert.Equal(2, t.TopicId); Assert.Equal(2, t.MarksEarned); Assert.Equal(50m, t.Pct); });
        Assert.Equal([true, false, true, false], items.Select(i => i.Response.IsCorrect!.Value));
        Assert.Equal(0, items[3].Response.MarksAwarded);
    }

    [Theory]
    [InlineData(100, Band.Strong)]
    [InlineData(70, Band.Strong)]
    [InlineData(69.99, Band.Developing)]
    [InlineData(50, Band.Developing)]
    [InlineData(49.99, Band.PriorityGap)]
    [InlineData(0, Band.PriorityGap)]
    public void SC04_band_thresholds(double pct, Band band) => Assert.Equal(band, Scoring.BandFor((decimal)pct, Bands));

    [Fact]
    public void SC04_thresholds_come_from_config() =>
        Assert.Equal(Band.Developing, Scoring.BandFor(75, new ScoringOptions { StrongMinPct = 80, DevelopingMinPct = 60 }));
}

public class CsvTests
{
    [Fact]
    public void Respects_quotes_commas_doubled_quotes_and_line_breaks()
    {
        var text = "﻿\"a\",\"b\"\r\n\"1, 2\",\"say \"\"hi\"\"\"\n\"line\nbreak\",\"\"\n";
        var rows = Csv.Parse(text);
        Assert.Equal(3, rows.Count);
        Assert.Equal(new[] { "1, 2", "say \"hi\"" }, rows[1].Fields);
        Assert.Equal(new[] { "line\nbreak", "" }, rows[2].Fields);
        Assert.Equal(3, rows[2].RowNumber);
    }

    [Fact]
    public void Unclosed_quote_is_an_error() => Assert.Throws<FormatException>(() => Csv.Parse("\"a,b\n"));

    [Fact]
    public void Writer_neutralises_spreadsheet_formulas() =>
        Assert.Equal("\"'=HYPERLINK(1)\",\"+27821234567\",\"-4\"\r\n", Csv.Line(["=HYPERLINK(1)", "+27821234567", "-4"]));
}

public class ImportValidationTests
{
    private static readonly Dictionary<string, Topic> Topics = TopicSeed.All.ToDictionary(t => t.Code);

    [Fact]
    public void Valid_file_has_no_errors()
    {
        var (questions, errors) = QuestionImporter.Validate(Subject.Maths, TestCsv.Maths, Topics);
        Assert.Empty(errors);
        Assert.Equal(3, questions.Count);
        Assert.Equal(5.05m, questions[1].Question.AnswerValue);
        Assert.Equal("Algebra", questions[0].Section);
    }

    [Fact]
    public void AD06_each_problem_is_reported_with_its_row()
    {
        var csv = TestCsv.Header +
            TestCsv.Row("X1", "MATH-99-NOPE", "MCQ", "$x$", "A", "B", "C", "D", "B") +       // row 2: unknown topic
            TestCsv.Row("X2", "MATH-11-ALG", "MCQ", "$x$", "A", "", "C", "D", "E") +         // row 3: missing option, bad correct
            TestCsv.NumRow("X3", "MATH-11-ALG", "Solve $x$", "5,05", "-1") +                  // row 4: comma answer, negative tolerance
            TestCsv.Row("X1", "MATH-11-ALG", "MCQ", "$\\frac{1}{2$", "A", "B", "C", "D", "A"); // row 5: duplicate code, bad LaTeX

        var (_, errors) = QuestionImporter.Validate(Subject.Maths, csv, Topics);

        Assert.Contains(errors, e => e is { Row: 2, Column: "topic_code" });
        Assert.Contains(errors, e => e is { Row: 3, Column: "option_b" });
        Assert.Contains(errors, e => e is { Row: 3, Column: "correct_option" });
        Assert.Contains(errors, e => e is { Row: 4, Column: "answer_value" });
        Assert.Contains(errors, e => e is { Row: 4, Column: "tolerance" });
        Assert.Contains(errors, e => e is { Row: 5, Column: "code" });
        Assert.Contains(errors, e => e is { Row: 5, Column: "stem" });
    }

    [Fact]
    public void Topic_must_belong_to_the_subject_being_imported()
    {
        var (_, errors) = QuestionImporter.Validate(Subject.PhysicalSciences, TestCsv.Maths, Topics);
        Assert.Contains(errors, e => e.Column == "topic_code" && e.Message.Contains("belongs to Mathematics"));
    }

    [Fact]
    public void Missing_columns_are_reported()
    {
        var (_, errors) = QuestionImporter.Validate(Subject.Maths, "\"code\",\"stem\"\n\"M1\",\"x\"\n", Topics);
        Assert.Single(errors);
        Assert.Contains("topic_code", errors[0].Column);
    }

    /// <summary>The real v1.1 files, when present locally. They aren't committed because they hold the answers.</summary>
    [Theory]
    [InlineData(Subject.Maths, "engineered-tutors-maths-diagnostic-v1.1.csv", 25, 50)]
    [InlineData(Subject.PhysicalSciences, "engineered-tutors-physical-sciences-diagnostic-v1.1.csv", 20, 40)]
    public void Real_question_files_validate(Subject subject, string file, int count, int marks)
    {
        var dir = Environment.GetEnvironmentVariable("ET_QUESTIONS_DIR");
        var path = dir is null ? null : Path.Combine(dir, file);
        if (path is null || !File.Exists(path)) return; // not available on this machine

        var (questions, errors) = QuestionImporter.Validate(subject, File.ReadAllText(path), Topics);
        Assert.Empty(errors);
        Assert.Equal(count, questions.Count);
        Assert.Equal(marks, questions.Sum(q => q.Question.Marks));
    }
}
