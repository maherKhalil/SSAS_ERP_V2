using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
  public void Configure(EntityTypeBuilder<Tenant> builder)
  {
    builder.ToTable("Tenants", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_Tenants_Status",
        "[Status] IN (N'Provisioning', N'Active', N'Suspended', N'Archived')");
      table.HasCheckConstraint(
        "CK_Tenants_StatusChangeReasonCode",
        "[StatusChangeReasonCode] IN (N'Created', N'ProvisioningCompleted', N'Administrative', N'Security', N'Compliance', N'Operational', N'CustomerClosure', N'IssueResolved')");
      table.HasCheckConstraint("CK_Tenants_TenantCode_NotBlank", "LEN(LTRIM(RTRIM([TenantCode]))) > 0");
      table.HasCheckConstraint("CK_Tenants_TenantName_NotBlank", "LEN(LTRIM(RTRIM([TenantName]))) > 0");
      table.HasCheckConstraint("CK_Tenants_StatusMetadata", "[StatusChangedUtc] >= [CreatedUtc]");
    });

    builder.HasKey(tenant => tenant.Id);
    builder.Property(tenant => tenant.Id).HasColumnName("TenantId").ValueGeneratedNever();
    builder.Ignore(tenant => tenant.TenantId);
    builder.Property(tenant => tenant.TenantCode)
      .HasConversion(code => code.Value, value => TenantCode.Create(value).Value)
      .HasMaxLength(TenantCode.MaximumLength)
      .IsRequired();
    builder.Property(tenant => tenant.NormalizedTenantCode)
      .HasField("normalizedTenantCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(TenantCode.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(tenant => tenant.TenantName)
      .HasConversion(name => name.Value, value => TenantName.Create(value).Value)
      .HasMaxLength(TenantName.MaximumLength)
      .IsRequired();
    builder.Property(tenant => tenant.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(tenant => tenant.CreatedUtc).IsRequired();
    builder.Property(tenant => tenant.CreatedBy).HasMaxLength(Tenant.ActorMaximumLength).IsRequired();
    builder.Property(tenant => tenant.ModifiedUtc);
    builder.Property(tenant => tenant.ModifiedBy).HasMaxLength(Tenant.ActorMaximumLength);
    builder.Property(tenant => tenant.StatusChangedUtc).IsRequired();
    builder.Property(tenant => tenant.StatusChangedBy).HasMaxLength(Tenant.ActorMaximumLength).IsRequired();
    builder.Property(tenant => tenant.StatusChangeReasonCode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(tenant => tenant.RowVersion).IsRowVersion().IsConcurrencyToken();
    builder.HasIndex(tenant => tenant.NormalizedTenantCode)
      .IsUnique()
      .HasDatabaseName("UX_Tenants_NormalizedTenantCode");
    builder.HasIndex(tenant => new { tenant.Status, tenant.TenantName, tenant.Id })
      .HasDatabaseName("IX_Tenants_Status_TenantName_TenantId");
    builder.Ignore(tenant => tenant.IsAuthenticationEligible);
  }
}
