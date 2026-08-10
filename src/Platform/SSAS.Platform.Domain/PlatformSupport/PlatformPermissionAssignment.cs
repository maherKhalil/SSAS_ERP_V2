using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.PlatformSupport;

// Global, non-tenant-owned (ADR-015 / DEC-TEN-0018). A direct grant of one platform-support
// permission to a PlatformSupportPrincipal. Mirrors RolePermissionAssignment's shape but carries
// no TenantId and never implements ITenantOwnedEntity.
public sealed class PlatformPermissionAssignment : Entity<long>
{
  private PlatformPermissionAssignment(
    long id,
    long platformSupportPrincipalId,
    PermissionName permissionName,
    DateTimeOffset assignedUtc,
    string assignedBy) : base(id)
  {
    PlatformSupportPrincipalId = platformSupportPrincipalId;
    PermissionName = permissionName;
    AssignedUtc = assignedUtc.ToUniversalTime();
    AssignedBy = assignedBy;
  }

  private PlatformPermissionAssignment()
    : base(0)
  {
    PermissionName = null!;
    AssignedBy = string.Empty;
  }

  public long PlatformSupportPrincipalId { get; private set; }

  public PermissionName PermissionName { get; private set; }

  public DateTimeOffset AssignedUtc { get; private set; }

  public string AssignedBy { get; private set; }

  public DateTimeOffset? RemovedUtc { get; private set; }

  public string? RemovedBy { get; private set; }

  public bool IsActive => RemovedUtc is null;

  internal static PlatformPermissionAssignment Create(
    long platformSupportPrincipalId,
    PermissionName permissionName,
    DateTimeOffset assignedUtc,
    string assignedBy) => new(0, platformSupportPrincipalId, permissionName, assignedUtc, assignedBy);

  internal void Remove(DateTimeOffset removedUtc, string removedBy)
  {
    RemovedUtc = removedUtc.ToUniversalTime();
    RemovedBy = removedBy;
  }
}
