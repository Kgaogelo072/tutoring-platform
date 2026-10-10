using System.Text;
using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Features.Attempts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Features.Admin;

public sealed class UpdateLeadRequest
{
    public LeadStatus? Status { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateAssessmentRequest
{
    public int? TimeLimitMinutes { get; set; }
}

/// <summary>Admin routes (section 6, below the gold line). Every route requires the Admin policy (AD-01, NF-05).</summary>
public static class AdminEndpoints
{
    public const string Policy = "Admin";

    public static void MapAdmin(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/admin").RequireAuthorization(Policy);

        g.MapGet("/me", (HttpContext http) => Results.Ok(new
        {
            name = http.User.FindFirst("name")?.Value ?? http.User.Identity?.Name,
        }));

        g.MapGet("/leads", async (string? search, LeadStatus? status, string? sort, string? dir,
                AdminQueries q, CancellationToken ct) =>
            Results.Ok(await q.LeadsAsync(search, status, sort, !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase), ct)));

        g.MapPatch("/leads/{id:guid}", async (Guid id, UpdateLeadRequest body, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
            if (lead is null) return Results.NotFound();
            if (body.Status is { } s)
            {
                if (!Enum.IsDefined(s)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
                lead.Status = s;
            }
            if (body.Notes is not null)
            {
                if (body.Notes.Length > 4000)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["notes"] = ["Notes can be up to 4000 characters."] });
                lead.Notes = body.Notes;
            }
            lead.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { lead.Id, lead.Status, lead.Notes, lead.UpdatedAt });
        });

        g.MapGet("/learners/{id:guid}", async (Guid id, AdminQueries q, CancellationToken ct) =>
            await q.LearnerAsync(id, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());

        g.MapPost("/questions/import", ImportAsync).DisableAntiforgery();

        g.MapGet("/assessments", async (AppDbContext db, CancellationToken ct) =>
        {
            var list = await db.Assessments.AsNoTracking()
                .OrderBy(a => a.Subject).ThenByDescending(a => a.Version)
                .Select(a => new
                {
                    a.Id, a.Subject, a.Version, a.TimeLimitMinutes, a.CreatedAt, a.PublishedAt,
                    questionCount = a.Questions.Count,
                    totalMarks = a.Questions.Sum(q => q.Question.Marks),
                    attemptCount = db.Attempts.Count(t => t.AssessmentId == a.Id),
                })
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapPatch("/assessments/{id:guid}", async (Guid id, UpdateAssessmentRequest body, AppDbContext db, CancellationToken ct) =>
        {
            var a = await db.Assessments.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return Results.NotFound();
            if (a.PublishedAt is not null)
                return Results.Problem(statusCode: 409, detail: "Published assessments can't be changed. Import a new version instead.");
            if (body.TimeLimitMinutes is { } m)
            {
                if (m is < 5 or > 180)
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["timeLimitMinutes"] = ["Choose 5 to 180 minutes."] });
                a.TimeLimitMinutes = m;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { a.Id, a.TimeLimitMinutes });
        });

        g.MapPost("/assessments/{id:guid}/publish", async (Guid id, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var a = await db.Assessments.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return Results.NotFound();
            if (a.PublishedAt is not null) return Results.Problem(statusCode: 409, detail: "This version is already published.");
            if (a.Questions.Count == 0) return Results.Problem(statusCode: 409, detail: "This version has no questions.");
            a.PublishedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { a.Id, a.Subject, a.Version, a.PublishedAt });
        });

        g.MapGet("/assessments/{id:guid}/preview", async (Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var a = await db.Assessments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (a is null) return Results.NotFound();
            var questions = await db.AssessmentQuestions.AsNoTracking()
                .Where(aq => aq.AssessmentId == id)
                .Include(aq => aq.Question).ThenInclude(q => q.Options)
                .OrderBy(aq => aq.Order)
                .ToListAsync(ct);

            // AD-07: the learner view, with options shuffled as an attempt would. Nothing is saved.
            var list = questions.Select((aq, i) =>
            {
                var ids = aq.Question.Options.Select(o => o.Id).ToArray();
                Random.Shared.Shuffle(ids);
                return AttemptService.ToDto(aq.Question, i + 1, string.Join(',', ids), null, null);
            }).ToList();
            return Results.Ok(new
            {
                a.Id, a.Subject, subjectName = Display.SubjectName(a.Subject), a.Version, a.TimeLimitMinutes, a.PublishedAt,
                questions = list,
            });
        });

        g.MapGet("/export/leads.csv", async (AdminQueries q, TimeProvider clock, CancellationToken ct) =>
        {
            var csv = await q.ExportCsvAsync(ct);
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return Results.File(bytes, "text/csv; charset=utf-8",
                $"engineered-tutors-leads-{clock.GetUtcNow().UtcDateTime.AddHours(2):yyyy-MM-dd}.csv");
        });
    }

    private static async Task<IResult> ImportAsync(
        [FromForm] IFormFile file, [FromForm] Subject subject, [FromForm] int? timeLimitMinutes,
        QuestionImporter importer, IOptions<SiteOptions> site, CancellationToken ct)
    {
        if (file.Length == 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["The file is empty."] });
        if (file.Length > 2_000_000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["The file must be under 2 MB."] });
        var minutes = timeLimitMinutes ?? site.Value.DefaultMinutes(subject);
        if (minutes is < 5 or > 180)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["timeLimitMinutes"] = ["Choose 5 to 180 minutes."] });

        string text;
        using (var reader = new StreamReader(file.OpenReadStream(), new UTF8Encoding(false, throwOnInvalidBytes: true)))
        {
            try { text = await reader.ReadToEndAsync(ct); }
            catch (DecoderFallbackException)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["The file must be saved as UTF-8."] });
            }
        }

        var (result, errors) = await importer.ImportAsync(subject, text, minutes, ct);
        if (errors.Count > 0)
            return Results.Json(new { title = "Nothing was imported. Fix these rows and try again.", errors }, statusCode: 422);
        return Results.Ok(result);
    }
}
