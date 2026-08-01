using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.ValueObjects;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class AuthenticationAccountConfiguration : IEntityTypeConfiguration<AuthenticationAccount>
{
  public void Configure(EntityTypeBuilder<AuthenticationAccount> builder)
  {
    builder.ToTable("AuthenticationAccounts", PlatformPersistenceConstants.Schema, table => table.HasCheckConstraint(
      "CK_AuthenticationAccounts_PasswordHashStatus",
      "([Status] = N'PendingSetup' AND [PasswordHash] IS NULL AND [EmailVerifiedUtc] IS NULL AND [PasswordChangedUtc] IS NULL) OR " +
      "([Status] IN (N'Active', N'Disabled') AND [PasswordHash] IS NOT NULL AND [EmailVerifiedUtc] IS NOT NULL AND [PasswordChangedUtc] IS NOT NULL)"));
    builder.HasKey(account => account.Id);
    builder.Property(account => account.Id).HasColumnName("AuthenticationAccountId").UseIdentityColumn();
    builder.Property(account => account.IdentityId).IsRequired();
    builder.Property(account => account.LoginEmail)
      .HasConversion(email => email.Value, value => LoginEmail.Create(value).Value)
      .HasMaxLength(320)
      .IsRequired();
    builder.Property(account => account.NormalizedLoginEmail)
      .HasField("normalizedLoginEmail")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(320)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property<string?>("passwordHash").HasColumnName("PasswordHash").HasMaxLength(1024);
    builder.Property(account => account.EmailVerifiedUtc);
    builder.Property(account => account.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    builder.Property(account => account.FailedAttemptCount).IsRequired();
    builder.Property(account => account.LockoutEndUtc);
    builder.Property(account => account.SecurityVersion).IsRequired();
    builder.Property(account => account.PasswordChangedUtc);
    builder.HasIndex(account => account.IdentityId).IsUnique();
    builder.HasIndex(account => account.NormalizedLoginEmail).IsUnique();
    builder.HasOne<PlatformIdentity>().WithOne().HasForeignKey<AuthenticationAccount>(account => account.IdentityId)
      .OnDelete(DeleteBehavior.Restrict);
    builder.Ignore(account => account.HasPassword);
    builder.Property(account => account.CreatedUtc).IsRequired();
    builder.Property(account => account.CreatedBy).HasMaxLength(256);
    builder.Property(account => account.ModifiedUtc).IsRequired();
    builder.Property(account => account.ModifiedBy).HasMaxLength(256);
    builder.Property(account => account.RowVersion).IsRowVersion().IsConcurrencyToken();
    builder.Property(account => account.IdentityId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
  }
}
