using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantLocalizationOverrideVersionConfiguration : IEntityTypeConfiguration<TenantLocalizationOverrideVersion>
{
  public void Configure(EntityTypeBuilder<TenantLocalizationOverrideVersion> builder)
  {
    builder.ToTable("TenantLocalizationOverrideVersions", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_TenantLocalizationOverrideVersions_Culture", "[Culture] IN ('en', 'ar')");
      table.HasCheckConstraint("CK_TenantLocalizationOverrideVersions_Format", "[TextFormat] IN ('PlainText', 'MultilineText')");
      table.HasCheckConstraint("CK_TenantLocalizationOverrideVersions_ChangeType", "[ChangeType] IN ('Created', 'Updated', 'Undone', 'RestoredDefault')");
      table.HasCheckConstraint("CK_TenantLocalizationOverrideVersions_Versions", "[VersionNumber] > 0 AND [CatalogVersion] > 0 AND [ResourceVersion] > 0");
      table.HasCheckConstraint(
        "CK_TenantLocalizationOverrideVersions_Value",
        "([IsActive] = 0 AND [ChangeType] IN ('Undone', 'RestoredDefault') AND [PlainTextValue] IS NULL AND [MultilineTextValue] IS NULL) OR " +
        "([IsActive] = 1 AND [ChangeType] IN ('Created', 'Updated', 'Undone') AND " +
        "(([TextFormat] = 'PlainText' AND [PlainTextValue] IS NOT NULL AND [MultilineTextValue] IS NULL) OR " +
        "([TextFormat] = 'MultilineText' AND [PlainTextValue] IS NULL AND [MultilineTextValue] IS NOT NULL)))");
      table.HasCheckConstraint(
        "CK_TenantLocalizationOverrideVersions_Lineage",
        "([ChangeType] = 'Created' AND [PriorLogicalVersionNumber] IS NULL AND [UndoTargetVersionNumber] IS NULL) OR " +
        "([ChangeType] IN ('Updated', 'RestoredDefault') AND [PriorLogicalVersionNumber] IS NOT NULL AND [UndoTargetVersionNumber] IS NULL) OR " +
        "([ChangeType] = 'Undone' AND [UndoTargetVersionNumber] IS NOT NULL)");
    });
    builder.HasKey(version => version.Id);
    builder.Property(version => version.Id)
      .HasColumnName("TenantLocalizationOverrideVersionId")
      .ValueGeneratedNever();
    builder.Property(version => version.TenantLocalizationOverrideId).IsRequired();
    builder.Property(version => version.TenantId).IsRequired();
    builder.Property(version => version.ResourceKey)
      .HasConversion(value => value.Value, value => ResourceKey.Create(value).Value)
      .HasMaxLength(ResourceKey.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(version => version.Culture)
      .HasConversion(value => value.Value, value => LocalizationCulture.Create(value).Value)
      .HasColumnType("varchar(2)")
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(version => version.VersionNumber)
      .HasConversion(value => value.Value, value => TenantOverrideVersion.Create(value).Value)
      .IsRequired();
    builder.Property(version => version.TextFormat)
      .HasConversion<string>()
      .HasColumnType("varchar(24)")
      .HasMaxLength(24)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(version => version.PlainTextValue).HasMaxLength(LocalizationText.PlainTextMaximumLength);
    builder.Property(version => version.MultilineTextValue).HasMaxLength(LocalizationText.MultilineTextMaximumLength);
    builder.Ignore(version => version.Value);
    builder.Property(version => version.IsActive).IsRequired();
    builder.Property(version => version.ChangeType)
      .HasConversion<string>()
      .HasColumnType("varchar(32)")
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(version => version.PriorLogicalVersionNumber)
      .HasConversion(
        value => value.HasValue ? value.Value.Value : (long?)null,
        value => value.HasValue ? TenantOverrideVersion.Create(value.Value).Value : null);
    builder.Property(version => version.UndoTargetVersionNumber)
      .HasConversion(
        value => value.HasValue ? value.Value.Value : (long?)null,
        value => value.HasValue ? TenantOverrideVersion.Create(value.Value).Value : null);
    builder.Property(version => version.CatalogVersion)
      .HasConversion(value => value.Value, value => CatalogVersion.Create(value).Value)
      .IsRequired();
    builder.Property(version => version.ResourceVersion)
      .HasConversion(value => value.Value, value => ResourceVersion.Create(value).Value)
      .IsRequired();
    builder.Property(version => version.PlaceholderFingerprint)
      .HasConversion(value => value.Bytes, value => PlaceholderFingerprint.FromBytes(value).Value)
      .HasColumnType("binary(32)")
      .IsRequired();
    builder.Property(version => version.CompatibilityFingerprint)
      .HasConversion(value => value.Bytes, value => CompatibilityFingerprint.FromBytes(value).Value)
      .HasColumnType("binary(32)")
      .IsRequired();
    builder.Property(version => version.ActorId)
      .HasMaxLength(TenantLocalizationOverride.ActorMaximumLength)
      .IsRequired();
    builder.Property(version => version.OccurredUtc).IsRequired();
    builder.HasIndex(version => new { version.TenantLocalizationOverrideId, version.VersionNumber })
      .IsUnique()
      .HasDatabaseName("UX_TenantLocalizationOverrideVersions_Override_Version");
    builder.HasIndex(version => new { version.TenantId, version.ResourceKey, version.Culture, version.VersionNumber })
      .HasDatabaseName("IX_TenantLocalizationOverrideVersions_Tenant_Resource_Culture_Version");
  }
}
