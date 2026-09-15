using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// THE IDENTITY-TO-EMPLOYEE MAPPING'S TABLE (ADR-030, FP-015 data-model).
//
// Modelled on `UserBranchAccessConfiguration`, which is `ADR-030` Decision 4 already implemented twice: a
// Platform-resident table pairing a `long` user key with a `Guid` key belonging to a row in the TENANT
// database.
//
// ---- RESIDENCY IS THIS FILE, AND ONLY THIS FILE.
//
// Configuring the type on the Platform context — and nowhere else — is what keeps the mapping out of the
// tenant database. Not `ITenantOwnedEntity`, which the type declines, and not the assembly it lives in.
// `TenantModelResidencyTests` asserts the absence directly.
public sealed class UserEmployeeLinkConfiguration : IEntityTypeConfiguration<UserEmployeeLink>
{
  public void Configure(EntityTypeBuilder<UserEmployeeLink> builder)
  {
    builder.ToTable("UserEmployeeLink", PlatformPersistenceConstants.Schema);
    builder.HasKey(link => link.Id);
    builder.Property(link => link.Id).HasColumnName("UserEmployeeLinkId").UseIdentityColumn();

    builder.Property(link => link.TenantId).IsRequired();
    builder.Property(link => link.TenantUserId).IsRequired();
    builder.Property(link => link.EmployeeId).IsRequired();
    builder.Property(link => link.CreatedUtc).IsRequired();
    builder.Property(link => link.CreatedBy).HasMaxLength(UserEmployeeLink.ActorMaximumLength);
    builder.Property(link => link.ModifiedUtc).IsRequired();
    builder.Property(link => link.ModifiedBy).HasMaxLength(UserEmployeeLink.ActorMaximumLength);

    // ================================================================================================
    // THE CARDINALITY RULE NEEDS TWO INDEXES, BECAUSE EACH ENFORCES ONE DIRECTION.
    // ================================================================================================
    //
    // `ADR-030` Decision 3 is *at most one live link each way*, and it is a rule about what may be true at
    // once rather than a schema instruction — so the package chooses the enforcement, and it chooses the
    // database. **Two unique indexes cannot be forgotten by a handler**, which is the same argument
    // `REQ-SS-0003` makes against enforcing a permission rule at every call site.
    //
    // UNFILTERED, because removal is PHYSICAL. There are no dead rows to exclude: a link is removed only by
    // administrative correction, and — the mistake this package most expects — **termination is not such an
    // event.** `REQ-SS-0006` requires the link to survive it, because severing it makes a terminated
    // employee's retained payslips unattributable.
    //
    // `TenantId` LEADS BOTH, for the neighbour's stated reason: every read is already tenant-scoped, and it
    // makes one tenant's links a contiguous range rather than a scattered lookup.

    // One employee per user. This also serves the READ: self-service resolution asks *given this tenant
    // user, which employee* on every request, and that is a seek on this index's leading columns. No
    // separate covering index is specified — if measurement later shows one is needed, that is a change
    // with evidence rather than a guess now.
    builder.HasIndex(link => new { link.TenantId, link.TenantUserId })
      .IsUnique()
      .HasDatabaseName("UX_UserEmployeeLink_TenantId_TenantUserId");

    // One user per employee. The other direction, and it is not implied by the first: without it, two
    // different users could each claim the same employee.
    builder.HasIndex(link => new { link.TenantId, link.EmployeeId })
      .IsUnique()
      .HasDatabaseName("UX_UserEmployeeLink_TenantId_EmployeeId");

    // ---- FK TO THE USER, RESTRICT, AND THE COMPOSITE PRINCIPAL KEY IS THE POINT.
    //
    // `(TenantId, TenantUserId)` as the principal key makes a link naming ANOTHER TENANT'S USER impossible
    // to store, rather than something a handler must remember to check. Tenant isolation on this table is
    // therefore a database guarantee and not application code.
    //
    // Restrict rather than Cascade for the neighbour's stated reason — a user is deactivated, never deleted
    // — and here for a second: a cascade would destroy the attributability `REQ-SS-0006` exists to protect.
    builder.HasOne<TenantUser>()
      .WithMany()
      .HasForeignKey(link => new { link.TenantId, link.TenantUserId })
      .HasPrincipalKey(user => new { user.TenantId, user.Id })
      .OnDelete(DeleteBehavior.Restrict);

    // ---- NO FOREIGN KEY ON EmployeeId, AND NONE IS POSSIBLE.
    //
    // `Employee` lives in the tenant database; this table lives in the platform database. The constraint is
    // impossible across catalogs and would become invalid the moment a tenant is promoted to dedicated
    // storage. `ADR-030` Decision 4 states this is a consequence rather than an oversight, and that
    // referential integrity across the link is the application's to maintain.
  }
}
