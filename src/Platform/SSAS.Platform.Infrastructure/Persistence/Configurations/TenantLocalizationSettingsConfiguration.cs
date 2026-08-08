using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Domain.Localization;
using SSAS.Platform.Domain.Tenants;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantLocalizationSettingsConfiguration : IEntityTypeConfiguration<TenantLocalizationSettings>
{
  public void Configure(EntityTypeBuilder<TenantLocalizationSettings> builder)
  {
    builder.ToTable("TenantLocalizationSettings", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_TenantLocalizationSettings_Culture", "[TenantDefaultCulture] IN ('en', 'ar')");
      table.HasCheckConstraint("CK_TenantLocalizationSettings_Version", "[TenantLocalizationVersion] > 0");
    });
    builder.Ignore(settings => settings.Id);
    builder.HasKey(settings => settings.TenantId);
    builder.Property(settings => settings.TenantId).ValueGeneratedNever();
    builder.Property(settings => settings.TenantDefaultCulture)
      .HasConversion(value => value.Value, value => LocalizationCulture.Create(value).Value)
      .HasColumnType("varchar(2)")
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(settings => settings.TenantLocalizationVersion)
      .HasConversion(value => value.Value, value => TenantLocalizationVersion.Create(value).Value)
      .IsRequired();
    builder.Property(settings => settings.CreatedUtc).IsRequired();
    builder.Property(settings => settings.ModifiedUtc).IsRequired();
    builder.Property(settings => settings.CreatedBy).HasMaxLength(TenantLocalizationOverride.ActorMaximumLength);
    builder.Property(settings => settings.ModifiedBy).HasMaxLength(TenantLocalizationOverride.ActorMaximumLength);
    builder.Property(settings => settings.RowVersion).IsRowVersion().IsConcurrencyToken();
    builder.HasOne<Tenant>().WithOne().HasForeignKey<TenantLocalizationSettings>(settings => settings.TenantId).OnDelete(DeleteBehavior.Restrict);
  }
}
