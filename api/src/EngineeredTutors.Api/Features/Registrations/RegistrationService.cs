using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Notifications;
using EngineeredTutors.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Features.Registrations;

/// <summary>Creates parents, learners, consent and attempt tokens (REG-05, REG-07, REG-08, NT-01).</summary>
public sealed class RegistrationService(
    AppDbContext db, Protector protector, EmailQueue email, IOptions<SiteOptions> siteOptions, TimeProvider clock)
{
    public const string ConfirmationMessage =
        "Thank you! We've emailed the test links. If you can't find the email, check your spam folder.";

    /// <summary>Links issued more recently than this are not replaced when the learner registers again.</summary>
    internal static readonly TimeSpan RecentLinkWindow = TimeSpan.FromMinutes(15);

    private SiteOptions Site => siteOptions.Value;

    public sealed record Outcome(RegistrationResponse? Response, Dictionary<string, string[]>? Errors);

    /// <summary>Published assessments that registrations can use, latest version per subject.</summary>
    public async Task<Dictionary<Subject, Assessment>> AvailableAssessmentsAsync(CancellationToken ct)
    {
        var published = await db.Assessments.AsNoTracking()
            .Where(a => a.PublishedAt != null)
            .ToListAsync(ct);
        return published.GroupBy(a => a.Subject).ToDictionary(g => g.Key, g => g.MaxBy(a => a.Version)!);
    }

    public async Task<Outcome> RegisterAsync(ValidRegistration reg, string ipAddress, CancellationToken ct)
    {
        var available = await AvailableAssessmentsAsync(ct);
        var unavailable = new Dictionary<string, string[]>();
        foreach (var l in reg.Learners)
            foreach (var s in l.Subjects.Where(s => !available.ContainsKey(s.Subject)))
                unavailable[$"learners[{l.Index}].subjects"] =
                    [$"The {Display.SubjectName(s.Subject)} diagnostic isn't open yet. Please choose another subject."];
        if (unavailable.Count > 0) return new(null, unavailable);

        // A concurrent registration with the same ID number can trip the unique index; retry once against fresh data.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RegisterOnceAsync(reg, ipAddress, available, ct);
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<Outcome> RegisterOnceAsync(
        ValidRegistration reg, string ipAddress, Dictionary<Subject, Assessment> available, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The submitting parent is reused only when both phone and email match, so nobody can attach learners
        // to someone else's record by knowing their phone number.
        var submitter = await db.Parents.Include(p => p.Lead)
            .FirstOrDefaultAsync(p => p.Phone == reg.Phone && p.Email == reg.Email, ct);

        // Links per parent, so each email lists only that parent's learners.
        var emails = new Dictionary<Guid, (Parent Parent, List<EmailTemplates.LearnerLinks> Learners)>();
        var screenLinks = new List<LearnerLinksDto>();
        var anyNew = false;

        foreach (var input in reg.Learners)
        {
            var idHash = input.IdNumber is null ? null : protector.HashId(input.IdNumber);
            var existing = await FindExistingAsync(input, idHash, reg.Phone, ct);

            if (existing is not null)
            {
                // REG-07: same learner. Add an ID number if they didn't have one, resend links to the email on file.
                if (existing.IdNumberHash is null && idHash is not null)
                    SetIdNumber(existing, input, idHash);

                var tests = UpsertSubjectsAndRotateLinks(existing, input, available, now);
                AddToEmail(emails, existing.Parent, new EmailTemplates.LearnerLinks(existing.FirstName, tests));
                continue;
            }

            anyNew = true;
            if (submitter is null)
            {
                submitter = new Parent
                {
                    Id = Guid.NewGuid(),
                    FullName = reg.ParentName,
                    Phone = reg.Phone,
                    Email = reg.Email,
                    Source = reg.Source,
                    CreatedAt = now,
                };
                db.Parents.Add(submitter);
                db.Leads.Add(new Lead { Id = Guid.NewGuid(), ParentId = submitter.Id, Status = LeadStatus.New, UpdatedAt = now });
            }

            var learner = new Learner
            {
                Id = Guid.NewGuid(),
                ParentId = submitter.Id,
                Parent = submitter,
                FirstName = input.FirstName,
                Surname = input.Surname,
                School = input.School,
                Grade = input.Grade,
                Goal = input.Goal,
                NameKey = input.NameKey,
                CreatedAt = now,
            };
            if (idHash is not null) SetIdNumber(learner, input, idHash);
            db.Learners.Add(learner);

            var newTests = new List<EmailTemplates.TestLink>();
            var dtoTests = new List<SubjectLinkDto>();
            foreach (var s in input.Subjects)
            {
                db.LearnerSubjects.Add(new LearnerSubject { LearnerId = learner.Id, Subject = s.Subject, LatestMark = s.LatestMark });
                var assessment = available[s.Subject];
                var url = NewAttempt(learner.Id, assessment, now);
                newTests.Add(new(s.Subject, assessment.TimeLimitMinutes, url, false));
                dtoTests.Add(new(s.Subject, url, assessment.TimeLimitMinutes));
            }
            AddToEmail(emails, submitter, new EmailTemplates.LearnerLinks(learner.FirstName, newTests));
            screenLinks.Add(new LearnerLinksDto(learner.FirstName, dtoTests));
        }

        // REG-05: consent is stored against the submitting parent's record whenever one exists.
        if (submitter is not null)
        {
            db.Consents.Add(new Consent
            {
                Id = Guid.NewGuid(),
                ParentId = submitter.Id,
                TextVersion = reg.ConsentVersion,
                AcceptedAt = now,
                IpAddress = ipAddress,
            });
            if (anyNew && submitter.Lead is not null) submitter.Lead.UpdatedAt = now;
        }

        foreach (var (parent, learners) in emails.Values)
        {
            var (subject, html) = EmailTemplates.ParentLinks(parent.FullName, learners);
            email.Enqueue(parent.Email, subject, html);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(new RegistrationResponse(ConfirmationMessage, screenLinks), null);
    }

    /// <summary>REG-07 order: (1) ID number, (2) parent phone + learner name, unless that learner has a different ID.</summary>
    private async Task<Learner?> FindExistingAsync(ValidLearner input, string? idHash, string phone, CancellationToken ct)
    {
        if (idHash is not null)
        {
            var byId = await LearnerQuery().FirstOrDefaultAsync(l => l.IdNumberHash == idHash, ct);
            if (byId is not null) return byId;
        }

        var byName = await LearnerQuery()
            .Where(l => l.Parent.Phone == phone && l.NameKey == input.NameKey)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);
        return byName.FirstOrDefault(l => idHash is null || l.IdNumberHash is null || l.IdNumberHash == idHash);
    }

    private IQueryable<Learner> LearnerQuery() => db.Learners
        .Include(l => l.Parent)
        .Include(l => l.Subjects)
        .Include(l => l.Attempts).ThenInclude(a => a.Assessment);

    private void SetIdNumber(Learner learner, ValidLearner input, string idHash)
    {
        learner.IdType = input.IdType;
        learner.IdNumberEncrypted = protector.Encrypt(input.IdNumber!);
        learner.IdNumberHash = idHash;
        learner.IdLast4 = input.IdNumber![^4..];
        learner.DateOfBirth = input.DateOfBirth;
    }

    /// <summary>
    /// For a matched learner: adds any newly chosen subject, and issues fresh links for unfinished tests. Only token
    /// hashes are stored, so resending a link means replacing its token; the old link stops working.
    /// </summary>
    private List<EmailTemplates.TestLink> UpsertSubjectsAndRotateLinks(
        Learner learner, ValidLearner input, Dictionary<Subject, Assessment> available, DateTime now)
    {
        foreach (var s in input.Subjects)
        {
            var current = learner.Subjects.FirstOrDefault(x => x.Subject == s.Subject);
            if (current is null)
                db.LearnerSubjects.Add(new LearnerSubject { LearnerId = learner.Id, Subject = s.Subject, LatestMark = s.LatestMark });
            else if (s.LatestMark is not null)
                current.LatestMark = s.LatestMark;
        }

        var subjects = learner.Subjects.Select(x => x.Subject).Union(input.Subjects.Select(s => s.Subject)).Order();
        var tests = new List<EmailTemplates.TestLink>();
        foreach (var subject in subjects)
        {
            var latest = learner.Attempts.Where(a => a.Assessment.Subject == subject).MaxBy(a => a.CreatedAt);
            if (latest is { Status: AttemptStatus.Submitted })
            {
                tests.Add(new(subject, latest.Assessment.TimeLimitMinutes, null, true));
            }
            else if (latest is not null && now - latest.TokenExpiresAt.AddDays(-Site.TokenLifetimeDays) < RecentLinkWindow)
            {
                // Links went out minutes ago (e.g. a double-tapped submit). Replacing them now would break the links
                // the parent is looking at, so point them to the earlier email instead.
                tests.Add(new(subject, latest.Assessment.TimeLimitMinutes, null, false, RecentlySent: true));
            }
            else if (latest is not null)
            {
                var token = AttemptTokens.New();
                latest.TokenHash = AttemptTokens.Hash(token);
                latest.TokenExpiresAt = now.AddDays(Site.TokenLifetimeDays);
                tests.Add(new(subject, latest.Assessment.TimeLimitMinutes, LinkFor(token), false));
            }
            else if (available.TryGetValue(subject, out var assessment))
            {
                tests.Add(new(subject, assessment.TimeLimitMinutes, NewAttempt(learner.Id, assessment, now), false));
            }
        }
        return tests;
    }

    private string NewAttempt(Guid learnerId, Assessment assessment, DateTime now)
    {
        var token = AttemptTokens.New();
        db.Attempts.Add(new Attempt
        {
            Id = Guid.NewGuid(),
            LearnerId = learnerId,
            AssessmentId = assessment.Id,
            TokenHash = AttemptTokens.Hash(token),
            CreatedAt = now,
            TokenExpiresAt = now.AddDays(Site.TokenLifetimeDays),
            Status = AttemptStatus.NotStarted,
        });
        return LinkFor(token);
    }

    private string LinkFor(string token) => $"{Site.BaseUrl.TrimEnd('/')}/d/{token}";

    private static void AddToEmail(
        Dictionary<Guid, (Parent, List<EmailTemplates.LearnerLinks>)> emails, Parent parent, EmailTemplates.LearnerLinks links)
    {
        if (!emails.TryGetValue(parent.Id, out var entry)) emails[parent.Id] = entry = (parent, []);
        entry.Item2.Add(links);
    }
}
