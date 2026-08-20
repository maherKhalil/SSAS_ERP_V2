using SSAS.BuildingBlocks.Domain;

namespace SSAS.HR.Domain.Departments;

// WHO CURRENTLY HEADS A DEPARTMENT (REQ-HR-0102, ADR-026 decision 7).
//
// ================================================================================================
// THIS IS A SEPARATE TABLE BECAUSE A COLUMN WOULD BREAK SHARED→DEDICATED CUTOVER.
// ================================================================================================
//
// The obvious model is `Department.ManagerEmployeeId`. Combined with `Employee.DepartmentId` — which
// arrives in a later phase — that forms a CYCLE in the table-level foreign-key graph.
// `TenantCutoverCopyPlan.Order` places tables principals-before-dependents and returns
// `CutoverCopyOrderUndecidable` when no table is ready; with a mutual reference neither is ever ready. It
// does not degrade or warn. Cutover would stop working for every tenant.
//
// This record has foreign keys pointing OUTWARD to both Department and Employee, so it is a dependent of
// each and a principal of neither. The graph stays acyclic, referential integrity is fully preserved, and
// Platform's cutover engine is untouched. `ADR-026` decision 7 names the condition under which the direct
// column becomes available again: a cutover engine that can copy in two passes.
//
// There is precedent for declining a convenient foreign key on classification grounds — `ADR-024` gave
// `EmployeeBranchAssignment` no branch FK — and the reason is recorded rather than left to be rediscovered.
//
// ---- IT IS NOT BRANCH-OWNED.
//
// Heading a department is a company-level relationship. The manager works at some branch and the department
// spans branches, so a `BranchId` here would name one of several arbitrarily.
//
// ---- IT IS CURRENT STATE, NOT HISTORY.
//
// One row per department, keyed by the department itself, so "at most one manager" is a fact of the schema
// rather than something a handler remembers to check. Clearing a manager removes the row. That is not a
// violation of the no-physical-delete rule, which governs DEPARTMENTS: this is an association, and its
// removal is what "this department has no manager" means. If manager history is ever required it will be a
// separate append-only log, exactly as branch history is.
public sealed class DepartmentManager : Entity<Guid>, IAuditableEntity, ITenantOwnedEntity, ICompanyOwnedEntity
{
  public const int ActorMaximumLength = 256;

  private DepartmentManager(
    Guid departmentId,
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    DateTimeOffset assignedUtc,
    string assignedBy) : base(departmentId)
  {
    TenantId = tenantId;
    CompanyId = companyId;
    EmployeeId = employeeId;
    AssignedUtc = assignedUtc.ToUniversalTime();
    AssignedBy = assignedBy;
  }

  private DepartmentManager()
    : base(Guid.Empty)
  {
    AssignedBy = string.Empty;
  }

  // THE IDENTITY IS THE DEPARTMENT. `Entity<Guid>.Id` carries the DepartmentId and is mapped as the primary
  // key, which is what makes "at most one manager per department" unrepresentable rather than merely
  // refused. A surrogate key plus a unique index would enforce the same rule one step further from the
  // reader; the existing repository conventions favour the identity that the invariant is actually about.
  public Guid DepartmentId => Id;

  public Guid TenantId { get; set; }

  public Guid CompanyId { get; set; }

  public Guid EmployeeId { get; private set; }

  public DateTimeOffset AssignedUtc { get; private set; }

  public string AssignedBy { get; private set; }

  // CreatedUtc/CreatedBy/ModifiedUtc/ModifiedBy are owned by the IAuditableEntity persistence
  // infrastructure and are never stamped by the Domain.
  public DateTimeOffset CreatedUtc { get; private set; }

  public DateTimeOffset ModifiedUtc { get; private set; }

  public string? CreatedBy { get; private set; }

  public string? ModifiedBy { get; private set; }

  public byte[] RowVersion { get; private set; } = [];

  // ---- PHASE 1 CREATES THE RECORD AND VALIDATES ONLY WHAT IT CAN SEE.
  //
  // That the employee belongs to the same company, is not terminated, and is not a member of the department
  // they would manage are all cross-aggregate questions requiring repository lookups. They are Phase 2, and
  // nothing here pretends to answer them.
  public static Result<DepartmentManager> Assign(
    Guid departmentId,
    Guid tenantId,
    Guid companyId,
    Guid employeeId,
    string actor,
    DateTimeOffset occurredUtc)
  {
    if (departmentId == Guid.Empty || employeeId == Guid.Empty)
    {
      return Result.Failure<DepartmentManager>(DepartmentErrors.InvalidManagerAssignment);
    }

    if (!IsValidActor(actor))
    {
      return Result.Failure<DepartmentManager>(DepartmentErrors.InvalidActor);
    }

    return Result.Success(new DepartmentManager(
      departmentId, tenantId, companyId, employeeId, occurredUtc, actor.Trim()));
  }

  private static bool IsValidActor(string actor) =>
    !string.IsNullOrWhiteSpace(actor) && actor.Trim().Length <= ActorMaximumLength;

  DateTimeOffset IAuditableEntity.CreatedUtc
  {
    get => CreatedUtc;
    set => CreatedUtc = value;
  }

  string? IAuditableEntity.CreatedBy
  {
    get => CreatedBy;
    set => CreatedBy = value;
  }

  DateTimeOffset IAuditableEntity.ModifiedUtc
  {
    get => ModifiedUtc;
    set => ModifiedUtc = value;
  }

  string? IAuditableEntity.ModifiedBy
  {
    get => ModifiedBy;
    set => ModifiedBy = value;
  }
}
