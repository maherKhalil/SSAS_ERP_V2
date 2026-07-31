using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.TenantUsers;

public sealed class TenantUserRoleAssignment : Entity<long>, ITenantOwnedEntity
{
  private TenantUserRoleAssignment(
    long id,
    Guid tenantId,
    long tenantUserId,
    long roleId,
    DateTimeOffset assignedUtc,
    string assignedBy) : base(id)
  {
    TenantId = tenantId;
    TenantUserId = tenantUserId;
    RoleId = roleId;
    AssignedUtc = assignedUtc.ToUniversalTime();
    AssignedBy = assignedBy;
  }

  private TenantUserRoleAssignment()
    : base(0)
  {
    AssignedBy = string.Empty;
  }

  public Guid TenantId { get; private set; }

  public long TenantUserId { get; private set; }

  public long RoleId { get; private set; }

  public DateTimeOffset AssignedUtc { get; private set; }

  public string AssignedBy { get; private set; }

  public DateTimeOffset? RemovedUtc { get; private set; }

  public string? RemovedBy { get; private set; }

  public bool IsActive => RemovedUtc is null;

  internal static TenantUserRoleAssignment Create(
    Guid tenantId,
    long tenantUserId,
    long roleId,
    DateTimeOffset assignedUtc,
    string assignedBy) => new(0, tenantId, tenantUserId, roleId, assignedUtc, assignedBy);

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
