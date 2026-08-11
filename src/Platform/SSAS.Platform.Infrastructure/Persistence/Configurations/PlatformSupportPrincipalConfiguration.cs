using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.PlatformSupport;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class PlatformSupportPrincipalConfiguration : IEntityTypeConfiguration<PlatformSupportPrincipal>
{
  public void Configure(EntityTypeBuilder<PlatformSupportPrincipal> builder)
  {
    builder.ToTable("PlatformSupportPrincipals", PlatformPersistenceConstants.Schema, table =>
      table.HasCheckConstraint("CK_PlatformSupportPrincipals_Status", "[Status] IN (N'Active', N'Disabled')"));
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

    // Composite alternate key so a PlatformAuthenticationSession can bind (PrincipalId, IdentityId) by FK,
    // structurally guaranteeing the session's identity matches the principal's identity (DEC-TEN-0022).
    builder.HasAlternateKey(principal => new { principal.Id, principal.IdentityId })
      .HasName("AK_PlatformSupportPrincipals_PrincipalIdentity");

    builder.Ignore(principal => principal.ActivePermissions);
    builder.Navigation(principal => principal.PermissionAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);

    // Lifecycle status (ADR-016 / DEC-TEN-0020). Default 'Active' backfills any pre-existing row; status
    // transition metadata stays NULL until the first Disable/Re-enable (registration is not a transition).
    builder.Property(principal => principal.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .HasDefaultValue(PlatformSupportPrincipalStatus.Active)
      .IsRequired();
    builder.Property(principal => principal.StatusChangedUtc);
    builder.Property(principal => principal.StatusChangedBy).HasMaxLength(256);

    builder.Property(principal => principal.CreatedUtc).IsRequired();
    builder.Property(principal => principal.CreatedBy).HasMaxLength(256);
    builder.Property(principal => principal.ModifiedUtc).IsRequired();
    builder.Property(principal => principal.ModifiedBy).HasMaxLength(256);
    builder.Property(principal => principal.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
