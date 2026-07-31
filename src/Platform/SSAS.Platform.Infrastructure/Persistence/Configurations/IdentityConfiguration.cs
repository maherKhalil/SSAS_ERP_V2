using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.ValueObjects;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class IdentityConfiguration : IEntityTypeConfiguration<PlatformIdentity>
{
  public void Configure(EntityTypeBuilder<PlatformIdentity> builder)
  {
    builder.ToTable("Identities", PlatformPersistenceConstants.Schema);
    builder.HasKey(identity => identity.Id);
    builder.Property(identity => identity.Id).HasColumnName("IdentityId").UseIdentityColumn();
    builder.Property(identity => identity.Subject)
      .HasConversion(subject => subject.Value, value => AuthenticationSubject.Create(value).Value)
      .HasMaxLength(256)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.HasIndex(identity => identity.Subject).IsUnique();
    ConfigureAudit(builder);
    builder.Property(identity => identity.RowVersion).IsRowVersion().IsConcurrencyToken();
  }

  private static void ConfigureAudit(EntityTypeBuilder<PlatformIdentity> builder)
  {
    builder.Property(identity => identity.CreatedUtc).IsRequired();
    builder.Property(identity => identity.CreatedBy).HasMaxLength(256);
    builder.Property(identity => identity.ModifiedUtc).IsRequired();
    builder.Property(identity => identity.ModifiedBy).HasMaxLength(256);
  }
}
