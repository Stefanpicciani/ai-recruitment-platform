using Microsoft.EntityFrameworkCore;

namespace Recruitment.Recruiter.Web.Data;

/// <summary>
/// EF Core mapping for the Recruiter database.
/// The schema itself is owned by Data/Sql/schema.sql (applied by <see cref="DatabaseInitializer"/>),
/// so this mapping only mirrors table/column names, lengths and relationships.
/// </summary>
public class RecruiterDbContext(DbContextOptions<RecruiterDbContext> options) : DbContext(options)
{
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<JobOpportunity> JobOpportunities => Set<JobOpportunity>();
    public DbSet<RequiredSkill> RequiredSkills => Set<RequiredSkill>();
    public DbSet<ResumeDocument> ResumeDocuments => Set<ResumeDocument>();
    public DbSet<ResumeSubmission> ResumeSubmissions => Set<ResumeSubmission>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("dbo");

        b.Entity<Candidate>(e =>
        {
            e.ToTable("Candidates");
            e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.HasIndex(x => x.Email).HasDatabaseName("IX_Candidates_Email");
        });

        b.Entity<JobOpportunity>(e =>
        {
            e.ToTable("JobOpportunities");
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Department).HasMaxLength(100).IsRequired();
            e.Property(x => x.Location).HasMaxLength(160).IsRequired();
            e.Property(x => x.EmploymentType).HasMaxLength(40).IsRequired();
            e.Property(x => x.Description).IsRequired();
            e.Property(x => x.Requirements).IsRequired();
            e.HasIndex(x => x.IsActive).HasDatabaseName("IX_JobOpportunities_IsActive");
        });

        b.Entity<RequiredSkill>(e =>
        {
            e.ToTable("RequiredSkills");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Opportunity).WithMany(o => o.RequiredSkills)
                .HasForeignKey(x => x.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_RequiredSkills_JobOpportunities");
        });

        b.Entity<ResumeDocument>(e =>
        {
            e.ToTable("ResumeDocuments");
            e.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.StoredFileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.StoredPath).HasMaxLength(1024).IsRequired();
            e.HasOne(x => x.Candidate).WithMany(c => c.Resumes)
                .HasForeignKey(x => x.CandidateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ResumeDocuments_Candidates");
            e.HasOne(x => x.Opportunity).WithMany(o => o.Resumes)
                .HasForeignKey(x => x.OpportunityId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ResumeDocuments_JobOpportunities");
        });

        b.Entity<ResumeSubmission>(e =>
        {
            e.ToTable("ResumeSubmissions");
            e.Property(x => x.CandidateName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.StoredPath).HasMaxLength(1024).IsRequired();
            e.HasOne(x => x.Opportunity).WithMany()
                .HasForeignKey(x => x.OpportunityId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ResumeSubmissions_JobOpportunities");
        });
    }
}
