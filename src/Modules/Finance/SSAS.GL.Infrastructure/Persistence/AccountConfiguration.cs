using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.GL.Domain.Accounts;

namespace SSAS.GL.Infrastructure.Persistence;

// THE CHART OF ACCOUNTS (REQ-GL-0005..0008, OD-GL-0003).
//
// ================================================================================================
// THERE IS NO CompanyId COLUMN, AND THAT IS THE RULING RATHER THAN AN OMISSION.
// ================================================================================================
//
// `OD-GL-0003` ruled the chart TENANT-level. `Account` is `ITenantOwnedEntity` and deliberately NOT
// `ICompanyOwnedEntity`, so there is no company column and no company foreign key — and, more importantly,
// no `AuthorizeCurrentCompanyAsync` on the write path. An architecture guard asserts the column's absence
// from the composed model, so a future convention or shadow property cannot add one silently and quietly
// turn account maintenance into a company-scoped write.
//
// Every string column is `nvarchar` (`DEC-GL-0006`): `Constraints.md` requires Arabic and English, and an
// account name is exactly the field a user writes in their own language.
public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
  public void Configure(EntityTypeBuilder<Account> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("GlAccounts", GlPersistenceConstants.TenantSchema);

    builder.HasKey(account => account.Id);

    builder.Property(account => account.TenantId).IsRequired();

    // The display value, casing preserved. Value-converted, so it is projectable but NOT usable in a
    // predicate — which is what the normalized shadows below are for (`DEC-POS-0030`).
    builder.Property(account => account.Code)
      .HasConversion(code => code.Value, value => AccountCode.Create(value).Value)
      .HasMaxLength(AccountCode.MaximumLength)
      .IsRequired();

    builder.Property(account => account.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(AccountCode.MaximumLength)
      .UseCollation(GlPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(account => account.Name)
      .HasConversion(name => name.Value, value => AccountName.Create(value).Value)
      .HasMaxLength(AccountName.MaximumLength)
      .IsRequired();

    // The search column. No index: a leading-wildcard LIKE cannot seek, so an index would be storage and
    // write cost buying nothing. Same reasoning as HR's department and position search columns.
    builder.Property(account => account.NormalizedName)
      .HasField("normalizedName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(AccountName.MaximumLength)
      .UseCollation(GlPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(account => account.IsActive).IsRequired();

    builder.Property(account => account.CreatedUtc).IsRequired();
    builder.Property(account => account.CreatedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);
    builder.Property(account => account.ModifiedUtc).IsRequired();
    builder.Property(account => account.ModifiedBy).HasMaxLength(GlPersistenceConstants.ActorMaximumLength);

    builder.Property(account => account.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.Ignore(account => account.DomainEvents);

    // ---- UNIQUE WITHIN THE TENANT, NOT WITHIN A COMPANY.
    //
    // The direct consequence of the tenant-level ruling: two companies in one tenant read ONE chart, so they
    // cannot each own a `4100`. Contrast `DepartmentCode`, which is unique within a company because
    // departments are company-owned. The difference is the ownership decision, visible here as a shorter key.
    builder.HasIndex(account => new { account.TenantId, account.NormalizedCode })
      .IsUnique()
      .HasDatabaseName("UX_GlAccounts_Tenant_NormalizedCode");
  }
}
