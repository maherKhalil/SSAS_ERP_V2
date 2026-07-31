using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Roles;

public sealed class RolePermissionAssignment : Entity<long>, ITenantOwnedEntity
{
  private RolePermissionAssignment(
    long id,
    Guid tenantId,
    long roleId,
    PermissionName permissionName,
    DateTimeOffset assignedUtc,
    string assignedBy) : base(id)
  {
    TenantId = tenantId;
    RoleId = roleId;
    PermissionName = permissionName;
    AssignedUtc = assignedUtc.ToUniversalTime();
    AssignedBy = assignedBy;
  }

  private RolePermissionAssignment()
    : base(0)
  {
    PermissionName = null!;
    AssignedBy = string.Empty;
  }

  public Guid TenantId { get; private set; }

  public long RoleId { get; private set; }

  public PermissionName PermissionName { get; private set; }

  public DateTimeOffset AssignedUtc { get; private set; }

  public string AssignedBy { get; private set; }

  public DateTimeOffset? RemovedUtc { get; private set; }

  public string? RemovedBy { get; private set; }

  public bool IsActive => RemovedUtc is null;

  internal static RolePermissionAssignment Create(
    Guid tenantId,
    long roleId,
    PermissionName permissionName,
    DateTimeOffset assignedUtc,
    string assignedBy) => new(0, tenantId, roleId, permissionName, assignedUtc, assignedBy);

  internal void Remove(DateTimeOffset removedUtc, string removedBy)
  {
    RemovedUtc = removedUtc.ToUniversalTime();
    RemovedBy = removedBy;
  }

  Guid ITenantOwnedEntity.TenantId
  {
    get => TenantId;
    set => TenantId = value;
  }
}
