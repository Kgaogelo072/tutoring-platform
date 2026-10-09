namespace EngineeredTutors.Api.Data;

// Entities follow section 5.1 of the Diagnostic MVP Requirements v1.2. All timestamps are UTC (NF-07).

public enum Subject { Maths, PhysicalSciences }

public enum IdType { SA_ID, PASSPORT }

public enum QuestionType { MCQ, NUM }

public enum AttemptStatus { NotStarted, InProgress, Submitted }

public enum LeadStatus { New, Tested, ReportSent, CallBooked, Registered, NotInterested }

public enum Band { Strong, Developing, PriorityGap }

public class Parent
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    /// <summary>E.164 format, e.g. +27821234567 (REG-01).</summary>
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public List<Learner> Learners { get; set; } = [];
    public List<Consent> Consents { get; set; } = [];
    public Lead? Lead { get; set; }
}

public class Consent
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public string TextVersion { get; set; } = "";
    public DateTime AcceptedAt { get; set; }
    public string IpAddress { get; set; } = "";
}

public class Learner
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public Parent Parent { get; set; } = null!;
    public string FirstName { get; set; } = "";
    public string Surname { get; set; } = "";
    public string School { get; set; } = "";
    public int Grade { get; set; }
    public string Goal { get; set; } = "";
    /// <summary>First name + surname, lower-cased with spaces and accents removed. Fallback duplicate key (REG-07).</summary>
    public string NameKey { get; set; } = "";
    public IdType? IdType { get; set; }
    /// <summary>AES-GCM ciphertext of the normalised ID number (NF-09).</summary>
    public string? IdNumberEncrypted { get; set; }
    /// <summary>HMAC-SHA256 of the normalised ID number; unique when set.</summary>
    public string? IdNumberHash { get; set; }
    public string? IdLast4 { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<LearnerSubject> Subjects { get; set; } = [];
    public List<Attempt> Attempts { get; set; } = [];

    public string FullName => $"{FirstName} {Surname}";
}

public class LearnerSubject
{
    public Guid LearnerId { get; set; }
    public Subject Subject { get; set; }
    public int? LatestMark { get; set; }
}

public class Lead
{
    public Guid Id { get; set; }
    public Guid ParentId { get; set; }
    public Parent Parent { get; set; } = null!;
    public LeadStatus Status { get; set; }
    public string Notes { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public class Topic
{
    public int Id { get; set; }
    public Subject Subject { get; set; }
    public int Grade { get; set; }
    /// <summary>e.g. MATH-11-TRIG</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Question
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public QuestionType Type { get; set; }
    public string StemLatex { get; set; } = "";
    public string? ImageUrl { get; set; }
    public int Marks { get; set; }
    public int Difficulty { get; set; }
    public decimal? AnswerValue { get; set; }
    public decimal? Tolerance { get; set; }
    public string? Unit { get; set; }
    public string Explanation { get; set; } = "";

    public List<QuestionOption> Options { get; set; } = [];
}

public class QuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    /// <summary>A–D as written in the import file. Display order is shuffled per attempt (DX-05).</summary>
    public string Label { get; set; } = "";
    public string TextLatex { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public class Assessment
{
    public Guid Id { get; set; }
    public Subject Subject { get; set; }
    public int Version { get; set; }
    public int TimeLimitMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Null while draft. Once set, the assessment's questions are frozen (SC-06).</summary>
    public DateTime? PublishedAt { get; set; }

    public List<AssessmentQuestion> Questions { get; set; } = [];
}

public class AssessmentQuestion
{
    public Guid AssessmentId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public int Order { get; set; }
    public string Section { get; set; } = "";
}

public class Attempt
{
    public Guid Id { get; set; }
    public Guid LearnerId { get; set; }
    public Learner Learner { get; set; } = null!;
    public Guid AssessmentId { get; set; }
    public Assessment Assessment { get; set; } = null!;
    /// <summary>SHA-256 of the access token. The token itself is never stored.</summary>
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime TokenExpiresAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? DeadlineAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public AttemptStatus Status { get; set; }
    public decimal? OverallPct { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<Response> Responses { get; set; } = [];
    public List<TopicScore> TopicScores { get; set; } = [];
}

public class Response
{
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    /// <summary>Position of this question in the attempt (from AssessmentQuestion.Order).</summary>
    public int Order { get; set; }
    public Guid? SelectedOptionId { get; set; }
    /// <summary>Comma-separated option IDs in the order shown to the learner (DX-05).</summary>
    public string? OptionOrderShown { get; set; }
    public string? NumericRaw { get; set; }
    public decimal? NumericValue { get; set; }
    public bool? IsCorrect { get; set; }
    public int? MarksAwarded { get; set; }
    public DateTime? AnsweredAt { get; set; }
}

public class TopicScore
{
    public Guid AttemptId { get; set; }
    public int TopicId { get; set; }
    public Topic Topic { get; set; } = null!;
    public int MarksEarned { get; set; }
    public int MarksAvailable { get; set; }
    public decimal Pct { get; set; }
    public Band Band { get; set; }
}

/// <summary>Outbox for queued, retried email (NT-03). Bodies are encrypted because they contain test links.</summary>
public class EmailMessage
{
    public Guid Id { get; set; }
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    /// <summary>Encrypted HTML body; cleared once sent.</summary>
    public string? BodyEncrypted { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public bool Failed { get; set; }
    public string? LastError { get; set; }
}
