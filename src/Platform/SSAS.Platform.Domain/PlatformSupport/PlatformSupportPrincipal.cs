using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Domain.PlatformSupport;

// Global platform-plane authority (ADR-015 / DEC-TEN-0018), anchored to the existing global Identity.
// It is deliberately NOT ITenantOwnedEntity and carries no TenantId: platform-support authority is
// independent of tenant-role membership. Authority is expressed through its revocable PlatformSupport
// permission assignments and its lifecycle status (ADR-016 / DEC-TEN-0020): a Disabled principal's
// authority is unusable while its assignments are retained for a later re-enable.
public sealed class PlatformSupportPrincipal : AggregateRoot<long>, IAuditableEntity
{
  private readonly List<PlatformPermissionAssignment> permissionAssignments = [];

  private PlatformSupportPrincipal(long id, long identityId)
    : base(id)
  {
    IdentityId = identityId;
  }

  private PlatformSupportPrincipal()
    : base(0)
  {
  }

  public long IdentityId { get; private set; }

  public IReadOnlyCollection<PlatformPermissionAssignment> PermissionAssignments => permissionAssignments.AsReadOnly();

  public IEnumerable<PermissionName> ActivePermissions =>
    permissionAssignments.Where(assignment => assignment.IsActive).Select(assignment => assignment.PermissionName);

  public byte[] RowVersion { get; private set; } = [];

  public PlatformSupportPrincipalStatus Status { get; private set; } = PlatformSupportPrincipalStatus.Active;

  // Null until the first lifecycle transition; registration is not a transition (ADR-016 / DEC-TEN-0020).
  public DateTimeOffset? StatusChangedUtc { get; private set; }

  public string? StatusChangedBy { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<PlatformSupportPrincipal> Register(long identityId)
  {
    return identityId <= 0
      ? Result.Failure<PlatformSupportPrincipal>(PlatformSupportErrors.IdentityRequired)
      : Result.Success(new PlatformSupportPrincipal(0, identityId));
  }

  public Result GrantPermission(PermissionDefinition permission, string actor, DateTimeOffset occurredUtc)
  {
    // A disabled principal cannot receive new authority (ADR-016 / DEC-TEN-0020); revoke stays allowed.
    if (Status != PlatformSupportPrincipalStatus.Active)
    {
      return Result.Failure(PlatformSupportErrors.PrincipalDisabled);
    }

    // The catalog is the sole authority for scope: only PlatformSupport permissions may be granted.
    if (permission.Scope != PermissionScope.PlatformSupport)
    {
      return Result.Failure(PlatformSupportErrors.TenantPermissionRejected);
    }

    if (permissionAssignments.Any(assignment => assignment.IsActive && assignment.PermissionName.Equals(permission.Name)))
    {
      return Result.Failure(PlatformSupportErrors.DuplicatePermissionAssignment);
    }

    permissionAssignments.Add(PlatformPermissionAssignment.Create(Id, permission.Name, occurredUtc, actor));
    return Result.Success();
  }

  public Result RevokePermission(PermissionName permissionName, string actor, DateTimeOffset occurredUtc)
  {
    var assignment = permissionAssignments.SingleOrDefault(item => item.IsActive && item.PermissionName.Equals(permissionName));
    if (assignment is null)
    {
      return Result.Failure(PlatformSupportErrors.PermissionAssignmentNotFound);
    }

    assignment.Remove(occurredUtc, actor);
    return Result.Success();
  }

  public Result Disable(string actor, DateTimeOffset occurredUtc)
  {
    if (Status != PlatformSupportPrincipalStatus.Active)
    {
      return Result.Failure(PlatformSupportErrors.InvalidStatusTransition);
    }

    // Assignments are retained; only the lifecycle status changes.
    Status = PlatformSupportPrincipalStatus.Disabled;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
    return Result.Success();
  }

  public Result Reenable(string actor, DateTimeOffset occurredUtc)
  {
    if (Status != PlatformSupportPrincipalStatus.Disabled)
    {
      return Result.Failure(PlatformSupportErrors.InvalidStatusTransition);
    }

    // Restores eligibility of still-active retained assignments; revoked assignments stay revoked.
    Status = PlatformSupportPrincipalStatus.Active;
    StatusChangedUtc = occurredUtc.ToUniversalTime();
    StatusChangedBy = actor;
    return Result.Success();
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
