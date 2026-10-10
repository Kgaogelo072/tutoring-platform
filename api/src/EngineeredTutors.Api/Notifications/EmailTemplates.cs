using System.Globalization;
using System.Text;
using EngineeredTutors.Api.Data;
using static System.Net.WebUtility;

namespace EngineeredTutors.Api.Notifications;

/// <summary>
/// Email bodies. ID numbers are never included (NF-09). Every user-supplied value is HTML-encoded.
/// </summary>
public static class EmailTemplates
{
    public sealed record TestLink(Subject Subject, int Minutes, string? Url, bool Completed, bool RecentlySent = false);
    public sealed record LearnerLinks(string FirstName, IReadOnlyList<TestLink> Tests);

    /// <summary>NT-01: parent confirmation with each learner's test links. Also used when links are resent (REG-07).</summary>
    public static (string Subject, string Html) ParentLinks(string parentName, IReadOnlyList<LearnerLinks> learners)
    {
        var names = learners.Select(l => l.FirstName).ToList();
        var who = names.Count switch
        {
            1 => names[0],
            _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
        };

        var sb = new StringBuilder();
        sb.Append(Open());
        sb.Append($"<p>Hi {HtmlEncode(FirstWord(parentName))},</p>");
        sb.Append($"<p>Thank you for registering {HtmlEncode(who)} for the free Engineered Tutors diagnostic. ");
        sb.Append("Here are the test links. Each link is personal to one learner and one subject, so please don't share them.</p>");

        foreach (var l in learners)
        {
            sb.Append($"<h2 style=\"font-size:18px;margin:24px 0 8px\">{HtmlEncode(l.FirstName)}</h2><ul style=\"padding-left:20px\">");
            foreach (var t in l.Tests)
            {
                var name = HtmlEncode(Display.SubjectName(t.Subject));
                if (t.Completed)
                    sb.Append($"<li>{name}: already completed. Thank you!</li>");
                else if (t.RecentlySent)
                    sb.Append($"<li>{name}: the link is in the email we sent a few minutes ago.</li>");
                else
                    sb.Append($"<li style=\"margin-bottom:8px\"><a href=\"{HtmlEncode(t.Url)}\">{name} diagnostic</a> ({t.Minutes} minutes)</li>");
            }
            sb.Append("</ul>");
        }

        sb.Append("<h2 style=\"font-size:18px;margin:24px 0 8px\">Before starting</h2><ul style=\"padding-left:20px\">");
        sb.Append("<li>Use a phone, tablet or laptop with a steady connection, somewhere quiet.</li>");
        sb.Append("<li>Have paper and a pen ready for working out.</li>");
        sb.Append("<li>The timer starts when the learner taps <strong>Start</strong> and can't be paused. ");
        sb.Append("If the page closes, opening the link again carries on where they left off.</li>");
        sb.Append("<li>Links expire after 30 days.</li></ul>");

        sb.Append("<h2 style=\"font-size:18px;margin:24px 0 8px\">What happens next</h2>");
        sb.Append("<p>Once a test is submitted, I'll review the results and send you a short report on the topics that ");
        sb.Append("need attention, usually within 48 hours. Then we can talk about a plan for Grade 12.</p>");
        sb.Append("<p>Kind regards,<br>Engineered Tutors</p>");
        sb.Append(Close());

        var subject = learners.Count == 1
            ? $"Diagnostic test links for {learners[0].FirstName}"
            : "Your diagnostic test links";
        return (subject, sb.ToString());
    }

    /// <summary>NT-02: admin alert when an attempt is submitted.</summary>
    public static (string Subject, string Html) AttemptSubmitted(
        string learnerName, Subject subject, decimal overallPct, bool autoSubmitted, string adminUrl)
    {
        var pct = overallPct.ToString("0.#", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        sb.Append(Open());
        sb.Append($"<p><strong>{HtmlEncode(learnerName)}</strong> submitted the {HtmlEncode(Display.SubjectName(subject))} diagnostic");
        sb.Append(autoSubmitted ? " (submitted automatically when time ran out)." : ".");
        sb.Append($"</p><p style=\"font-size:22px;margin:16px 0\">Overall: <strong>{pct}%</strong></p>");
        sb.Append($"<p><a href=\"{HtmlEncode(adminUrl)}\">Open the results in admin</a></p>");
        sb.Append(Close());
        return ($"Diagnostic submitted: {learnerName}, {Display.SubjectName(subject)} {pct}%", sb.ToString());
    }

    private static string FirstWord(string s) => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? s;

    private static string Open() =>
        "<!doctype html><html><body style=\"margin:0;background:#f5f6f8\">" +
        "<div style=\"max-width:560px;margin:0 auto;padding:24px;font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.55;color:#1a2233;background:#ffffff\">" +
        "<p style=\"font-size:20px;font-weight:bold;color:#0c1f45;margin:0 0 16px\">Engineered Tutors</p>";

    private static string Close() => "</div></body></html>";
}
