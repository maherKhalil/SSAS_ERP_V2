using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.TenantUsers;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class AccountActionTokenConfiguration : IEntityTypeConfiguration<AccountActionToken>
{
  public void Configure(EntityTypeBuilder<AccountActionToken> builder)
  {
    builder.ToTable("AccountActionTokens", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_AccountActionTokens_OwnershipBinding",
        "([Purpose] = N'Invitation' AND [TenantId] IS NOT NULL AND [TenantUserId] IS NOT NULL) OR " +
        "([Purpose] = N'PasswordReset' AND [TenantId] IS NULL AND [TenantUserId] IS NULL)");
      table.HasCheckConstraint("CK_AccountActionTokens_Expiry", "[ExpiresUtc] > [IssuedUtc]");
    });
    builder.HasKey(token => token.Id);
    builder.Property(token => token.Id).HasColumnName("AccountActionTokenId").UseIdentityColumn();
    builder.Property(token => token.PublicId).IsRequired();
    builder.Property<byte[]>("secretHash").HasColumnName("SecretHash").HasColumnType("binary(32)").IsFixedLength().IsRequired();
    builder.Property(token => token.Purpose)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(token => token.IdentityId).IsRequired();
    builder.Property(token => token.AuthenticationAccountId).IsRequired();
    builder.Property(token => token.TenantId);
    builder.Property(token => token.TenantUserId);
    builder.Property(token => token.IssuedUtc).IsRequired();
    builder.Property(token => token.ExpiresUtc).IsRequired();
    builder.Property(token => token.ConsumedUtc);
    builder.Property(token => token.RevokedUtc);
    builder.Property(token => token.RevokedBy).HasMaxLength(256);
    builder.Property(token => token.RevocationReason).HasMaxLength(128);
    builder.HasIndex(token => token.PublicId).IsUnique();
    builder.HasIndex("secretHash").IsUnique();
    builder.HasIndex(token => new { token.Purpose, token.AuthenticationAccountId })
      .IsUnique()
      .HasFilter("[ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL AND [TenantUserId] IS NULL");
    builder.HasIndex(token => new { token.Purpose, token.TenantId, token.TenantUserId })
      .IsUnique()
      .HasFilter("[ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL AND [TenantUserId] IS NOT NULL");
    builder.HasOne<PlatformIdentity>().WithMany().HasForeignKey(token => token.IdentityId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<AuthenticationAccount>().WithMany().HasForeignKey(token => token.AuthenticationAccountId)
      .OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<TenantUser>().WithMany().HasForeignKey(token => new { token.TenantId, token.TenantUserId })
      .HasPrincipalKey(user => new { user.TenantId, Id = user.Id })
      .OnDelete(DeleteBehavior.Restrict);
    builder.Property(token => token.CreatedUtc).IsRequired();
    builder.Property(token => token.CreatedBy).HasMaxLength(256);
    builder.Property(token => token.ModifiedUtc).IsRequired();
    builder.Property(token => token.ModifiedBy).HasMaxLength(256);
    builder.Property(token => token.RowVersion).IsRowVersion().IsConcurrencyToken();

    foreach (var propertyName in new[]
    {
      nameof(AccountActionToken.PublicId),
      "secretHash",
      nameof(AccountActionToken.Purpose),
      nameof(AccountActionToken.IdentityId),
      nameof(AccountActionToken.AuthenticationAccountId),
      nameof(AccountActionToken.TenantId),
      nameof(AccountActionToken.TenantUserId),
      nameof(AccountActionToken.IssuedUtc),
      nameof(AccountActionToken.ExpiresUtc)
    })
    {
      builder.Property(propertyName).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
  }
}
