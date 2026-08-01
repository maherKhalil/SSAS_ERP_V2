using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Authentication;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantSelectionTransactionConfiguration : IEntityTypeConfiguration<TenantSelectionTransaction>
{
  public void Configure(EntityTypeBuilder<TenantSelectionTransaction> builder)
  {
    builder.ToTable("TenantSelectionTransactions", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_TenantSelectionTransactions_Expiry", "[ExpiresUtc] > [CreatedUtc]");
      table.HasCheckConstraint("CK_TenantSelectionTransactions_Lifecycle", "NOT ([ConsumedUtc] IS NOT NULL AND [RevokedUtc] IS NOT NULL)");
    });
    builder.HasKey(transaction => transaction.Id);
    builder.Property(transaction => transaction.Id).HasColumnName("TenantSelectionTransactionId").UseIdentityColumn();
    builder.Property(transaction => transaction.PublicId).IsRequired();
    builder.Property(transaction => transaction.IdentityId).IsRequired();
    builder.Property(transaction => transaction.ClientId)
      .HasMaxLength(64)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(transaction => transaction.SecurityVersionAtAuthentication).IsRequired();
    builder.Property<byte[]>("secretHash").HasColumnName("SecretHash").HasColumnType("binary(32)").IsFixedLength().IsRequired();
    builder.Property(transaction => transaction.CreatedUtc).IsRequired();
    builder.Property(transaction => transaction.ExpiresUtc).IsRequired();
    builder.Property(transaction => transaction.ConsumedUtc);
    builder.Property(transaction => transaction.RevokedUtc);
    builder.Property(transaction => transaction.ModifiedUtc).IsRequired();
    builder.Property(transaction => transaction.CreatedBy).HasMaxLength(256);
    builder.Property(transaction => transaction.ModifiedBy).HasMaxLength(256);
    builder.Property(transaction => transaction.RowVersion).IsRowVersion().IsConcurrencyToken();
    builder.HasOne<PlatformIdentity>().WithMany().HasForeignKey(transaction => transaction.IdentityId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(transaction => transaction.PublicId).IsUnique().HasDatabaseName("UX_TenantSelectionTransactions_PublicId");
    builder.HasIndex(transaction => new { transaction.IdentityId, transaction.ClientId, transaction.ConsumedUtc, transaction.RevokedUtc, transaction.ExpiresUtc })
      .HasDatabaseName("IX_TenantSelectionTransactions_IdentityClientLifecycle");
    builder.HasIndex(transaction => new { transaction.ConsumedUtc, transaction.RevokedUtc, transaction.ExpiresUtc })
      .HasDatabaseName("IX_TenantSelectionTransactions_Unresolved");

    foreach (var propertyName in new[]
    {
      nameof(TenantSelectionTransaction.PublicId),
      nameof(TenantSelectionTransaction.IdentityId),
      nameof(TenantSelectionTransaction.ClientId),
      nameof(TenantSelectionTransaction.SecurityVersionAtAuthentication),
      "secretHash",
      nameof(TenantSelectionTransaction.CreatedUtc),
      nameof(TenantSelectionTransaction.ExpiresUtc)
    })
    {
      builder.Property(propertyName).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
  }
}
