using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Authentication;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenRecordConfiguration : IEntityTypeConfiguration<RefreshTokenRecord>
{
  public void Configure(EntityTypeBuilder<RefreshTokenRecord> builder)
  {
    builder.ToTable("RefreshTokenRecords", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_RefreshTokenRecords_Expiry", "[ExpiresUtc] > [CreatedUtc]");
      table.HasCheckConstraint("CK_RefreshTokenRecords_Lifecycle", "NOT ([ConsumedUtc] IS NOT NULL AND [RevokedUtc] IS NOT NULL)");
    });
    builder.HasKey(token => token.Id);
    builder.Property(token => token.Id).HasColumnName("RefreshTokenRecordId").UseIdentityColumn();
    builder.Property(token => token.AuthenticationSessionId).IsRequired();
    builder.Property(token => token.PublicId).IsRequired();
    builder.Property<byte[]>("secretHash").HasColumnName("SecretHash").HasColumnType("binary(32)").IsFixedLength().IsRequired();
    builder.Property(token => token.TokenFamilyId).IsRequired();
    builder.Property(token => token.ClientId)
      .HasMaxLength(64)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(token => token.CreatedUtc).IsRequired();
    builder.Property(token => token.ExpiresUtc).IsRequired();
    builder.Property(token => token.ConsumedUtc);
    builder.Property(token => token.RevokedUtc);
    builder.Property(token => token.ReplacedByRefreshTokenRecordId);
    builder.Property(token => token.RowVersion).IsRowVersion().IsConcurrencyToken();
    builder.HasIndex(token => token.PublicId).IsUnique().HasDatabaseName("UX_RefreshTokenRecords_PublicId");
    builder.HasIndex(token => new { token.AuthenticationSessionId, token.CreatedUtc })
      .HasDatabaseName("IX_RefreshTokenRecords_Session_Created");
    builder.HasIndex(token => token.ReplacedByRefreshTokenRecordId)
      .IsUnique()
      .HasFilter("[ReplacedByRefreshTokenRecordId] IS NOT NULL")
      .HasDatabaseName("UX_RefreshTokenRecords_Replacement");
    builder.HasAlternateKey(token => new { token.AuthenticationSessionId, token.TokenFamilyId, token.ClientId, token.Id })
      .HasName("AK_RefreshTokenRecords_SessionFamilyClientRecord");
    builder.HasOne<AuthenticationSession>()
      .WithMany(session => session.RefreshTokenRecords)
      .HasForeignKey(token => new { token.AuthenticationSessionId, token.TokenFamilyId, token.ClientId })
      .HasPrincipalKey(session => new { Id = session.Id, session.TokenFamilyId, session.ClientId })
      .OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(token => token.ReplacedByRefreshTokenRecord)
      .WithMany()
      .HasForeignKey(token => new
      {
        token.AuthenticationSessionId,
        token.TokenFamilyId,
        token.ClientId,
        token.ReplacedByRefreshTokenRecordId
      })
      .HasPrincipalKey(token => new
      {
        token.AuthenticationSessionId,
        token.TokenFamilyId,
        token.ClientId,
        ReplacedByRefreshTokenRecordId = token.Id
      })
      .OnDelete(DeleteBehavior.Restrict);

    foreach (var propertyName in new[]
    {
      nameof(RefreshTokenRecord.AuthenticationSessionId),
      nameof(RefreshTokenRecord.PublicId),
      "secretHash",
      nameof(RefreshTokenRecord.TokenFamilyId),
      nameof(RefreshTokenRecord.ClientId),
      nameof(RefreshTokenRecord.CreatedUtc),
      nameof(RefreshTokenRecord.ExpiresUtc)
    })
    {
      builder.Property(propertyName).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
  }
}
