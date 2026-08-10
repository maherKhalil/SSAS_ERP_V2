using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class PlatformPermissionAssignmentConfiguration : IEntityTypeConfiguration<PlatformPermissionAssignment>
{
  public void Configure(EntityTypeBuilder<PlatformPermissionAssignment> builder)
  {
    builder.ToTable("PlatformPermissionAssignments", PlatformPersistenceConstants.Schema, table =>
      table.HasCheckConstraint(
        "CK_PlatformPermissionAssignments_PermissionName_NotBlank",
        "LEN(LTRIM(RTRIM([PermissionName]))) > 0"));
    builder.HasKey(assignment => assignment.Id);
    builder.Property(assignment => assignment.Id).HasColumnName("PlatformPermissionAssignmentId").UseIdentityColumn();
    builder.Property(assignment => assignment.PlatformSupportPrincipalId).IsRequired();
    builder.Property(assignment => assignment.PermissionName)
      .HasConversion(permission => permission.Value, value => PermissionName.Create(value).Value)
      .HasMaxLength(200)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();
    builder.Property(assignment => assignment.AssignedUtc).IsRequired();
    builder.Property(assignment => assignment.AssignedBy).HasMaxLength(256).IsRequired();
    builder.Property(assignment => assignment.RemovedBy).HasMaxLength(256);

    // One active grant per (principal, permission); revoked rows are retained for history.
    builder.HasIndex(assignment => new { assignment.PlatformSupportPrincipalId, assignment.PermissionName })
      .IsUnique()
      .HasFilter("[RemovedUtc] IS NULL")
      .HasDatabaseName("UX_PlatformPermissionAssignments_Principal_Permission");
    builder.HasOne<PlatformSupportPrincipal>()
      .WithMany(principal => principal.PermissionAssignments)
      .HasForeignKey(assignment => assignment.PlatformSupportPrincipalId)
      .HasPrincipalKey(principal => principal.Id)
      .OnDelete(DeleteBehavior.Restrict);
  }
}
