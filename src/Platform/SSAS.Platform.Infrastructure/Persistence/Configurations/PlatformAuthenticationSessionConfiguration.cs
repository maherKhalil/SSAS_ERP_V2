using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.PlatformSupport;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// Platform-plane session persistence (ADR-016 Phase 3C / DEC-TEN-0022). Mirrors the tenant
// AuthenticationSession's mature security mechanics, but is non-tenant: NO TenantId/TenantUserId/CompanyId,
// no tenant query filter. It is anchored to Identity and to the PlatformSupportPrincipal (composite FK on
// (PrincipalId, IdentityId) so identity and principal cannot diverge). The tenant AuthenticationSessions
// table is untouched.
public sealed class PlatformAuthenticationSessionConfiguration : IEntityTypeConfiguration<PlatformAuthenticationSession>
{
  public void Configure(EntityTypeBuilder<PlatformAuthenticationSession> builder)
  {
    builder.ToTable("PlatformAuthenticationSessions", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_PlatformAuthenticationSessions_Status", "[Status] IN (N'Active', N'Revoked', N'Compromised')");
      table.HasCheckConstraint(
        "CK_PlatformAuthenticationSessions_RevocationReason",
        "[RevocationReason] IS NULL OR [RevocationReason] IN (N'SessionLimitExceeded', N'SecurityStateChanged', N'IdentityIneligible', N'Administrative', N'UserLogout', N'PlatformPrincipalIneligible')");
      table.HasCheckConstraint(
        "CK_PlatformAuthenticationSessions_LifecycleMetadata",
        "([Status] = N'Active' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR " +
        "([Status] = N'Revoked' AND [RevokedUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR " +
        "([Status] = N'Compromised' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NOT NULL AND [CompromisedByRefreshTokenRecordId] IS NOT NULL)");
      table.HasCheckConstraint("CK_PlatformAuthenticationSessions_Expiry", "[IdleExpiresUtc] > [CreatedUtc] AND [AbsoluteExpiresUtc] > [CreatedUtc] AND [IdleExpiresUtc] <= [AbsoluteExpiresUtc]");
      table.HasCheckConstraint("CK_PlatformAuthenticationSessions_SecurityVersion", "[SecurityVersionAtCreation] > 0");
    });

    builder.HasKey(session => session.Id);
    builder.Property(session => session.Id).HasColumnName("PlatformAuthenticationSessionId").UseIdentityColumn();
    builder.Property(session => session.IdentityId).IsRequired();
    builder.Property(session => session.PlatformSupportPrincipalId).IsRequired();
    builder.Property(session => session.ClientId)
      .HasMaxLength(64)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(session => session.TokenFamilyId).IsRequired();
    builder.Property(session => session.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(session => session.CreatedUtc).IsRequired();
    builder.Property(session => session.LastRefreshedUtc);
    builder.Property(session => session.IdleExpiresUtc).IsRequired();
    builder.Property(session => session.AbsoluteExpiresUtc).IsRequired();
    builder.Property(session => session.SecurityVersionAtCreation).IsRequired();
    builder.Property(session => session.RevokedUtc);
    builder.Property(session => session.RevokedBy).HasMaxLength(256);
    builder.Property(session => session.RevocationReason)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation);
    builder.Property(session => session.CompromisedUtc);
    builder.Property(session => session.CompromisedByRefreshTokenRecordId);
    builder.Property(session => session.ModifiedUtc).IsRequired();
    builder.Property(session => session.CreatedBy).HasMaxLength(256);
    builder.Property(session => session.ModifiedBy).HasMaxLength(256);
    builder.Property(session => session.RowVersion).IsRowVersion().IsConcurrencyToken();

    builder.HasOne<PlatformIdentity>().WithMany()
      .HasForeignKey(session => session.IdentityId)
      .OnDelete(DeleteBehavior.Restrict);
    // Explicit authority binding + structural identity/principal consistency (DEC-TEN-0022).
    builder.HasOne<PlatformSupportPrincipal>().WithMany()
      .HasForeignKey(session => new { session.PlatformSupportPrincipalId, session.IdentityId })
      .HasPrincipalKey(principal => new { principal.Id, principal.IdentityId })
      .OnDelete(DeleteBehavior.Restrict);
    builder.HasAlternateKey(session => new { session.Id, session.TokenFamilyId, session.ClientId })
      .HasName("AK_PlatformAuthenticationSessions_SessionFamilyClient");

    builder.HasIndex(session => new { session.IdentityId, session.Status, session.IdleExpiresUtc, session.AbsoluteExpiresUtc })
      .HasDatabaseName("IX_PlatformAuthenticationSessions_Identity_ActiveExpiry");
    builder.HasIndex(session => new { session.PlatformSupportPrincipalId, session.Status })
      .HasDatabaseName("IX_PlatformAuthenticationSessions_Principal_Status");
    builder.Navigation(session => session.RefreshTokenRecords).UsePropertyAccessMode(PropertyAccessMode.Field);

    foreach (var propertyName in new[]
    {
      nameof(PlatformAuthenticationSession.IdentityId),
      nameof(PlatformAuthenticationSession.PlatformSupportPrincipalId),
      nameof(PlatformAuthenticationSession.ClientId),
      nameof(PlatformAuthenticationSession.TokenFamilyId),
      nameof(PlatformAuthenticationSession.CreatedUtc),
      nameof(PlatformAuthenticationSession.AbsoluteExpiresUtc),
      nameof(PlatformAuthenticationSession.SecurityVersionAtCreation)
    })
    {
      builder.Property(propertyName).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
  }
}
