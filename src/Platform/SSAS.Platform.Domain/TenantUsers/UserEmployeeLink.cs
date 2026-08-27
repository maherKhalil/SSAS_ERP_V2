using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Domain.TenantUsers;

// ==================================================================================================
// WHICH EMPLOYEE A TENANT USER IS (ADR-030, FP-015 domain-model).
// ==================================================================================================
//
// `ADR-030` deliberately names no schema — *"`DEC-L-023` rules the shape; the package rules the schema"* —
// so this type and its table are FP-015's to name, and they are named from the tree rather than inferred
// from the ADR's prose.
//
// ---- IT IS A LINK, NOT AN ACCESS.
//
// `UserBranchAccess` and `UserCompanyAccess` are CAPABILITY GRANTS: they say where a user may operate.
// **This says who a user is.** Borrowing the family's suffix would put an identity fact into the
// vocabulary of authorization — the same category error `ADR-030` Decision 1 rejected when it refused to
// put the link in HR.
//
// ---- `Entity<long>` AND NOT `AggregateRoot<long>`.
//
// Both nearest neighbours are `Entity<long>`; `TenantUser` is the aggregate. The link has no lifecycle of
// its own, no invariant beyond its uniqueness, and nothing holds a reference to it. **It is a fact about a
// `TenantUser`**, not a thing the business reasons about separately.
//
// ---- `TenantUserId`, NOT `IdentityId`, AND THE DIFFERENCE IS NOT COSMETIC.
//
// One identity may be many tenant users. The question `ADR-030` exists to answer is *given the
// authenticated caller IN THIS TENANT, which employee is this?* — a membership question, not a login one.
//
// ---- THE KEY TYPES ARE ASYMMETRIC AND THAT IS NOT A DEFECT.
//
// `long` on the user side, `Guid` on the employee side, because those are the two aggregates' real
// identifiers (`TenantUser : AggregateRoot<long>`, `Employee : AggregateRoot<Guid>`). **The link records
// them; it does not reconcile them.**
//
// ==================================================================================================
// `ITenantOwnedEntity` IS DECLINED, AND NOT BECAUSE THE NEIGHBOURS DECLINE IT.
// ==================================================================================================
//
// There is no convention to follow: all three of `UserBranchAccess`, `UserCompanyAccess` and `TenantUser`
// are Platform-resident and carry a `TenantId`; the first two decline the interface and the third takes
// it. **Whichever this type did would look like a rule, and there is no rule** — so the decision is made
// on mechanism instead.
//
// **Residency is decided by MODEL MEMBERSHIP.** `TenantCutoverCopyPlan.Build(IModel)` selects
// `ITenantOwnedEntity` *within the model it is handed*, and the one production caller hands it
// `ITenantModelSource.Model`. Neither the interface nor the assembly decides, and both have a
// counter-example: `TenantUser` carries the interface and does not travel; `Branch` and `Company` are
// Platform-Domain types that do.
//
// So the interface is inert for a type the tenant model does not contain — **which means its only
// mechanical effect is to make this type travel IF it is ever added to that model.** A `UserEmployeeLink`
// that travelled would carry the mapping into the tenant database and break `ADR-030` Decision 1: the
// mapping would follow the employee and leave the identity behind, which is the precise failure Decision 2
// rejected a column for.
//
// **Carrying the interface would make that a mistake someone must not make. Declining it makes the
// mistake inert** — the same reasoning `ModulePermissionDefinition` gives for having no `Scope` property:
// *"with no property there is nothing to review, and the escalation cannot be expressed."*
//
// ---- REMOVAL IS PHYSICAL, AND TERMINATION IS NOT A REMOVAL.
//
// Physical, following `UserBranchAccess`: retaining removed rows would mean excluding them from every
// uniqueness test and every check thereafter. A link is removed only by administrative correction — the
// wrong employee was linked, or a successor replaces a predecessor.
//
// **Termination is not such an event.** `REQ-SS-0006` requires the link to survive it, because severing
// it makes a terminated employee's retained payslips unattributable. The guard on a terminated employee is
// on the identity's ability to authenticate — **never on the link.** The spec records this in four places
// because it is the single most likely implementation mistake in the package.
public sealed class UserEmployeeLink : Entity<long>, IAuditableEntity
{
  public const int ActorMaximumLength = 256;

  private UserEmployeeLink(long id, Guid tenantId, long tenantUserId, Guid employeeId) : base(id)
  {
    TenantId = tenantId;
    TenantUserId = tenantUserId;
    EmployeeId = employeeId;
  }

  private UserEmployeeLink()
    : base(0)
  {
  }

  public Guid TenantId { get; private set; }

  public long TenantUserId { get; private set; }

  // AN OPAQUE CROSS-DATABASE IDENTIFIER. `Employee` lives in the tenant database, so there is no foreign
  // key and none is possible (`ADR-030` Decision 4). Existence and tenant ownership are the application's
  // to validate against the tenant database before any row here is written.
  public Guid EmployeeId { get; private set; }

  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public static Result<UserEmployeeLink> Create(Guid tenantId, long tenantUserId, Guid employeeId)
  {
    if (tenantId == Guid.Empty || tenantUserId <= 0 || employeeId == Guid.Empty)
    {
      return Result.Failure<UserEmployeeLink>(IdentityAccessErrors.InvalidUserEmployeeLink);
    }

    return Result.Success(new UserEmployeeLink(0, tenantId, tenantUserId, employeeId));
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
