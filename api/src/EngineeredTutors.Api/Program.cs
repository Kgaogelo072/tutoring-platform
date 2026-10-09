using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using EngineeredTutors.Api;
using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Domain;
using EngineeredTutors.Api.Features.Admin;
using EngineeredTutors.Api.Features.Attempts;
using EngineeredTutors.Api.Features.Registrations;
using EngineeredTutors.Api.Notifications;
using EngineeredTutors.Api.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<SiteOptions>(config.GetSection("Site"));
builder.Services.Configure<ScoringOptions>(config.GetSection("Scoring"));
builder.Services.Configure<SecurityOptions>(config.GetSection("Security"));
builder.Services.Configure<EmailOptions>(config.GetSection("Email"));
builder.Services.Configure<TurnstileOptions>(config.GetSection("Turnstile"));
builder.Services.Configure<AdminAuthOptions>(config.GetSection("AdminAuth"));
builder.Services.Configure<SeedOptions>(config.GetSection("Seed"));
builder.Services.PostConfigure<TurnstileOptions>(o => o.AllowMissingSecret &= builder.Environment.IsDevelopment());

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("Default")
                   ?? throw new InvalidOperationException("ConnectionStrings__Default is not configured. See api/.env.example.")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new Protector(sp.GetRequiredService<IOptions<SecurityOptions>>().Value));
builder.Services.AddScoped<EmailQueue>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AttemptService>();
builder.Services.AddScoped<SubmissionService>();
builder.Services.AddScoped<QuestionImporter>();
builder.Services.AddScoped<AdminQueries>();
builder.Services.AddHttpClient<ITurnstileVerifier, TurnstileVerifier>(c => c.Timeout = TimeSpan.FromSeconds(10));

if (string.Equals(config["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, PickupEmailSender>();

if (config.GetValue("BackgroundServices:Enabled", true))
{
    builder.Services.AddHostedService<EmailDispatcher>();
    builder.Services.AddHostedService<AutoSubmitService>();
}

builder.AddAdminAuth();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails();

// REG-06: a loose per-request limit for abuse. The 5-per-hour rule counts successful registrations (see below).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RegistrationEndpoints.RateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = config.GetValue("Site:RegistrationRequestsPerIpPerHour", 20),
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Azure App Service terminates TLS and appends the client IP to X-Forwarded-For.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(origins).AllowAnyHeader().WithMethods("GET", "POST", "PUT", "PATCH")
        .WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

if (config.GetValue("ForwardedHeaders:Enabled", false)) app.UseForwardedHeaders();

app.UseExceptionHandler(e => e.Run(async http =>
{
    var ex = http.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, detail) = ex switch
    {
        PublishedAssessmentException p => (409, p.Message),
        BadHttpRequestException b => (b.StatusCode, "The request was not valid."),
        _ => (500, "Something went wrong. Please try again."),
    };
    http.Response.StatusCode = status;
    await Results.Problem(statusCode: status, detail: detail).ExecuteAsync(http);
}));
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");
api.MapRegistrations();
api.MapAttempts();
api.MapAdmin();

// Fail fast on missing keys rather than at the first registration.
_ = app.Services.GetRequiredService<Protector>();

if (app.Environment.IsDevelopment() && config.GetValue("Seed:Enabled", true))
    await DevSeeder.RunAsync(app.Services);

app.Run();

public partial class Program;
