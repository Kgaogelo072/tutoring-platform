using EngineeredTutors.Api.Data;

namespace EngineeredTutors.Api.Domain;

public sealed class ScoringOptions
{
    /// <summary>Topic score at or above this is Strong (SC-04).</summary>
    public decimal StrongMinPct { get; set; } = 70;
    /// <summary>Topic score at or above this (and below Strong) is Developing; below it is a Priority gap.</summary>
    public decimal DevelopingMinPct { get; set; } = 50;
}

/// <summary>Pure grading and topic scoring, SC-01 to SC-04.</summary>
public static class Scoring
{
    public sealed record Item(Question Question, Response Response);

    /// <summary>Marks a single response all-or-nothing. Unanswered scores 0.</summary>
    public static (bool IsCorrect, int Marks) Grade(Question q, Response r)
    {
        var correct = q.Type switch
        {
            QuestionType.MCQ => r.SelectedOptionId is { } sel && q.Options.Any(o => o.Id == sel && o.IsCorrect),
            QuestionType.NUM => r.NumericValue is { } v && q.AnswerValue is { } a &&
                                NumericAnswer.IsCorrect(v, a, q.Tolerance ?? 0),
            _ => false,
        };
        return (correct, correct ? q.Marks : 0);
    }

    public static decimal Pct(int earned, int available) =>
        available == 0 ? 0 : Math.Round(100m * earned / available, 2, MidpointRounding.AwayFromZero);

    public static Band BandFor(decimal pct, ScoringOptions o) =>
        pct >= o.StrongMinPct ? Band.Strong : pct >= o.DevelopingMinPct ? Band.Developing : Band.PriorityGap;

    /// <summary>Grades every response in place and returns topic scores plus the overall percentage.</summary>
    public static (List<TopicScore> Topics, decimal OverallPct) Score(Guid attemptId, IEnumerable<Item> items, ScoringOptions o)
    {
        var list = items.ToList();
        foreach (var (q, r) in list)
        {
            var (ok, marks) = Grade(q, r);
            r.IsCorrect = ok;
            r.MarksAwarded = marks;
        }

        var topics = list
            .GroupBy(i => i.Question.TopicId)
            .Select(g =>
            {
                var earned = g.Sum(i => i.Response.MarksAwarded ?? 0);
                var available = g.Sum(i => i.Question.Marks);
                var pct = Pct(earned, available);
                return new TopicScore
                {
                    AttemptId = attemptId,
                    TopicId = g.Key,
                    MarksEarned = earned,
                    MarksAvailable = available,
                    Pct = pct,
                    Band = BandFor(pct, o),
                };
            })
            .OrderBy(t => t.TopicId)
            .ToList();

        var overall = Pct(list.Sum(i => i.Response.MarksAwarded ?? 0), list.Sum(i => i.Question.Marks));
        return (topics, overall);
    }
}
