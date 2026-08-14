using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.TenantErp.Configurations;

// Company is the pilot tenant ERP entity (ADR-017). Moved from the platform schema to the tenant schema
// and from PlatformDbContext to TenantDbContext.
//
// TWO THINGS CHANGED, AND ONLY TWO: the schema, and the removal of the physical foreign key to Tenant.
// Every column, conversion, collation, check constraint and index is preserved exactly, because this is
// an ownership move rather than a redesign — anything else would make the moved data unverifiable
// against the rows it replaces.
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
  public void Configure(EntityTypeBuilder<Company> builder)
  {
    builder.ToTable("Companies", TenantPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_Companies_Status",
        "[Status] IN (N'Inactive', N'Active', N'Archived')");
      table.HasCheckConstraint(
        "CK_Companies_StatusChangeReasonCode",
        "[StatusChangeReasonCode] IN (N'Created', N'Administrative', N'Operational', N'Compliance', N'CustomerRequest', N'IssueResolved')");
      table.HasCheckConstraint("CK_Companies_CompanyCode_NotBlank", "LEN(LTRIM(RTRIM([CompanyCode]))) > 0");
      table.HasCheckConstraint("CK_Companies_CompanyName_NotBlank", "LEN(LTRIM(RTRIM([CompanyName]))) > 0");
      // BaseCurrencyCode is a stored, already-uppercased ISO-4217 alphabetic code. The binary-collated
      // column makes this LIKE a case-sensitive backstop against raw-SQL writes; full ISO-4217 set
      // membership is enforced in Domain/Application, not by the database.
      table.HasCheckConstraint("CK_Companies_BaseCurrencyCode", "[BaseCurrencyCode] LIKE '[A-Z][A-Z][A-Z]'");
    });

    builder.HasKey(company => company.Id);
    builder.Property(company => company.Id).HasColumnName("CompanyId").ValueGeneratedNever();
    builder.Ignore(company => company.CompanyId);

    // TenantId is RETAINED in every placement, including a dedicated database that holds exactly one
    // tenant (ADR-017 "TenantId retention"). It is what the global query filter and the write-side tenant
    // guard both key on, and dropping it would make the database unmovable back to shared storage.
    builder.Property(company => company.TenantId).IsRequired();

    builder.Property(company => company.CompanyCode)
      .HasConversion(code => code.Value, value => CompanyCode.Create(value).Value)
      .HasMaxLength(CompanyCode.MaximumLength)
      .IsRequired();
    builder.Property(company => company.NormalizedCompanyCode)
      .HasField("normalizedCompanyCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(CompanyCode.MaximumLength)
      .UseCollation(TenantPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.CompanyName)
      .HasConversion(name => name.Value, value => CompanyName.Create(value).Value)
      .HasMaxLength(CompanyName.MaximumLength)
      .IsRequired();
    builder.Property(company => company.BaseCurrencyCode)
      .HasConversion(currency => currency.Value, value => BaseCurrencyCode.Create(value).Value)
      .HasColumnType("char")
      .HasMaxLength(BaseCurrencyCode.RequiredLength)
      .IsFixedLength()
      .UseCollation(TenantPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(TenantPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.StatusChangeReasonCode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(TenantPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.StatusChangedUtc).IsRequired();
    builder.Property(company => company.StatusChangedBy).HasMaxLength(Company.ActorMaximumLength).IsRequired();
    builder.Property(company => company.CreatedUtc).IsRequired();
    builder.Property(company => company.CreatedBy).HasMaxLength(Company.ActorMaximumLength);
    builder.Property(company => company.ModifiedUtc).IsRequired();
    builder.Property(company => company.ModifiedBy).HasMaxLength(Company.ActorMaximumLength);
    builder.Property(company => company.RowVersion).IsRowVersion().IsConcurrencyToken();

    // Tenant-scoped uniqueness is binding in EVERY placement, dedicated included (ADR-017). A bare
    // NormalizedCompanyCode constraint would work in a dedicated database and break the instant the same
    // schema were applied to shared storage — which is exactly what makes one portable schema possible.
    builder.HasIndex(company => new { company.TenantId, company.NormalizedCompanyCode })
      .IsUnique()
      .HasDatabaseName("UX_Companies_TenantId_NormalizedCompanyCode");

    // TenantId leads the index so the retained global filter stays a seek rather than a scan, in shared
    // and dedicated databases alike.
    builder.HasIndex(company => new { company.TenantId, company.Status, company.CompanyName, company.Id })
      .HasDatabaseName("IX_Companies_TenantId_Status_CompanyName_CompanyId");

    // NO foreign key to Tenant. Tenant lives in the Platform database and Company now lives in a tenant
    // ERP database, so the constraint would be a cross-database foreign key — prohibited by ADR-017, and
    // impossible once the two are separate catalogs. TenantId remains a trusted identifier, enforced by
    // the global query filter, the write-side tenant guard, and validation at creation. Commit ordering
    // (ADR-017) guarantees a tenant is routable only while it exists.
  }
}
