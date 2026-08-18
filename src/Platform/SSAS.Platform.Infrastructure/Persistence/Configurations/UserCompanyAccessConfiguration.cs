using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class UserCompanyAccessConfiguration : IEntityTypeConfiguration<UserCompanyAccess>
{
  public void Configure(EntityTypeBuilder<UserCompanyAccess> builder)
  {
    builder.ToTable("UserCompanyAccess", PlatformPersistenceConstants.Schema);
    builder.HasKey(access => access.Id);
    builder.Property(access => access.Id).HasColumnName("UserCompanyAccessId").UseIdentityColumn();

    builder.Property(access => access.TenantId).IsRequired();
    builder.Property(access => access.TenantUserId).IsRequired();
    builder.Property(access => access.CompanyId).IsRequired();
    builder.Property(access => access.CreatedUtc).IsRequired();
    builder.Property(access => access.CreatedBy).HasMaxLength(UserCompanyAccess.ActorMaximumLength);
    builder.Property(access => access.ModifiedUtc).IsRequired();
    builder.Property(access => access.ModifiedBy).HasMaxLength(UserCompanyAccess.ActorMaximumLength);

    // ---- ONE ROW PER USER PER COMPANY. Tenant leads because every read is already scoped to a tenant, and
    // it makes the assignment set for one user a contiguous range rather than a scattered lookup. The same
    // shape UserBranchAccess uses for the sibling dimension.
    //
    // UNFILTERED, because removal here is PHYSICAL. Unlike role assignments, which retain a removed row to
    // record that authority once existed, company access is a live capability list: the audit trail of who
    // could act where belongs to the platform audit stream, not to rows that would then have to be excluded
    // from every authorization check and every uniqueness test.
    builder.HasIndex(access => new { access.TenantId, access.TenantUserId, access.CompanyId })
      .IsUnique()
      .HasDatabaseName("UX_UserCompanyAccess_TenantId_TenantUserId_CompanyId");

    // The company-deactivation impact query: "which users can still act within this company". Without it
    // that question is a scan of every assignment in the estate.
    builder.HasIndex(access => new { access.TenantId, access.CompanyId })
      .HasDatabaseName("IX_UserCompanyAccess_TenantId_CompanyId");

    // ---- FK TO THE USER, RESTRICT. The composite (TenantId, TenantUserId) principal key is the same shape
    // UserBranchAccess and TenantUserRoleAssignment use, so a row can never name a user from another tenant.
    // Restrict rather than Cascade: a user is deactivated, never deleted, and a cascade here would silently
    // erase the record of which companies an investigated account could reach.
    builder.HasOne<TenantUser>()
      .WithMany()
      .HasForeignKey(access => new { access.TenantId, access.TenantUserId })
      .HasPrincipalKey(user => new { user.TenantId, user.Id })
      .OnDelete(DeleteBehavior.Restrict);

    // ---- NO FOREIGN KEY ON CompanyId. Company lives in the tenant database; this table lives in the
    // platform database. The constraint is impossible across catalogs and would become invalid the moment a
    // tenant is promoted to dedicated storage (ADR-017). Validation is the application's job, performed
    // against the tenant database before any row here is written (ADR-025 decision 5).
  }
}
