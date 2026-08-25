using Microsoft.Data.SqlClient;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using Microsoft.EntityFrameworkCore;
using SSAS.Attendance.Domain.Calendars;
using SSAS.Attendance.Domain.Periods;
using SSAS.Attendance.Domain.Records;
using SSAS.Attendance.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ================================================================================================
// ATTENDANCE AGAINST REAL SQL (FP-013). THIS IS WHERE THE GUARANTEES LIVE.
// ================================================================================================
//
// **`IAppendOnlyEntity` is enforced by `TenantDbContext.PreventAppendOnlyMutation`, not by
// `AttendanceRecord`.** A unit test of the aggregate would happily mutate a record and never learn that the
// write boundary refuses — which is precisely why `OD-ATT-0012`'s adjustments-never-edits ruling is only
// really proved here.
//
// Column types are asserted from `sys.columns` rather than from the EF model. Asserting from the model tests
// the model's opinion of the database; FP-009 established that the catalog views are the only version that
// catches a hand-written migration.
public sealed class AttendanceSchemaSqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-ATT-0005")]
  public async Task Every_attendance_string_column_is_nvarchar()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    // `Constraints.md` requires Arabic and English, and a leave type's name is exactly the field a user
    // writes in their own language.
    var nonUnicode = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND ty.name IN ('varchar', 'char', 'text');
      """);

    Assert.Equal(0, nonUnicode);
  }

  // ================================================================================================
  // NO MONEY COLUMN EXISTS, ASSERTED POSITIVELY (DEC-ATT-0004, AC-ATT-0010).
  // ================================================================================================
  //
  // Money in this product is `decimal(19,4)` (`ADR-027` d1). **No column in this module uses it**, and that
  // is the module boundary made checkable: Attendance records HOW MUCH, Payroll decides what it is worth.
  //
  // A rule a test can check is a rule; a rule only a reviewer can check is a hope.
  [Fact]
  [Trait("Decision", "DEC-ATT-0004")]
  public async Task No_attendance_column_uses_the_money_type_and_every_quantity_is_decimal_9_2()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var money = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND ty.name = 'decimal' AND c.precision = 19 AND c.scale = 4;
      """);

    Assert.Equal(0, money);

    // And every decimal that IS there is the quantity shape.
    var wrongQuantity = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND ty.name = 'decimal' AND (c.precision <> 9 OR c.scale <> 2);
      """);

    Assert.Equal(0, wrongQuantity);
  }

  // ---- CALENDAR DAYS ARE `date`, NOT `datetimeoffset`.
  //
  // The one place this module departs from the `DateTimeOffset` convention, deliberately: storing a holiday
  // or an attendance date as an instant invites an offset conversion to move it across midnight into the
  // previous day, and every downstream day count would still look plausible.
  [Fact]
  public async Task Calendar_day_columns_are_stored_as_date()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var wrong = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns AS c
      JOIN sys.tables AS t ON t.object_id = c.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%'
        AND c.name IN ('HolidayDate', 'AttendanceDate', 'StartDate', 'EndDate')
        AND ty.name <> 'date';
      """);

    Assert.Equal(0, wrong);
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0006")]
  public async Task No_attendance_foreign_key_crosses_to_a_platform_database()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    // `ADR-022` bars the cross-DATABASE case. Company and Branch live in the TENANT catalog, so those keys
    // are intra-catalog and legal; anything else would have to be a synonym or a linked server, and neither
    // can appear in `sys.foreign_keys` for this catalog. Asserting the keys resolve WITHIN this database is
    // therefore the checkable form of the rule.
    var unresolved = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.foreign_keys AS fk
      JOIN sys.tables AS t ON t.object_id = fk.parent_object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      LEFT JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
      WHERE s.name = 'tenant' AND t.name LIKE 'Attendance%' AND rt.object_id IS NULL;
      """);

    Assert.Equal(0, unresolved);
  }

  // ================================================================================================
  // THE UNIQUE INDEX THAT MUST NOT EXIST (OD-ATT-0012, AC-ATT-0014).
  // ================================================================================================
  //
  // **A second row for the same employee-date IS an adjustment.** The analysis package flagged this as the
  // sharpest coupling in the data model: a unique index chosen from the happy path would silently foreclose
  // the entire correction model, and the failure would appear as a mysterious constraint violation on a
  // legitimate business act.
  //
  // Asserted here because it is an ABSENCE, and absences do not fail on their own.
  [Fact]
  [Trait("Decision", "OD-ATT-0012")]
  public async Task Attendance_records_carry_no_unique_index_on_employee_and_date()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();

    var offending = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.indexes AS i
      JOIN sys.tables AS t ON t.object_id = i.object_id
      JOIN sys.schemas AS s ON s.schema_id = t.schema_id
      WHERE s.name = 'tenant' AND t.name = 'AttendanceRecords' AND i.is_unique = 1
        AND EXISTS (
          SELECT 1 FROM sys.index_columns AS ic
          JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
          WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND c.name = 'AttendanceDate');
      """);

    Assert.Equal(0, offending);
  }

  // ================================================================================================
  // THE APPEND-ONLY REFUSAL, PROVED — AND BOTH STATES, BECAUSE TESTING ONE PROVES HALF OF IT.
  // ================================================================================================
  //
  // `PreventAppendOnlyMutation` refuses `Modified` **or** `Deleted` UNCONDITIONALLY. This is the guarantee
  // the whole `OD-ATT-0012` ruling rests on, and the guarantee that makes REOPENING a period safe: a
  // reopened period permits appending and never editing, by anyone, whatever permission they hold.
  [Fact]
  [Trait("Decision", "DEC-ATT-0009")]
  public async Task An_attendance_record_cannot_be_modified()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    var recordId = await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();
    var record = await context.Set<AttendanceRecord>().FirstAsync(row => row.Id == recordId);

    // Nothing on the type has a public setter except the branch stamp, so the mutation goes through the one
    // door the interface leaves open — which is exactly the door the write boundary watches.
    record.BranchId = Guid.NewGuid();

    await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
  }

  [Fact]
  [Trait("Decision", "DEC-ATT-0009")]
  public async Task An_attendance_record_cannot_be_deleted()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    var recordId = await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();
    var record = await context.Set<AttendanceRecord>().FirstAsync(row => row.Id == recordId);

    context.Set<AttendanceRecord>().Remove(record);

    await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
  }

  // ---- AND AN ADJUSTMENT FOR THE SAME EMPLOYEE-DATE INSERTS CLEANLY.
  //
  // The positive half of the unique-index assertion above: not merely that the index is absent, but that the
  // business act it would have blocked actually works.
  [Fact]
  [Trait("Requirement", "REQ-ATT-0019")]
  public async Task A_second_row_for_the_same_employee_and_date_is_accepted_because_it_is_an_adjustment()
  {
    await using var fixture = await AttendanceFixture.CreateAsync();
    var originalId = await fixture.SeedRecordAsync();

    await using var context = fixture.CreateContext();

    var adjustment = AttendanceRecord.Adjust(
      fixture.CompanyA, fixture.PeriodId, fixture.Employee, AttendanceFixture.RecordDate, originalId,
      workedDelta: -2m, overtimeDelta: 0m, overtimeTier: null,
      paidAbsenceDelta: 0m, unpaidAbsenceDelta: 2m, note: "Two hours were unpaid leave").Value;

    adjustment.BranchId = fixture.BranchId;
    context.Set<AttendanceRecord>().Add(adjustment);

    await context.SaveChangesAsync();

    var rows = await context.Set<AttendanceRecord>()
      .Where(row => row.EmployeeId == fixture.Employee && row.AttendanceDate == AttendanceFixture.RecordDate)
      .ToListAsync();

    Assert.Equal(2, rows.Count);

    // And the truth for the employee-date is their SUM — the arithmetic `IAttendanceSummary` performs.
    Assert.Equal(6m, rows.Sum(row => row.WorkedQuantity));
    Assert.Equal(2m, rows.Sum(row => row.UnpaidAbsenceQuantity));
  }

  private sealed class AttendanceFixture : IAsyncDisposable
  {
    private const string Actor = "fp013-attendance-tests";

    public static readonly DateOnly RecordDate = new(2026, 9, 14);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string catalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid Employee { get; } = Guid.NewGuid();

    public Guid BranchId { get; } = Guid.NewGuid();

    public Guid PeriodId { get; private set; }

    public static async Task<AttendanceFixture> CreateAsync()
    {
      var fixture = new AttendanceFixture();
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
        branchAuthorizer: new GrantingBranchAuthorizer(BranchId),
        companyAuthorizer: new GrantingCompanyAuthorizer(CompanyA),
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
}
