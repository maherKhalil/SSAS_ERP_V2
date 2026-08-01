using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.TenantUsers;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class AuthenticationSessionConfiguration : IEntityTypeConfiguration<AuthenticationSession>
{
  public void Configure(EntityTypeBuilder<AuthenticationSession> builder)
  {
    builder.ToTable("AuthenticationSessions", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_AuthenticationSessions_Status", "[Status] IN (N'Active', N'Revoked', N'Compromised')");
      table.HasCheckConstraint(
        "CK_AuthenticationSessions_RevocationReason",
        "[RevocationReason] IS NULL OR [RevocationReason] IN (N'SessionLimitExceeded', N'PasswordReset', N'SecurityStateChanged', N'IdentityIneligible', N'MembershipIneligible', N'TenantIneligible', N'Administrative', N'UserLogout')");
      table.HasCheckConstraint(
        "CK_AuthenticationSessions_LifecycleMetadata",
        "([Status] = N'Active' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR " +
        "([Status] = N'Revoked' AND [RevokedUtc] IS NOT NULL AND [RevocationReason] IS NOT NULL AND [CompromisedUtc] IS NULL AND [CompromisedByRefreshTokenRecordId] IS NULL) OR " +
        "([Status] = N'Compromised' AND [RevokedUtc] IS NULL AND [RevocationReason] IS NULL AND [CompromisedUtc] IS NOT NULL AND [CompromisedByRefreshTokenRecordId] IS NOT NULL)");
      table.HasCheckConstraint("CK_AuthenticationSessions_Expiry", "[IdleExpiresUtc] > [CreatedUtc] AND [AbsoluteExpiresUtc] > [CreatedUtc] AND [IdleExpiresUtc] <= [AbsoluteExpiresUtc]");
    });

    builder.HasKey(session => session.Id);
    builder.Property(session => session.Id).HasColumnName("AuthenticationSessionId").UseIdentityColumn();
    builder.Property(session => session.IdentityId).IsRequired();
    builder.Property(session => session.TenantUserId).IsRequired();
    builder.Property(session => session.TenantId).IsRequired();
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

    builder.HasOne<PlatformIdentity>().WithMany().HasForeignKey(session => session.IdentityId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Tenant>().WithMany().HasForeignKey(session => session.TenantId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TenantUser>().WithMany()
      .HasForeignKey(session => new { session.TenantId, session.TenantUserId })
      .HasPrincipalKey(user => new { user.TenantId, TenantUserId = user.Id })
      .OnDelete(DeleteBehavior.Restrict);
    builder.HasAlternateKey(session => new { session.Id, session.TokenFamilyId, session.ClientId })
      .HasName("AK_AuthenticationSessions_SessionFamilyClient");
    builder.HasIndex(session => new { session.IdentityId, session.Status, session.IdleExpiresUtc, session.AbsoluteExpiresUtc })
      .HasDatabaseName("IX_AuthenticationSessions_Identity_ActiveExpiry");
    builder.HasIndex(session => new { session.IdentityId, session.CreatedUtc, session.Id })
      .HasDatabaseName("IX_AuthenticationSessions_Identity_Created");
    builder.HasIndex(session => new { session.TenantId, session.TenantUserId, session.IdentityId })
      .HasDatabaseName("IX_AuthenticationSessions_TenantMembershipIdentity");
    builder.Navigation(session => session.RefreshTokenRecords).UsePropertyAccessMode(PropertyAccessMode.Field);

    foreach (var propertyName in new[]
    {
      nameof(AuthenticationSession.IdentityId),
      nameof(AuthenticationSession.TenantUserId),
      nameof(AuthenticationSession.TenantId),
      nameof(AuthenticationSession.ClientId),
      nameof(AuthenticationSession.TokenFamilyId),
      nameof(AuthenticationSession.CreatedUtc),
      nameof(AuthenticationSession.AbsoluteExpiresUtc),
      nameof(AuthenticationSession.SecurityVersionAtCreation)
    })
    {
      builder.Property(propertyName).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
  }
}
