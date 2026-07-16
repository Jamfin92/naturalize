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
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<OfficerAccount> Officers => Set<OfficerAccount>();

    /*
     * On deletion, and why every relationship below says Restrict.
     *
     * These four foreign keys used to be Cascade, so `db.Applicants.Remove(a)`
     * silently destroyed that applicant's cases, evidence, decisions AND their
     * entire audit trail. Restrict inverts the failure: an accidental hard delete
     * now THROWS instead of quietly erasing the record of who did what to
     * someone's citizenship application. Deletion is a soft delete or it does not
     * happen.
     *
     * On the query filters, and why each dependent repeats its principal's.
     *
     * A naive filter on NaturalizationCase would be `!x.IsDeleted`. That is a
     * NullReferenceException waiting to happen: a case whose APPLICANT is
     * soft-deleted passes such a filter, but `Include(c => c.Applicant)` then
     * resolves to null (the principal is filtered out) and `c.Applicant.FullName`
     * blows up with a 500. So every dependent's filter includes its principal's,
     * all the way up the chain. EF logs
     * PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning about
     * exactly this hazard; it is suppressed in Program.cs precisely BECAUSE the
     * chaining below makes it unreachable.
     *
     * Note EF allows only one HasQueryFilter per entity — a second call replaces
     * the first rather than ANDing with it.
     */
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Applicant>(e =>
        {
            e.Property(x => x.AlienNumber).HasMaxLength(16).IsRequired();

            /*
             * Unfiltered on purpose. An A-Number identifies a person; withdrawing
             * their record does not release it back into the pool. So POST
             * /api/applicants must probe for collisions with IgnoreQueryFilters()
             * — otherwise the filter hides the soft-deleted row, this index fires
             * instead, and the caller gets a raw 500 rather than "that A-Number
             * belongs to a withdrawn applicant, restore it".
             */
            e.HasIndex(x => x.AlienNumber).IsUnique();

            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.MiddleName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.CountryOfBirth).HasMaxLength(100);
            e.Property(x => x.Nationality).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.DeletedBy).HasMaxLength(120);

            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        b.Entity<NaturalizationCase>(e =>
        {
            e.Property(x => x.ReceiptNumber).HasMaxLength(24).IsRequired();
            e.HasIndex(x => x.ReceiptNumber).IsUnique();   // unfiltered, as above
            e.Property(x => x.FieldOffice).HasMaxLength(100);
            e.Property(x => x.DeletedBy).HasMaxLength(120);

            // Store enums as text so the DB stays readable and insertion-order safe.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.Status);

            e.HasOne(x => x.Applicant)
                .WithMany(a => a.Cases)
                .HasForeignKey(x => x.ApplicantId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(x => !x.IsDeleted && !x.Applicant.IsDeleted);
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
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CaseId).IsUnique();

            e.HasQueryFilter(x => !x.Case.IsDeleted && !x.Case.Applicant.IsDeleted);
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
                .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(x => !x.Case.IsDeleted && !x.Case.Applicant.IsDeleted);
        });

        b.Entity<CaseEvent>(e =>
        {
            e.Property(x => x.EventType).HasMaxLength(120);
            e.Property(x => x.Actor).HasMaxLength(120);
            e.Property(x => x.Notes).HasMaxLength(1000);

            e.HasOne(x => x.Case)
                .WithMany(c => c.Events)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OccurredAt);

            /*
             * Hidden when the case is withdrawn, but still in the file. The rows
             * survive; only the read paths stop returning them. To read the trail
             * of a withdrawn record, IgnoreQueryFilters() — which is exactly what
             * the soft-delete tests assert.
             */
            e.HasQueryFilter(x => !x.Case.IsDeleted && !x.Case.Applicant.IsDeleted);
        });

        // Append-only. No FK, no soft-delete flag, no query filter: this must
        // outlive the row it describes, or it is not an audit log.
        b.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
            e.Property(x => x.Action).HasMaxLength(32).IsRequired();
            e.Property(x => x.Actor).HasMaxLength(120).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(1000);

            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.OccurredAt);
        });

        b.Entity<OfficerAccount>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.FieldOffice).HasMaxLength(100);

            // Stored as text, like every other enum in the model: readable in the
            // database and safe against members being reordered.
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();

            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        });
    }
}
