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
