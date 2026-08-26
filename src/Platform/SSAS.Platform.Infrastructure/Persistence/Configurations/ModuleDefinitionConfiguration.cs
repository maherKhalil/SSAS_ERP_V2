using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// THE FOUR GATEABLE MODULES (FP-014, `OD-SUB-0005`).
//
// A surrogate primary key with a UNIQUE ORDINAL INDEX on the natural key -- the disagreement `data-model.md`
// itself sanctions and the shape `domain-model.md` specifies. Nothing that depended on `ModuleKey` being
// unique has lost anything; what changed is that this table looks like every other table in the database.
//
// The collation is ordinal for the same reason `NormalizedTenantCode`'s is: a culture-sensitive match could
// make two distinct module keys equal on one machine and not another, and this key decides entitlement.
public sealed class ModuleDefinitionConfiguration : IEntityTypeConfiguration<ModuleDefinition>
{
  public void Configure(EntityTypeBuilder<ModuleDefinition> builder)
  {
    builder.ToTable("ModuleDefinitions", PlatformPersistenceConstants.Schema, table =>
      table.HasCheckConstraint(
        "CK_ModuleDefinitions_ModuleKey_NotBlank", "LEN(LTRIM(RTRIM([ModuleKey]))) > 0"));

    builder.HasKey(module => module.Id);
    builder.Property(module => module.Id).HasColumnName("ModuleDefinitionId").ValueGeneratedNever();

    builder.Property(module => module.ModuleKey)
      .HasConversion(key => key.Value, value => ModuleKey.Create(value).Value)
      .HasMaxLength(ModuleKey.MaximumLength)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(module => module.DisplayName).HasMaxLength(200).IsRequired();
    builder.Property(module => module.IsGateable).IsRequired();
    builder.Property(module => module.CreatedUtc).IsRequired();
    builder.Property(module => module.CreatedBy).HasMaxLength(256).IsRequired();
    builder.Property(module => module.ModifiedBy).HasMaxLength(256);
    builder.Property(module => module.RowVersion).IsRowVersion();

    builder.HasIndex(module => module.ModuleKey)
      .IsUnique()
      .HasDatabaseName("UX_ModuleDefinitions_ModuleKey");
  }
}
