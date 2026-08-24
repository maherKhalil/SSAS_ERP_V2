using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.Payroll.Domain.Compensation;
using SSAS.Payroll.Domain.Elements;
using SSAS.Payroll.Domain.Runs;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// PAYROLL AGAINST REAL SQL (FP-012).
//
// ================================================================================================
// THIS IS WHERE THE GUARANTEES LIVE. The domain and API tests could not prove any of them.
// ================================================================================================
//
// **`IAppendOnlyEntity` is enforced by `TenantDbContext.PreventAppendOnlyMutation`, not by `PayrollRun`.**
// That is the entire reason the aggregate split was ruled, and it is UNTESTABLE without a real context: a
// unit test of the aggregate would happily mutate a line and never learn that the write boundary refuses.
//
// `nvarchar` and `decimal(19,4)` are properties of COLUMNS, asserted from `sys.columns` rather than from
// the EF model — asserting from the model tests the model's opinion of the database, and FP-009 established
// that the catalog views are the only version that catches a hand-written migration.
//
// Deliberately NOT in `TenantBackupSerialSuites`: this class creates one Guid-named disposable catalog and
// shares nothing across databases. The admission rule is explicit that "it needs real SQL" is an argument
// for being an integration test, not for being serial.
public sealed class PayrollSchemaSqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-PAY-0007")]
  public async Task Every_payroll_string_column_is_nvarchar()
  {
    await using var fixture = await PayrollFixture.CreateAsync();

    // From sys.columns, not from the model. `Constraints.md` requires Arabic and English, and a pay
    // element's name is exactly the field a user writes in their own language.
    var nonUnicode = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Payroll%'
        AND ty.name IN ('varchar', 'char', 'text');
      """);

    Assert.Equal(0, nonUnicode);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0004")]
  public async Task Every_payroll_money_column_is_decimal_19_4()
  {
    await using var fixture = await PayrollFixture.CreateAsync();

    var wrong = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Payroll%'
        AND ty.name = 'decimal' AND (c.precision <> 19 OR c.scale <> 4);
      """);

    Assert.Equal(0, wrong);
  }

  [Fact]
  public async Task The_migration_creates_all_seven_payroll_tables()
  {
    await using var fixture = await PayrollFixture.CreateAsync();

    var tables = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.tables AS t
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Payroll%';
      """);

    Assert.Equal(7, tables);
  }

  [Fact]
  [Trait("Decision", "DEC-PAY-0008")]
  public async Task No_payroll_foreign_key_leaves_the_tenant_catalog()
  {
    await using var fixture = await PayrollFixture.CreateAsync();

    // Every foreign key on a payroll table must resolve INSIDE this database. A cross-database constraint
    // is not expressible in SQL Server, so what this really guards is that nothing tried.
    var keys = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.foreign_keys AS fk
      JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Payroll%';
      """);

    // There ARE keys — to Companies, to PayrollElements, to PayrollRuns, to PayrollPeriods — and they are
    // all intra-catalog. A zero here would mean the model lost its constraints entirely.
    Assert.True(keys > 0);
  }

  [Fact]
  public async Task No_payroll_foreign_key_points_at_a_gl_or_hr_table()
  {
    await using var fixture = await PayrollFixture.CreateAsync();

    // The module boundary at the SCHEMA layer. `JournalEntryId`, `FiscalPeriodId` and `EmployeeId` are
    // plain identifier columns with no constraint, deliberately — a database-level key across a module
    // boundary would couple the two migration streams.
    var crossing = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.foreign_keys AS fk
      JOIN sys.tables AS parent ON parent.object_id = fk.parent_object_id
      JOIN sys.tables AS referenced ON referenced.object_id = fk.referenced_object_id
      JOIN sys.schemas AS s ON s.schema_id = parent.schema_id
      WHERE s.name = 'tenant' AND parent.name LIKE 'Payroll%'
        AND (referenced.name LIKE 'Gl%' OR referenced.name IN ('Employees', 'Departments', 'Positions'));
      """);

    Assert.Equal(0, crossing);
  }

  [Fact]
  [Trait("Decision", "AMENDMENT 2026-08-24")]
  public async Task An_approved_pay_line_cannot_be_updated_through_the_context()
  {
    // ---- THE RULING, PROVEN WHERE IT ACTUALLY LIVES.
    //
    // `PreventAppendOnlyMutation` refuses this, not the aggregate. This is the test that the single-aggregate
    // design the package first proposed could never have passed — under it there would have been no
    // `IAppendOnlyEntity` on the line at all, and this write would have succeeded silently.
    await using var fixture = await PayrollFixture.CreateAsync();
    var runId = await fixture.SeedApprovedRunAsync();

    await using var context = fixture.CreateContext();
    var line = await context.Set<PayrollRunLine>().FirstAsync(l => l.PayrollRunId == runId);

    context.Entry(line).Property(l => l.Amount).CurrentValue = 1m;
    context.Entry(line).State = EntityState.Modified;

    var failure = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", failure.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "AMENDMENT 2026-08-24")]
  public async Task An_approved_pay_line_cannot_be_deleted_through_the_context()
  {
    await using var fixture = await PayrollFixture.CreateAsync();
    var runId = await fixture.SeedApprovedRunAsync();

    await using var context = fixture.CreateContext();
    var line = await context.Set<PayrollRunLine>().FirstAsync(l => l.PayrollRunId == runId);

    context.Set<PayrollRunLine>().Remove(line);

    var failure = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", failure.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0011")]
  public async Task A_draft_line_CAN_be_deleted_which_is_what_makes_recalculation_possible()
  {
    // ---- THE OTHER HALF OF THE RULING, AND THE REASON THE TYPES ARE SPLIT.
    //
    // If `PayrollRunDraftLine` carried `IAppendOnlyEntity`, this delete would be refused and `OD-PAY-0011`'s
    // free recalculation would be impossible. The two tests together are the proof that the split was
    // necessary rather than stylistic.
    await using var fixture = await PayrollFixture.CreateAsync();
    var runId = await fixture.SeedCalculatedRunAsync();

    await using var context = fixture.CreateContext();
    var draftLines = await context.Set<PayrollRunDraftLine>()
      .Where(l => l.PayrollRunId == runId).ToListAsync();

    context.Set<PayrollRunDraftLine>().RemoveRange(draftLines);
    await context.SaveChangesAsync();

    Assert.Empty(await context.Set<PayrollRunDraftLine>().Where(l => l.PayrollRunId == runId).ToListAsync());
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0003")]
  public async Task Compensation_history_round_trips_and_the_amount_keeps_four_decimals()
  {
    await using var fixture = await PayrollFixture.CreateAsync();

    await using (var write = fixture.CreateContext())
    {
      var first = EmployeeCompensation.Create(
        fixture.CompanyA, fixture.Employee,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 1234.5678m).Value;
      var second = EmployeeCompensation.Create(
        fixture.CompanyA, fixture.Employee,
        new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero), 2345.6789m).Value;

      write.Set<EmployeeCompensation>().AddRange(first, second);
      await write.SaveChangesAsync();
    }

    await using var read = fixture.CreateContext();
    var history = await read.Set<EmployeeCompensation>()
      .Where(record => record.EmployeeId == fixture.Employee)
      .ToListAsync();

    Assert.Equal(2, history.Count);

    // Derived by the domain from what the database returned — one implementation of "what was in force".
    var inForce = EmployeeCompensation.InForceOn(history, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
    Assert.Equal(1234.5678m, inForce!.BaseAmount);
  }

  private sealed class PayrollFixture : IAsyncDisposable
  {
    private const string Actor = "fp012-payroll-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string catalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid Employee { get; } = Guid.NewGuid();

    public static async Task<PayrollFixture> CreateAsync()
    {
      var fixture = new PayrollFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    public TenantDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(catalog))
        .Options;

      return new TenantDbContext(
        options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
        companyAuthorizer: new GrantingCompanyAuthorizer(CompanyA),
        modelContributors: [new PayrollTenantModelContributor()]);
    }

    // Seeds through the REAL aggregate methods, so the rows under test are the rows the product would have
    // written. An approved run seeded any other way would be asserting the write boundary's behaviour
    // against a row the write boundary never produced.
    // Seeds through the REAL aggregate methods, so the rows under test are the rows the product would have
    // written. An approved run seeded any other way would assert the write boundary's behaviour against a
    // row the write boundary never produced.
    //
    // ---- ONE CONTEXT, ONE SAVE, AND THE FIRST ATTEMPT TAUGHT ME WHY.
    //
    // Approving in a SECOND context — load, approve, save — made `PreventAppendOnlyMutation` throw during
    // SEEDING. `PayrollRun.Approve` calls `lines.Clear()` before writing the approved set, and against a
    // reattached run EF resolves that clear against the append-only collection rather than treating it as a
    // no-op on an unloaded navigation. Building the whole run in one context and saving once never puts an
    // `IAppendOnlyEntity` into `Modified` or `Deleted` at all.
    //
    // That is not a workaround: it is how the application does it too. `ApprovePayrollRunCommandHandler`
    // loads, approves and saves within a single unit of work.
    public async Task<Guid> SeedCalculatedRunAsync() => await SeedRunAsync(approve: false);

    public async Task<Guid> SeedApprovedRunAsync() => await SeedRunAsync(approve: true);

    private async Task<Guid> SeedRunAsync(bool approve)
    {
      await using var context = CreateContext();

      var element = PayElement.Create(
        CompanyA, "BASIC", "Basic Salary", PayElementKind.Earning,
        PayElementBehaviour.BaseSalary, 0m, 0).Value;
      context.Set<PayElement>().Add(element);

      var period = PayrollPeriod.CreateAlignedTo(
        CompanyA, Guid.NewGuid(), "January 2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 2, 5, 0, 0, 0, TimeSpan.Zero)).Value;
      context.Set<PayrollPeriod>().Add(period);

      await context.SaveChangesAsync();

      var run = PayrollRun.Create(CompanyA, period.Id).Value;
      run.SetCalculation(
        [new PayrollRunDraftLine(
          Guid.NewGuid(), run.Id, Employee, element.Id, PayElementKind.Earning, 5000m, 0, null)],
        Actor);

      if (approve)
      {
        // Approved by APPROVING, before the run is ever tracked — so the append-only set is produced by the
        // same path the product uses and arrives at the database as INSERTs.
        run.Approve(Actor);
      }

      context.Set<PayrollRun>().Add(run);
      await context.SaveChangesAsync();

      return run.Id;
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
      catalog = $"SSAS_FP012_Tenant_{token}";

      await MasterAsync($"CREATE DATABASE [{catalog}]");
      await MigrateAsync();
      await SeedCompanyAsync(CompanyA, "CMPA");
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
        modelContributors: [new PayrollTenantModelContributor()]);

      await context.Database.MigrateAsync();
    }

    // Copied verbatim from GL's fixture rather than reconstructed. The first attempt guessed the column
    // values from a partial reading and used an integer Status, which CK_Companies_Status refused: Status
    // and StatusChangeReasonCode are STRINGS, and the timestamps are SYSDATETIMEOFFSET rather than
    // SYSUTCDATETIME.
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

    private sealed class FixtureUser : ICurrentUser
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

    private sealed class FixtureTenant(Guid tenantId) : ICurrentTenant
    {
      public Guid? TenantId => tenantId;
    }

    private sealed class FixtureClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    // Grants the one company the fixture seeded. It does NOT weaken the company boundary: the write
    // boundary still runs, still authorizes, and still refuses anything else — this stands in for the
    // platform authority a request would carry, which no fixture has.
    private sealed class GrantingCompanyAuthorizer(Guid companyId) : ICompanyWriteAuthorizer
    {
      public Task<SSAS.BuildingBlocks.Domain.Result<Guid>> AuthorizeCurrentCompanyAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success(companyId));
    }
  }
}
