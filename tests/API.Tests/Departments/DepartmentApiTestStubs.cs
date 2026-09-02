using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Domain.Departments;

namespace SSAS.API.Tests.Departments;

// ==================================================================================================
// THE DATABASE-BACKED COLLABORATORS, STOOD IN FOR (FP-007 Phase 4).
// ==================================================================================================
//
// Everything ELSE in the harness is production: the real handlers, the real DepartmentScopeResolver, the
// real permission filter, the real error mapper and the real routes. What is replaced here is only what
// would need SQL Server — which is exactly what Integration.Tests already proves, and proving it twice
// more slowly would not make the HTTP contract any better tested.
//
// Each stub is CONTROLLABLE rather than fixed, because these tests are about which answer reaches the
// caller: a refusal has to be producible on demand so the status code and problem code can be asserted.
// ==================================================================================================
// ⚠ THIS STUB IGNORES THE ID IT IS GIVEN, AND THAT IS A TRADE RATHER THAN THE DEFAULT (274)
// ==================================================================================================
//
// `GetAsync`, `GetManagerEmployeeIdAsync` and the children read all take a `departmentId` and **none of
// them uses it**. `Reset` seeds `Detail` with a sample, so SUCCESS IS THE DEFAULT and a refusal is opt-in
// through `DetailError`. This is recorded because the note belongs at the site a reader arrives at, not
// only at the stub that made the opposite choice.
//
// ---- WHAT IT BUYS: THIS SUITE CAN EXPRESS A SUCCESSFUL CREATE.
//
// Every write route reads back through the scoped path, and a create reads back by an id the aggregate
// MINTS — unknowable to a test. Because this stub answers whatever it is asked, that read-back succeeds
// and `DepartmentEndpointTests` can assert `201`, the `Location` header and the created representation.
// `StubPositionReads` matches on id and therefore cannot: its create routes always answer 500.
//
// ---- WHAT IT COSTS: NOTHING HERE PROVES THE ROUTE PASSED THE RIGHT ID.
//
// A handler that bound `/{departmentId}` correctly and then queried a DIFFERENT id would pass every test in
// this file. ⚠ **`StubPositionReads` gets that check for free — every 200 in the position suite proves
// route→handler→read propagation as a SIDE EFFECT of the id match.**
//
// ⚠⚠ NO GUARD IS PROPOSED, AND THE REASON IS THAT THE FAILURE IS NOT CONSTRUCTIBLE. ASP.NET binds by NAME,
// so a mismatch produces a binding failure — a 400 or a 404 — not a cheerful 200 over the wrong row. The
// state described needs a handler that accepts the id and deliberately passes another, which is a typo
// visible in four lines and under pressure from nothing. Recorded so it is not re-opened as a gap.
//
// ---- AND THE LAYER THAT ACTUALLY DECIDES WHICH ROW COMES BACK IS WELL COVERED, JUST NOT HERE.
//
// `DepartmentApplicationSqlServerTests` reads a **CompanyB** department through a **CompanyA**-scoped graph
// and asserts `NotFound` — a test whose pass DEPENDS on the id being used, because ignoring it would return
// the CompanyA row and go green. Six further sites assert the returned content belongs to the requested
// department. **Handler → read service → SQL is asserted against real SQL; only route → handler is not.**
//
// ⚠⚠⚠ THE DISTINCTION WORTH CARRYING AWAY: INCIDENTAL PROTECTION IS REAL PROTECTION AND CANNOT CARRY A
// CITATION. The position suite's id match genuinely prevents the defect, so *is this safe* is answered yes.
// It still must not be cited for a criterion, because when it reddens it names the wrong subject. Two
// different questions, and a citation sweep runs them together.
public sealed class StubDepartmentReads : IDepartmentReadService
{
  public DepartmentDetail? Detail { get; set; }

  public Error? DetailError { get; set; }

  public IReadOnlyList<DepartmentListItem> Page { get; set; } = [];

  public IReadOnlyList<DepartmentChild>? Children { get; set; }

  public Guid? ManagerEmployeeId { get; set; }

  public SearchDepartmentsQuery? LastQuery { get; private set; }

  public DepartmentReadScope? LastScope { get; private set; }

  public void Reset()
  {
    Detail = SampleDetail();
    DetailError = null;
    Page = [];
    Children = [];
    ManagerEmployeeId = null;
    LastQuery = null;
    LastScope = null;
  }

  public static DepartmentDetail SampleDetail(
    DepartmentStatus status = DepartmentStatus.Active,
    Guid? parentDepartmentId = null,
    DepartmentManagerSummary? manager = null) => new(
    DepartmentApiTestHost.DepartmentId,
    DepartmentApiTestHost.CompanyA,
    "FIN",
    "Finance",
    parentDepartmentId,
    status,
    manager?.EmployeeId,
    manager,
    [0, 0, 0, 0, 0, 0, 7, 209]);

  public Task<Result<DepartmentDetail>> GetAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(DetailError is { } error
      ? Result.Failure<DepartmentDetail>(error)
      : Result.Success(Detail!));
  }

  public Task<Guid?> GetManagerEmployeeIdAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(ManagerEmployeeId);
  }

  public Task<Result<PagedResult<DepartmentListItem>>> SearchAsync(
    DepartmentReadScope scope, SearchDepartmentsQuery query, CancellationToken cancellationToken = default)
  {
    LastScope = scope;
    LastQuery = query;

    return Task.FromResult(Result.Success(
      new PagedResult<DepartmentListItem>([.. Page], query.Page, query.PageSize, Page.Count)));
  }

  public Task<Result<IReadOnlyList<DepartmentChild>>> GetChildrenAsync(
    DepartmentReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Children is null
      ? Result.Failure<IReadOnlyList<DepartmentChild>>(DepartmentErrors.NotFound)
      : Result.Success(Children));
  }
}

// Returns a real Department aggregate so the command handlers exercise their genuine domain transitions
// and rowversion comparison rather than a shape invented for the test.
public sealed class StubDepartmentRepository : IDepartmentRepository
{
  public static readonly byte[] CurrentRowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

  public Department? Department { get; set; }

  public DepartmentManager? Manager { get; set; }

  public bool CodeExists { get; set; }

  public bool HasActiveChildren { get; set; }

  public IReadOnlyList<Department> Ancestry { get; set; } = [];

  public void Reset()
  {
    Department = NewDepartment(DepartmentStatus.Active);
    Manager = null;
    CodeExists = false;
    HasActiveChildren = false;
    Ancestry = [];
  }

  public static Department NewDepartment(DepartmentStatus status)
  {
    var department = SSAS.HR.Domain.Departments.Department.Create(
      DepartmentCode.Create("FIN").Value,
      DepartmentName.Create("Finance").Value,
      parentDepartmentId: null,
      "hr-user",
      Guid.NewGuid(),
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)).Value;

    department.TenantId = DepartmentApiTestHost.TenantId;
    department.CompanyId = DepartmentApiTestHost.CompanyA;

    if (status == DepartmentStatus.Inactive)
    {
      department.Deactivate("seed", Guid.NewGuid(), DateTimeOffset.UtcNow);
    }

    // The concurrency token is database-generated, so a test that needs a known value has to place it.
    // Done through the same property EF writes to, not through a parallel test-only field.
    typeof(Department)
      .GetProperty(nameof(SSAS.HR.Domain.Departments.Department.RowVersion))!
      .SetValue(department, CurrentRowVersion);

    return department;
  }

  public Task<Department?> GetByIdAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Department);

  public Task<bool> CodeExistsAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeExists);

  public Task<bool> CodeExistsForAnotherAsync(
    Guid companyId,
    string normalizedCode,
    Guid excludedDepartmentId,
    CancellationToken cancellationToken = default) =>
    Task.FromResult(CodeExists);

  public Task AddAsync(Department department, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;

  public Task<IReadOnlyList<Department>> GetAncestryAsync(
    Guid departmentId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Ancestry);

  public Task<bool> HasActiveChildrenAsync(
    Guid departmentId, CancellationToken cancellationToken = default) =>
    Task.FromResult(HasActiveChildren);

  public Task<DepartmentManager?> GetManagerAsync(
    Guid departmentId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Manager);

  public Task SetManagerAsync(DepartmentManager manager, CancellationToken cancellationToken = default)
  {
    Manager = manager;
    return Task.CompletedTask;
  }

  public Task ClearManagerAsync(DepartmentManager manager, CancellationToken cancellationToken = default)
  {
    Manager = null;
    return Task.CompletedTask;
  }

  public Task AppendDepartmentAssignmentAsync(
    EmployeeDepartmentAssignment assignment, CancellationToken cancellationToken = default) =>
    Task.CompletedTask;
}
