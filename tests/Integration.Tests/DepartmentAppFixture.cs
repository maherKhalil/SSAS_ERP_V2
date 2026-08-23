using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// THE HARNESS FOR THE DEPARTMENT APPLICATION PROOFS (FP-007 Phase 2).
//
// ================================================================================================
// WHAT IS REAL HERE, AND WHAT IS DELIBERATELY NOT.
// ================================================================================================
//
// REAL: the tenant database and its full migration chain, the real `DepartmentRepository`, the real
// `SqlServerDepartmentHierarchyLock` taking `sp_getapplock` on a real connection, the real handlers, the
// real `DepartmentReadService`, and a genuine EF transaction per operation. Every one of those is part of
// what the concurrency and hierarchy proofs are testing.
//
// PERMISSIVE: the scope resolver. Whether a caller is ALLOWED to do these things is decided by
// `DepartmentScopeResolver` and proven against it in `HR.Tests` — permission before scope, tenant
// administration granting nothing, an empty company set refusing. Rebuilding the Platform authorization
// graph here would prove the same decisions again, far more slowly, and would not make the hierarchy proofs
// any stronger. The stub is visible rather than hidden so nobody mistakes these tests for authorization
// coverage.
//
// Each `Graph()` gets its OWN context and connection, which is what lets two of them genuinely contend
// inside SQL Server rather than being serialised by a shared client.
internal sealed class DepartmentAppFixture : IAsyncDisposable
{
  private const string Actor = "department-phase2-tests";

  private readonly string token = Guid.NewGuid().ToString("N")[..12];
  private readonly List<DepartmentGraph> graphs = [];

  private string tenantCatalog = string.Empty;

  public Guid Tenant { get; } = Guid.NewGuid();

  public Guid CompanyA { get; } = Guid.NewGuid();

  public Guid CompanyB { get; } = Guid.NewGuid();

  public Guid BranchA { get; } = Guid.NewGuid();

  public Guid BranchB { get; } = Guid.NewGuid();

  public static async Task<DepartmentAppFixture> CreateAsync()
  {
    var fixture = new DepartmentAppFixture();
    await fixture.InitializeAsync();
    return fixture;
  }

  // A COMPLETE, INDEPENDENT PRODUCTION GRAPH. Separate context, separate connection, separate transactions.
  public DepartmentGraph Graph(
    Guid? company = null,
    IReadOnlyList<Guid>? visibleBranches = null,
    bool canViewEmployees = true)
  {
    var graph = new DepartmentGraph(
      ConnectionFor(tenantCatalog),
      Tenant,
      company ?? CompanyA,
      CompanyB,
      visibleBranches ?? [BranchA, BranchB],
      canViewEmployees);
    graphs.Add(graph);
    return graph;
  }

  // Created through the REAL create handler, so the seeded rows are the rows the application would write.
  public async Task<Guid> CreateDepartmentAsync(
    string code, string name, Guid? parent = null, Guid? company = null)
  {
    await using var graph = new DepartmentGraph(
      ConnectionFor(tenantCatalog), Tenant, company ?? CompanyA, CompanyB, [BranchA, BranchB]);

    var created = await graph.Create().HandleAsync(
      new CreateDepartmentCommand(company ?? CompanyA, code, name, parent));

    Assert.True(created.IsSuccess, created.Error.Code);

    return created.Value;
  }

  // ---- ONE HOLDING DEPARTMENT PER COMPANY, CREATED ON FIRST USE.
  //
  // Written with raw SQL and a reserved code that no test names, so it is invisible to the assertions here
  // while still satisfying the foreign key. It is INSERTED ONLY IF ABSENT, which is what stops repeated
  // calls from producing one department per seeded employee.
  private async Task<Guid> HoldingDepartmentAsync(Guid companyId)
  {
    const string code = "ZZ-EMPLOYEE-HOME";

    var existing = await ScalarGuidAsync($"""
      SELECT TOP 1 [DepartmentId] FROM [tenant].[Departments]
      WHERE [TenantId] = '{Tenant}' AND [CompanyId] = '{companyId}' AND [NormalizedCode] = N'{code}'
      """);

    if (existing is { } found)
    {
      return found;
    }

    var departmentId = Guid.NewGuid();

    await ExecuteAsync($"""
      INSERT INTO [tenant].[Departments]
        ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [NormalizedName], [ParentDepartmentId],
         [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy], [ModifiedUtc],
         [ModifiedBy])
      VALUES
        ('{departmentId}', '{Tenant}', '{companyId}', N'{code}', N'{code}', N'Employee Home', N'EMPLOYEE HOME', NULL,
         N'Active', SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}',
         SYSDATETIMEOFFSET(), N'{Actor}');
      """);

    return departmentId;
  }

  // ---- ONE HOLDING POSITION PER COMPANY, ON THE SAME TERMS (FP-008 Phase 3).
  //
  // `Employee.PositionId` is NOT NULL with a RESTRICT foreign key from Phase 3 onward, so a seeded employee
  // needs a real position. Reserved code, created if absent, invisible to every assertion in this file —
  // the department twin of this method explains the pattern.
  private async Task<Guid> HoldingPositionAsync(Guid companyId)
  {
    const string code = "ZZ-EMPLOYEE-HOME";

    var existing = await ScalarGuidAsync($"""
      SELECT TOP 1 [PositionId] FROM [tenant].[Positions]
      WHERE [TenantId] = '{Tenant}' AND [CompanyId] = '{companyId}' AND [NormalizedCode] = N'{code}'
      """);

    if (existing is { } found)
    {
      return found;
    }

    var positionId = Guid.NewGuid();

    await ExecuteAsync($"""
      INSERT INTO [tenant].[Positions]
        ([PositionId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Title], [NormalizedTitle],
         [JobGradeId], [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy],
         [ModifiedUtc], [ModifiedBy])
      VALUES
        ('{positionId}', '{Tenant}', '{companyId}', N'{code}', N'{code}', N'Employee Home',
         N'EMPLOYEE HOME', NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
         SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
      """);

    return positionId;
  }

  public async Task<byte[]> RowVersionAsync(Guid departmentId)
  {
    await using var context = NewContext();

    var department = await context.Set<Department>()
      .AsNoTracking()
      .SingleAsync(item => item.Id == departmentId);

    return department.RowVersion;
  }

  public async Task<Guid?> ParentOfAsync(Guid departmentId)
  {
    await using var context = NewContext();

    return (await context.Set<Department>()
      .AsNoTracking()
      .SingleAsync(item => item.Id == departmentId)).ParentDepartmentId;
  }

  public async Task<DepartmentStatus> StatusAsync(Guid departmentId)
  {
    await using var context = NewContext();

    return (await context.Set<Department>()
      .AsNoTracking()
      .SingleAsync(item => item.Id == departmentId)).Status;
  }

  public async Task<Guid?> ManagerOfAsync(Guid departmentId)
  {
    await using var context = NewContext();

    var manager = await context.Set<DepartmentManager>()
      .AsNoTracking()
      .SingleOrDefaultAsync(item => item.Id == departmentId);

    return manager?.EmployeeId;
  }

  public async Task<int> ManagerRowCountAsync(Guid departmentId) =>
    await ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[DepartmentManagers] WHERE [DepartmentId] = '{departmentId}'");

  // ---- WALK THE WHOLE COMPANY HIERARCHY AND PROVE IT TERMINATES.
  //
  // Verified rather than inferred: a test that asserted "one move failed" would still pass if the surviving
  // state were somehow cyclic. This follows every department's parent chain to a root, and a repetition
  // means a cycle exists.
  public async Task AssertAcyclicAsync()
  {
    await using var context = NewContext();

    var all = await context.Set<Department>()
      .AsNoTracking()
      .ToDictionaryAsync(department => department.Id, department => department.ParentDepartmentId);

    foreach (var start in all.Keys)
    {
      var seen = new HashSet<Guid>();
      var current = (Guid?)start;

      while (current is { } id)
      {
        Assert.True(seen.Add(id), $"A cycle exists in the persisted hierarchy, reached from {start}.");

        current = all.TryGetValue(id, out var parent) ? parent : null;
      }
    }
  }

  public async Task<Guid> InsertEmployeeAsync(
    string employeeNumber,
    Guid? company = null,
    Guid? branch = null,
    bool terminated = false,
    // NAMED ONLY BY THE TESTS THAT COUNT MEMBERS. Every other caller leaves it null and keeps the holding
    // department described below — which is what stops incidental employees from being counted by the
    // `employeeCount` tests that DO care which department a person is in.
    Guid? department = null)
  {
    var employeeId = Guid.NewGuid();
    var status = terminated ? "Terminated" : "Active";
    var reason = terminated ? "Resignation" : "Created";
    var terminationDate = terminated ? "SYSDATETIMEOFFSET()" : "NULL";

    // ---- EVERY EMPLOYEE NEEDS A DEPARTMENT FROM FP-007 PHASE 3.
    //
    // These employees exist to be MANAGERS of the departments under test, so which department they belong
    // to is incidental to every assertion in this file — but the column is NOT NULL and the foreign key is
    // real, so one has to exist. A per-company holding department keeps that incidental fact out of the
    // tests, and reusing one rather than creating a department per employee keeps it from polluting the
    // department counts those tests DO assert on.
    var homeDepartment = department ?? await HoldingDepartmentAsync(company ?? CompanyA);
    var homePosition = await HoldingPositionAsync(company ?? CompanyA);

    await ExecuteAsync($"""
      INSERT INTO [tenant].[Employees]
        ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [PositionId],
         [EmployeeNumber],
         [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [TerminationDate], [Status],
         [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy],
         [ModifiedUtc], [ModifiedBy])
      VALUES
        ('{employeeId}', '{Tenant}', '{company ?? CompanyA}', '{branch ?? BranchA}', '{homeDepartment}',
         '{homePosition}',
         N'{employeeNumber}',
         N'{employeeNumber.ToUpperInvariant()}', N'Person {employeeNumber}', SYSDATETIMEOFFSET(),
         {terminationDate}, N'{status}', N'{reason}', SYSDATETIMEOFFSET(), N'{Actor}',
         SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
      """);

    await ExecuteAsync($"""
      INSERT INTO [tenant].[EmployeeBranchAssignments]
        ([EmployeeBranchAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourceBranchId],
         [DestinationBranchId], [EffectiveFromUtc], [TransferredBy], [ReasonCode], [CreatedUtc], [CreatedBy])
      VALUES
        ('{Guid.NewGuid()}', '{Tenant}', '{company ?? CompanyA}', '{employeeId}', NULL,
         '{branch ?? BranchA}', SYSDATETIMEOFFSET(), N'{Actor}', N'InitialAssignment',
         SYSDATETIMEOFFSET(), N'{Actor}');
      """);

    return employeeId;
  }

  // Written directly rather than through the Employee termination handler: this fixture exists to test
  // departments, and the employee lifecycle is proven in its own suite.
  public Task TerminateEmployeeAsync(Guid employeeId) =>
    ExecuteAsync($"""
      UPDATE [tenant].[Employees]
      SET [Status] = N'Terminated',
          [StatusChangeReasonCode] = N'Resignation',
          [TerminationDate] = SYSDATETIMEOFFSET(),
          [StatusChangedUtc] = SYSDATETIMEOFFSET()
      WHERE [EmployeeId] = '{employeeId}';
      """);

  public async ValueTask DisposeAsync()
  {
    foreach (var graph in graphs)
    {
      await graph.DisposeAsync();
    }

    if (string.IsNullOrEmpty(tenantCatalog))
    {
      return;
    }

    try
    {
      await MasterAsync(
        $"IF DB_ID('{tenantCatalog}') IS NOT NULL BEGIN " +
        $"ALTER DATABASE [{tenantCatalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
        $"DROP DATABASE [{tenantCatalog}]; END");
    }
    catch (SqlException)
    {
      // A disposal problem must not turn a passing test red; the database is disposable either way.
    }
  }

  private TenantDbContext NewContext()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(ConnectionFor(tenantCatalog))
      .Options;

    return new TenantDbContext(
      options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
      modelContributors: [new HrTenantModelContributor()]);
  }

  private async Task InitializeAsync()
  {
    tenantCatalog = $"SSAS_FP007P2_{token}";

    await MasterAsync($"CREATE DATABASE [{tenantCatalog}]");

    await using (var connection = new SqlConnection(ConnectionFor(tenantCatalog)))
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;

      await using var context = new TenantDbContext(
        options, new FixtureUser(), new FixtureTenant(null), new FixtureClock(),
        modelContributors: [new HrTenantModelContributor()]);

      await context.Database.MigrateAsync();
    }

    await SeedCompanyAsync(CompanyA, "CMPA");
    await SeedCompanyAsync(CompanyB, "CMPB");
    await SeedBranchAsync(BranchA, "BRA", main: true);
    await SeedBranchAsync(BranchB, "BRB", main: false);
  }

  private Task SeedCompanyAsync(Guid companyId, string code) =>
    ExecuteAsync($"""
      INSERT INTO [tenant].[Companies]
        ([CompanyId], [TenantId], [CompanyCode], [NormalizedCompanyCode], [CompanyName],
         [BaseCurrencyCode], [Status], [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy],
         [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
      VALUES
        ('{companyId}', '{Tenant}', N'{code}', N'{code}', N'Company {code}',
         'SAR', N'Active', N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
         SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
      """);

  private Task SeedBranchAsync(Guid branchId, string code, bool main) =>
    ExecuteAsync($"""
      INSERT INTO [tenant].[Branches]
        ([BranchId], [TenantId], [BranchCode], [NormalizedBranchCode], [BranchName],
         [IsMainBranch], [IsActive], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
      VALUES
        ('{branchId}', '{Tenant}', N'{code}', N'{code}', N'Branch {code}',
         {(main ? 1 : 0)}, 1, SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
      """);

  public async Task<int> ScalarAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt32(
      await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  // Null when the row does not exist, which is what makes the holding-department lookup a create-if-absent
  // rather than a create-every-time.
  private async Task<Guid?> ScalarGuidAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;

    var value = await command.ExecuteScalarAsync();

    return value is Guid id ? id : null;
  }

  private async Task ExecuteAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
  }

  private static async Task MasterAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor("master"));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    await command.ExecuteNonQueryAsync();
  }

  private static string Configured() =>
    IntegrationSqlEnvironment.BaseConnectionString;

  private static string ConnectionFor(string catalog) =>
    new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

  internal sealed class FixtureUser : ICurrentUser
  {
    public string? UserId => Actor;

    public string? UserName => Actor;

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  internal sealed class FixtureTenant(Guid? tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  internal sealed class FixtureClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}

// ONE INDEPENDENT APPLICATION GRAPH: its own context, its own connection, its own transactions.
internal sealed class DepartmentGraph : IAsyncDisposable
{
  private readonly TenantDbContext context;
  private readonly SingleContextAccessor accessor;
  private readonly SingleContextUnitOfWork unitOfWork;
  private readonly DepartmentRepository repository;
  private readonly DepartmentScopeResolver scope;
  private readonly SqlServerDepartmentHierarchyLock hierarchyLock;

  private readonly IReadOnlyList<Guid> branches;

  private readonly bool canViewEmployees;

  public DepartmentGraph(
    string connectionString,
    Guid tenantId,
    Guid company,
    Guid otherCompany,
    IReadOnlyList<Guid>? visibleBranches = null,
    bool canViewEmployees = true)
  {
    branches = visibleBranches ?? [];
    this.canViewEmployees = canViewEmployees;

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connectionString)
      .Options;

    // ---- THE COMPANY WRITE BOUNDARY IS REAL AND IS SUPPLIED WITH A TRUSTED ANSWER.
    //
    // Department is `ICompanyOwnedEntity`, so `ApplyCompanyRulesAsync` refuses every save unless a
    // `ICompanyWriteAuthorizer` is present — the production guard, and it fires here exactly as it would in
    // the Host. What is stubbed is only WHICH company that authority returns; the stamping, the
    // "CompanyId cannot change after creation" rule and the cross-company refusal are all still Platform's
    // own code running for real against these writes.
    context = new TenantDbContext(
      options,
      new DepartmentAppFixture.FixtureUser(),
      new DepartmentAppFixture.FixtureTenant(tenantId),
      new DepartmentAppFixture.FixtureClock(),
      companyAuthorizer: new TrustedCompany(company),
      modelContributors: [new HrTenantModelContributor()]);

    accessor = new SingleContextAccessor(context);
    unitOfWork = new SingleContextUnitOfWork(context);
    repository = new DepartmentRepository(accessor);

    // THE REAL RESOLVER, over stubbed Platform authorities. See the note beside the stubs below.
    scope = new DepartmentScopeResolver(
      new StubCompanyAccess(company),
      new StubCurrentCompany(company),
      new DepartmentAppFixture.FixtureTenant(tenantId),
      new StubCurrentTenantUser(),
      new DepartmentUser(canViewEmployees));
    hierarchyLock = new SqlServerDepartmentHierarchyLock(accessor);

    Company = company;
    OtherCompany = otherCompany;
    TenantId = tenantId;
  }

  public Guid Company { get; }

  public Guid OtherCompany { get; }

  public Guid TenantId { get; }

  public CreateDepartmentCommandHandler Create() => new(
    repository, scope, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureUser(),
    new DepartmentAppFixture.FixtureClock());

  public UpdateDepartmentCommandHandler Update() => new(
    repository, scope, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureClock());

  public ChangeDepartmentParentCommandHandler ChangeParent() => new(
    repository, scope, hierarchyLock, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureClock());

  public MoveDepartmentToRootCommandHandler MoveToRoot() => new(
    repository, scope, hierarchyLock, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureClock());

  public DeactivateDepartmentCommandHandler Deactivate() => new(
    repository, scope, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureUser(),
    new DepartmentAppFixture.FixtureClock());

  public ReactivateDepartmentCommandHandler Reactivate() => new(
    repository, scope, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureUser(),
    new DepartmentAppFixture.FixtureClock());

  public AssignDepartmentManagerCommandHandler AssignManager() => new(
    repository, new EmployeeRepository(accessor), scope, unitOfWork,
    new DepartmentAppFixture.FixtureTenant(TenantId),
    new DepartmentAppFixture.FixtureUser(),
    new DepartmentAppFixture.FixtureClock());

  public ClearDepartmentManagerCommandHandler ClearManager() => new(
    repository, scope, unitOfWork, new DepartmentAppFixture.FixtureTenant(TenantId));

  // The employee half of the read is REAL too: the real employee scope resolver over the same stubbed
  // Platform authorities, and the real employee read service. That is what makes the manager-disclosure
  // tests mean something — an undisclosed manager is undisclosed because the employee scope said so.
  // ---- A DIRECT SEAM ONTO THIS GRAPH'S OWN CONTEXT AND UNIT OF WORK.
  //
  // Deliberately narrow, and deliberately NOT routed through a handler. It exists so the fixture's
  // translation parity with `TenantUnitOfWork` can be proven for what it is — a property of the unit of
  // work — rather than inferred from a handler that may or may not reach the failing branch.
  //
  // `AssignDepartmentManagerCommandHandler` reads before it writes and REASSIGNS when a row already
  // exists, so no sequence of handler calls can be made to attempt a duplicate insert on demand: the
  // primary-key branch is reachable only through a genuine interleave. This performs the same save the
  // handler would have performed, without needing to win a race to do it.
  public async Task<Result<int>> SaveManagerDirectlyAsync(
    DepartmentManager manager, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(manager);

    context.Set<DepartmentManager>().Add(manager);

    return await unitOfWork.SaveChangesAsync(cancellationToken);
  }

  public GetDepartmentQueryHandler Get() => new(
    scope,
    new DepartmentReadService(accessor),
    new SSAS.HR.Application.Employees.Reads.EmployeeScopeResolver(
      new StubCompanyAccess(Company),
      new StubBranchAccess(branches),
      new StubCurrentBranchResolver(branches[0]),
      new StubCurrentCompany(Company),
      new DepartmentAppFixture.FixtureTenant(TenantId),
      new StubCurrentTenantUser(),
      new DepartmentUser(canViewEmployees)),
    new EmployeeReadService(accessor));

  // ---- THE REAL WIRE COMPOSER, over this graph's own context (FP-007 employeeCount).
  //
  // `DepartmentCompositionServices` is the type the HTTP surface resolves, not a re-implementation of it,
  // so these tests prove the SHIPPED composition — including that an unscoped caller gets null rather than
  // a number — against a real database rather than against a stub that could agree with a wrong answer.
  public SSAS.HR.API.Departments.DepartmentCompositionServices EmployeeCounts() => new(
    new SSAS.HR.Application.Employees.Reads.EmployeeScopeResolver(
      new StubCompanyAccess(Company),
      new StubBranchAccess(branches),
      new StubCurrentBranchResolver(branches[0]),
      new StubCurrentCompany(Company),
      new DepartmentAppFixture.FixtureTenant(TenantId),
      new StubCurrentTenantUser(),
      new DepartmentUser(canViewEmployees)),
    new EmployeeReadService(accessor));

  public SearchDepartmentsQueryHandler Search() => new(scope, new DepartmentReadService(accessor));

  public GetDepartmentChildrenQueryHandler Children() => new(scope, new DepartmentReadService(accessor));

  public async ValueTask DisposeAsync() => await context.DisposeAsync();

  // The one context this graph owns. Real repository, real read service, real lock — all over this.
  private sealed class SingleContextAccessor(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  // A minimal unit of work over that same context. Not the production `TenantUnitOfWork` — which needs a
  // provider and a dispatcher — but it opens a REAL EF transaction on the REAL connection, which is what
  // the hierarchy lock enlists in and what the concurrency proof depends on.
  //
  // ================================================================================================
  // IT MIRRORS `TenantUnitOfWork`'S FAILURE TRANSLATION EXACTLY, AND MUST BE KEPT IN SYNC WITH IT.
  // ================================================================================================
  //
  // Same three catches, same order, same error instances. That is not tidiness — it is the double's ONLY
  // justification. A substitute that translates fewer failures than the type it stands in for turns a
  // production `Result` into a test-only exception, and every test above it then asserts against behaviour
  // the Host does not have.
  //
  // That is exactly what happened: this class caught `DbUpdateConcurrencyException` alone, so when two
  // concurrent manager assignments raced to INSERT and the loser hit `PK_DepartmentManagers` instead of the
  // rowversion check, the `DbUpdateException` escaped here — while production, which already translates
  // 2601/2627, would have returned a `Result`. The gap was in the double, never in the product.
  //
  // ---- WHY THE PRODUCTION ERROR INSTANCES RATHER THAN DEPARTMENT-LOCAL EQUIVALENTS.
  //
  // `IdentityAccessErrors.*` is what a handler actually receives from `TenantUnitOfWork`, so returning
  // anything else here — however similar in meaning — would make these tests agree with a contract nothing
  // in production implements. The two codes are DELIBERATELY DISTINGUISHABLE at the Result layer:
  // production distinguishes them, and FP-007 shipped against that. They converge only at the HTTP
  // boundary, where both map to 409.
  private sealed class SingleContextUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
  {
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      try
      {
        return Result.Success(await context.SaveChangesAsync(cancellationToken));
      }
      catch (DbUpdateConcurrencyException)
      {
        // The database refused a stale token. Surfaced as the named business refusal rather than as an
        // exception crossing the application boundary.
        return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.ConcurrencyConflict);
      }
      catch (DbUpdateException exception)
        when (exception.InnerException is SqlException { Number: 2601 or 2627 })
      {
        // 2627 primary key, 2601 unique index. The constraint had the last word — which for the department
        // manager association is the DESIGN: the primary key on DepartmentId is what makes a second row
        // unrepresentable, so a losing INSERT arriving here is the invariant holding, not failing.
        return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.UniqueConstraintViolation);
      }
      catch (DbUpdateException)
      {
        return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.WriteFailure);
      }
    }

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      new EfTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(
      Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : ITransaction
    {
      public Task CommitAsync(CancellationToken cancellationToken = default) =>
        transaction.CommitAsync(cancellationToken);

      public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        transaction.RollbackAsync(cancellationToken);

      public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
  }

  // ---- ONLY THE PLATFORM AUTHORITIES ARE STUBBED. THE RESOLVER IS REAL.
  //
  // `DepartmentReadScope` cannot be constructed outside `SSAS.HR.Application` — private constructor,
  // internal factory, one call site — which is the guarantee that no read can fabricate a scope. That
  // guarantee holds against this test too, and rather than weakening it with `InternalsVisibleTo` the graph
  // composes the REAL `DepartmentScopeResolver` and stubs what sits behind it.
  //
  // So the permission checks in these tests are the real ones: the stub user below holds exactly the four
  // department permissions, and the company access stub grants one company and refuses the rest. What is
  // NOT re-proven here is which users the Platform resolvers would give those answers for — that is
  // Platform's own suite, and the resolver's own decisions are covered in HR.Tests.
  private sealed class StubCompanyAccess(Guid permitted)
    : SSAS.BuildingBlocks.Tenancy.Companies.ITenantCompanyAccessResolver
  {
    public Task<Result<IReadOnlyList<SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary>>>
      GetPermittedCompaniesAsync(
        Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(
        Result.Success<IReadOnlyList<SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary>>(
          [new SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary(permitted, "CODE", "Name")]));

    public Task<Result> AuthorizeCompanyAsync(
      Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(companyId == permitted
        ? Result.Success()
        : Result.Failure(new Error("Company.Denied", "Denied.")));
  }

  private sealed class StubCurrentCompany(Guid companyId) : ICurrentCompany
  {
    public Guid? CompanyId => companyId;
  }

  // The trusted answer the write boundary stamps from. One company per graph, which is what makes the
  // cross-company refusals in these tests mean something: a graph scoped to CompanyA genuinely cannot write
  // a CompanyB row, because Platform's own boundary refuses it rather than because a stub said no.
  private sealed class TrustedCompany(Guid companyId)
    : SSAS.Platform.Application.Companies.ICompanyWriteAuthorizer
  {
    public Task<Result<Guid>> AuthorizeCurrentCompanyAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success(companyId));
  }

  private sealed class StubCurrentTenantUser : SSAS.BuildingBlocks.Tenancy.ICurrentTenantUser
  {
    public long? TenantUserId => 1;
  }

  // ---- THE BRANCH AUTHORITY BEHIND THE MANAGER DISCLOSURE.
  //
  // Which branches this caller may see employees in. A graph built with a NARROWER set than the branch its
  // manager works in is how the "assigned but undisclosed" path is proven — the employee scope genuinely
  // excludes them rather than a stub deciding to hide them.
  private sealed class StubBranchAccess(IReadOnlyList<Guid> permitted)
    : SSAS.BuildingBlocks.Tenancy.Branches.ITenantBranchAccessResolver
  {
    public Task<Result<IReadOnlyList<SSAS.BuildingBlocks.Tenancy.Branches.BranchAccessSummary>>>
      GetPermittedBranchesAsync(
        Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(
        Result.Success<IReadOnlyList<SSAS.BuildingBlocks.Tenancy.Branches.BranchAccessSummary>>(
          permitted
            .Select(id => new SSAS.BuildingBlocks.Tenancy.Branches.BranchAccessSummary(
              id, "BR", "Branch", false))
            .ToArray()));

    public Task<Result> AuthorizeBranchAsync(
      Guid tenantId, long tenantUserId, Guid branchId, CancellationToken cancellationToken = default) =>
      Task.FromResult(permitted.Contains(branchId)
        ? Result.Success()
        : Result.Failure(new Error("Branch.Denied", "Denied.")));
  }

  private sealed class StubCurrentBranchResolver(Guid branchId)
    : SSAS.BuildingBlocks.Tenancy.Branches.ICurrentBranchResolver
  {
    public Task<Result<Guid>> ResolveCurrentBranchAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success(branchId));
  }

  // The four department permissions, and — unless a test says otherwise — `HR.Employees.View` as well.
  // Never `Platform.Tenant.Administer`: these tests must not be able to pass on administrator authority.
  //
  // ---- WHY THE EMPLOYEE PERMISSION IS HERE AT ALL, AND WHY IT IS OPTIONAL.
  //
  // Reading a department resolves its MANAGER through the employee read scope, so a caller holding only the
  // four department permissions is told a manager exists and nothing more. That is correct product
  // behaviour — and it is also, silently, the answer every test got before this parameter existed, which
  // made two different reasons for redaction indistinguishable:
  //
  //   * not authorized for employees at all      → undisclosed
  //   * authorized, but the manager is elsewhere → undisclosed
  //
  // A branch-scope test that redacts for the FIRST reason proves nothing about branch scope. So the default
  // grants the permission — making branch scope the only thing left that can redact — and a test wanting to
  // prove the permission gate itself asks for a caller without it.
  private sealed class DepartmentUser(bool canViewEmployees = true) : ICurrentUser
  {
    public string? UserId => "department-phase2-tests";

    public string? UserName => "department-phase2-tests";

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions =>
      canViewEmployees
        ?
        [
          SSAS.HR.Application.Permissions.HrPermissionNames.ViewDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.CreateDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.UpdateDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.DeactivateDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.ViewEmployees
        ]
        :
        [
          SSAS.HR.Application.Permissions.HrPermissionNames.ViewDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.CreateDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.UpdateDepartments,
          SSAS.HR.Application.Permissions.HrPermissionNames.DeactivateDepartments
        ];
  }
}
