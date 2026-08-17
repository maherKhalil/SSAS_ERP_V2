using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Branches;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Configurations;

// Branch is tenant ERP data (Branch foundation B0/B1), configured exactly like Company: same schema, same
// collation discipline, same tenant-leading index shape, and no foreign key across the plane boundary.
public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
  public void Configure(EntityTypeBuilder<Branch> builder)
  {
    builder.ToTable("Branches", TenantPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_Branches_BranchCode_NotBlank", "LEN(LTRIM(RTRIM([BranchCode]))) > 0");
      table.HasCheckConstraint("CK_Branches_BranchName_NotBlank", "LEN(LTRIM(RTRIM([BranchName]))) > 0");
    });

    builder.HasKey(branch => branch.Id);
    builder.Property(branch => branch.Id).HasColumnName("BranchId").ValueGeneratedNever();
    builder.Ignore(branch => branch.BranchId);

    // Retained in every placement, dedicated included — the global query filter and the write-side tenant
    // guard both key on it (ADR-017 "TenantId retention").
    builder.Property(branch => branch.TenantId).IsRequired();

    builder.Property(branch => branch.BranchCode)
      .HasConversion(code => code.Value, value => BranchCode.Create(value).Value)
      .HasMaxLength(BranchCode.MaximumLength)
      .IsRequired();
    builder.Property(branch => branch.NormalizedBranchCode)
      .HasField("normalizedBranchCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(BranchCode.MaximumLength)
      .UseCollation(TenantPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(branch => branch.BranchName)
      .HasConversion(name => name.Value, value => BranchName.Create(value).Value)
      .HasMaxLength(BranchName.MaximumLength)
      .IsRequired();

    builder.Property(branch => branch.IsMainBranch).IsRequired();
    builder.Property(branch => branch.IsActive).IsRequired();
    builder.Property(branch => branch.CreatedUtc).IsRequired();
    builder.Property(branch => branch.CreatedBy).HasMaxLength(Branch.ActorMaximumLength);
    builder.Property(branch => branch.ModifiedUtc).IsRequired();
    builder.Property(branch => branch.ModifiedBy).HasMaxLength(Branch.ActorMaximumLength);
    builder.Property(branch => branch.RowVersion).IsRowVersion().IsConcurrencyToken();

    // ---- CODE UNIQUENESS, PER TENANT AND UNFILTERED.
    //
    // It covers INACTIVE branches too. A deactivated branch keeps its code because business documents and
    // platform access rows still name it; letting a new branch reuse that code would make the same code
    // mean two different places across the tenant's history.
    builder.HasIndex(branch => new { branch.TenantId, branch.NormalizedBranchCode })
      .IsUnique()
      .HasDatabaseName("UX_Branches_TenantId_NormalizedBranchCode");

    // ---- AT MOST ONE MAIN BRANCH PER TENANT, expressed as a FILTERED UNIQUE INDEX rather than a trigger.
    //
    // The invariant is row-local plus a per-tenant uniqueness, which is precisely what a filtered unique
    // index states; a trigger would be a procedural restatement of something the index enforces natively
    // and would have to be re-argued for multi-row DML.
    //
    // THE FILTER INCLUDES IsActive, and that is the deliberate choice: the invariant is "at most one ACTIVE
    // main branch". A deactivated branch that was once main keeps IsMainBranch as a historical fact, and
    // filtering on the flag alone would make deactivating a main branch and promoting a replacement
    // impossible without first rewriting history.
    builder.HasIndex(branch => branch.TenantId)
      .IsUnique()
      .HasFilter("[IsMainBranch] = 1 AND [IsActive] = 1")
      .HasDatabaseName("UX_Branches_TenantId_MainBranch");

    // The login and branch-picker read: active branches for a tenant, named. TenantId leads so the retained
    // global filter stays a seek.
    builder.HasIndex(branch => new { branch.TenantId, branch.IsActive, branch.BranchName })
      .HasDatabaseName("IX_Branches_TenantId_IsActive_BranchName");

    // NO foreign key to Tenant, for the reason Company has none: Tenant lives in the platform database and
    // this table lives in a tenant ERP database, so the constraint would cross catalogs.
  }
}
