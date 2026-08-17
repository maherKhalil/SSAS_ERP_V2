using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class UserBranchAccessConfiguration : IEntityTypeConfiguration<UserBranchAccess>
{
  public void Configure(EntityTypeBuilder<UserBranchAccess> builder)
  {
    builder.ToTable("UserBranchAccess", PlatformPersistenceConstants.Schema);
    builder.HasKey(access => access.Id);
    builder.Property(access => access.Id).HasColumnName("UserBranchAccessId").UseIdentityColumn();

    builder.Property(access => access.TenantId).IsRequired();
    builder.Property(access => access.TenantUserId).IsRequired();
    builder.Property(access => access.BranchId).IsRequired();
    builder.Property(access => access.CreatedUtc).IsRequired();
    builder.Property(access => access.CreatedBy).HasMaxLength(UserBranchAccess.ActorMaximumLength);
    builder.Property(access => access.ModifiedUtc).IsRequired();
    builder.Property(access => access.ModifiedBy).HasMaxLength(UserBranchAccess.ActorMaximumLength);

    // ---- ONE ROW PER USER PER BRANCH. Tenant leads because every read is already scoped to a tenant, and
    // it makes the assignment set for one user a contiguous range rather than a scattered lookup.
    //
    // UNFILTERED, because removal here is PHYSICAL. Unlike role assignments, which retain a removed row to
    // record that authority once existed, branch access is a live capability list: the audit trail of who
    // could enter where belongs to the platform audit stream, not to rows that would then have to be
    // excluded from every authorization check and every uniqueness test.
    builder.HasIndex(access => new { access.TenantId, access.TenantUserId, access.BranchId })
      .IsUnique()
      .HasDatabaseName("UX_UserBranchAccess_TenantId_TenantUserId_BranchId");

    // The branch-deactivation impact query: "which users can still enter this branch". Without it that
    // question is a scan of every assignment in the estate, and it runs while an administrator waits.
    builder.HasIndex(access => new { access.TenantId, access.BranchId })
      .HasDatabaseName("IX_UserBranchAccess_TenantId_BranchId");

    // ---- FK TO THE USER, RESTRICT. The composite (TenantId, TenantUserId) principal key is the same shape
    // TenantUserRoleAssignment uses, so a row can never name a user from another tenant. Restrict rather
    // than Cascade: a user is deactivated, never deleted, and a cascade here would silently erase the
    // record of which branches an investigated account could reach.
    builder.HasOne<TenantUser>()
      .WithMany()
      .HasForeignKey(access => new { access.TenantId, access.TenantUserId })
      .HasPrincipalKey(user => new { user.TenantId, user.Id })
      .OnDelete(DeleteBehavior.Restrict);

    // ---- NO FOREIGN KEY ON BranchId. Branch lives in the tenant database; this table lives in the
    // platform database. The constraint is impossible across catalogs and would become invalid the moment
    // a tenant is promoted to dedicated storage. Validation is the application's job, performed against the
    // tenant database before any row here is written.
  }
}
