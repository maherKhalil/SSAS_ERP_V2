using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class RolePermissionAssignmentConfiguration : IEntityTypeConfiguration<RolePermissionAssignment>
{
  public void Configure(EntityTypeBuilder<RolePermissionAssignment> builder)
  {
    builder.ToTable("RolePermissionAssignments", PlatformPersistenceConstants.Schema);
    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id).HasColumnName("AssignmentId").UseIdentityColumn();
    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.PermissionName)
      .HasConversion(permission => permission.Value, value => PermissionName.Create(value).Value)
      .HasMaxLength(200)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(assignment => assignment.AssignedUtc).IsRequired();
    builder.Property(assignment => assignment.AssignedBy).HasMaxLength(256).IsRequired();
    builder.Property(assignment => assignment.RemovedBy).HasMaxLength(256);
    builder.HasIndex(assignment => new { assignment.TenantId, assignment.RoleId, assignment.PermissionName })
      .IsUnique()
      .HasFilter("[RemovedUtc] IS NULL");
    builder.HasOne<Role>()
      .WithMany(role => role.PermissionAssignments)
      .HasForeignKey(assignment => new { assignment.TenantId, assignment.RoleId })
      .HasPrincipalKey(role => new { role.TenantId, role.Id })
      .OnDelete(DeleteBehavior.Restrict);
  }
}
