using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Companies;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
  public void Configure(EntityTypeBuilder<Company> builder)
  {
    builder.ToTable("Companies", PlatformPersistenceConstants.Schema, table =>
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
    builder.Property(company => company.TenantId).IsRequired();
    builder.Property(company => company.CompanyCode)
      .HasConversion(code => code.Value, value => CompanyCode.Create(value).Value)
      .HasMaxLength(CompanyCode.MaximumLength)
      .IsRequired();
    builder.Property(company => company.NormalizedCompanyCode)
      .HasField("normalizedCompanyCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(CompanyCode.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
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
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.StatusChangeReasonCode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(company => company.StatusChangedUtc).IsRequired();
    builder.Property(company => company.StatusChangedBy).HasMaxLength(Company.ActorMaximumLength).IsRequired();
    builder.Property(company => company.CreatedUtc).IsRequired();
    builder.Property(company => company.CreatedBy).HasMaxLength(Company.ActorMaximumLength);
    builder.Property(company => company.ModifiedUtc).IsRequired();
    builder.Property(company => company.ModifiedBy).HasMaxLength(Company.ActorMaximumLength);
    builder.Property(company => company.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.HasIndex(company => new { company.TenantId, company.NormalizedCompanyCode })
      .IsUnique()
      .HasDatabaseName("UX_Companies_TenantId_NormalizedCompanyCode");
    builder.HasIndex(company => new { company.TenantId, company.Status, company.CompanyName, company.Id })
      .HasDatabaseName("IX_Companies_TenantId_Status_CompanyName_CompanyId");

    builder.HasOne<Tenant>()
      .WithMany()
      .HasForeignKey(company => company.TenantId)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
