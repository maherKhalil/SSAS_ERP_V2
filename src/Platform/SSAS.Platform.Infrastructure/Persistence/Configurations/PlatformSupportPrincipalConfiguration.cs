using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.PlatformSupport;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class PlatformSupportPrincipalConfiguration : IEntityTypeConfiguration<PlatformSupportPrincipal>
{
  public void Configure(EntityTypeBuilder<PlatformSupportPrincipal> builder)
  {
    builder.ToTable("PlatformSupportPrincipals", PlatformPersistenceConstants.Schema);
    builder.HasKey(principal => principal.Id);
    builder.Property(principal => principal.Id).HasColumnName("PlatformSupportPrincipalId").UseIdentityColumn();
    builder.Property(principal => principal.IdentityId).IsRequired();

    // One platform-support authority record per global Identity (ADR-015: anchored to Identity).
    builder.HasIndex(principal => principal.IdentityId)
      .IsUnique()
      .HasDatabaseName("UX_PlatformSupportPrincipals_IdentityId");
    builder.HasOne<PlatformIdentity>()
      .WithMany()
      .HasForeignKey(principal => principal.IdentityId)
      .HasPrincipalKey(identity => identity.Id)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Ignore(principal => principal.ActivePermissions);
    builder.Navigation(principal => principal.PermissionAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);

    builder.Property(principal => principal.CreatedUtc).IsRequired();
    builder.Property(principal => principal.CreatedBy).HasMaxLength(256);
    builder.Property(principal => principal.ModifiedUtc).IsRequired();
    builder.Property(principal => principal.ModifiedBy).HasMaxLength(256);
    builder.Property(principal => principal.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
