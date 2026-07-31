using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.TenantUsers;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantUserRoleAssignmentConfiguration : IEntityTypeConfiguration<TenantUserRoleAssignment>
{
  public void Configure(EntityTypeBuilder<TenantUserRoleAssignment> builder)
  {
    builder.ToTable("TenantUserRoleAssignments", PlatformPersistenceConstants.Schema);
    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id).HasColumnName("AssignmentId").UseIdentityColumn();
    builder.Property(assignment => assignment.TenantId).IsRequired();
    builder.Property(assignment => assignment.AssignedUtc).IsRequired();
    builder.Property(assignment => assignment.AssignedBy).HasMaxLength(256).IsRequired();
    builder.Property(assignment => assignment.RemovedBy).HasMaxLength(256);
    builder.HasIndex(assignment => new { assignment.TenantId, assignment.TenantUserId, assignment.RoleId })
      .IsUnique()
      .HasFilter("[RemovedUtc] IS NULL");
    builder.HasOne<TenantUser>()
      .WithMany(user => user.RoleAssignments)
      .HasForeignKey(assignment => new { assignment.TenantId, assignment.TenantUserId })
      .HasPrincipalKey(user => new { user.TenantId, user.Id })
      .OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Role>()
      .WithMany()
      .HasForeignKey(assignment => new { assignment.TenantId, assignment.RoleId })
      .HasPrincipalKey(role => new { role.TenantId, role.Id })
      .OnDelete(DeleteBehavior.Restrict);
  }
}
