using EngineeredTutors.Api.Data;

namespace EngineeredTutors.Api.Features.Registrations;

public sealed class RegistrationRequest
{
    public ParentInput Parent { get; set; } = new();
    public List<LearnerInput> Learners { get; set; } = [];
    /// <summary>"I am the parent or legal guardian and over 18" (REG-04).</summary>
    public bool IsGuardianOver18 { get; set; }
    /// <summary>Consent statement checkbox (REG-04).</summary>
    public bool ConsentAccepted { get; set; }
    /// <summary>Consent text version shown on the form (REG-05).</summary>
    public string? ConsentTextVersion { get; set; }
    /// <summary>Cloudflare Turnstile response token (REG-06).</summary>
    public string? TurnstileToken { get; set; }
    /// <summary>Honeypot (REG-06). Hidden from people; bots fill it in.</summary>
    public string? Website { get; set; }
}

public sealed class ParentInput
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
}

public sealed class LearnerInput
{
    public string? FirstName { get; set; }
    public string? Surname { get; set; }
    public string? School { get; set; }
    public int? Grade { get; set; }
    public List<SubjectInput> Subjects { get; set; } = [];
    public string? Goal { get; set; }
    public IdType? IdType { get; set; }
    public string? IdNumber { get; set; }
}

public sealed class SubjectInput
{
    public Subject Subject { get; set; }
    /// <summary>Latest mark, 0–100, optional.</summary>
    public int? LatestMark { get; set; }
}

/// <summary>
/// Same shape whether learners are new or matched existing records (REG-07). Test links are emailed in every case;
/// <see cref="Links"/> holds links for newly registered learners only, to show on screen (REG-08).
/// </summary>
public sealed record RegistrationResponse(string Message, IReadOnlyList<LearnerLinksDto> Links);

public sealed record LearnerLinksDto(string FirstName, IReadOnlyList<SubjectLinkDto> Tests);

public sealed record SubjectLinkDto(Subject Subject, string Url, int Minutes);

/// <summary>Dropdown values (REG-01, REG-02). The web form uses the same lists.</summary>
public static class RegistrationLists
{
    public static readonly string[] Sources =
        ["Google search", "Facebook", "Instagram", "TikTok", "WhatsApp", "Friend or family", "School", "Other"];

    public static readonly string[] Goals =
    [
        "Pass the subject",
        "Improve my mark",
        "Get a distinction",
        "Qualify for engineering or science",
        "Not sure yet",
    ];
}
