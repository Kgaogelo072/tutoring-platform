using EngineeredTutors.Api.Data;

namespace EngineeredTutors.Api.Features.Attempts;

/// <summary>Instructions and status for the test page (DX-02, DX-08, DX-12).</summary>
public sealed record AttemptStateDto(
    AttemptStatus Status,
    string LearnerFirstName,
    Subject Subject,
    string SubjectName,
    int TimeLimitMinutes,
    int QuestionCount,
    int NumericQuestionCount,
    bool CalculatorAllowed,
    DateTime? StartedAt,
    DateTime? DeadlineAt,
    DateTime ServerNow,
    int? RemainingSeconds);

/// <summary>A question as the learner sees it. Never includes the correct option, answer value or tolerance.</summary>
public sealed record QuestionDto(
    Guid Id,
    int Number,
    QuestionType Type,
    string StemLatex,
    string? ImageUrl,
    int Marks,
    string? Unit,
    IReadOnlyList<OptionDto> Options,
    Guid? SelectedOptionId,
    string? NumericRaw);

public sealed record OptionDto(Guid Id, string TextLatex);

public sealed record QuestionsDto(int RemainingSeconds, DateTime DeadlineAt, IReadOnlyList<QuestionDto> Questions);

public sealed class SaveResponseRequest
{
    /// <summary>MCQ: the chosen option, or null to clear.</summary>
    public Guid? SelectedOptionId { get; set; }
    /// <summary>NUM: the learner's input as typed, or null/empty to clear.</summary>
    public string? NumericRaw { get; set; }
}

public sealed record SaveResponseResult(Guid QuestionId, DateTime? AnsweredAt, decimal? NumericValue);
