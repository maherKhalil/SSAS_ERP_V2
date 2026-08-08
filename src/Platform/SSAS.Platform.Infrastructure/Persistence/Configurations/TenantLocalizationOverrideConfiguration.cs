using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantLocalizationOverrideConfiguration : IEntityTypeConfiguration<TenantLocalizationOverride>
{
  public void Configure(EntityTypeBuilder<TenantLocalizationOverride> builder)
  {
    builder.ToTable("TenantLocalizationOverrides", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_TenantLocalizationOverrides_Culture", "[Culture] IN ('en', 'ar')");
      table.HasCheckConstraint("CK_TenantLocalizationOverrides_Format", "[TextFormat] IN ('PlainText', 'MultilineText')");
      table.HasCheckConstraint("CK_TenantLocalizationOverrides_Versions", "[CurrentVersionNumber] > 0 AND [CatalogVersion] > 0 AND [ResourceVersion] > 0");
      table.HasCheckConstraint(
        "CK_TenantLocalizationOverrides_Value",
        "([IsActive] = 0 AND [CurrentPlainTextValue] IS NULL AND [CurrentMultilineTextValue] IS NULL) OR " +
        "([IsActive] = 1 AND (([TextFormat] = 'PlainText' AND [CurrentPlainTextValue] IS NOT NULL AND [CurrentMultilineTextValue] IS NULL) OR " +
        "([TextFormat] = 'MultilineText' AND [CurrentPlainTextValue] IS NULL AND [CurrentMultilineTextValue] IS NOT NULL)))");
    });
    builder.HasKey(localizationOverride => localizationOverride.Id);
    builder.Property(localizationOverride => localizationOverride.Id)
      .HasColumnName("TenantLocalizationOverrideId")
      .ValueGeneratedNever();
    builder.Property(localizationOverride => localizationOverride.TenantId).IsRequired();
    builder.Property(localizationOverride => localizationOverride.ResourceKey)
      .HasConversion(value => value.Value, value => ResourceKey.Create(value).Value)
      .HasMaxLength(ResourceKey.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.Culture)
      .HasConversion(value => value.Value, value => LocalizationCulture.Create(value).Value)
      .HasColumnType("varchar(2)")
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.TextFormat)
      .HasConversion<string>()
      .HasColumnType("varchar(24)")
      .HasMaxLength(24)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.CurrentPlainTextValue).HasMaxLength(LocalizationText.PlainTextMaximumLength);
    builder.Property(localizationOverride => localizationOverride.CurrentMultilineTextValue).HasMaxLength(LocalizationText.MultilineTextMaximumLength);
    builder.Ignore(localizationOverride => localizationOverride.CurrentValue);
    builder.Property(localizationOverride => localizationOverride.IsActive).IsRequired();
    builder.Property(localizationOverride => localizationOverride.CurrentVersionNumber)
      .HasConversion(value => value.Value, value => TenantOverrideVersion.Create(value).Value)
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.CatalogVersion)
      .HasConversion(value => value.Value, value => CatalogVersion.Create(value).Value)
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.ResourceVersion)
      .HasConversion(value => value.Value, value => ResourceVersion.Create(value).Value)
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.PlaceholderFingerprint)
      .HasConversion(value => value.Bytes, value => PlaceholderFingerprint.FromBytes(value).Value)
      .HasColumnType("binary(32)")
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.CompatibilityFingerprint)
      .HasConversion(value => value.Bytes, value => CompatibilityFingerprint.FromBytes(value).Value)
      .HasColumnType("binary(32)")
      .IsRequired();
    builder.Property(localizationOverride => localizationOverride.CreatedUtc).IsRequired();
    builder.Property(localizationOverride => localizationOverride.ModifiedUtc).IsRequired();
    builder.Property(localizationOverride => localizationOverride.CreatedBy).HasMaxLength(TenantLocalizationOverride.ActorMaximumLength);
    builder.Property(localizationOverride => localizationOverride.ModifiedBy).HasMaxLength(TenantLocalizationOverride.ActorMaximumLength);
    builder.Property(localizationOverride => localizationOverride.RowVersion).IsRowVersion().IsConcurrencyToken();
    builder.HasIndex(localizationOverride => new { localizationOverride.TenantId, localizationOverride.ResourceKey, localizationOverride.Culture })
      .IsUnique()
      .HasDatabaseName("UX_TenantLocalizationOverrides_Tenant_Resource_Culture");
    builder.HasIndex(localizationOverride => new { localizationOverride.TenantId, localizationOverride.Culture, localizationOverride.ResourceKey })
      .HasDatabaseName("IX_TenantLocalizationOverrides_Tenant_Culture_Resource");
    builder.HasAlternateKey(localizationOverride => new
    {
      localizationOverride.TenantId,
      localizationOverride.Id,
      localizationOverride.ResourceKey,
      localizationOverride.Culture
    });
    builder.HasOne<Tenant>().WithMany().HasForeignKey(localizationOverride => localizationOverride.TenantId).OnDelete(DeleteBehavior.Restrict);
    builder.HasMany(localizationOverride => localizationOverride.Versions)
      .WithOne()
      .HasForeignKey(version => new
      {
        version.TenantId,
        version.TenantLocalizationOverrideId,
        version.ResourceKey,
        version.Culture
      })
      .HasPrincipalKey(localizationOverride => new
      {
        localizationOverride.TenantId,
        localizationOverride.Id,
        localizationOverride.ResourceKey,
        localizationOverride.Culture
      })
      .OnDelete(DeleteBehavior.Restrict);
    builder.Navigation(localizationOverride => localizationOverride.Versions).HasField("versions");

    builder.Property(localizationOverride => localizationOverride.TenantId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    builder.Property(localizationOverride => localizationOverride.ResourceKey).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    builder.Property(localizationOverride => localizationOverride.Culture).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
  }
}
