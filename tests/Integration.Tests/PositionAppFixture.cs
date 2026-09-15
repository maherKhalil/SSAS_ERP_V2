using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// THE HARNESS FOR THE POSITION APPLICATION PROOFS (FP-008 Phase 2).
//
// ================================================================================================
// WHAT IS REAL HERE, AND WHAT IS DELIBERATELY NOT.
// ================================================================================================
//
// REAL: the tenant database and its full migration chain, the real repositories, the real handlers, the
// real read services, the real `PositionScopeResolver`, and a genuine EF transaction per operation. The
// dependent-refusal proofs and the concurrency proofs are about exactly those.
//
// STUBBED: only the Platform authorities BEHIND the resolver — which companies this caller may reach, and
// which company the write boundary stamps. The resolver itself is the production one, so the permission
// checks in these tests are the real checks. `PositionReadScope` cannot be constructed outside
// `SSAS.HR.Application` — private constructor, internal factory, one call site — and rather than weakening
// that with `InternalsVisibleTo`, the graph composes the real resolver and stubs what sits behind it.
//
// ---- THERE ARE NO BRANCHES IN THIS FIXTURE, AND THAT IS THE POINT (DEC-POS-0020, BRULE-POS-0003).
//
// `DepartmentAppFixture` seeds two branches because its manager disclosure runs through the branch-scoped
// employee path. Nothing in the position surface has a branch dimension, so this fixture has no branch to
// seed and no branch authority to stub — an absence that is easier to verify than an unused stub.
//
// Each `Graph()` gets its OWN context and connection, which is what lets two of them genuinely contend
// inside SQL Server rather than being serialised by a shared client.
internal sealed class PositionAppFixture : IAsyncDisposable
{
  private const string Actor = "position-phase2-tests";

  private readonly string token = Guid.NewGuid().ToString("N")[..12];
  private readonly List<PositionGraph> graphs = [];

  private string tenantCatalog = string.Empty;

  public Guid Tenant { get; } = Guid.NewGuid();

  public Guid CompanyA { get; } = Guid.NewGuid();

  public Guid CompanyB { get; } = Guid.NewGuid();

  public static async Task<PositionAppFixture> CreateAsync()
  {
    var fixture = new PositionAppFixture();
    await fixture.InitializeAsync();
    return fixture;
  }

  // A COMPLETE, INDEPENDENT PRODUCTION GRAPH. Separate context, separate connection, separate transactions.
  //
  // `permissions` defaults to all twelve. A test proving a permission gate asks for a narrower set, which is
  // the only way to tell "refused because unauthorized" apart from "refused because absent".
  public PositionGraph Graph(Guid? company = null, IReadOnlyCollection<string>? permissions = null)
  {
    var graph = new PositionGraph(
      ConnectionFor(tenantCatalog), Tenant, company ?? CompanyA, permissions);

    graphs.Add(graph);
    return graph;
  }

  // Created through the REAL handlers, so the seeded rows are the rows the application would write.
  public async Task<Guid> CreatePositionAsync(
    string code, string title, Guid? jobGradeId = null, Guid? company = null)
  {
    await using var graph = new PositionGraph(
      ConnectionFor(tenantCatalog), Tenant, company ?? CompanyA);

    var created = await graph.CreatePosition().HandleAsync(
      new CreatePositionCommand(company ?? CompanyA, code, title, jobGradeId));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    return created.Value;
  }

  public async Task<Guid> CreateJobGradeAsync(
    string code, string name, int rankOrder, Guid? salaryGradeId = null, Guid? company = null)
  {
    await using var graph = new PositionGraph(
      ConnectionFor(tenantCatalog), Tenant, company ?? CompanyA);

    var created = await graph.CreateJobGrade().HandleAsync(
      new CreateJobGradeCommand(company ?? CompanyA, code, name, rankOrder, salaryGradeId));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    return created.Value;
  }

  public async Task<Guid> CreateSalaryGradeAsync(
    string code,
    string name,
    int rankOrder,
    decimal? minimum = null,
    decimal? midpoint = null,
    decimal? maximum = null,
    Guid? company = null)
  {
    await using var graph = new PositionGraph(
      ConnectionFor(tenantCatalog), Tenant, company ?? CompanyA);

    var created = await graph.CreateSalaryGrade().HandleAsync(
      new CreateSalaryGradeCommand(company ?? CompanyA, code, name, rankOrder, minimum, midpoint, maximum));

    Assert.True(created.IsSuccess, created.IsFailure ? created.Error.Code : null);

    return created.Value;
  }

  // The stored token, read outside any graph. Every mutation carries one (`NFR-POS-0302`), and a test that
  // wants to prove the STALE path needs the CURRENT one first.
  public async Task<byte[]> RowVersionAsync(string table, string keyColumn, Guid id)
  {
    await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText =
      $"SELECT [RowVersion] FROM [tenant].[{table}] WHERE [{keyColumn}] = '{id}'";

    return (byte[])(await command.ExecuteScalarAsync())!;
  }

  public async Task<int> ScalarAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt32(
      await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

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

  private async Task InitializeAsync()
  {
    tenantCatalog = $"SSAS_FP008P2_{token}";

    await MasterAsync($"CREATE DATABASE [{tenantCatalog}]");

    await using (var connection = new SqlConnection(ConnectionFor(tenantCatalog)))
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;

      await using var context = new TenantDbContext(
        options, new FixtureUser([]), new FixtureTenant(Tenant), new FixtureClock(),
        modelContributors: [new HrTenantModelContributor()]);

      await context.Database.MigrateAsync();
    }

    await SeedCompanyAsync(CompanyA, "CMPA");
    await SeedCompanyAsync(CompanyB, "CMPB");
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

  // ---- THE TWELVE, AND NEVER `Platform.Tenant.Administer`.
  //
  // These tests must not be able to pass on administrator authority: `ADR-025` decision 8 makes company
  // scope and functional permission independent, and a fixture that granted administration would let a
  // missing permission check go unnoticed.
  internal static IReadOnlyCollection<string> AllPositionPermissions =>
  [
    HrPermissionNames.ViewPositions,
    HrPermissionNames.CreatePositions,
    HrPermissionNames.UpdatePositions,
    HrPermissionNames.DeactivatePositions,
    HrPermissionNames.ViewJobGrades,
    HrPermissionNames.CreateJobGrades,
    HrPermissionNames.UpdateJobGrades,
    HrPermissionNames.DeactivateJobGrades,
    HrPermissionNames.ViewSalaryGrades,
    HrPermissionNames.CreateSalaryGrades,
    HrPermissionNames.UpdateSalaryGrades,
    HrPermissionNames.DeactivateSalaryGrades
  ];

  internal sealed class FixtureUser(IReadOnlyCollection<string> permissions) : ICurrentUser
  {
    public string? UserId => Actor;

    public string? UserName => Actor;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => permissions;
  }

  // Carries a real tenant. See `PositionSchemaSqlServerTests.FixtureTenant` for why a null one throws out of
  // the tenant filter before any SQL is sent.
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
internal sealed class PositionGraph : IAsyncDisposable
{
  private readonly TenantDbContext context;
  private readonly SingleContextAccessor accessor;
  private readonly SingleContextUnitOfWork unitOfWork;
  private readonly PositionRepository positions;
  private readonly JobGradeRepository jobGrades;
  private readonly SalaryGradeRepository salaryGrades;
  private readonly PositionScopeResolver scope;
  private readonly PositionAppFixture.FixtureTenant tenant;
  private readonly PositionAppFixture.FixtureUser user;

  public PositionGraph(
    string connectionString,
    Guid tenantId,
    Guid company,
    IReadOnlyCollection<string>? permissions = null)
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connectionString)
      .Options;

    tenant = new PositionAppFixture.FixtureTenant(tenantId);
    user = new PositionAppFixture.FixtureUser(
      permissions ?? PositionAppFixture.AllPositionPermissions);

    // ---- THE COMPANY WRITE BOUNDARY IS REAL AND IS SUPPLIED WITH A TRUSTED ANSWER.
    //
    // Position, JobGrade and SalaryGrade are all `ICompanyOwnedEntity`, so `ApplyCompanyRulesAsync` refuses
    // every save unless a `ICompanyWriteAuthorizer` is present — the production guard, firing here exactly
    // as it would in the Host. What is stubbed is only WHICH company that authority returns; the stamping,
    // the "CompanyId cannot change after creation" rule and the cross-company refusal are all Platform's
    // own code running for real against these writes (`BRULE-POS-0001`).
    context = new TenantDbContext(
      options,
      user,
      tenant,
      new PositionAppFixture.FixtureClock(),
      companyAuthorizer: new TrustedCompany(company),
      modelContributors: [new HrTenantModelContributor()]);

    accessor = new SingleContextAccessor(context);
    unitOfWork = new SingleContextUnitOfWork(context);
    positions = new PositionRepository(accessor);
    jobGrades = new JobGradeRepository(accessor);
    salaryGrades = new SalaryGradeRepository(accessor);

    // THE REAL RESOLVER, over stubbed Platform authorities.
    scope = new PositionScopeResolver(
      new StubCompanyAccess(company),
      new StubCurrentCompany(company),
      tenant,
      new StubCurrentTenantUser(),
      user);

    Company = company;
    TenantId = tenantId;
  }

  public Guid Company { get; }

  public Guid TenantId { get; }

  public CreatePositionCommandHandler CreatePosition() => new(
    positions, jobGrades, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public UpdatePositionCommandHandler UpdatePosition() => new(
    positions, jobGrades, scope, unitOfWork, tenant, new PositionAppFixture.FixtureClock());

  public DeactivatePositionCommandHandler DeactivatePosition() => new(
    positions, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public ReactivatePositionCommandHandler ReactivatePosition() => new(
    positions, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public CreateJobGradeCommandHandler CreateJobGrade() => new(
    jobGrades, salaryGrades, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public UpdateJobGradeCommandHandler UpdateJobGrade() => new(
    jobGrades, salaryGrades, scope, unitOfWork, tenant, new PositionAppFixture.FixtureClock());

  public DeactivateJobGradeCommandHandler DeactivateJobGrade() => new(
    jobGrades, positions, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public ReactivateJobGradeCommandHandler ReactivateJobGrade() => new(
    jobGrades, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public CreateSalaryGradeCommandHandler CreateSalaryGrade() => new(
    salaryGrades, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public UpdateSalaryGradeCommandHandler UpdateSalaryGrade() => new(
    salaryGrades, scope, unitOfWork, tenant, new PositionAppFixture.FixtureClock());

  public DeactivateSalaryGradeCommandHandler DeactivateSalaryGrade() => new(
    salaryGrades, jobGrades, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public ReactivateSalaryGradeCommandHandler ReactivateSalaryGrade() => new(
    salaryGrades, scope, unitOfWork, tenant, user, new PositionAppFixture.FixtureClock());

  public GetPositionQueryHandler GetPosition() => new(scope, new PositionReadService(accessor));

  public SearchPositionsQueryHandler SearchPositions() => new(scope, new PositionReadService(accessor));

  public GetJobGradeQueryHandler GetJobGrade() => new(scope, new JobGradeReadService(accessor));

  public SearchJobGradesQueryHandler SearchJobGrades() => new(scope, new JobGradeReadService(accessor));

  public GetSalaryGradeQueryHandler GetSalaryGrade() => new(scope, new SalaryGradeReadService(accessor));

  public SearchSalaryGradesQueryHandler SearchSalaryGrades() =>
    new(scope, new SalaryGradeReadService(accessor));

  public async ValueTask DisposeAsync() => await context.DisposeAsync();

  // The one context this graph owns. Real repositories and real read services, all over it.
  private sealed class SingleContextAccessor(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  // A minimal unit of work over that same context, mirroring `TenantUnitOfWork`'s failure translation
  // EXACTLY — same three catches, same order, same error instances. That is not tidiness; it is the
  // double's only justification. A substitute that translates fewer failures than the type it stands in for
  // turns a production `Result` into a test-only exception, and every test above it then asserts against
  // behaviour the Host does not have. `DepartmentAppFixture` learned that the hard way and its comment
  // records how; this is the same class, kept in step with it deliberately.
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
        return Result.Failure<int>(SSAS.Platform.Domain.IdentityAccessErrors.ConcurrencyConflict);
      }
      catch (DbUpdateException exception)
        when (exception.InnerException is SqlException { Number: 2601 or 2627 })
      {
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
  // cross-company refusals mean something: a graph scoped to CompanyA genuinely cannot write a CompanyB row,
  // because Platform's own boundary refuses it rather than because a stub said no.
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
}
