using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantDatabaseConfiguration : IEntityTypeConfiguration<TenantDatabase>
{
  public void Configure(EntityTypeBuilder<TenantDatabase> builder)
  {
    builder.ToTable("TenantDatabases", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantDatabases_HostingMode",
        "[HostingMode] IN (N'PlatformManaged', N'CustomerManaged')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_StorageMode",
        "[StorageMode] IN (N'Shared', N'Dedicated')");
      table.HasCheckConstraint(
        "CK_TenantDatabases_ProvisioningStatus",
        "[ProvisioningStatus] IN (N'Registered', N'Provisioning', N'Onboarding', N'Ready', N'Disabled')");
      // ADR-017: CustomerManaged implies Dedicated. The one invalid combination is rejected by the
      // database, so no application path and no direct SQL write can create it.
      table.HasCheckConstraint(
        "CK_TenantDatabases_CustomerManagedIsDedicated",
        "NOT ([HostingMode] = N'CustomerManaged' AND [StorageMode] = N'Shared')");
      table.HasCheckConstraint("CK_TenantDatabases_ServerKey_NotBlank", "LEN(LTRIM(RTRIM([ServerKey]))) > 0");
      table.HasCheckConstraint("CK_TenantDatabases_DatabaseName_NotBlank", "LEN(LTRIM(RTRIM([DatabaseName]))) > 0");
    });

    builder.HasKey(database => database.Id);
    builder.Property(database => database.Id).HasColumnName("TenantDatabaseId").UseIdentityColumn();

    builder.Property(database => database.HostingMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.StorageMode)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.ProvisioningStatus)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // Trusted routing metadata only: a configuration lookup key and a catalog name. No host, no port, no
    // credential and no connection string is persisted for any hosting mode.
    builder.Property(database => database.ServerKey)
      .HasMaxLength(TenantDatabase.ServerKeyMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(database => database.DatabaseName)
      .HasMaxLength(TenantDatabase.DatabaseNameMaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    // (ServerKey, DatabaseName) is the physical identity of an endpoint, so it also makes the bootstrap
    // idempotent under concurrent hosts: a losing racer hits this index rather than creating a duplicate.
    builder.HasIndex(database => new { database.ServerKey, database.DatabaseName })
      .IsUnique()
      .HasDatabaseName("UX_TenantDatabases_ServerKey_DatabaseName");

    builder.Property(database => database.CreatedUtc).IsRequired();
    builder.Property(database => database.CreatedBy).HasMaxLength(TenantDatabase.ActorMaximumLength);
    builder.Property(database => database.ModifiedUtc).IsRequired();
    builder.Property(database => database.ModifiedBy).HasMaxLength(TenantDatabase.ActorMaximumLength);
    builder.Property(database => database.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
