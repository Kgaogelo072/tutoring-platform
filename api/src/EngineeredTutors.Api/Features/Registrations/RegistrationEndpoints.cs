using EngineeredTutors.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Features.Registrations;

public static class RegistrationEndpoints
{
    public const string RateLimitPolicy = "registrations";

    public static void MapRegistrations(this RouteGroupBuilder api)
    {
        api.MapPost("/registrations", RegisterAsync).RequireRateLimiting(RateLimitPolicy);

        // Form setup: dropdown values, which subjects are open, and the current consent version.
        api.MapGet("/registrations/options", async (RegistrationService svc, IOptions<SiteOptions> site, CancellationToken ct) =>
        {
            var available = await svc.AvailableAssessmentsAsync(ct);
            return Results.Ok(new
            {
                sources = RegistrationLists.Sources,
                goals = RegistrationLists.Goals,
                subjects = Enum.GetValues<Subject>().Select(s => new
                {
                    subject = s,
                    name = Display.SubjectName(s),
                    available = available.ContainsKey(s),
                    minutes = available.TryGetValue(s, out var a) ? a.TimeLimitMinutes : site.Value.DefaultMinutes(s),
                }),
                maxLearners = site.Value.MaxLearnersPerRegistration,
                consentTextVersion = site.Value.ConsentTextVersion,
            });
        });
    }

    private static async Task<IResult> RegisterAsync(
        RegistrationRequest request, HttpContext http, RegistrationService svc, ITurnstileVerifier turnstile,
        IOptions<SiteOptions> site, TimeProvider clock, ILoggerFactory logs, AppDbContext db, CancellationToken ct)
    {
        // Honeypot (REG-06): look successful to the bot, store nothing.
        if (!string.IsNullOrEmpty(request.Website))
        {
            logs.CreateLogger("Registrations").LogInformation("Honeypot registration discarded");
            return Results.Ok(new RegistrationResponse(RegistrationService.ConfirmationMessage, []));
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(2));
        var (valid, errors) = RegistrationValidator.Validate(request, site.Value, today);
        if (valid is null) return Results.ValidationProblem(errors);

        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "";

        // REG-06: at most N successful registrations per IP per hour.
        var since = clock.GetUtcNow().UtcDateTime.AddHours(-1);
        if (ip.Length > 0 && await db.Consents.CountAsync(c => c.IpAddress == ip && c.AcceptedAt > since, ct)
            >= site.Value.RegistrationsPerIpPerHour)
            return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                detail: "There have been too many registrations from this connection. Please try again in an hour.");
        if (!await turnstile.VerifyAsync(request.TurnstileToken, ip, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["turnstileToken"] = ["Please complete the security check and try again."],
            });

        var outcome = await svc.RegisterAsync(valid, ip, ct);
        return outcome.Errors is not null ? Results.ValidationProblem(outcome.Errors) : Results.Ok(outcome.Response);
    }
}
