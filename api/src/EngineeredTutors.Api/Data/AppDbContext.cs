using Microsoft.EntityFrameworkCore;

namespace EngineeredTutors.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<Learner> Learners => Set<Learner>();
    public DbSet<LearnerSubject> LearnerSubjects => Set<LearnerSubject>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentQuestion> AssessmentQuestions => Set<AssessmentQuestion>();
    public DbSet<Attempt> Attempts => Set<Attempt>();
    public DbSet<Response> Responses => Set<Response>();
    public DbSet<TopicScore> TopicScores => Set<TopicScore>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<Subject>().HaveConversion<string>().HaveMaxLength(32);
        b.Properties<IdType>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<QuestionType>().HaveConversion<string>().HaveMaxLength(8);
        b.Properties<AttemptStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<LeadStatus>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<Band>().HaveConversion<string>().HaveMaxLength(16);
        b.Properties<decimal>().HavePrecision(18, 6);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Parent>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(16);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Source).HasMaxLength(64);
            e.HasIndex(x => x.Phone);
            e.HasOne(x => x.Lead).WithOne(x => x.Parent).HasForeignKey<Lead>(x => x.ParentId);
            e.HasMany(x => x.Consents).WithOne().HasForeignKey(x => x.ParentId);
        });

        m.Entity<Consent>(e =>
        {
            e.Property(x => x.TextVersion).HasMaxLength(32);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.HasIndex(x => new { x.IpAddress, x.AcceptedAt });
        });

        m.Entity<Learner>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(60);
            e.Property(x => x.Surname).HasMaxLength(60);
            e.Property(x => x.School).HasMaxLength(120);
            e.Property(x => x.Goal).HasMaxLength(64);
            e.Property(x => x.NameKey).HasMaxLength(120);
            e.Property(x => x.IdNumberEncrypted).HasMaxLength(256);
            e.Property(x => x.IdNumberHash).HasMaxLength(64);
            e.Property(x => x.IdLast4).HasMaxLength(4);
            e.HasOne(x => x.Parent).WithMany(x => x.Learners).HasForeignKey(x => x.ParentId);
            // Uniqueness only for learners who gave an ID number (section 5.1).
            e.HasIndex(x => x.IdNumberHash).IsUnique().HasFilter("[IdNumberHash] IS NOT NULL");
            e.HasIndex(x => new { x.ParentId, x.NameKey });
            e.Ignore(x => x.FullName);
        });

        m.Entity<LearnerSubject>(e =>
        {
            e.HasKey(x => new { x.LearnerId, x.Subject });
            e.HasOne<Learner>().WithMany(x => x.Subjects).HasForeignKey(x => x.LearnerId);
        });

        m.Entity<Lead>(e =>
        {
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.HasIndex(x => x.ParentId).IsUnique();
        });

        m.Entity<Topic>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(80);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasData(TopicSeed.All);
        });

        m.Entity<Question>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(16);
            e.Property(x => x.ImageUrl).HasMaxLength(200);
            e.Property(x => x.Unit).HasMaxLength(16);
            e.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.QuestionId);
        });

        m.Entity<QuestionOption>(e => e.Property(x => x.Label).HasMaxLength(1));

        m.Entity<Assessment>(e =>
        {
            e.HasIndex(x => new { x.Subject, x.Version }).IsUnique();
            e.HasMany(x => x.Questions).WithOne().HasForeignKey(x => x.AssessmentId);
        });

        m.Entity<AssessmentQuestion>(e =>
        {
            e.HasKey(x => new { x.AssessmentId, x.QuestionId });
            e.Property(x => x.Section).HasMaxLength(64);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<Attempt>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.Status, x.DeadlineAt });
            e.Property(x => x.OverallPct).HasPrecision(5, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Learner).WithMany(x => x.Attempts).HasForeignKey(x => x.LearnerId);
            e.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Responses).WithOne().HasForeignKey(x => x.AttemptId);
            e.HasMany(x => x.TopicScores).WithOne().HasForeignKey(x => x.AttemptId);
        });

        m.Entity<Response>(e =>
        {
            e.HasKey(x => new { x.AttemptId, x.QuestionId });
            e.Property(x => x.NumericRaw).HasMaxLength(32);
            e.Property(x => x.OptionOrderShown).HasMaxLength(200);
            e.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<TopicScore>(e =>
        {
            e.HasKey(x => new { x.AttemptId, x.TopicId });
            e.Property(x => x.Pct).HasPrecision(5, 2);
            e.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<EmailMessage>(e =>
        {
            e.Property(x => x.To).HasMaxLength(254);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => new { x.SentAt, x.Failed, x.NextAttemptAt });
        });
    }

    /// <summary>
    /// Enforces SC-06: once an assessment is published, its questions, options and question list cannot change.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        GuardPublishedAssessments();
        return base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        GuardPublishedAssessments();
        return base.SaveChanges();
    }

    private void GuardPublishedAssessments()
    {
        var changedQuestionIds = new HashSet<Guid>();
        var changedAssessmentIds = new HashSet<Guid>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted or EntityState.Added)) continue;
            switch (entry.Entity)
            {
                case Question q when entry.State != EntityState.Added:
                    changedQuestionIds.Add(q.Id);
                    break;
                case QuestionOption o:
                    changedQuestionIds.Add(o.QuestionId);
                    break;
                case AssessmentQuestion aq:
                    changedAssessmentIds.Add(aq.AssessmentId);
                    break;
                case Assessment a when entry.State == EntityState.Modified:
                    var published = entry.OriginalValues.GetValue<DateTime?>(nameof(Assessment.PublishedAt));
                    if (published is not null)
                        throw new PublishedAssessmentException();
                    break;
            }
        }

        if (changedQuestionIds.Count == 0 && changedAssessmentIds.Count == 0) return;

        var frozen = AssessmentQuestions
            .Where(aq => changedQuestionIds.Contains(aq.QuestionId) || changedAssessmentIds.Contains(aq.AssessmentId))
            .Join(Assessments, aq => aq.AssessmentId, a => a.Id, (aq, a) => a.PublishedAt)
            .Any(p => p != null);
        if (!frozen && changedAssessmentIds.Count > 0)
            frozen = Assessments.Any(a => changedAssessmentIds.Contains(a.Id) && a.PublishedAt != null);

        if (frozen) throw new PublishedAssessmentException();
    }
}

public class PublishedAssessmentException()
    : InvalidOperationException("This assessment is published. Its questions cannot be changed; import a new version instead.");
