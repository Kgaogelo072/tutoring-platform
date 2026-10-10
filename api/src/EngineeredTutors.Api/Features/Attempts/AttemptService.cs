using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Domain;
using EngineeredTutors.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Features.Attempts;

/// <summary>A failure the test page shows to the learner. <see cref="Code"/> lets the page pick the right screen.</summary>
public sealed class AttemptException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;

    public static AttemptException NotFound() =>
        new(404, "not_found", "This test link isn't valid. Please check the link in your email.");
    public static AttemptException Expired() =>
        new(410, "expired", "This test link has expired. Please contact us for a new one.");
    public static AttemptException Submitted() =>
        new(409, "submitted", "This test has already been submitted.");
    public static AttemptException TimeUp() =>
        new(409, "time_up", "Time is up. Your test has been submitted, and answers can no longer be changed.");
    public static AttemptException NotStarted() =>
        new(409, "not_started", "This test hasn't been started yet.");
}

public sealed class AttemptService(
    AppDbContext db, SubmissionService submissions, IOptions<SiteOptions> site, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private TimeSpan Grace => TimeSpan.FromSeconds(site.Value.GraceSeconds);

    /// <summary>
    /// Finds the attempt for a token. Submitted attempts always resolve, so an old link still shows the thank-you
    /// screen (DX-12). An in-progress attempt past its deadline is submitted here (DX-09).
    /// </summary>
    private async Task<Attempt> ResolveAsync(string token, CancellationToken ct)
    {
        if (!AttemptTokens.LooksValid(token)) throw AttemptException.NotFound();
        var hash = AttemptTokens.Hash(token);
        var attempt = await db.Attempts
            .Include(a => a.Learner)
            .Include(a => a.Assessment)
            .FirstOrDefaultAsync(a => a.TokenHash == hash, ct)
            ?? throw AttemptException.NotFound();

        if (attempt.Status == AttemptStatus.Submitted) return attempt;
        if (attempt.Status == AttemptStatus.InProgress && Now > attempt.DeadlineAt + Grace)
        {
            await submissions.SubmitAsync(attempt.Id, automatic: true, ct);
            await db.Entry(attempt).ReloadAsync(ct);
            return attempt;
        }
        if (attempt.TokenExpiresAt < Now && attempt.Status == AttemptStatus.NotStarted) throw AttemptException.Expired();
        return attempt;
    }

    public async Task<AttemptStateDto> GetStateAsync(string token, CancellationToken ct)
    {
        var attempt = await ResolveAsync(token, ct);
        var types = await db.AssessmentQuestions
            .Where(aq => aq.AssessmentId == attempt.AssessmentId)
            .Select(aq => aq.Question.Type)
            .ToListAsync(ct);

        var now = Now;
        int? remaining = attempt.Status == AttemptStatus.InProgress
            ? (int)Math.Max(0, Math.Ceiling((attempt.DeadlineAt!.Value - now).TotalSeconds))
            : null;

        return new AttemptStateDto(
            attempt.Status,
            attempt.Learner.FirstName,
            attempt.Assessment.Subject,
            Display.SubjectName(attempt.Assessment.Subject),
            attempt.Assessment.TimeLimitMinutes,
            types.Count,
            types.Count(t => t == QuestionType.NUM),
            site.Value.CalculatorAllowed,
            attempt.StartedAt,
            attempt.DeadlineAt,
            now,
            remaining);
    }

    /// <summary>DX-02, DX-05: starts the timer and fixes the option order. Calling it again just returns the state.</summary>
    public async Task<AttemptStateDto> StartAsync(string token, CancellationToken ct)
    {
        var attempt = await ResolveAsync(token, ct);
        if (attempt.Status == AttemptStatus.Submitted) throw AttemptException.Submitted();

        if (attempt.Status == AttemptStatus.NotStarted)
        {
            var questions = await db.AssessmentQuestions
                .Where(aq => aq.AssessmentId == attempt.AssessmentId)
                .Include(aq => aq.Question).ThenInclude(q => q.Options)
                .OrderBy(aq => aq.Order)
                .ToListAsync(ct);

            var now = Now;
            attempt.Status = AttemptStatus.InProgress;
            attempt.StartedAt = now;
            attempt.DeadlineAt = now.AddMinutes(attempt.Assessment.TimeLimitMinutes);
            foreach (var aq in questions)
            {
                string? order = null;
                if (aq.Question.Type == QuestionType.MCQ)
                {
                    var ids = aq.Question.Options.Select(o => o.Id).ToArray();
                    Random.Shared.Shuffle(ids);
                    order = string.Join(',', ids);
                }
                db.Responses.Add(new Response
                {
                    AttemptId = attempt.Id,
                    QuestionId = aq.QuestionId,
                    Order = aq.Order,
                    OptionOrderShown = order,
                });
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Two "Start" taps at once: the first one won (row version or response keys). Use its state.
                db.ChangeTracker.Clear();
            }
        }
        return await GetStateAsync(token, ct);
    }

    /// <summary>DX-03, DX-04, DX-05, DX-08: questions in the stored order with any saved answers. No answer data.</summary>
    public async Task<QuestionsDto> GetQuestionsAsync(string token, CancellationToken ct)
    {
        var attempt = await ResolveAsync(token, ct);
        if (attempt.Status == AttemptStatus.Submitted) throw AttemptException.Submitted();
        if (attempt.Status == AttemptStatus.NotStarted) throw AttemptException.NotStarted();

        var responses = await db.Responses.AsNoTracking()
            .Where(r => r.AttemptId == attempt.Id)
            .Include(r => r.Question).ThenInclude(q => q.Options)
            .OrderBy(r => r.Order)
            .ToListAsync(ct);

        var list = responses.Select((r, i) => ToDto(r.Question, i + 1, r.OptionOrderShown, r.SelectedOptionId, r.NumericRaw)).ToList();
        var remaining = (int)Math.Max(0, Math.Ceiling((attempt.DeadlineAt!.Value - Now).TotalSeconds));
        return new QuestionsDto(remaining, attempt.DeadlineAt.Value, list);
    }

    /// <summary>Builds the learner view of a question. Shared with the admin preview (AD-07).</summary>
    public static QuestionDto ToDto(Question q, int number, string? optionOrder, Guid? selected, string? numericRaw)
    {
        IReadOnlyList<OptionDto> options = [];
        if (q.Type == QuestionType.MCQ)
        {
            var byId = q.Options.ToDictionary(o => o.Id);
            var ids = optionOrder?.Split(',').Select(Guid.Parse).Where(byId.ContainsKey).ToList()
                      ?? q.Options.OrderBy(o => o.Label).Select(o => o.Id).ToList();
            options = ids.Select(id => new OptionDto(id, byId[id].TextLatex)).ToList();
        }
        return new QuestionDto(q.Id, number, q.Type, q.StemLatex, q.ImageUrl, q.Marks,
            q.Type == QuestionType.NUM ? q.Unit : null, options, selected, numericRaw);
    }

    /// <summary>DX-06, DX-09, DX-11: validates and saves one answer immediately.</summary>
    public async Task<SaveResponseResult> SaveResponseAsync(
        string token, Guid questionId, SaveResponseRequest body, CancellationToken ct)
    {
        var attempt = await ResolveAsync(token, ct);
        if (attempt.Status == AttemptStatus.Submitted)
            throw attempt.SubmittedAt >= attempt.DeadlineAt ? AttemptException.TimeUp() : AttemptException.Submitted();
        if (attempt.Status == AttemptStatus.NotStarted) throw AttemptException.NotStarted();

        var response = await db.Responses.AsNoTracking()
            .Include(r => r.Question).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(r => r.AttemptId == attempt.Id && r.QuestionId == questionId, ct)
            ?? throw new AttemptException(404, "question_not_found", "That question isn't part of this test.");

        Guid? selected = null;
        string? raw = null;
        decimal? value = null;
        if (response.Question.Type == QuestionType.MCQ)
        {
            if (body.SelectedOptionId is { } opt && response.Question.Options.All(o => o.Id != opt))
                throw new AttemptException(400, "invalid_option", "That option isn't part of this question.");
            selected = body.SelectedOptionId;
        }
        else if (!string.IsNullOrWhiteSpace(body.NumericRaw))
        {
            raw = body.NumericRaw.Length <= 32 ? body.NumericRaw : null;
            value = NumericAnswer.Parse(raw);
            if (value is null)
                throw new AttemptException(400, "invalid_number",
                    "Type a number only, e.g. -4 or 5,05. Leave out units and other symbols.");
        }

        var now = Now;
        var cutoff = now - Grace;
        var answeredAt = selected is null && raw is null ? (DateTime?)null : now;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock the attempt row while it is still open, so a concurrent submit can't score without this answer.
        var open = await db.Attempts
            .Where(a => a.Id == attempt.Id && a.Status == AttemptStatus.InProgress && a.DeadlineAt >= cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, a => a.Status), ct);
        if (open == 0)
        {
            await tx.RollbackAsync(ct);
            await submissions.SubmitAsync(attempt.Id, automatic: true, ct);
            throw AttemptException.TimeUp();
        }

        await db.Responses
            .Where(r => r.AttemptId == attempt.Id && r.QuestionId == questionId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.SelectedOptionId, selected)
                .SetProperty(r => r.NumericRaw, raw)
                .SetProperty(r => r.NumericValue, value)
                .SetProperty(r => r.AnsweredAt, answeredAt), ct);
        await tx.CommitAsync(ct);

        return new SaveResponseResult(questionId, answeredAt, value);
    }

    /// <summary>DX-10, DX-12: manual submit. Submitting twice is harmless.</summary>
    public async Task<AttemptStateDto> SubmitAsync(string token, CancellationToken ct)
    {
        var attempt = await ResolveAsync(token, ct);
        if (attempt.Status == AttemptStatus.NotStarted) throw AttemptException.NotStarted();
        if (attempt.Status == AttemptStatus.InProgress)
            await submissions.SubmitAsync(attempt.Id, automatic: false, ct);
        db.ChangeTracker.Clear();
        return await GetStateAsync(token, ct);
    }
}
