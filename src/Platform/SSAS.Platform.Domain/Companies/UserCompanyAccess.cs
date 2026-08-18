using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.Companies;

// WHICH COMPANIES A TENANT USER MAY ACT WITHIN (FP-006C1, ADR-025 decision 5).
//
// IT LIVES IN THE PLATFORM DATABASE, with the user it authorizes. The relationship that actually needs
// enforcing is the one to TenantUser, which is in this catalog; putting the row beside Company instead would
// lose that foreign key to gain one that cannot exist. The platform plane is also the one that stays
// available while a tenant database is mid-cutover or unreachable.
//
// CompanyId IS AN OPAQUE CROSS-DATABASE IDENTIFIER. There is deliberately NO foreign key to Company: the
// company row lives in the tenant database, and a physical constraint across catalogs is impossible the
// moment a tenant is promoted to dedicated storage (ADR-017) — the same reason UserBranchAccess has no
// foreign key to Branch (ADR-023 decision 4). Existence, tenant ownership and Active state are validated by
// the application against the tenant database BEFORE any row here is written.
//
// IT IS NOT ITenantOwnedEntity, deliberately, and for the same reason UserBranchAccess is not: the global
// tenant query filter keys on the AMBIENT tenant, and these rows are read on paths that resolve scope before
// an ambient tenant context is guaranteed. TenantId is retained as a trusted column and every query filters
// on it explicitly.
//
// NO ROW EXISTS FOR A TENANT ADMINISTRATOR. Their scope is every active company in the tenant, derived from
// Platform.Tenant.Administer rather than stored — see ITenantCompanyAccessResolver. Materialising rows for
// them would need synchronising on every company created, and would have to exist before the first company
// does.
public sealed class UserCompanyAccess : Entity<long>, IAuditableEntity
{
  public const int ActorMaximumLength = 256;

  private UserCompanyAccess(long id, Guid tenantId, long tenantUserId, Guid companyId) : base(id)
  {
    TenantId = tenantId;
    TenantUserId = tenantUserId;
    CompanyId = companyId;
  }

  private UserCompanyAccess()
    : base(0)
  {
  }

  public Guid TenantId { get; private set; }

  public long TenantUserId { get; private set; }

  public Guid CompanyId { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<UserCompanyAccess> Create(Guid tenantId, long tenantUserId, Guid companyId)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0 || companyId == Guid.Empty)
    {
      return Result.Failure<UserCompanyAccess>(CompanyAccessErrors.AssignmentInvalid);
    }

    return Result.Success(new UserCompanyAccess(0, tenantId, tenantUserId, companyId));
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
