using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Domain;
using EngineeredTutors.Api.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Features.Attempts;

/// <summary>Submits and scores an attempt exactly once (DX-09, DX-12, SC-01 to SC-05, NT-02).</summary>
public sealed class SubmissionService(
    AppDbContext db, EmailQueue email, IOptions<SiteOptions> site, IOptions<ScoringOptions> scoring,
    TimeProvider clock, ILogger<SubmissionService> log)
{
    /// <summary>Returns true if this call submitted the attempt; false if it was already submitted or not started.</summary>
    public async Task<bool> SubmitAsync(Guid attemptId, bool automatic, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Claim the attempt atomically. This locks the row, so answer saves (which lock it too) wait for us and
        // then see that it is submitted.
        var claimed = await db.Attempts
            .Where(a => a.Id == attemptId && a.Status == AttemptStatus.InProgress)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, AttemptStatus.Submitted)
                .SetProperty(a => a.SubmittedAt, a => automatic && a.DeadlineAt < now ? a.DeadlineAt : (DateTime?)now), ct);
        if (claimed == 0) return false;

        // The caller may already track this attempt with pre-claim values (status, row version); refresh them.
        var tracked = db.ChangeTracker.Entries<Attempt>().FirstOrDefault(e => e.Entity.Id == attemptId);
        if (tracked is not null) await tracked.ReloadAsync(ct);

        var attempt = await db.Attempts
            .Include(a => a.Learner).ThenInclude(l => l.Parent).ThenInclude(p => p.Lead)
            .Include(a => a.Assessment)
            .Include(a => a.Responses).ThenInclude(r => r.Question).ThenInclude(q => q.Options)
            .FirstAsync(a => a.Id == attemptId, ct);

        var (topics, overall) = Scoring.Score(
            attempt.Id, attempt.Responses.Select(r => new Scoring.Item(r.Question, r)), scoring.Value);
        attempt.TopicScores.AddRange(topics);
        attempt.OverallPct = overall;

        if (attempt.Learner.Parent.Lead is { Status: LeadStatus.New } lead)
        {
            lead.Status = LeadStatus.Tested;
            lead.UpdatedAt = now;
        }

        if (!string.IsNullOrWhiteSpace(site.Value.AdminAlertEmail))
        {
            var (subject, html) = EmailTemplates.AttemptSubmitted(
                attempt.Learner.FullName, attempt.Assessment.Subject, overall, automatic,
                $"{site.Value.BaseUrl.TrimEnd('/')}/admin/learners/{attempt.LearnerId}");
            email.Enqueue(site.Value.AdminAlertEmail, subject, html);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        log.LogInformation("Attempt {AttemptId} submitted ({Mode}), overall {Pct}%",
            attempt.Id, automatic ? "auto" : "manual", overall);
        return true;
    }
}

/// <summary>Auto-submits attempts whose time ran out while the page was closed (DX-09).</summary>
public sealed class AutoSubmitService(IServiceScopeFactory scopes, IOptions<SiteOptions> site, TimeProvider clock,
    ILogger<AutoSubmitService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await SubmitExpiredAsync(stop); }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogError(ex, "Auto-submit sweep failed"); }
            try { await Task.Delay(TimeSpan.FromSeconds(30), stop); } catch (OperationCanceledException) { }
        }
    }

    public async Task<int> SubmitExpiredAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddSeconds(-site.Value.GraceSeconds);
        List<Guid> due;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            due = await db.Attempts
                .Where(a => a.Status == AttemptStatus.InProgress && a.DeadlineAt < cutoff)
                .OrderBy(a => a.DeadlineAt)
                .Select(a => a.Id).Take(100).ToListAsync(ct);
        }

        var count = 0;
        foreach (var id in due)
        {
            using var scope = scopes.CreateScope();
            if (await scope.ServiceProvider.GetRequiredService<SubmissionService>().SubmitAsync(id, true, ct)) count++;
        }
        return count;
    }
}
