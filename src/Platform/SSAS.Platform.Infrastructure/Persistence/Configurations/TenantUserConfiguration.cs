using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
  public void Configure(EntityTypeBuilder<TenantUser> builder)
  {
    builder.ToTable("TenantUsers", PlatformPersistenceConstants.Schema);
    builder.HasKey(user => user.Id);
    builder.Property(user => user.Id).HasColumnName("TenantUserId").UseIdentityColumn();
    builder.Property(user => user.TenantId).IsRequired();
    builder.Property(user => user.IdentityId).IsRequired();
    builder.Property(user => user.Email)
      .HasConversion(email => email.Value, value => EmailAddress.Create(value).Value)
      .HasMaxLength(320)
      .IsRequired();
    builder.Property(user => user.NormalizedEmail)
      .HasField("normalizedEmail")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(320)
      .IsRequired();
    builder.Property(user => user.DisplayName)
      .HasConversion(name => name.Value, value => UserDisplayName.Create(value).Value)
      .HasMaxLength(200)
      .IsRequired();
    builder.Property(user => user.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    builder.HasIndex(user => new { user.TenantId, user.NormalizedEmail }).IsUnique();
    builder.HasIndex(user => new { user.TenantId, user.IdentityId }).IsUnique();
    builder.HasAlternateKey(user => new { user.TenantId, user.Id });
    builder.HasOne<PlatformIdentity>().WithMany().HasForeignKey(user => user.IdentityId).OnDelete(DeleteBehavior.Restrict);
    builder.Ignore(user => user.ActiveRoleIds);
    builder.Navigation(user => user.RoleAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.Property(user => user.CreatedUtc).IsRequired();
    builder.Property(user => user.CreatedBy).HasMaxLength(256);
    builder.Property(user => user.ModifiedUtc).IsRequired();
    builder.Property(user => user.ModifiedBy).HasMaxLength(256);
    builder.Property(user => user.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
