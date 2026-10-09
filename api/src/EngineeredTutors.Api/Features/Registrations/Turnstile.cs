using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Features.Registrations;

public sealed class TurnstileOptions
{
    public string SecretKey { get; set; } = "";
    /// <summary>Skip verification when no secret is set. Only ever true in Development.</summary>
    public bool AllowMissingSecret { get; set; }
}

public interface ITurnstileVerifier
{
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct);
}

/// <summary>Cloudflare Turnstile server-side check (REG-06).</summary>
public sealed class TurnstileVerifier(HttpClient http, IOptions<TurnstileOptions> options, ILogger<TurnstileVerifier> log)
    : ITurnstileVerifier
{
    private const string Endpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrEmpty(o.SecretKey))
        {
            if (o.AllowMissingSecret) return true;
            log.LogError("Turnstile:SecretKey is not configured; rejecting registration");
            return false;
        }
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;

        var form = new Dictionary<string, string> { ["secret"] = o.SecretKey, ["response"] = token };
        if (!string.IsNullOrEmpty(remoteIp)) form["remoteip"] = remoteIp;
        try
        {
            using var res = await http.PostAsync(Endpoint, new FormUrlEncodedContent(form), ct);
            var body = await res.Content.ReadFromJsonAsync<SiteVerify>(ct);
            return body?.Success == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            log.LogWarning(ex, "Turnstile verification request failed");
            return false;
        }
    }

    private sealed record SiteVerify([property: JsonPropertyName("success")] bool Success);
}
