using System.Net;
using System.Net.Mail;
using EngineeredTutors.Api.Data;
using EngineeredTutors.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngineeredTutors.Api.Notifications;

public sealed class EmailOptions
{
    /// <summary>"Smtp" sends for real; "Pickup" writes .html files to PickupDirectory (local development).</summary>
    public string Mode { get; set; } = "Pickup";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Engineered Tutors";
    public string PickupDirectory { get; set; } = "mail-pickup";
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public bool SmtpSsl { get; set; } = true;
    public int MaxAttempts { get; set; } = 8;
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string html, CancellationToken ct);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.SmtpHost, o.SmtpPort)
        {
            EnableSsl = o.SmtpSsl,
            Credentials = string.IsNullOrEmpty(o.SmtpUser) ? null : new NetworkCredential(o.SmtpUser, o.SmtpPassword),
        };
        using var msg = new MailMessage(new MailAddress(o.From, o.FromName), new MailAddress(to))
        {
            Subject = subject,
            Body = html,
            IsBodyHtml = true,
        };
        await client.SendMailAsync(msg, ct);
    }
}

/// <summary>Development sender: writes each email to a file so links can be clicked without a mail server.</summary>
public sealed class PickupEmailSender(IOptions<EmailOptions> options, IHostEnvironment env, ILogger<PickupEmailSender> log)
    : IEmailSender
{
    public async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var dir = Path.Combine(env.ContentRootPath, options.Value.PickupDirectory);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.html");
        var header = $"<!-- To: {WebUtility.HtmlEncode(to)} | Subject: {WebUtility.HtmlEncode(subject)} -->\n";
        await File.WriteAllTextAsync(path, header + html, ct);
        log.LogInformation("Email written to {Path}", path);
    }
}

/// <summary>Adds messages to the outbox. The caller's SaveChanges commits them with the rest of its work.</summary>
public sealed class EmailQueue(AppDbContext db, Protector protector, TimeProvider clock)
{
    public void Enqueue(string to, string subject, string html)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        db.EmailMessages.Add(new EmailMessage
        {
            Id = Guid.NewGuid(),
            To = to,
            Subject = subject,
            BodyEncrypted = protector.Encrypt(html),
            CreatedAt = now,
            NextAttemptAt = now,
        });
    }
}

/// <summary>Sends queued email with exponential back-off (NT-03). Bodies are wiped once sent.</summary>
public sealed class EmailDispatcher(IServiceScopeFactory scopes, ILogger<EmailDispatcher> log, TimeProvider clock)
    : BackgroundService
{
    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await SendDueAsync(stop); }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogError(ex, "Email dispatch loop failed"); }
            try { await Task.Delay(PollInterval, stop); } catch (OperationCanceledException) { }
        }
    }

    public async Task<int> SendDueAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var protector = scope.ServiceProvider.GetRequiredService<Protector>();
        var o = scope.ServiceProvider.GetRequiredService<IOptions<EmailOptions>>().Value;
        var now = clock.GetUtcNow().UtcDateTime;

        var due = await db.EmailMessages
            .Where(m => m.SentAt == null && !m.Failed && m.NextAttemptAt <= now)
            .OrderBy(m => m.NextAttemptAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var m in due)
        {
            try
            {
                await sender.SendAsync(m.To, m.Subject, protector.Decrypt(m.BodyEncrypted!), ct);
                m.SentAt = clock.GetUtcNow().UtcDateTime;
                m.BodyEncrypted = null;
                m.LastError = null;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                m.AttemptCount++;
                m.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (m.AttemptCount >= o.MaxAttempts)
                {
                    m.Failed = true;
                    m.BodyEncrypted = null;
                    log.LogError("Email {Id} failed permanently after {Count} attempts", m.Id, m.AttemptCount);
                }
                else
                {
                    m.NextAttemptAt = now.AddSeconds(30 * Math.Pow(2, m.AttemptCount - 1));
                    log.LogWarning("Email {Id} failed (attempt {Count}); retrying", m.Id, m.AttemptCount);
                }
            }
            await db.SaveChangesAsync(ct);
        }
        return due.Count;
    }
}
