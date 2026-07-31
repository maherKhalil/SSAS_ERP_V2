using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.Roles;

public sealed class Role : AggregateRoot<long>, IAuditableEntity, ITenantOwnedEntity
{
  private readonly List<RolePermissionAssignment> permissionAssignments = [];
  private string normalizedRoleName = string.Empty;

  private Role(long id, Guid tenantId, RoleName name, string? description, RoleType roleType)
    : base(id)
  {
    TenantId = tenantId;
    Name = name;
    normalizedRoleName = name.NormalizedRoleName;
    Description = description;
    RoleType = roleType;
    Status = RoleStatus.Active;
  }

  private Role()
    : base(0)
  {
    Name = null!;
  }

  public Guid TenantId { get; private set; }

  public RoleName Name { get; private set; }

  public string NormalizedRoleName => normalizedRoleName;

  public string? Description { get; private set; }

  public RoleType RoleType { get; private set; }

  public RoleStatus Status { get; private set; }

  public IReadOnlyCollection<RolePermissionAssignment> PermissionAssignments => permissionAssignments.AsReadOnly();

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public IEnumerable<PermissionName> ActivePermissions => Status != RoleStatus.Retired
    ? permissionAssignments.Where(assignment => assignment.IsActive).Select(assignment => assignment.PermissionName)
    : [];

  public bool CanReceiveUserAssignments => Status == RoleStatus.Active;

  public static Role CreateCustom(
    Guid tenantId,
    RoleName name,
    string? description,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    var role = new Role(0, tenantId, name, NormalizeDescription(description), RoleType.Custom);
    role.RaiseDomainEvent(new RoleCreated(eventId, occurredUtc, tenantId, name.Value));
    return role;
  }

  public static Role CreateSystem(Guid tenantId, RoleName name, string? description) =>
    new(0, tenantId, name, NormalizeDescription(description), RoleType.System);

  public Result Update(RoleName name, string? description, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (RoleType == RoleType.System)
    {
      return Result.Failure(IdentityAccessErrors.ProtectedSystemRole);
    }

    if (Status != RoleStatus.Active)
    {
      return Result.Failure(IdentityAccessErrors.InvalidRoleTransition);
    }

    Name = name;
    normalizedRoleName = name.NormalizedRoleName;
    Description = NormalizeDescription(description);
    RaiseDomainEvent(new RoleUpdated(eventId, occurredUtc, TenantId, Id));
    return Result.Success();
  }

  public Result RequestRetirement(Guid eventId, DateTimeOffset occurredUtc)
  {
    if (RoleType == RoleType.System)
    {
      return Result.Failure(IdentityAccessErrors.ProtectedSystemRole);
    }

    if (Status != RoleStatus.Active)
    {
      return Result.Failure(IdentityAccessErrors.InvalidRoleTransition);
    }

    Status = RoleStatus.RetirementPending;
    RaiseDomainEvent(new RoleRetirementRequested(eventId, occurredUtc, TenantId, Id));
    return Result.Success();
  }

  public Result Retire(bool hasActiveUserAssignments, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (RoleType == RoleType.System)
    {
      return Result.Failure(IdentityAccessErrors.ProtectedSystemRole);
    }

    if (Status != RoleStatus.RetirementPending)
    {
      return Result.Failure(IdentityAccessErrors.InvalidRoleTransition);
    }

    if (hasActiveUserAssignments)
    {
      return Result.Failure(IdentityAccessErrors.RoleHasActiveUsers);
    }

    Status = RoleStatus.Retired;
    RaiseDomainEvent(new RoleRetired(eventId, occurredUtc, TenantId, Id));
    return Result.Success();
  }

  public Result AssignPermission(
    PermissionDefinition permission,
    string actor,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    if (RoleType == RoleType.System)
    {
      return Result.Failure(IdentityAccessErrors.ProtectedSystemRole);
    }

    if (Status != RoleStatus.Active)
    {
      return Result.Failure(IdentityAccessErrors.RoleNotAssignable);
    }

    if (permission.Scope != PermissionScope.Tenant)
    {
      return Result.Failure(IdentityAccessErrors.PlatformPermissionRejected);
    }

    if (permissionAssignments.Any(assignment => assignment.IsActive && assignment.PermissionName.Equals(permission.Name)))
    {
      return Result.Failure(IdentityAccessErrors.DuplicatePermissionAssignment);
    }

    permissionAssignments.Add(RolePermissionAssignment.Create(TenantId, Id, permission.Name, occurredUtc, actor));
    RaiseDomainEvent(new RolePermissionAssigned(eventId, occurredUtc, TenantId, Id, permission.Name.Value));
    return Result.Success();
  }

  public Result RemovePermission(PermissionName permissionName, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (RoleType == RoleType.System)
    {
      return Result.Failure(IdentityAccessErrors.ProtectedSystemRole);
    }

    var assignment = permissionAssignments.SingleOrDefault(item => item.IsActive && item.PermissionName.Equals(permissionName));
    if (assignment is null)
    {
      return Result.Failure(IdentityAccessErrors.PermissionAssignmentNotFound);
    }

    assignment.Remove(occurredUtc, actor);
    RaiseDomainEvent(new RolePermissionRemoved(eventId, occurredUtc, TenantId, Id, permissionName.Value));
    return Result.Success();
  }

  Guid ITenantOwnedEntity.TenantId
  {
    get => TenantId;
    set => TenantId = value;
  }

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }

  private static string? NormalizeDescription(string? description)
  {
    var trimmed = description?.Trim();
    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}
