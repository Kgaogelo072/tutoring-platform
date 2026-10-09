using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Features.Attempts;
using EngineeredTutors.Api.Features.Registrations;
using EngineeredTutors.Api.Notifications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Tests.Integration;

/// <summary>A clock the tests can move forward.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

public sealed class CapturingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Html)> Sent { get; } = [];
    public bool Fail { get; set; }

    public Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("SMTP down");
        lock (Sent) Sent.Add((to, subject, html));
        return Task.CompletedTask;
    }
}

public sealed class AlwaysPassTurnstile : ITurnstileVerifier
{
    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct) => Task.FromResult(true);
}

/// <summary>
/// Runs the real API against a throwaway LocalDB database. One instance is shared by every integration test (they
/// run one at a time), so tests use unique phone numbers and ID numbers and never depend on absolute times.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DevKey = "test-admin-key";
    public TestClock Clock { get; } = new();
    public CapturingEmailSender Email { get; } = new();
    private readonly string _db = $"EngineeredTutorsTest_{Guid.NewGuid():N}";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Guid MathsAssessmentId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        string Key() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        builder.UseSetting("ConnectionStrings:Default",
            $"Server=(localdb)\\MSSQLLocalDB;Database={_db};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60");
        builder.UseSetting("Security:EncryptionKey", Key());
        builder.UseSetting("Security:IdHashKey", Key());
        builder.UseSetting("AdminAuth:DevKey", DevKey);
        builder.UseSetting("Site:AdminAlertEmail", "tutor@example.com");
        builder.UseSetting("Site:BaseUrl", "https://test.example");
        builder.UseSetting("BackgroundServices:Enabled", "false");
        builder.UseSetting("ForwardedHeaders:Enabled", "false");
        builder.UseSetting("Email:Mode", "Pickup");
        builder.UseSetting("Site:RegistrationRequestsPerIpPerHour", "100000");

        builder.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(Email);
            s.RemoveAll<ITurnstileVerifier>();
            s.AddSingleton<ITurnstileVerifier, AlwaysPassTurnstile>();
            s.AddSingleton<IStartupFilter, TestClientIpFilter>();
        });
    }

    public async Task InitializeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        var admin = Admin();
        MathsAssessmentId = await ImportAndPublishAsync(admin, Subject.Maths, TestCsv.Maths, 45);
        await ImportAndPublishAsync(admin, Subject.PhysicalSciences, TestCsv.Physics, 40);
    }

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    public HttpClient Admin()
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-Dev-Admin-Key", DevKey);
        return c;
    }

    public static async Task<Guid> ImportAndPublishAsync(HttpClient admin, Subject subject, string csv, int minutes)
    {
        using var res = await PostCsvAsync(admin, subject, csv, minutes);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = body.GetProperty("assessmentId").GetGuid();
        (await admin.PostAsync($"/api/admin/assessments/{id}/publish", null)).EnsureSuccessStatusCode();
        return id;
    }

    public static Task<HttpResponseMessage> PostCsvAsync(HttpClient admin, Subject subject, string csv, int minutes)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "questions.csv");
        form.Add(new StringContent(subject.ToString()), "subject");
        form.Add(new StringContent(minutes.ToString()), "timeLimitMinutes");
        return admin.PostAsync("/api/admin/questions/import", form);
    }

    public T WithDb<T>(Func<AppDbContext, T> f)
    {
        using var scope = Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<int> RunAutoSubmitAsync()
    {
        var svc = new AutoSubmitService(Services.GetRequiredService<IServiceScopeFactory>(),
            Services.GetRequiredService<IOptions<SiteOptions>>(), Clock, NullLogger<AutoSubmitService>.Instance);
        return await svc.SubmitExpiredAsync(CancellationToken.None);
    }

    public async Task<int> RunEmailDispatchAsync()
    {
        var svc = new EmailDispatcher(Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EmailDispatcher>.Instance, Clock);
        return await svc.SendDueAsync(CancellationToken.None);
    }
}

/// <summary>TestServer has no client IP. Tests can set one with X-Test-IP; otherwise each request gets a random one,
/// so the REG-06 per-IP limit only applies where a test asks for it.</summary>
public sealed class TestClientIpFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((ctx, nxt) =>
        {
            var ip = ctx.Request.Headers["X-Test-IP"].FirstOrDefault() ?? $"10.{Random.Shared.Next(256)}.{Random.Shared.Next(256)}.{Random.Shared.Next(1, 255)}";
            ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
            return nxt();
        });
        next(app);
    };
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
