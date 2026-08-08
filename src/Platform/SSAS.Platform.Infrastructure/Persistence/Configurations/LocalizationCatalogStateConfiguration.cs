using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.BuildingBlocks.Localization;
using SSAS.Platform.Domain.Localization;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class LocalizationCatalogStateConfiguration : IEntityTypeConfiguration<LocalizationCatalogState>
{
  public void Configure(EntityTypeBuilder<LocalizationCatalogState> builder)
  {
    builder.ToTable("LocalizationCatalogStates", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_LocalizationCatalogStates_Singleton", "[LocalizationCatalogStateId] = 1");
      table.HasCheckConstraint("CK_LocalizationCatalogStates_Versions", "[CatalogSchemaVersion] > 0 AND [HighestActivatedCatalogVersion] > 0");
    });
    builder.HasKey(state => state.Id);
    builder.Property(state => state.Id).HasColumnName("LocalizationCatalogStateId").ValueGeneratedNever();
    builder.Property(state => state.CatalogSchemaVersion)
      .HasConversion(value => value.Value, value => CatalogSchemaVersion.Create(value).Value)
      .IsRequired();
    builder.Property(state => state.HighestActivatedCatalogVersion)
      .HasConversion(value => value.Value, value => CatalogVersion.Create(value).Value)
      .IsRequired();
    builder.Property(state => state.CreatedUtc).IsRequired();
    builder.Property(state => state.ModifiedUtc).IsRequired();
    builder.Property(state => state.CreatedBy).HasMaxLength(TenantLocalizationOverride.ActorMaximumLength);
    builder.Property(state => state.ModifiedBy).HasMaxLength(TenantLocalizationOverride.ActorMaximumLength);
    builder.Property(state => state.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
