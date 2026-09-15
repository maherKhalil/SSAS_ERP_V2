using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.Attendance.Domain.Leave;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE SHARED ATTENDANCE INTEGRATION FIXTURE (promoted in T-146).
// ==================================================================================================
//
// **Promoted from a private nested class in `AttendanceSchemaSqlServerTests`, exactly as `GlFixture` was in
// T-142** — a second fixture creating catalogs the same way is `DEC-L-080` in test infrastructure, and test
// infrastructure is where duplication is least visible because nobody reviews a fixture twice.
//
// **It seeds a period and a record and NOTHING ELSE.** A caller needing a working calendar or a leave type
// seeds them itself — `AttendanceOverlapChainSqlServerTests` does. **A fixture grows on its THIRD caller,
// not its second**: the second is where a shared fixture is quietly reshaped into a two-caller special case.
internal sealed class AttendanceFixture : IAsyncDisposable
{
  private const string Actor = "fp013-attendance-tests";

  public static readonly DateOnly RecordDate = new(2026, 9, 14);

  private readonly string token = Guid.NewGuid().ToString("N")[..12];

  private string catalog = string.Empty;

  public Guid Tenant { get; } = Guid.NewGuid();

  public Guid CompanyA { get; } = Guid.NewGuid();

  // The second company exists so a scope authorized for ONE of them can be shown not to reach the other
  // (item 233).
  public Guid CompanyB { get; } = Guid.NewGuid();

  public Guid Employee { get; } = Guid.NewGuid();

  public Guid BranchId { get; } = Guid.NewGuid();

  public Guid PeriodId { get; private set; }

  public static async Task<AttendanceFixture> CreateAsync()
  {
    var fixture = new AttendanceFixture();
    await fixture.InitializeAsync();
    return fixture;
  }

  public TenantDbContext CreateContext() => CreateContext(loggerFactory: null);

  // ---- LEAVE, UNDER ONE COMPANY, WITH BOTH KINDS OF TYPE PRESENT (item 233).
  //
  // A SENSITIVE type and an ORDINARY one, each with a request, because the redaction rule discriminates
  // between them: a test arranging only the sensitive one cannot tell a discriminating rule from a
  // blanket one, and the blanket one looks safer so nothing prompts the question.
  // ⚠ `monthOffset` exists because `UX_AttendanceLeaveRequests_Employee_Range_Active` is unique on
  // EMPLOYEE and RANGE and is NOT company-qualified: one person cannot be on leave twice at once,
  // whichever company the request belongs to. The same employee under both companies is what makes the
  // company predicate the only discriminator, so the RANGES move instead of the person.
  public async Task<AttendanceSeededLeave> SeedLeaveAsync(
    Guid company, string prefix, Guid employeeId, int monthOffset = 0)
  {
    await using var context = CreateContext(company);

    var sensitive = LeaveType.Create(
      company, prefix + "-SICK", prefix + " Sick", LeaveBehaviour.PaidFromBalance, true).Value;
    var ordinary = LeaveType.Create(
      company, prefix + "-ANN", prefix + " Annual", LeaveBehaviour.PaidFromBalance, false).Value;
    context.Set<LeaveType>().AddRange(sensitive, ordinary);
    await context.SaveChangesAsync();

    var sensitiveRequest = LeaveRequest.Submit(
      company, employeeId, sensitive.Id,
      new DateOnly(2026, 3, 2).AddMonths(monthOffset),
      new DateOnly(2026, 3, 3).AddMonths(monthOffset), 2m).Value;
    var ordinaryRequest = LeaveRequest.Submit(
      company, employeeId, ordinary.Id,
      new DateOnly(2026, 4, 6).AddMonths(monthOffset),
      new DateOnly(2026, 4, 7).AddMonths(monthOffset), 2m).Value;
    context.Set<LeaveRequest>().AddRange(sensitiveRequest, ordinaryRequest);
    await context.SaveChangesAsync();

    return new AttendanceSeededLeave(sensitive.Id, ordinary.Id, sensitiveRequest.Id, ordinaryRequest.Id);
  }

  // Returns the CONCRETE type deliberately: the point of item 233 is that `AttendanceReadService` had
  // never been constructed. `InternalsVisibleTo("SSAS.Integration.Tests")` has been on this assembly all
  // along -- the seam was opened and nobody walked through it.
  public static AttendanceReadService Reads(TenantDbContext context, IAttendanceScopeResolver resolver) =>
    new(new SingleAttendanceContext(context), resolver);

  // The REAL resolver. `sensitive` decides whether the caller holds `Attendance.Leave.ViewSensitive`,
  // which is the only difference between clauses 1 and 2.
  public AttendanceScopeResolver Resolver(bool sensitive, params Guid[] permitted) =>
    new(
      new GrantingCompanyAccess(permitted),
      new GrantingBranchAccess(BranchId),
      new FixtureTenant(Tenant),
      new FixtureTenantUser(),
      new PermittedUser(sensitive));

  // ⚠ The logger factory is an OPTIONAL seam and it exists for one question: does EF Core itself log a
  // failed command? That cannot be settled by reading our Serilog configuration — it is EF's behaviour,
  // not ours — and it cannot be settled without a real server refusing a real statement.
  public TenantDbContext CreateContext(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
  {
    var builder = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(ConnectionFor(catalog));

    if (loggerFactory is not null)
    {
      builder = builder.UseLoggerFactory(loggerFactory);
    }

    var options = builder.Options;

    return new TenantDbContext(
      options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
      branchAuthorizer: new GrantingBranchAuthorizer(BranchId),
      companyAuthorizer: new GrantingCompanyAuthorizer(CompanyA),
      modelContributors: [new AttendanceTenantModelContributor()]);
  }

  // The company the WRITE boundary will authorize. Seeding under `CompanyB` needs a context authorized for
  // `CompanyB`: `ApplyCompanyRulesAsync` refuses the save otherwise, which is the boundary working.
  public TenantDbContext CreateContext(Guid company)
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(ConnectionFor(catalog))
      .Options;

    return new TenantDbContext(
      options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
      branchAuthorizer: new GrantingBranchAuthorizer(BranchId),
      companyAuthorizer: new GrantingCompanyAuthorizer(company),
      modelContributors: [new AttendanceTenantModelContributor()]);
  }

  // ---- ONE CONTEXT, ONE SAVE (the FP-012 scar).
  //
  // Seeding an append-only row across two contexts made `PreventAppendOnlyMutation` throw during SETUP,
  // which reads as an environment problem rather than as the fixture bug it is. Everything below is built
  // and saved once.
  public async Task<Guid> SeedRecordAsync()
  {
    await using var context = CreateContext();

    var period = AttendancePeriod.Create(
      CompanyA, "September 2026", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)).Value;
    period.TenantId = Tenant;

    var record = AttendanceRecord.Observe(
      CompanyA, period.Id, Employee, RecordDate,
      workedQuantity: 8m, overtimeQuantity: 0m, overtimeTier: null,
      paidAbsenceQuantity: 0m, unpaidAbsenceQuantity: 0m, note: null).Value;

    // The write boundary stamps this in production; the fixture supplies it because no branch context
    // exists here. Stated so nobody reads it as the application's normal path.
    record.BranchId = BranchId;
    record.TenantId = Tenant;

    context.Set<AttendancePeriod>().Add(period);
    context.Set<AttendanceRecord>().Add(record);
    await context.SaveChangesAsync();

    PeriodId = period.Id;
    return record.Id;
  }

  public async Task<int> ScalarAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor(catalog));
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return Convert.ToInt32(
      await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
  }

  private async Task InitializeAsync()
  {
    catalog = $"SSAS_FP013_Tenant_{token}";

    await MasterAsync($"CREATE DATABASE [{catalog}]");
    await MigrateAsync();
    await SeedCompanyAsync(CompanyA, "CMPA");
    await SeedCompanyAsync(CompanyB, "CMPB");
    await SeedBranchAsync();
  }

  private async Task MigrateAsync()
  {
    await using var connection = new SqlConnection(ConnectionFor(catalog));

    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
        TenantPersistenceConstants.MigrationHistoryTable,
        TenantPersistenceConstants.MigrationHistorySchema))
      .Options;

    await using var context = new TenantDbContext(
      options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
      modelContributors: [new AttendanceTenantModelContributor()]);

    await context.Database.MigrateAsync();
  }

  // Copied verbatim from Payroll's fixture, which copied GL's. Status and StatusChangeReasonCode are
  // STRINGS and the timestamps are SYSDATETIMEOFFSET — FP-012's first attempt guessed integers and
  // SYSUTCDATETIME, and `CK_Companies_Status` refused it during setup.
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

  // Attendance is the first module outside HR with a branch foreign key, so this is the first fixture
  // outside HR that has to seed a Branch.
  //
  // ---- THE COLUMN SET WAS GUESSED FIRST, AND SQL SERVER REFUSED IT.
  //
  // The first attempt copied the COMPANY seed's shape — a Status/StatusChangedUtc/StatusChangedBy triple
  // — because the two tables look alike. Branches has `IsActive`, a plain bit, and every one of those
  // three columns is invalid. Eight tests failed during SETUP, which reads as an environment problem
  // rather than as the fixture bug it was.
  //
  // This statement is now copied from `DepartmentAppFixture`, which has been seeding Branches correctly
  // since FP-007 — the same remedy the company seed above records, applied to the same class of mistake
  // one table over.
  private async Task SeedBranchAsync()
  {
    var columns = await ScalarAsync("""
      SELECT COUNT(*) FROM sys.tables AS t
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      WHERE s.name = 'tenant' AND t.name = 'Branches';
      """);

    if (columns == 0)
    {
      return;
    }

    await ExecuteAsync($"""
      INSERT INTO [tenant].[Branches]
        ([BranchId], [TenantId], [BranchCode], [NormalizedBranchCode], [BranchName],
         [IsMainBranch], [IsActive], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
      VALUES
        ('{BranchId}', '{Tenant}', N'BR1', N'BR1', N'Branch One',
         1, 1, SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
      """);
  }

  private async Task ExecuteAsync(string sql)
  {
    await using var connection = new SqlConnection(ConnectionFor(catalog));
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

  private static string ConnectionFor(string name) =>
    IntegrationSqlEnvironment.ForCatalog(name);

  public async ValueTask DisposeAsync()
  {
    if (string.IsNullOrEmpty(catalog))
    {
      return;
    }

    await MasterAsync($"""
      IF DB_ID('{catalog}') IS NOT NULL
      BEGIN
        ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [{catalog}];
      END
      """);
  }

  // The same nested stubs Payroll's fixture carries, and per-file for the same reason: each integration
  // fixture composes the production graph it is testing, and a shared stub would quietly become a fifth
  // opinion about what a request carries.
  private sealed class FixtureUser : ICurrentUser
  {
    public string? UserId => Actor;

    public string? UserName => Actor;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class GrantingCompanyAccess(IReadOnlyList<Guid> permitted)
    : SSAS.BuildingBlocks.Tenancy.Companies.ITenantCompanyAccessResolver
  {
    public Task<SSAS.BuildingBlocks.Domain.Result<IReadOnlyList<
      SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary>>> GetPermittedCompaniesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success<IReadOnlyList<
        SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary>>(
        permitted.Select(id =>
          new SSAS.BuildingBlocks.Tenancy.Companies.CompanyAccessSummary(id, "CODE", "Name")).ToArray()));

    public Task<SSAS.BuildingBlocks.Domain.Result> AuthorizeCompanyAsync(
      Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(permitted.Contains(companyId)
        ? SSAS.BuildingBlocks.Domain.Result.Success()
        : SSAS.BuildingBlocks.Domain.Result.Failure(
          new SSAS.BuildingBlocks.Domain.Error("Company.Denied", "Denied.")));
  }

  private sealed class GrantingBranchAccess(Guid branchId)
    : SSAS.BuildingBlocks.Tenancy.Branches.ITenantBranchAccessResolver
  {
    public Task<SSAS.BuildingBlocks.Domain.Result<IReadOnlyList<
      SSAS.BuildingBlocks.Tenancy.Branches.BranchAccessSummary>>> GetPermittedBranchesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success<IReadOnlyList<
        SSAS.BuildingBlocks.Tenancy.Branches.BranchAccessSummary>>(
        [new SSAS.BuildingBlocks.Tenancy.Branches.BranchAccessSummary(branchId, "BR", "Branch", true)]));

    public Task<SSAS.BuildingBlocks.Domain.Result> AuthorizeBranchAsync(
      Guid tenantId, long tenantUserId, Guid branch, CancellationToken cancellationToken = default) =>
      Task.FromResult(branch == branchId
        ? SSAS.BuildingBlocks.Domain.Result.Success()
        : SSAS.BuildingBlocks.Domain.Result.Failure(
          new SSAS.BuildingBlocks.Domain.Error("Branch.Denied", "Denied.")));
  }

  private sealed class FixtureTenantUser : SSAS.BuildingBlocks.Tenancy.ICurrentTenantUser
  {
    public long? TenantUserId => 42;
  }

  private sealed class PermittedUser(bool sensitive) : ICurrentUser
  {
    public string? UserId => Actor;

    public string? UserName => Actor;

    public string? Email => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => sensitive
      ? [AttendancePermissionNames.ViewLeave, AttendancePermissionNames.ViewSensitiveLeave]
      : [AttendancePermissionNames.ViewLeave];
  }

  private sealed class SingleAttendanceContext(TenantDbContext context)
    : SSAS.BuildingBlocks.Infrastructure.Persistence.ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  private sealed class FixtureTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId => tenantId;
  }

  private sealed class FixtureClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }

  // ---- AND THE BRANCH AUTHORIZER, WHICH THE FIRST ATTEMPT OMITTED AND THE BOUNDARY CAUGHT.
  //
  // `AttendanceRecord` is the first branch-owned entity outside HR, and `TenantDbContext.ApplyBranchRules`
  // refuses a branch-owned write when no authorizer is present: *"A trusted branch context is required to
  // save branch-owned entities."* Setting `record.BranchId` directly is NOT enough, and that is the point
  // — the boundary stamps and authorizes from a TRUSTED source rather than trusting the value on the row.
  //
  // Three tests failed on that before this existed. The machinery working exactly as `OD-ATT-0011`
  // requires is what produced the failure.
  private sealed class GrantingBranchAuthorizer(Guid branchId) : SSAS.Platform.Application.Branches.IBranchWriteAuthorizer
  {
    public Task<SSAS.BuildingBlocks.Domain.Result<Guid>> AuthorizeCurrentBranchAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success(branchId));
  }

  // Grants the one company the fixture seeded. It does NOT weaken the company boundary: the write boundary
  // still runs, still authorizes and still refuses anything else — this stands in for the platform
  // authority a request would carry, which no fixture has.
  private sealed class GrantingCompanyAuthorizer(Guid companyId) : ICompanyWriteAuthorizer
  {
    public Task<SSAS.BuildingBlocks.Domain.Result<Guid>> AuthorizeCurrentCompanyAsync(
      Guid tenantId, CancellationToken cancellationToken = default) =>
      Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success(companyId));
  }
}

internal sealed record AttendanceSeededLeave(
  Guid SensitiveTypeId, Guid OrdinaryTypeId, Guid SensitiveRequestId, Guid OrdinaryRequestId);
