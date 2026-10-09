using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Security;

public sealed class AdminAuthOptions
{
    /// <summary>Entra ID tenant (directory) ID.</summary>
    public string TenantId { get; set; } = "";
    /// <summary>Application (client) ID of the API app registration; tokens must be issued for it.</summary>
    public string ApiClientId { get; set; } = "";
    /// <summary>Object IDs of accounts allowed into admin. AD-01: the tutor's account only.</summary>
    public string[] AllowedObjectIds { get; set; } = [];
    /// <summary>Development only: a shared key sent as X-Dev-Admin-Key. Ignored outside Development.</summary>
    public string DevKey { get; set; } = "";
}

public static class AdminAuth
{
    public const string DevScheme = "DevAdmin";
    private const string OidClaim = "oid";

    /// <summary>The dev key works only in Development and in the automated tests, never in production.</summary>
    private static bool IsLocal(IHostEnvironment env) => env.IsDevelopment() || env.IsEnvironment("Testing");

    public static void AddAdminAuth(this WebApplicationBuilder builder)
    {
        var o = builder.Configuration.GetSection("AdminAuth").Get<AdminAuthOptions>() ?? new();
        var useDev = IsLocal(builder.Environment) && !string.IsNullOrEmpty(o.DevKey);
        var schemes = new List<string>();

        var auth = builder.Services.AddAuthentication();
        if (!string.IsNullOrEmpty(o.TenantId) && !string.IsNullOrEmpty(o.ApiClientId))
        {
            auth.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                jwt.Authority = $"https://login.microsoftonline.com/{o.TenantId}/v2.0";
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.ValidAudiences = [o.ApiClientId, $"api://{o.ApiClientId}"];
                jwt.TokenValidationParameters.ValidIssuer = $"https://login.microsoftonline.com/{o.TenantId}/v2.0";
                jwt.TokenValidationParameters.NameClaimType = "name";
            });
            schemes.Add(JwtBearerDefaults.AuthenticationScheme);
        }
        if (useDev)
        {
            auth.AddScheme<AuthenticationSchemeOptions, DevAdminHandler>(DevScheme, null);
            schemes.Add(DevScheme);
        }

        var allowed = o.AllowedObjectIds.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        builder.Services.AddAuthorization(a => a.AddPolicy(Features.Admin.AdminEndpoints.Policy, p =>
        {
            // With no scheme configured the policy can never pass, so admin stays closed rather than open.
            p.AddAuthenticationSchemes(schemes.Count > 0 ? [.. schemes] : [DevScheme]);
            p.RequireAuthenticatedUser();
            p.RequireAssertion(ctx =>
                ctx.User.Identity?.AuthenticationType == DevScheme ||
                (ctx.User.FindFirst(OidClaim)?.Value is { } oid && allowed.Contains(oid)));
        }));

        if (schemes.Count == 0)
            auth.AddScheme<AuthenticationSchemeOptions, DevAdminHandler>(DevScheme, null);
    }

    /// <summary>Local development sign-in. Registered as a real scheme only in Development with a DevKey set.</summary>
    private sealed class DevAdminHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        IOptions<AdminAuthOptions> admin, IHostEnvironment env)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var key = admin.Value.DevKey;
            if (!IsLocal(env) || string.IsNullOrEmpty(key)) return Task.FromResult(AuthenticateResult.NoResult());
            if (!Request.Headers.TryGetValue("X-Dev-Admin-Key", out var sent)) return Task.FromResult(AuthenticateResult.NoResult());
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent.ToString()), Encoding.UTF8.GetBytes(key)))
                return Task.FromResult(AuthenticateResult.Fail("Invalid dev admin key"));

            var identity = new ClaimsIdentity([new Claim("name", "Local admin")], DevScheme, "name", null);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), DevScheme)));
        }
    }
}
