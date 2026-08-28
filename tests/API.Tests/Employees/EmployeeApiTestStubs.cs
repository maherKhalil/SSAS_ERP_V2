using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Domain.Employees;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Employees;

public static class EmployeeApiTestServices
{
  // The production request-context registration, minus the company chain the harness supplies itself.
  // Deliberately calls the REAL extension rather than reproducing it: a divergence between what the tests
  // compose and what the Host composes is exactly the class of defect FP-006C5 exists to close.
  public static IServiceCollection AddPlatformRequestContextForTests(this IServiceCollection services) =>
    services.AddPlatformRequestContext();
}

// ---- THE COMPANY DIMENSION.
//
// Answers the resolver's two questions. Every denial returns the same InvalidSelection the real resolver
// returns, so the concealment under test is the production one.
public sealed class StubCompanyAccess : ITenantCompanyAccessResolver
{
  public IReadOnlyList<Guid> Permitted { get; set; } = [];

  public Task<Result<IReadOnlyList<CompanyAccessSummary>>> GetPermittedCompaniesAsync(
    Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<CompanyAccessSummary>>(
      Permitted.Select(id => new CompanyAccessSummary(id, "CODE", "Name")).ToArray()));

  public Task<Result> AuthorizeCompanyAsync(
    Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Permitted.Contains(companyId)
      ? Result.Success()
      : Result.Failure(new Error("Company.InvalidSelection", "denied")));
}

public sealed class StubBranchAccess : ITenantBranchAccessResolver
{
  public IReadOnlyList<Guid> Permitted { get; set; } = [];

  public Task<Result<IReadOnlyList<BranchAccessSummary>>> GetPermittedBranchesAsync(
    Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Result.Success<IReadOnlyList<BranchAccessSummary>>(
      Permitted.Select(id => new BranchAccessSummary(id, "CODE", "Name", false)).ToArray()));

  public Task<Result> AuthorizeBranchAsync(
    Guid tenantId, long tenantUserId, Guid branchId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Permitted.Contains(branchId)
      ? Result.Success()
      : Result.Failure(new Error("Branch.InvalidSelection", "denied")));
}

public sealed class StubCurrentBranch : ICurrentBranchResolver
{
  public Guid BranchId { get; set; }

  public Error? Error { get; set; }

  public Task<Result<Guid>> ResolveCurrentBranchAsync(CancellationToken cancellationToken = default) =>
    Task.FromResult(Error is { } error ? Result.Failure<Guid>(error) : Result.Success(BranchId));
}

// Stands in for CurrentCompany, which needs the tenant database to establish. It answers both the
// establishing and the reporting question from one value, exactly as the production type does.
public sealed class StubCompanyEstablisher : ICompanyContextEstablisher, ICurrentCompany
{
  public Guid? Established { get; set; }

  public Error? Error { get; set; }

  public Guid? CompanyId => Error is null ? Established : null;

  public Task<Result<Guid>> EstablishAsync(CancellationToken cancellationToken = default) =>
    Task.FromResult(Error is { } error
      ? Result.Failure<Guid>(error)
      : Result.Success(Established ?? Guid.Empty));
}

public sealed class StubEmployeeReads : IEmployeeReadService
{
  public EmployeeDetail? Detail { get; set; }

  public IReadOnlyList<EmployeeSummary> Page { get; set; } = [];

  public IReadOnlyList<EmployeeBranchHistoryEntry>? History { get; set; }

  public EmployeeSearchCriteria? LastCriteria { get; private set; }

  public EmployeeReadScope? LastScope { get; private set; }

  public void Reset()
  {
    Detail = SampleDetail();
    Page = [];
    History = [];
    LastCriteria = null;
    LastScope = null;
    // The two composed counts reset with everything else. They are seeded per test and the fixture is
    // shared, so leaving them would let one test's number decide another's assertion.
    PositionHolderCount = 0;
    DepartmentMemberCount = 0;
  }

  // The list-row counterpart of SampleDetail, carrying the same seeded department so the two wire tests
  // assert against one fixture rather than two that could drift apart.
  public static EmployeeSummary SampleSummary() => new(
    EmployeeApiTestHost.EmployeeId,
    EmployeeApiTestHost.CompanyA,
    EmployeeApiTestHost.BranchA,
    new EmployeeDepartmentSummary(EmployeeApiTestHost.DepartmentA, "FIN", "Finance"),
    "EMP-00147",
    "Layla Haddad",
    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    EmployeeStatus.Active);

  public static EmployeeDetail SampleDetail(
    EmployeeStatus status = EmployeeStatus.Active, DateTimeOffset? terminationDate = null) => new(
    EmployeeApiTestHost.EmployeeId,
    EmployeeApiTestHost.CompanyA,
    EmployeeApiTestHost.BranchA,
    // The seeded department, code and name together: the wire tests assert that all three reach the caller,
    // so a stub returning only the identifier could not tell a shipped sub-object from a missing one.
    new EmployeeDepartmentSummary(EmployeeApiTestHost.DepartmentA, "FIN", "Finance"),
    "EMP-00147",
    "Layla Haddad",
    "2990112345678",
    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    terminationDate,
    status,
    EmployeeStatusChangeReason.Created,
    new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    [0, 0, 0, 0, 0, 0, 7, 209]);

  public Task<EmployeeDetail?> GetEmployeeAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(Detail);
  }

  public Task<PagedResult<EmployeeSummary>> SearchEmployeesAsync(
    EmployeeReadScope scope, EmployeeSearchCriteria criteria, CancellationToken cancellationToken = default)
  {
    LastScope = scope;
    LastCriteria = criteria;

    return Task.FromResult(new PagedResult<EmployeeSummary>(
      [.. Page], criteria.PageNumber, criteria.PageSize, Page.Count));
  }

  public Task<IReadOnlyList<EmployeeBranchHistoryEntry>?> GetEmployeeBranchHistoryAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(History);
  }

  // The position history the FP-008 Phase 4 route reads. `History` stands in for the branch one; this
  // returns whatever the test seeded, so the route's projection and its 404-on-absence can both be driven.
  public IReadOnlyList<EmployeePositionHistoryEntry>? PositionHistory { get; set; } = [];

  public Task<IReadOnlyList<EmployeePositionHistoryEntry>?> GetEmployeePositionHistoryAsync(
    EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(PositionHistory);
  }

  // The holder count the position representation composes. Seeded by a test so the two `employeeCount`
  // cases — a number when the caller has an employee scope, null when they do not — can be told apart by
  // the VALUE rather than by whether the stub was reached.
  public int PositionHolderCount { get; set; }

  public Task<int> CountEmployeesByPositionAsync(
    EmployeeReadScope scope, Guid positionId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(PositionHolderCount);
  }

  // The department member count the department representation composes, seeded independently of the holder
  // count so a test can set one without moving the other — and so `0` and `null` remain distinguishable by
  // VALUE rather than by whether the stub was reached (FP-007 employeeCount, shipped 2026-08-22).
  public int DepartmentMemberCount { get; set; }

  public Task<int> CountEmployeesByDepartmentAsync(
    EmployeeReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(DepartmentMemberCount);
  }

  // FP-009. Records the scope like every other read on this stub, and returns whatever a test seeded — so a
  // route test can prove the CSV a caller receives came from rows the caller's scope admitted.
  public IReadOnlyList<EmployeeExportRow> ExportRows { get; set; } = [];

  public Task<IReadOnlyList<EmployeeExportRow>> ExportEmployeesAsync(
    EmployeeReadScope scope,
    EmployeeSearchCriteria criteria,
    int ceiling,
    CancellationToken cancellationToken = default)
  {
    LastScope = scope;

    return Task.FromResult(ExportRows);
  }
}

// Returns a real Employee aggregate so the command handlers exercise their genuine domain transitions and
// rowversion comparison rather than a shape invented for the test.
public sealed class StubEmployeeRepository : IEmployeeRepository
{
  public static readonly byte[] CurrentRowVersion = [0, 0, 0, 0, 0, 0, 7, 209];

  public Employee? Employee { get; set; }

  public bool NumberExists { get; set; }

  public bool NationalIdExists { get; set; }

  public void Reset()
  {
    Employee = NewEmployee(EmployeeStatus.Active);
    NumberExists = false;
    NationalIdExists = false;
  }

  public static Employee NewEmployee(EmployeeStatus status)
  {
    var employee = Employee.Create(
      EmployeeNumber.Create("EMP-00147").Value,
      EmployeeFullName.Create("Layla Haddad").Value,
      null,
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
      "hr-user",
      Guid.NewGuid(),
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)).Value;

    employee.TenantId = EmployeeApiTestHost.TenantId;
    employee.CompanyId = EmployeeApiTestHost.CompanyA;
    employee.BranchId = EmployeeApiTestHost.BranchA;

    employee.StampInitialAssignment(
      EmployeeApiTestHost.TenantId,
      EmployeeApiTestHost.CompanyA,
      EmployeeApiTestHost.BranchA,
      EmployeeApiTestHost.DepartmentA,
      EmployeeApiTestHost.PositionA,
      "seed",
      Guid.NewGuid(),
      new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));

    if (status == EmployeeStatus.Inactive)
    {
      employee.Deactivate(
        EmployeeStatusChangeReason.Administrative, "seed", Guid.NewGuid(), DateTimeOffset.UtcNow);
    }
    else if (status == EmployeeStatus.Terminated)
    {
      employee.Terminate(
        DateTimeOffset.UtcNow, EmployeeStatusChangeReason.Resignation, "seed", Guid.NewGuid(),
        DateTimeOffset.UtcNow);
    }

    SetRowVersion(employee, CurrentRowVersion);

    return employee;
  }

  // The concurrency token is database-generated, so a test that needs a known value has to place it. Done
  // through the same property EF writes to, not through a parallel test-only field.
  private static void SetRowVersion(Employee employee, byte[] rowVersion) =>
    typeof(Employee)
      .GetProperty(nameof(Employee.RowVersion))!
      .SetValue(employee, rowVersion);

  public Task<Employee?> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
    Task.FromResult(Employee);

  public Task<bool> EmployeeNumberExistsAsync(
    Guid companyId, string normalizedEmployeeNumber, CancellationToken cancellationToken = default) =>
    Task.FromResult(NumberExists);

  public Task<bool> NationalIdExistsAsync(
    Guid companyId, string normalizedNationalId, CancellationToken cancellationToken = default) =>
    Task.FromResult(NationalIdExists);

  public Task AddAsync(Employee employee, CancellationToken cancellationToken = default) => Task.CompletedTask;

  public Task AppendBranchAssignmentAsync(
    EmployeeBranchAssignment assignment, CancellationToken cancellationToken = default) => Task.CompletedTask;

  public Task AppendDepartmentAssignmentAsync(
    SSAS.HR.Domain.Departments.EmployeeDepartmentAssignment assignment,
    CancellationToken cancellationToken = default) => Task.CompletedTask;

  // ---- THE DEPARTMENT LOOKUP, ANSWERED BY ROLE RATHER THAN BY A FIXED COMPANY.
  //
  // Three distinguishable outcomes, because the create contract has three distinguishable refusals and a
  // single boolean could not tell them apart:
  //
  //   DepartmentA             → Active in WHICHEVER company the caller has established
  //   DepartmentInactive      → present in that company, but inactive
  //   DepartmentOtherCompany  → null, exactly as the real query's company predicate would leave it
  //
  // DepartmentA deliberately follows the established company instead of being pinned to CompanyA. These
  // HTTP tests switch companies to prove things about EMPLOYEE NUMBERS and scope, and pinning it would make
  // every one of them fail for an unrelated reason — a department that does not exist — while appearing to
  // test what it says. The cross-company refusal keeps its own proof in DepartmentOtherCompany, which is
  // never resolvable whatever company is established.
  public Task<DepartmentAssignmentTarget?> FindAssignableDepartmentAsync(
    Guid companyId, Guid departmentId, CancellationToken cancellationToken = default)
  {
    if (departmentId == EmployeeApiTestHost.DepartmentInactive)
    {
      return Task.FromResult<DepartmentAssignmentTarget?>(new(departmentId, IsActive: false));
    }

    return Task.FromResult<DepartmentAssignmentTarget?>(
      departmentId == EmployeeApiTestHost.DepartmentA || departmentId == EmployeeApiTestHost.DepartmentB
        ? new(departmentId, IsActive: true)
        : null);
  }

  public Task AppendPositionAssignmentAsync(
    SSAS.HR.Domain.Positions.EmployeePositionAssignment assignment,
    CancellationToken cancellationToken = default) => Task.CompletedTask;

  // The position lookup, answered on exactly the terms the department one is: three distinguishable
  // outcomes, because employee creation has three distinguishable refusals — inactive is named, unknown or
  // out-of-company is absent, and everything else is assignable.
  public Task<PositionAssignmentTarget?> FindAssignablePositionAsync(
    Guid companyId, Guid positionId, CancellationToken cancellationToken = default)
  {
    if (positionId == EmployeeApiTestHost.PositionInactive)
    {
      return Task.FromResult<PositionAssignmentTarget?>(new(positionId, IsActive: false));
    }

    return Task.FromResult<PositionAssignmentTarget?>(
      positionId == EmployeeApiTestHost.PositionA
        ? new(positionId, IsActive: true)
        : null);
  }

  // ---- THE BY-CODE PAIR (FP-009), ANSWERING ON EXACTLY THE SAME TERMS AS THEIR BY-IDENTIFIER SIBLINGS.
  //
  // The codes below map onto the SAME three outcomes — active, present-but-inactive, and absent — so a test
  // that proves something about an import's classification resolution proves it against the same shape the
  // single-create path is tested against. A stub that answered more generously here would let an import
  // succeed on a code the real query would report absent.
  //
  // The argument is the NORMALIZED code, so these compare uppercase: the real query runs against a
  // binary-collated column and matching case-insensitively here would hide a normalization bug.
  public Task<DepartmentAssignmentTarget?> FindAssignableDepartmentByCodeAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult<DepartmentAssignmentTarget?>(normalizedCode switch
    {
      EmployeeApiTestHost.DepartmentACode => new(EmployeeApiTestHost.DepartmentA, IsActive: true),
      EmployeeApiTestHost.DepartmentInactiveCode =>
        new(EmployeeApiTestHost.DepartmentInactive, IsActive: false),
      _ => null
    });

  public Task<PositionAssignmentTarget?> FindAssignablePositionByCodeAsync(
    Guid companyId, string normalizedCode, CancellationToken cancellationToken = default) =>
    Task.FromResult<PositionAssignmentTarget?>(normalizedCode switch
    {
      EmployeeApiTestHost.PositionACode => new(EmployeeApiTestHost.PositionA, IsActive: true),
      EmployeeApiTestHost.PositionInactiveCode =>
        new(EmployeeApiTestHost.PositionInactive, IsActive: false),
      _ => null
    });
}

// Failure carries whatever the write boundary would have produced, which is how the authorization-versus-
// storage distinction is exercised end to end at the HTTP layer.
public sealed class StubUnitOfWork : ITenantUnitOfWork
{
  public Error? Failure { get; set; }

  // ---- FAIL EXACTLY ONE SAVE, THEN BEHAVE (FP-009 Phase 2).
  //
  // `Failure` fails EVERY save, which cannot express the one path that matters here: an import whose
  // employee write fails and whose REFUSAL RECORD must then still be written. That sequence — fail, then
  // succeed — is precisely the race the import handler exists to survive, and a stub that failed both saves
  // would report the refusal as unrecordable and hide whether the record was attempted at all.
  public Error? FailOnce { get; set; }

  public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    if (FailOnce is { } once)
    {
      FailOnce = null;

      return Task.FromResult(Result.Failure<int>(once));
    }

    return Task.FromResult(Failure is { } error ? Result.Failure<int>(error) : Result.Success(1));
  }

  // ---- THE COMMIT CAN BE MADE TO THROW (T-091).
  //
  // `TerminateEmployeeCommandHandler` holds an open tenant transaction across a cross-database call, and the
  // ONE half-state it can reach is a commit that fails AFTER the account was closed. **A stub whose commit
  // always succeeds cannot express that**, so the half-state would be reasoned about and never exercised.
  public Exception? CommitFailure { get; set; }

  public int Commits { get; private set; }

  public int Rollbacks { get; private set; }

  // Reset with the rest of the host's state. A cumulative counter would make "nothing was committed"
  // depend on which tests ran first, which is a guard whose result is decided by xUnit's ordering.
  public void ResetTransactions()
  {
    CommitFailure = null;
    Commits = 0;
    Rollbacks = 0;
  }

  public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
    Task.FromResult<ITransaction>(new RecordingTransaction(this));

  // Records rather than no-ops, because "was the termination rolled back" is the assertion that separates a
  // refusal from a silent half-commit, and it is not visible in the response.
  private sealed class RecordingTransaction(StubUnitOfWork owner) : ITransaction
  {
    private bool completed;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
      if (owner.CommitFailure is { } failure)
      {
        completed = true;
        owner.Rollbacks++;
        throw failure;
      }

      completed = true;
      owner.Commits++;
      return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
      completed = true;
      owner.Rollbacks++;
      return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
      if (!completed)
      {
        owner.Rollbacks++;
      }

      return ValueTask.CompletedTask;
    }
  }
}

// T-091's door out of HR. Records what it was asked about, because "did termination actually try to close
// the account" is not visible in the response of a successful termination.
public sealed class StubTenantUserDeactivator : SSAS.BuildingBlocks.Tenancy.ITenantUserDeactivator
{
  public Error? Failure { get; set; }

  public List<Guid> Asked { get; } = [];

  public Task<Result> DeactivateForEmployeeAsync(
    Guid employeeId, CancellationToken cancellationToken = default)
  {
    Asked.Add(employeeId);

    return Task.FromResult(Failure is { } error ? Result.Failure(error) : Result.Success());
  }

  public void Reset()
  {
    Failure = null;
    Asked.Clear();
  }
}

// Records that a transfer was opened through the sanctioned channel rather than by mutating BranchId. The
// real implementation is internal to Platform.Infrastructure and its semantics — reference-equality matching,
// re-validation at save — are proven against real SQL in the C2 and C3 boundary suites.
public sealed class RecordingTransferScope : IBranchTransferScope
{
  private BranchTransferDeclaration? current;

  public BranchTransferDeclaration? Current => current;

  public Result<IDisposable> Begin(BranchTransferDeclaration declaration)
  {
    ArgumentNullException.ThrowIfNull(declaration);

    if (current is not null)
    {
      return Result.Failure<IDisposable>(BranchTransferErrors.TransferAlreadyInProgress);
    }

    current = declaration;

    return Result.Success<IDisposable>(new Closer(this));
  }

  private sealed class Closer(RecordingTransferScope owner) : IDisposable
  {
    public void Dispose() => owner.current = null;
  }
}
