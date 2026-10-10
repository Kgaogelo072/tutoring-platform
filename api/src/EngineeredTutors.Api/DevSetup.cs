using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Features.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api;

/// <summary>
/// Local-development convenience: loads KEY=VALUE pairs from the nearest .env file (searching up from the working
/// directory) into environment variables that aren't already set. .env is gitignored; hosted environments set real
/// environment variables or Key Vault references instead.
/// </summary>
public static class DotEnv
{
    public static void Load()
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)) return;

        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".env");
            if (!File.Exists(path)) continue;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim().Trim('"');
                if (value.Length > 0 && Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            return;
        }
    }
}

public sealed class SeedOptions
{
    /// <summary>Paths to the question CSVs. These hold the answers, so they stay out of the repo.</summary>
    public string MathsCsv { get; set; } = "";
    public string PhysicalSciencesCsv { get; set; } = "";
}

/// <summary>Development only: applies migrations and imports and publishes the diagnostics if none exist yet.</summary>
public static class DevSeeder
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DevSeeder");
        var seed = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        var site = sp.GetRequiredService<IOptions<SiteOptions>>().Value;
        var clock = sp.GetRequiredService<TimeProvider>();

        await db.Database.MigrateAsync(ct);

        foreach (var (subject, path) in new[] { (Subject.Maths, seed.MathsCsv), (Subject.PhysicalSciences, seed.PhysicalSciencesCsv) })
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (await db.Assessments.AnyAsync(a => a.Subject == subject, ct)) continue;
            if (!File.Exists(path))
            {
                log.LogWarning("Seed file for {Subject} not found: {Path}", subject, path);
                continue;
            }

            var importer = sp.GetRequiredService<QuestionImporter>();
            var (result, errors) = await importer.ImportAsync(subject, await File.ReadAllTextAsync(path, ct),
                site.DefaultMinutes(subject), ct);
            if (result is null)
            {
                foreach (var e in errors) log.LogWarning("Seed {Subject} row {Row} {Column}: {Message}", subject, e.Row, e.Column, e.Message);
                continue;
            }

            var a = await db.Assessments.FirstAsync(x => x.Id == result.AssessmentId, ct);
            a.PublishedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            log.LogInformation("Seeded and published {Subject} v{Version}: {Count} questions", subject, result.Version, result.QuestionCount);
        }
    }
}
