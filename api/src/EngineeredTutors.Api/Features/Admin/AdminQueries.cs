using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Domain;
using EngineeredTutors.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace EngineeredTutors.Api.Features.Admin;

public sealed record LeadTestDto(Subject Subject, AttemptStatus Status, decimal? OverallPct, DateTime? SubmittedAt);
public sealed record LeadLearnerDto(Guid Id, string Name, int Grade, string School, string? IdMasked, IReadOnlyList<LeadTestDto> Tests);
public sealed record LeadDto(
    Guid Id, Guid ParentId, string ParentName, string Phone, string Email, string Source, DateTime RegisteredAt,
    LeadStatus Status, string Notes, DateTime UpdatedAt, decimal? BestPct, IReadOnlyList<LeadLearnerDto> Learners);

public sealed record TopicScoreDto(string Code, string Name, int MarksEarned, int MarksAvailable, decimal Pct, Band Band);
public sealed record AnswerRowDto(
    int Number, string Code, string Section, string Topic, QuestionType Type, string StemLatex, string? ImageUrl,
    int Marks, string? AnswerGiven, string CorrectAnswer, bool? IsCorrect, int? MarksAwarded, DateTime? AnsweredAt,
    string Explanation);
public sealed record AttemptDetailDto(
    Guid Id, Subject Subject, int AssessmentVersion, AttemptStatus Status, DateTime CreatedAt, DateTime TokenExpiresAt,
    DateTime? StartedAt, DateTime? DeadlineAt, DateTime? SubmittedAt, int? TimeTakenSeconds, decimal? OverallPct,
    IReadOnlyList<TopicScoreDto> Topics, IReadOnlyList<AnswerRowDto> Questions);

/// <summary>Read models for the admin area (AD-02, AD-04, AD-08). ID numbers only ever leave here masked (NF-09).</summary>
public sealed class AdminQueries(AppDbContext db, Protector protector)
{
    public static string? Mask(string? last4) => last4 is null ? null : "•••• " + last4;

    public async Task<List<LeadDto>> LeadsAsync(string? search, LeadStatus? status, string? sort, bool desc, CancellationToken ct)
    {
        var parents = await db.Parents.AsNoTracking().AsSplitQuery()
            .Include(p => p.Lead)
            .Include(p => p.Learners).ThenInclude(l => l.Attempts).ThenInclude(a => a.Assessment)
            .Where(p => p.Lead != null && (status == null || p.Lead.Status == status))
            .ToListAsync(ct);

        var term = (search ?? "").Trim();
        if (term.Length > 0)
        {
            var phone = Normalise.SaMobile(term);
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            var idNorm = IdNumbers.Normalise(term);
            var idHash = idNorm.Length >= 6 && idNorm.All(char.IsAsciiLetterOrDigit) ? protector.HashId(idNorm) : null;

            parents = parents.Where(p =>
                (phone is not null && p.Phone == phone) ||
                (digits.Length >= 4 && p.Phone.Contains(digits.TrimStart('0'))) ||
                p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                p.Learners.Any(l =>
                    l.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (idHash is not null && l.IdNumberHash == idHash))).ToList();
        }

        var rows = parents.Select(p =>
        {
            var learners = p.Learners.OrderBy(l => l.CreatedAt).Select(l => new LeadLearnerDto(
                l.Id, l.FullName, l.Grade, l.School, Mask(l.IdLast4),
                l.Attempts
                    .GroupBy(a => a.Assessment.Subject)
                    .Select(g => g.MaxBy(a => a.CreatedAt)!)
                    .OrderBy(a => a.Assessment.Subject)
                    .Select(a => new LeadTestDto(a.Assessment.Subject, a.Status, a.OverallPct, a.SubmittedAt))
                    .ToList())).ToList();
            var best = learners.SelectMany(l => l.Tests).Max(t => t.OverallPct);
            return new LeadDto(p.Lead!.Id, p.Id, p.FullName, p.Phone, p.Email, p.Source, p.CreatedAt,
                p.Lead.Status, p.Lead.Notes, p.Lead.UpdatedAt, best, learners);
        });

        rows = (sort?.ToLowerInvariant()) switch
        {
            "name" => desc ? rows.OrderByDescending(r => r.ParentName) : rows.OrderBy(r => r.ParentName),
            "status" => desc ? rows.OrderByDescending(r => r.Status) : rows.OrderBy(r => r.Status),
            "score" => desc ? rows.OrderByDescending(r => r.BestPct ?? -1) : rows.OrderBy(r => r.BestPct ?? 101),
            _ => desc ? rows.OrderByDescending(r => r.RegisteredAt) : rows.OrderBy(r => r.RegisteredAt),
        };
        return rows.ToList();
    }

    public async Task<object?> LearnerAsync(Guid id, CancellationToken ct)
    {
        var l = await db.Learners.AsNoTracking().AsSplitQuery()
            .Include(x => x.Subjects)
            .Include(x => x.Parent).ThenInclude(p => p.Lead)
            .Include(x => x.Parent).ThenInclude(p => p.Consents)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (l is null) return null;

        var attempts = await db.Attempts.AsNoTracking().AsSplitQuery()
            .Where(a => a.LearnerId == id)
            .Include(a => a.Assessment)
            .Include(a => a.TopicScores).ThenInclude(t => t.Topic)
            .Include(a => a.Responses).ThenInclude(r => r.Question).ThenInclude(q => q.Options)
            .Include(a => a.Responses).ThenInclude(r => r.Question).ThenInclude(q => q.Topic)
            .OrderBy(a => a.Assessment.Subject).ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var assessmentIds = attempts.Select(a => a.AssessmentId).Distinct().ToList();
        var sections = await db.AssessmentQuestions.AsNoTracking()
            .Where(aq => assessmentIds.Contains(aq.AssessmentId))
            .ToDictionaryAsync(aq => (aq.AssessmentId, aq.QuestionId), aq => aq.Section, ct);

        return new
        {
            learner = new
            {
                l.Id, l.FirstName, l.Surname, l.School, l.Grade, l.Goal, l.IdType,
                idMasked = Mask(l.IdLast4), l.DateOfBirth, l.CreatedAt,
                subjects = l.Subjects.OrderBy(s => s.Subject).Select(s => new { s.Subject, s.LatestMark }),
            },
            parent = new { l.Parent.Id, l.Parent.FullName, l.Parent.Phone, l.Parent.Email, l.Parent.Source, l.Parent.CreatedAt },
            lead = l.Parent.Lead is { } lead ? new { lead.Id, lead.Status, lead.Notes, lead.UpdatedAt } : null,
            consents = l.Parent.Consents.OrderByDescending(c => c.AcceptedAt)
                .Select(c => new { c.TextVersion, c.AcceptedAt, c.IpAddress }),
            attempts = attempts.Select(a => new AttemptDetailDto(
                a.Id, a.Assessment.Subject, a.Assessment.Version, a.Status, a.CreatedAt, a.TokenExpiresAt,
                a.StartedAt, a.DeadlineAt, a.SubmittedAt,
                a is { StartedAt: { } s, SubmittedAt: { } e } ? (int)(e - s).TotalSeconds : null,
                a.OverallPct,
                a.TopicScores.OrderBy(t => t.TopicId)
                    .Select(t => new TopicScoreDto(t.Topic.Code, t.Topic.Name, t.MarksEarned, t.MarksAvailable, t.Pct, t.Band))
                    .ToList(),
                a.Responses.OrderBy(r => r.Order).Select((r, i) => new AnswerRowDto(
                    i + 1, r.Question.Code, sections.GetValueOrDefault((a.AssessmentId, r.QuestionId), ""),
                    r.Question.Topic.Name, r.Question.Type, r.Question.StemLatex, r.Question.ImageUrl, r.Question.Marks,
                    AnswerGiven(r), CorrectAnswer(r.Question), r.IsCorrect, r.MarksAwarded, r.AnsweredAt,
                    r.Question.Explanation)).ToList())),
        };
    }

    internal static string? AnswerGiven(Response r)
    {
        if (r.Question.Type == QuestionType.NUM) return r.NumericRaw;
        var opt = r.Question.Options.FirstOrDefault(o => o.Id == r.SelectedOptionId);
        return opt is null ? null : $"{opt.Label}: {opt.TextLatex}";
    }

    internal static string CorrectAnswer(Question q)
    {
        if (q.Type == QuestionType.MCQ)
        {
            var opt = q.Options.FirstOrDefault(o => o.IsCorrect);
            return opt is null ? "" : $"{opt.Label}: {opt.TextLatex}";
        }
        var value = q.AnswerValue?.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture) ?? "";
        var tol = q.Tolerance is > 0 ? $" ± {q.Tolerance.Value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)}" : "";
        var unit = string.IsNullOrEmpty(q.Unit) ? "" : $" {q.Unit}";
        return value + tol + unit;
    }

    /// <summary>AD-08: one row per learner per test, for writing reports by hand until Phase 2.</summary>
    public async Task<string> ExportCsvAsync(CancellationToken ct)
    {
        var learners = await db.Learners.AsNoTracking().AsSplitQuery()
            .Include(l => l.Parent).ThenInclude(p => p.Lead)
            .Include(l => l.Subjects)
            .Include(l => l.Attempts).ThenInclude(a => a.Assessment)
            .Include(l => l.Attempts).ThenInclude(a => a.TopicScores).ThenInclude(t => t.Topic)
            .OrderBy(l => l.Parent.CreatedAt).ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append(Csv.Line([
            "registered_sast", "parent_name", "parent_phone", "parent_email", "source", "lead_status", "lead_notes",
            "learner_first_name", "learner_surname", "school", "grade", "goal", "id_masked", "subject", "latest_mark",
            "test_status", "started_sast", "submitted_sast", "overall_pct", "topic_scores",
        ]));

        foreach (var l in learners)
        {
            foreach (var s in l.Subjects.OrderBy(s => s.Subject))
            {
                var a = l.Attempts.Where(x => x.Assessment.Subject == s.Subject).MaxBy(x => x.CreatedAt);
                var topics = a is null ? "" : string.Join("; ", a.TopicScores.OrderBy(t => t.TopicId)
                    .Select(t => $"{t.Topic.Name} {t.Pct.ToString("0.#", inv)}% ({t.Band})"));
                sb.Append(Csv.Line([
                    Display.Sast(l.Parent.CreatedAt), l.Parent.FullName, l.Parent.Phone, l.Parent.Email, l.Parent.Source,
                    l.Parent.Lead?.Status.ToString(), l.Parent.Lead?.Notes,
                    l.FirstName, l.Surname, l.School, l.Grade.ToString(inv), l.Goal, Mask(l.IdLast4),
                    Display.SubjectName(s.Subject), s.LatestMark?.ToString(inv),
                    a?.Status.ToString(), a?.StartedAt is { } st ? Display.Sast(st) : "",
                    a?.SubmittedAt is { } su ? Display.Sast(su) : "", a?.OverallPct?.ToString("0.#", inv), topics,
                ]));
            }
        }
        return sb.ToString();
    }
}
