using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Data;

public class NaturalizationDbContext(DbContextOptions<NaturalizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<NaturalizationCase> Cases => Set<NaturalizationCase>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<EvidenceDocument> Documents => Set<EvidenceDocument>();
    public DbSet<CaseEvent> Events => Set<CaseEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Applicant>(e =>
        {
            e.Property(x => x.AlienNumber).HasMaxLength(16).IsRequired();
            e.HasIndex(x => x.AlienNumber).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.CountryOfBirth).HasMaxLength(100);
            e.Property(x => x.Nationality).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(200);
        });

        b.Entity<NaturalizationCase>(e =>
        {
            e.Property(x => x.ReceiptNumber).HasMaxLength(24).IsRequired();
            e.HasIndex(x => x.ReceiptNumber).IsUnique();
            e.Property(x => x.FieldOffice).HasMaxLength(100);

            // Store enums as text so the DB stays readable and insertion-order safe.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.Status);

            e.HasOne(x => x.Applicant)
                .WithMany(a => a.Cases)
                .HasForeignKey(x => x.ApplicantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Decision>(e =>
        {
            e.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.DecidedBy).HasMaxLength(120);
            e.Property(x => x.Rationale).HasMaxLength(2000);
            e.Property(x => x.DenialReasonCode).HasMaxLength(64);

            // One decision per case, enforced by the database rather than by convention.
            e.HasOne(x => x.Case)
                .WithOne(c => c.Decision!)
                .HasForeignKey<Decision>(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CaseId).IsUnique();
        });

        b.Entity<EvidenceDocument>(e =>
        {
            e.Property(x => x.DocumentType).HasMaxLength(120);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);

            e.HasOne(x => x.Case)
                .WithMany(c => c.Documents)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CaseEvent>(e =>
        {
            e.Property(x => x.EventType).HasMaxLength(120);
            e.Property(x => x.Actor).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(1000);

            e.HasOne(x => x.Case)
                .WithMany(c => c.Events)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OccurredAt);
        });
    }
}
