using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
  public void Configure(EntityTypeBuilder<Role> builder)
  {
    builder.ToTable("Roles", PlatformPersistenceConstants.Schema);
    builder.HasKey(role => role.Id);
    builder.Property(role => role.Id).HasColumnName("RoleId").UseIdentityColumn();
    builder.Property(role => role.TenantId).IsRequired();
    builder.Property(role => role.Name)
      .HasConversion(name => name.Value, value => RoleName.Create(value).Value)
      .HasMaxLength(100)
      .IsRequired();
    builder.Property(role => role.NormalizedRoleName)
      .HasField("normalizedRoleName")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(100)
      .IsRequired();
    builder.Property(role => role.Description).HasMaxLength(500);
    builder.Property(role => role.RoleType).HasConversion<string>().HasMaxLength(16).IsRequired();
    builder.Property(role => role.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
    builder.HasIndex(role => new { role.TenantId, role.NormalizedRoleName }).IsUnique();
    builder.HasAlternateKey(role => new { role.TenantId, role.Id });
    builder.Ignore(role => role.ActivePermissions);
    builder.Navigation(role => role.PermissionAssignments).UsePropertyAccessMode(PropertyAccessMode.Field);
    builder.Property(role => role.CreatedUtc).IsRequired();
    builder.Property(role => role.CreatedBy).HasMaxLength(256);
    builder.Property(role => role.ModifiedUtc).IsRequired();
    builder.Property(role => role.ModifiedBy).HasMaxLength(256);
    builder.Property(role => role.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
