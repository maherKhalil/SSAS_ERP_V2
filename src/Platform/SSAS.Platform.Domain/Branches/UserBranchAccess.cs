using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Branches;

// WHICH BRANCHES A TENANT USER MAY WORK IN (Branch foundation B0/B1).
//
// IT LIVES IN THE PLATFORM DATABASE, with the user it authorizes. Authentication must be able to answer
// "which branches may this user enter" while deciding whether a login even completes, and the platform
// plane is the one that stays available when a tenant database is mid-cutover or unreachable.
//
// BranchId IS AN OPAQUE CROSS-DATABASE IDENTIFIER. There is deliberately NO foreign key to Branch: the
// branch row lives in the tenant database, and a physical constraint across catalogs is impossible the
// moment a tenant is promoted to dedicated storage — the same reason Company has no FK to Tenant
// (ADR-017). Existence, tenant ownership and active state are validated by the application against the
// tenant database BEFORE any row here is written.
//
// IT IS NOT ITenantOwnedEntity, deliberately, and for the same reason TenantDatabaseAssignment is not: the
// global tenant filter would hide these rows from the authentication path that must read them before an
// ambient tenant context exists. TenantId is retained as a trusted column and every query filters on it
// explicitly.
//
// NO ROW EXISTS FOR A TENANT ADMINISTRATOR. Their scope is every active branch in the tenant, derived from
// authority rather than stored — see ITenantBranchAccessResolver. Materialising rows for them would need
// synchronising on every branch creation, and would have to exist before the first branch does.
public sealed class UserBranchAccess : Entity<long>, IAuditableEntity
{
  public const int ActorMaximumLength = 256;

  private UserBranchAccess(long id, Guid tenantId, long tenantUserId, Guid branchId) : base(id)
  {
    TenantId = tenantId;
    TenantUserId = tenantUserId;
    BranchId = branchId;
  }

  private UserBranchAccess()
    : base(0)
  {
  }

  public Guid TenantId { get; private set; }

  public long TenantUserId { get; private set; }

  public Guid BranchId { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<UserBranchAccess> Create(Guid tenantId, long tenantUserId, Guid branchId)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0 || branchId == Guid.Empty)
    {
      return Result.Failure<UserBranchAccess>(BranchErrors.AssignmentInvalid);
    }

    return Result.Success(new UserBranchAccess(0, tenantId, tenantUserId, branchId));
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
