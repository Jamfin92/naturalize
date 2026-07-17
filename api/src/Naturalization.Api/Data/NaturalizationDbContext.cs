using Microsoft.EntityFrameworkCore;
using Naturalization.Api.Domain;

namespace Naturalization.Api.Data;

public class NaturalizationDbContext(DbContextOptions<NaturalizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<TownCode> TownCodes => Set<TownCode>();
    public DbSet<CountryCode> CountryCodes => Set<CountryCode>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    /*
     * On the applicant query filter.
     *
     * Withdrawing an applicant is a soft delete: the row stays, and a global
     * query filter (!IsDeleted) hides it from every read path. The one place that
     * MUST see through it is the A-Number collision check on create — an A-Number
     * identifies a person and is not released when their record is withdrawn — so
     * that probe uses IgnoreQueryFilters(). See ApplicantEndpoints.
     *
     * Nothing else carries a filter: the case/decision/evidence tables that once
     * chained their filters through the applicant's are gone, so the dependent
     * NullReference hazard EF used to warn about no longer exists.
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

            e.Property(x => x.NaturalizationNumber).HasMaxLength(24);
            e.Property(x => x.PetitionNumber).HasMaxLength(24);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.MiddleName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Address1).HasMaxLength(200);
            e.Property(x => x.TownCode).HasMaxLength(3);
            e.Property(x => x.CountryCode).HasMaxLength(3);
            e.Property(x => x.ZipCode).HasMaxLength(16);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.DecisionNotes).HasMaxLength(2000);
            e.Property(x => x.DeletedBy).HasMaxLength(120);

            // Store the status as text so the DB stays readable and insertion-order safe.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => x.Status);

            e.HasIndex(x => x.IsDeleted);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        // Town and country lookups share a shape. Code is short; index it (plus
        // BaseCode) so a lookup by code is a seek, not a scan.
        b.Entity<TownCode>(e =>
        {
            e.Property(x => x.BaseCode).HasMaxLength(1).IsRequired();
            e.Property(x => x.Code).HasMaxLength(3).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.BaseCode, x.Code }).IsUnique();
        });

        b.Entity<CountryCode>(e =>
        {
            e.Property(x => x.BaseCode).HasMaxLength(1).IsRequired();
            e.Property(x => x.Code).HasMaxLength(3).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.BaseCode, x.Code }).IsUnique();
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

        b.Entity<ApplicationUser>(e =>
        {
            // The DbSet is named Users for a concise db.Users, but the table is
            // ApplicationUsers — pin it so the two never drift and the migration
            // (which creates ApplicationUsers) matches the runtime model.
            e.ToTable("ApplicationUsers");

            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.FieldOffice).HasMaxLength(100);

            // Stored as text, like the status: readable in the database and safe
            // against members being reordered.
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16).IsRequired();

            e.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
        });
    }
}
