using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Events;
using SSAS.Platform.Domain.Roles;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.TenantUsers;

public sealed class TenantUser : AggregateRoot<long>, IAuditableEntity, ITenantOwnedEntity
{
  private readonly List<TenantUserRoleAssignment> roleAssignments = [];
  private string normalizedEmail = string.Empty;

  private TenantUser(
    long id,
    long identityId,
    Guid tenantId,
    EmailAddress email,
    UserDisplayName displayName) : base(id)
  {
    IdentityId = identityId;
    TenantId = tenantId;
    Email = email;
    normalizedEmail = email.NormalizedEmail;
    DisplayName = displayName;
    Status = TenantUserStatus.Pending;
  }

  private TenantUser()
    : base(0)
  {
    Email = null!;
    DisplayName = null!;
  }

  public long IdentityId { get; private set; }

  public Guid TenantId { get; private set; }

  public EmailAddress Email { get; private set; }

  public string NormalizedEmail => normalizedEmail;

  public UserDisplayName DisplayName { get; private set; }

  public TenantUserStatus Status { get; private set; }

  public IReadOnlyCollection<TenantUserRoleAssignment> RoleAssignments => roleAssignments.AsReadOnly();

  public byte[] RowVersion { get; private set; } = [];

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public IEnumerable<long> ActiveRoleIds => roleAssignments.Where(assignment => assignment.IsActive).Select(assignment => assignment.RoleId);

  public static TenantUser CreateActive(
    long identityId,
    Guid tenantId,
    EmailAddress email,
    UserDisplayName displayName,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    var tenantUser = new TenantUser(0, identityId, tenantId, email, displayName);
    tenantUser.Status = TenantUserStatus.Active;
    tenantUser.RaiseDomainEvent(new TenantUserActivated(eventId, occurredUtc, tenantId, identityId));
    return tenantUser;
  }

  public static TenantUser CreatePending(
    long identityId,
    Guid tenantId,
    EmailAddress email,
    UserDisplayName displayName,
    Guid eventId,
    DateTimeOffset occurredUtc)
  {
    var tenantUser = new TenantUser(0, identityId, tenantId, email, displayName);
    tenantUser.RaiseDomainEvent(new TenantUserInvited(eventId, occurredUtc, tenantId, identityId));
    return tenantUser;
  }

  public Result Activate(Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != TenantUserStatus.Pending)
    {
      return Result.Failure(IdentityAccessErrors.InvalidTenantUserTransition);
    }

    Status = TenantUserStatus.Active;
    RaiseDomainEvent(new TenantUserActivated(eventId, occurredUtc, TenantId, IdentityId));
    return Result.Success();
  }

  public Result UpdateProfile(EmailAddress email, UserDisplayName displayName)
  {
    if (Status == TenantUserStatus.Deactivated)
    {
      return Result.Failure(IdentityAccessErrors.InactiveTenantUser);
    }

    Email = email;
    normalizedEmail = email.NormalizedEmail;
    DisplayName = displayName;
    return Result.Success();
  }

  public Result Deactivate(Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != TenantUserStatus.Active)
    {
      return Result.Failure(IdentityAccessErrors.InvalidTenantUserTransition);
    }

    Status = TenantUserStatus.Deactivated;
    RaiseDomainEvent(new TenantUserDeactivated(eventId, occurredUtc, TenantId, Id));
    return Result.Success();
  }

  public Result Reactivate(Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != TenantUserStatus.Deactivated)
    {
      return Result.Failure(IdentityAccessErrors.InvalidTenantUserTransition);
    }

    Status = TenantUserStatus.Active;
    RaiseDomainEvent(new TenantUserReactivated(eventId, occurredUtc, TenantId, Id));
    return Result.Success();
  }

  public Result AssignRole(Role role, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    if (Status != TenantUserStatus.Active)
    {
      return Result.Failure(IdentityAccessErrors.InactiveTenantUser);
    }

    if (role.TenantId != TenantId)
    {
      return Result.Failure(IdentityAccessErrors.TenantMismatch);
    }

    if (!role.CanReceiveUserAssignments)
    {
      return Result.Failure(IdentityAccessErrors.RoleNotAssignable);
    }

    if (roleAssignments.Any(assignment => assignment.IsActive && assignment.RoleId == role.Id))
    {
      return Result.Failure(IdentityAccessErrors.DuplicateRoleAssignment);
    }

    roleAssignments.Add(TenantUserRoleAssignment.Create(TenantId, Id, role.Id, occurredUtc, actor));
    RaiseDomainEvent(new TenantUserRoleAssigned(eventId, occurredUtc, TenantId, Id, role.Id));
    return Result.Success();
  }

  public Result RemoveRole(long roleId, string actor, Guid eventId, DateTimeOffset occurredUtc)
  {
    var assignment = roleAssignments.SingleOrDefault(item => item.IsActive && item.RoleId == roleId);
    if (assignment is null)
    {
      return Result.Failure(IdentityAccessErrors.RoleAssignmentNotFound);
    }

    assignment.Remove(occurredUtc, actor);
    RaiseDomainEvent(new TenantUserRoleRemoved(eventId, occurredUtc, TenantId, Id, roleId));
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
}
