using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDatabaseAssignmentConfiguration : IEntityTypeConfiguration<TenantDatabaseAssignment>
{
  public void Configure(EntityTypeBuilder<TenantDatabaseAssignment> builder)
  {
    builder.ToTable("TenantDatabaseAssignments", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_TenantDatabaseAssignments_RoutingVersion", "[RoutingVersion] > 0");
      table.HasCheckConstraint(
        "CK_TenantDatabaseAssignments_EndedUtc",
        "[EndedUtc] IS NULL OR [EndedUtc] >= [AssignedUtc]");

      // EF MUST BE TOLD THIS TABLE HAS A TRIGGER (TS-Storage Phase E4). It is metadata only — no DDL is
      // generated from it — but without it EF reads the generated identity back with a bare `OUTPUT`, and
      // SQL Server refuses `OUTPUT` without `INTO` on any table carrying an enabled trigger. Declaring it
      // switches EF to the trigger-compatible strategy. The trigger itself is created by the migration.
      table.HasTrigger("TR_TenantDatabaseAssignments_EnforceRoutingVersion");
    });

    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id).HasColumnName("TenantDatabaseAssignmentId").UseIdentityColumn();

    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.TenantDatabaseId).IsRequired();
    builder.Property(assignment => assignment.RoutingVersion).IsRequired();
    builder.Property(assignment => assignment.AssignedUtc).IsRequired();
    builder.Property(assignment => assignment.EndedUtc);
    builder.Property(assignment => assignment.Reason).HasMaxLength(TenantDatabaseAssignment.ReasonMaximumLength);
    builder.Ignore(assignment => assignment.IsActive);

    // THE load-bearing invariant (ADR-017): at most ONE active assignment per tenant, enforced by the
    // schema rather than by application logic, so two concurrent writers cannot both commit. Filtering on
    // the nullable EndedUtc mirrors the existing [RemovedUtc] IS NULL active-row indexes and avoids
    // depending on an enum's stored string form inside an index filter.
    builder.HasIndex(assignment => assignment.TenantId)
      .IsUnique()
      .HasFilter("[EndedUtc] IS NULL")
      .HasDatabaseName("UX_TenantDatabaseAssignments_ActiveTenant");

    // "Which tenants are on this database" — needed by future placement/cutover reporting.
    builder.HasIndex(assignment => assignment.TenantDatabaseId)
      .HasDatabaseName("IX_TenantDatabaseAssignments_TenantDatabaseId");

    // Routing history for a tenant, newest first; also the read that computes the next RoutingVersion.
    builder.HasIndex(assignment => new { assignment.TenantId, assignment.RoutingVersion })
      .HasDatabaseName("IX_TenantDatabaseAssignments_TenantId_RoutingVersion");

    builder.HasOne<TenantDatabase>()
      .WithMany()
      .HasForeignKey(assignment => assignment.TenantDatabaseId)
      .HasPrincipalKey(database => database.Id)
      .OnDelete(DeleteBehavior.Restrict);

    // The tenant reference stays a real FK while both live in the Platform database. Company -> Tenant is
    // the only relationship ADR-017 turns into a cross-database, ID-only reference, and that happens when
    // Company moves, not here.
    builder.HasOne<Tenant>()
      .WithMany()
      .HasForeignKey(assignment => assignment.TenantId)
      .HasPrincipalKey(tenant => tenant.Id)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(assignment => assignment.CreatedUtc).IsRequired();
    builder.Property(assignment => assignment.CreatedBy).HasMaxLength(TenantDatabaseAssignment.ActorMaximumLength);
    builder.Property(assignment => assignment.ModifiedUtc).IsRequired();
    builder.Property(assignment => assignment.ModifiedBy).HasMaxLength(TenantDatabaseAssignment.ActorMaximumLength);
    builder.Property(assignment => assignment.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
