using EngineeredTutors.Api.Data;

namespace EngineeredTutors.Api;

public sealed class SiteOptions
{
    /// <summary>Public web origin used in links, e.g. https://engineeredtutors.co.za (no trailing slash).</summary>
    public string BaseUrl { get; set; } = "http://localhost:4200";
    /// <summary>Where completion alerts go (NT-02).</summary>
    public string AdminAlertEmail { get; set; } = "";
    /// <summary>Version of the consent statement currently on the form (REG-05).</summary>
    public string ConsentTextVersion { get; set; } = "0.3-draft";
    public int TokenLifetimeDays { get; set; } = 30;
    /// <summary>Answers arriving later than this after the deadline are rejected (DX-09).</summary>
    public int GraceSeconds { get; set; } = 30;
    /// <summary>Learners per registration (REG-03).</summary>
    public int MaxLearnersPerRegistration { get; set; } = 3;
    /// <summary>Registrations per IP per hour (REG-06).</summary>
    public int RegistrationsPerIpPerHour { get; set; } = 5;
    /// <summary>Default time limits (DX-13). Each assessment stores its own.</summary>
    public int MathsMinutes { get; set; } = 45;
    public int PhysicalSciencesMinutes { get; set; } = 40;
    /// <summary>Shown in the test instructions (DX-02). Open decision 3; neither v1.1 set needs one.</summary>
    public bool CalculatorAllowed { get; set; }

    public int DefaultMinutes(Subject s) => s == Subject.Maths ? MathsMinutes : PhysicalSciencesMinutes;
}

public static class Display
{
    /// <summary>South Africa has no daylight saving, so SAST is always UTC+2 (NF-07).</summary>
    private static readonly TimeSpan SastOffset = TimeSpan.FromHours(2);

    public static string Sast(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(SastOffset)
            .ToString("d MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture) + " SAST";

    public static string SubjectName(Subject s) => s switch
    {
        Subject.Maths => "Mathematics",
        Subject.PhysicalSciences => "Physical Sciences",
        _ => s.ToString(),
    };
}
