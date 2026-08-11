using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.PlatformSupport;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

// Platform-plane refresh-token persistence (ADR-016 Phase 3C / DEC-TEN-0022). A separate child table owned
// only by PlatformAuthenticationSession — the tenant RefreshTokenRecords table is untouched and never made
// polymorphic. Mirrors the tenant record's rotation/replacement/uniqueness security constraints.
public sealed class PlatformRefreshTokenRecordConfiguration : IEntityTypeConfiguration<PlatformRefreshTokenRecord>
{
  public void Configure(EntityTypeBuilder<PlatformRefreshTokenRecord> builder)
  {
    builder.ToTable("PlatformRefreshTokenRecords", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint("CK_PlatformRefreshTokenRecords_Expiry", "[ExpiresUtc] > [CreatedUtc]");
      table.HasCheckConstraint("CK_PlatformRefreshTokenRecords_Lifecycle", "NOT ([ConsumedUtc] IS NOT NULL AND [RevokedUtc] IS NOT NULL)");
    });
    builder.HasKey(token => token.Id);
    builder.Property(token => token.Id).HasColumnName("PlatformRefreshTokenRecordId").UseIdentityColumn();
    builder.Property(token => token.PlatformAuthenticationSessionId).IsRequired();
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
    builder.HasIndex(token => token.PublicId).IsUnique().HasDatabaseName("UX_PlatformRefreshTokenRecords_PublicId");
    builder.HasIndex(token => new { token.PlatformAuthenticationSessionId, token.CreatedUtc })
      .HasDatabaseName("IX_PlatformRefreshTokenRecords_Session_Created");
    builder.HasIndex(token => token.ReplacedByRefreshTokenRecordId)
      .IsUnique()
      .HasFilter("[ReplacedByRefreshTokenRecordId] IS NOT NULL")
      .HasDatabaseName("UX_PlatformRefreshTokenRecords_Replacement");
    builder.HasOne<PlatformAuthenticationSession>()
      .WithMany(session => session.RefreshTokenRecords)
      .HasForeignKey(token => new { token.PlatformAuthenticationSessionId, token.TokenFamilyId, token.ClientId })
      .HasPrincipalKey(session => new { Id = session.Id, session.TokenFamilyId, session.ClientId })
      .OnDelete(DeleteBehavior.Restrict);
    builder.HasOne(token => token.ReplacedByRefreshTokenRecord)
      .WithMany()
      .HasForeignKey(token => new
      {
        token.PlatformAuthenticationSessionId,
        token.TokenFamilyId,
        token.ClientId,
        token.ReplacedByRefreshTokenRecordId
      })
      .HasPrincipalKey(token => new
      {
        token.PlatformAuthenticationSessionId,
        token.TokenFamilyId,
        token.ClientId,
        ReplacedByRefreshTokenRecordId = token.Id
      })
      .OnDelete(DeleteBehavior.Restrict);

    foreach (var propertyName in new[]
    {
      nameof(PlatformRefreshTokenRecord.PlatformAuthenticationSessionId),
      nameof(PlatformRefreshTokenRecord.PublicId),
      "secretHash",
      nameof(PlatformRefreshTokenRecord.TokenFamilyId),
      nameof(PlatformRefreshTokenRecord.ClientId),
      nameof(PlatformRefreshTokenRecord.CreatedUtc),
      nameof(PlatformRefreshTokenRecord.ExpiresUtc)
    })
    {
      builder.Property(propertyName).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
  }
}
