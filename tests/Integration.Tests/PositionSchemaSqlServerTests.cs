using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.HR.Domain.Positions;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// THE POSITION SCHEMA AGAINST REAL SQL SERVER (FP-008 Phase 1).
//
// ================================================================================================
// WHY THESE ARE MOSTLY RAW SQL RATHER THAN GOING THROUGH THE APPLICATION.
// ================================================================================================
//
// Phase 1 delivers a SCHEMA and a domain. It delivers no command handler, no read scope and no API, so
// there is no application path to drive — and half-wiring an authorization graph to reach the database
// would be testing Platform's save pipeline, which FP-006 already proves, rather than the constraints this
// phase adds.
//
// Every assertion below is about something only the DATABASE enforces: a unique index, a check constraint,
// a foreign key's delete behaviour, a column type, a rowversion. Those cannot be proven by an in-memory
// provider, and a test that tried would assert the provider's behaviour rather than SQL Server's.
//
// THE ONE EXCEPTION IS THE OWNED-TYPE MATERIALIZATION, which is an EF behaviour and has to be read through
// EF. It is done as a READ over rows written in raw SQL, so it exercises the mapping without going through
// the tenant write boundary that Phase 2 has not yet given this feature a path to.
[Trait("Category", "SqlServer")]
public sealed class PositionSchemaSqlServerTests
{
  // ================================================================================================
  // THE FOUR TABLES EXIST, IN THE ONE TENANT MIGRATION STREAM
  // ================================================================================================
  //
  // Created by the SAME chain as Platform's own tenant tables (ADR-017). If HrTenantModelContributor ever
  // stopped contributing them the migration would still run and these tables would simply not be there —
  // which is the failure mode the contributor set is explicit rather than discovered to prevent.
  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task The_four_position_tables_are_created_by_the_tenant_migration_chain()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    foreach (var table in new[]
      { "Positions", "JobGrades", "SalaryGrades", "EmployeePositionAssignments" })
    {
      var count = await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'{table}'");

      Assert.Equal(1, count);
    }
  }

  // ---- RE-RUNNING THE MIGRATION IS A NO-OP, NOT AN ERROR.
  //
  // The orchestrator may call `MigrateAsync` against a database already at head — on restart, on a retried
  // provisioning step, or on a tenant that was migrated by an earlier deployment. A migration that threw or
  // duplicated on the second call would turn an ordinary retry into an outage.
  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task Re_running_the_tenant_migration_chain_changes_nothing()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.MigrateAsync();

    foreach (var table in new[]
      { "Positions", "JobGrades", "SalaryGrades", "EmployeePositionAssignments" })
    {
      Assert.Equal(1, await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'{table}'"));
    }
  }

  // ================================================================================================
  // THE THREE ABSENCES, READ FROM THE SERVER RATHER THAN FROM THE MODEL
  // ================================================================================================
  //
  // A shadow property that reached the database would be invisible on the CLR type and perfectly visible
  // here, which is why this asks SQL Server rather than reflection.
  [Theory]
  [InlineData("Positions")]
  [InlineData("JobGrades")]
  [InlineData("SalaryGrades")]
  [Trait("Decision", "DEC-POS-0001")]
  public async Task No_position_table_has_a_branch_column_and_all_have_both_ownership_columns(string table)
  {
    await using var fixture = await PositionFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount(table, "c.name LIKE N'%Branch%'")));
    Assert.Equal(2, await fixture.ScalarAsync(
      ColumnCount(table, "c.name IN (N'TenantId', N'CompanyId')")));
  }

  // ---- NO EMPLOYEE COLUMN ON ANY OF THE THREE AGGREGATES (DEC-POS-0002).
  //
  // This is the cutover cycle trap. `Employee.PositionId -> Position` plus any reverse key makes
  // `TenantCutoverCopyPlan.Order` return `CutoverCopyOrderUndecidable`, and Shared→Dedicated cutover stops
  // working for every tenant without degrading or warning. The history table is excluded because it is a
  // DEPENDENT of both and a principal of neither, which is what keeps the graph acyclic.
  [Theory]
  [InlineData("Positions")]
  [InlineData("JobGrades")]
  [InlineData("SalaryGrades")]
  [Trait("Decision", "DEC-POS-0002")]
  public async Task No_position_aggregate_table_references_an_employee(string table)
  {
    await using var fixture = await PositionFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount(table, "c.name LIKE N'%Employee%'")));
  }

  // ---- AND NO DEPARTMENT COLUMN ON Positions (OD-POS-003).
  //
  // `Employee.DepartmentId` is the single authority on an employee's department. A copy here would be a
  // second source of truth for the same fact, and the two could disagree.
  [Fact]
  [Trait("Decision", "OD-POS-003")]
  public async Task The_position_table_has_no_department_column()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount("Positions", "c.name LIKE N'%Department%'")));
  }

  // ---- AND NO SALARY GRADE POINTS BACK AT A JOB GRADE.
  //
  // The reference runs one way. A back-pointer would close the same cycle in a place nobody would look for
  // it, because the two ladders are peers in every other respect.
  [Fact]
  [Trait("Decision", "DEC-POS-0002")]
  public async Task The_salary_grade_table_has_no_job_grade_column()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount("SalaryGrades", "c.name LIKE N'%JobGrade%'")));
    Assert.Equal(1, await fixture.ScalarAsync(ColumnCount("JobGrades", "c.name = N'SalaryGradeId'")));
  }

  // ---- AND NO CURRENCY COLUMN ANYWHERE (DEC-POS-0015, ADR-027 decision 2).
  //
  // Amounts are denominated in the owning Company's immutable `BaseCurrencyCode`. A per-row copy would be a
  // second source of truth for a fact the Company already owns.
  [Fact]
  [Trait("Decision", "ADR-027")]
  public async Task No_position_table_stores_a_currency()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    foreach (var table in new[]
      { "Positions", "JobGrades", "SalaryGrades", "EmployeePositionAssignments" })
    {
      Assert.Equal(0, await fixture.ScalarAsync(ColumnCount(table, "c.name LIKE N'%Currenc%'")));
    }
  }

  // ================================================================================================
  // EVERY PERSISTED STRING IS nvarchar
  // ================================================================================================
  //
  // The standing guardrail. `Company.BaseCurrencyCode` is `char(3)` as a deliberate exception for a
  // constraint-validated ASCII code; nothing in this package inherits that exception, because nothing in
  // this package stores a currency.
  [Fact]
  public async Task Every_string_column_in_the_position_tables_is_nvarchar()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var nonUnicode = await fixture.ScalarAsync("""
      SELECT COUNT(*)
      FROM sys.columns c
      JOIN sys.tables t ON t.object_id = c.object_id
      JOIN sys.schemas s ON s.schema_id = t.schema_id
      JOIN sys.types ty ON ty.user_type_id = c.user_type_id
      WHERE s.name = N'tenant'
        AND t.name IN (N'Positions', N'JobGrades', N'SalaryGrades', N'EmployeePositionAssignments')
        AND ty.name IN (N'char', N'varchar', N'text')
      """);

    Assert.Equal(0, nonUnicode);
  }

  // ---- THE MONEY COLUMNS ARE decimal(19,4), NEVER float, real OR money (ADR-027 decision 1).
  [Fact]
  [Trait("Decision", "ADR-027")]
  public async Task The_salary_band_columns_are_decimal_19_4()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    foreach (var column in new[] { "MinimumAmount", "MidpointAmount", "MaximumAmount" })
    {
      var shape = await fixture.StringAsync($"""
        SELECT CONCAT(ty.name, N'(', c.precision, N',', c.scale, N')')
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE s.name = N'tenant' AND t.name = N'SalaryGrades' AND c.name = N'{column}'
        """);

      Assert.Equal("decimal(19,4)", shape);
    }
  }

  // ================================================================================================
  // THE SALARY BAND IS ATOMIC, AND THE DATABASE SAYS SO (DEC-POS-0027)
  // ================================================================================================
  //
  // Written in raw SQL, bypassing the value object entirely. The domain refuses a partial band and so does
  // the mapping; this is the third statement of the same rule, and it is the only one that holds against a
  // script, a bulk load, or a support engineer with a query window.
  [Theory]
  [InlineData("100.0000", "NULL", "NULL")]
  [InlineData("NULL", "200.0000", "NULL")]
  [InlineData("NULL", "NULL", "300.0000")]
  [InlineData("100.0000", "200.0000", "NULL")]
  [InlineData("100.0000", "NULL", "300.0000")]
  [InlineData("NULL", "200.0000", "300.0000")]
  [Trait("Decision", "DEC-POS-0027")]
  public async Task A_partially_priced_salary_grade_is_refused_by_the_database(
    string minimum, string midpoint, string maximum)
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertSalaryGradeAsync("S1", "Band 1", 10, minimum, midpoint, maximum));

    Assert.Contains("CK_SalaryGrades_Band_Atomic", failure.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "DEC-POS-0027")]
  public async Task A_fully_priced_and_a_wholly_unpriced_salary_grade_are_both_accepted()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertSalaryGradeAsync("S1", "Band 1", 10, "100.0000", "200.0000", "300.0000");
    await fixture.InsertSalaryGradeAsync("S2", "Band 2", 20, "NULL", "NULL", "NULL");

    Assert.Equal(2, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[SalaryGrades]"));
  }

  [Theory]
  [InlineData("300.0000", "200.0000", "100.0000")]
  [InlineData("100.0000", "300.0000", "200.0000")]
  [InlineData("200.0000", "100.0000", "300.0000")]
  public async Task An_out_of_order_salary_band_is_refused_by_the_database(
    string minimum, string midpoint, string maximum)
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertSalaryGradeAsync("S1", "Band 1", 10, minimum, midpoint, maximum));

    Assert.Contains("CK_SalaryGrades_Amounts_Ordered", failure.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task A_negative_salary_band_amount_is_refused_by_the_database()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertSalaryGradeAsync("S1", "Band 1", 10, "-1.0000", "200.0000", "300.0000"));

    Assert.Contains("CK_SalaryGrades_Amounts_NonNegative", failure.Message, StringComparison.Ordinal);
  }

  // A single-point band is a fixed-rate grade — a real structure, and the non-strict ordering admits it.
  [Fact]
  public async Task A_single_point_salary_band_is_accepted()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertSalaryGradeAsync("S1", "Band 1", 10, "150.0000", "150.0000", "150.0000");

    Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[SalaryGrades]"));
  }

  // ================================================================================================
  // THE OPTIONAL OWNED TYPE MATERIALIZES BOTH WAYS — THE ONE EF ASSERTION IN THIS FILE
  // ================================================================================================
  //
  // Rows are written in raw SQL and read through EF, so this proves the MAPPING rather than a save
  // pipeline. Two properties matter and neither is provable any other way: three null columns must produce
  // a null `Band` (not a zero-valued one), and four decimal places must survive the round trip.
  [Fact]
  [Trait("Decision", "DEC-POS-0027")]
  public async Task An_unpriced_grade_materializes_with_a_null_band_and_a_priced_one_keeps_four_decimals()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertSalaryGradeAsync("S1", "Band 1", 10, "NULL", "NULL", "NULL");
    await fixture.InsertSalaryGradeAsync("S2", "Band 2", 20, "1234.5678", "2345.6789", "3456.7891");

    await using var context = fixture.CreateContext();

    var grades = await context.Set<SalaryGrade>()
      .OrderBy(grade => grade.RankOrder)
      .ToListAsync();

    Assert.Equal(2, grades.Count);

    Assert.Null(grades[0].Band);

    var priced = grades[1].Band;

    Assert.NotNull(priced);
    Assert.Equal(1234.5678m, priced!.MinimumAmount);
    Assert.Equal(2345.6789m, priced.MidpointAmount);
    Assert.Equal(3456.7891m, priced.MaximumAmount);
  }

  // ================================================================================================
  // CODES ARE UNIQUE PER COMPANY, AND THE INDEX IS AUTHORITATIVE RATHER THAN ADVISORY
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-POS-0007")]
  public async Task A_duplicate_normalized_position_code_is_refused_within_one_company()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertPositionAsync("ACC-SR", "Senior Accountant", fixture.CompanyA);

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertPositionAsync("acc-sr", "Another", fixture.CompanyA));

    Assert.Contains(
      "UX_Positions_TenantId_CompanyId_NormalizedCode", failure.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task The_same_position_code_is_free_in_a_second_company()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertPositionAsync("ACC-SR", "Senior Accountant", fixture.CompanyA);
    await fixture.InsertPositionAsync("ACC-SR", "Senior Accountant", fixture.CompanyB);

    Assert.Equal(2, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[Positions]"));
  }

  // THE THREE LADDERS DO NOT SHARE A CODE SPACE. `G7` as a job grade and `G7` as a salary grade are
  // different rows in different tables, and neither collides with the other or with a position.
  [Fact]
  public async Task The_same_code_may_exist_once_in_each_of_the_three_tables()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertSalaryGradeAsync("G7", "Band 7", 70, "NULL", "NULL", "NULL");
    await fixture.InsertJobGradeAsync("G7", "Grade 7", 70);
    await fixture.InsertPositionAsync("G7", "Oddly Named Job", fixture.CompanyA);

    Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[SalaryGrades]"));
    Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[JobGrades]"));
    Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[Positions]"));
  }

  // ---- CONCURRENT INSERT ARBITRATION.
  //
  // The point of a binary-collated UNIQUE INDEX rather than a read-then-write check: two sessions that both
  // pass a pre-check still cannot both commit. Exactly one survives, and the loser's refusal originates in
  // the index rather than in application logic that a race can step around.
  [Fact]
  [Trait("Decision", "DEC-POS-0007")]
  public async Task Two_concurrent_inserts_of_one_code_leave_exactly_one_row()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var first = fixture.InsertPositionAsync("RACE", "First", fixture.CompanyA);
    var second = fixture.InsertPositionAsync("race", "Second", fixture.CompanyA);

    var outcomes = await Task.WhenAll(Capture(first), Capture(second));

    Assert.Equal(1, outcomes.Count(succeeded => succeeded));
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [tenant].[Positions] WHERE [NormalizedCode] = N'RACE'"));
  }

  // ================================================================================================
  // RANK IS UNIQUE WITHIN A LADDER (DEC-POS-0006)
  // ================================================================================================
  //
  // A ladder with two rung sevens has no order at all, which is the one property `RankOrder` exists to
  // provide.
  [Fact]
  [Trait("Decision", "DEC-POS-0006")]
  public async Task A_duplicate_rank_is_refused_within_one_ladder_and_company()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertJobGradeAsync("G7", "Grade 7", 70);

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertJobGradeAsync("G7B", "Grade 7 duplicate rank", 70));

    Assert.Contains(
      "UX_JobGrades_TenantId_CompanyId_RankOrder", failure.Message, StringComparison.Ordinal);
  }

  // The two ladders rank INDEPENDENTLY. Job grade 70 and salary grade 70 are unrelated facts, and a shared
  // rank space would couple two structures the package deliberately keeps separate.
  [Fact]
  public async Task The_two_ladders_may_share_a_rank()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    await fixture.InsertJobGradeAsync("G7", "Grade 7", 70);
    await fixture.InsertSalaryGradeAsync("S7", "Band 7", 70, "NULL", "NULL", "NULL");

    Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[JobGrades]"));
    Assert.Equal(1, await fixture.ScalarAsync("SELECT COUNT(*) FROM [tenant].[SalaryGrades]"));
  }

  // ---- AND THE RANK IS NOT CONSTRAINED TO BE POSITIVE **IN THE DATABASE**, DELIBERATELY.
  //
  // `BRULE-POS-0007` requires a positive rank and the aggregate enforces it. The package's constraint list
  // for these tables does NOT include a rank check, and adding an unlisted constraint would be filling a gap
  // the specification did not leave. The consequence is asserted rather than left to be discovered: a direct
  // SQL insert CAN write a zero rank, and only the application path refuses it.
  [Fact]
  [Trait("Decision", "DEC-POS-0006")]
  public async Task A_non_positive_rank_is_refused_by_the_domain_and_accepted_by_the_database()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    Assert.Equal(
      PositionErrors.InvalidRankOrder,
      JobGrade.Create(
        JobGradeCode.Create("G0").Value,
        JobGradeName.Create("Grade 0").Value,
        rankOrder: 0,
        salaryGradeId: null,
        "tester",
        Guid.NewGuid(),
        DateTimeOffset.UtcNow).Error);

    await fixture.InsertJobGradeAsync("G0", "Grade 0", 0);

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [tenant].[JobGrades] WHERE [RankOrder] = 0"));
  }

  // ================================================================================================
  // CHECK CONSTRAINTS AND REFERENTIAL BEHAVIOUR
  // ================================================================================================
  [Fact]
  public async Task A_blank_code_or_title_is_refused_by_the_database()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var blankCode = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertPositionAsync("   ", "Valid Title", fixture.CompanyA));
    Assert.Contains("CK_Positions_Code_NotBlank", blankCode.Message, StringComparison.Ordinal);

    var blankTitle = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertPositionAsync("VALID", "   ", fixture.CompanyA));
    Assert.Contains("CK_Positions_Title_NotBlank", blankTitle.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task An_unknown_status_is_refused_by_the_database()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.ExecuteAsync($"""
        UPDATE [tenant].[Positions] SET [Status] = N'Archived' WHERE 1 = 0;
        INSERT INTO [tenant].[Positions]
          ([PositionId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Title], [JobGradeId],
           [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy], [ModifiedUtc],
           [ModifiedBy])
        VALUES
          ('{Guid.NewGuid()}', '{fixture.Tenant}', '{fixture.CompanyA}', N'ARCH', N'ARCH', N'Archived',
           NULL, N'Archived', SYSDATETIMEOFFSET(), N'tester',
           SYSDATETIMEOFFSET(), N'tester', SYSDATETIMEOFFSET(), N'tester');
        """));

    Assert.Contains("CK_Positions_Status", failure.Message, StringComparison.Ordinal);
  }

  // A history record can never describe a move to the position it came from.
  [Fact]
  public async Task A_position_history_record_from_a_position_to_itself_is_refused()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var positionId = await fixture.InsertPositionAsync("ACC-SR", "Senior Accountant", fixture.CompanyA);
    var employeeId = await fixture.InsertEmployeeAsync("E-0001");

    var failure = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertPositionAssignmentAsync(employeeId, positionId, positionId));

    Assert.Contains(
      "CK_EmployeePositionAssignments_SourceDiffersFromDestination",
      failure.Message,
      StringComparison.Ordinal);
  }

  // The initial record — a null source — is the one shape that IS allowed, and nothing else identifies it.
  [Fact]
  public async Task A_position_history_record_with_a_null_source_is_accepted()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var positionId = await fixture.InsertPositionAsync("ACC-SR", "Senior Accountant", fixture.CompanyA);
    var employeeId = await fixture.InsertEmployeeAsync("E-0001");

    await fixture.InsertPositionAssignmentAsync(employeeId, sourcePositionId: null, positionId);

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [tenant].[EmployeePositionAssignments] WHERE [SourcePositionId] IS NULL"));
  }

  // ---- DELETE IS RESTRICTED EVERYWHERE. Positions and grades are deactivated, never deleted, so a cascade
  // would silently erase organizational structure — and here, an employee's job history along with it.
  [Fact]
  public async Task Deleting_a_referenced_grade_or_position_is_refused()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    var salaryGradeId = await fixture.InsertSalaryGradeAsync("S7", "Band 7", 70, "NULL", "NULL", "NULL");
    var jobGradeId = await fixture.InsertJobGradeAsync("G7", "Grade 7", 70, salaryGradeId);
    var positionId = await fixture.InsertPositionAsync(
      "ACC-SR", "Senior Accountant", fixture.CompanyA, jobGradeId);

    await Assert.ThrowsAsync<SqlException>(() =>
      fixture.ExecuteAsync($"DELETE FROM [tenant].[SalaryGrades] WHERE [SalaryGradeId] = '{salaryGradeId}'"));

    await Assert.ThrowsAsync<SqlException>(() =>
      fixture.ExecuteAsync($"DELETE FROM [tenant].[JobGrades] WHERE [JobGradeId] = '{jobGradeId}'"));

    Assert.Equal(1, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[Positions] WHERE [PositionId] = '{positionId}'"));
  }

  // ---- ROWVERSION ON THE THREE AGGREGATES, AND ON NOTHING ELSE (DEC-POS-0021).
  //
  // The history is never updated, so it has no concurrency state to protect; a rowversion there would imply
  // an update that cannot happen.
  [Fact]
  [Trait("Decision", "DEC-POS-0021")]
  public async Task The_aggregates_carry_a_rowversion_and_the_history_does_not()
  {
    await using var fixture = await PositionFixture.CreateAsync();

    foreach (var table in new[] { "Positions", "JobGrades", "SalaryGrades" })
    {
      Assert.Equal(1, await fixture.ScalarAsync(ColumnCount(table, "c.name = N'RowVersion'")));
    }

    Assert.Equal(0, await fixture.ScalarAsync(
      ColumnCount("EmployeePositionAssignments", "c.name = N'RowVersion'")));

    // And no Modified pair on the history either, for the same reason.
    Assert.Equal(0, await fixture.ScalarAsync(
      ColumnCount("EmployeePositionAssignments", "c.name IN (N'ModifiedUtc', N'ModifiedBy')")));
  }

  private static string ColumnCount(string table, string predicate) =>
    "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
    "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
    $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND {predicate}";

  private static async Task<bool> Capture(Task work)
  {
    try
    {
      await work;
      return true;
    }
    catch (SqlException)
    {
      return false;
    }
  }

  private sealed class PositionFixture : IAsyncDisposable
  {
    private const string Actor = "position-phase1-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string tenantCatalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid CompanyB { get; } = Guid.NewGuid();

    public Guid BranchA { get; } = Guid.NewGuid();

    public static async Task<PositionFixture> CreateAsync()
    {
      var fixture = new PositionFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    public TenantDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog))
        .Options;

      // The tenant passed here is REAL and must stay so — see `FixtureTenant` below for why. It also scopes
      // the band test's read to this fixture's own rows, which is the behaviour under test in production.
      return new TenantDbContext(
        options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
        modelContributors: [new HrTenantModelContributor()]);
    }

    public async Task MigrateAsync()
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));

      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;

      await using var context = new TenantDbContext(
        options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
        modelContributors: [new HrTenantModelContributor()]);

      await context.Database.MigrateAsync();
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

    public async Task<string?> StringAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return (await command.ExecuteScalarAsync())?.ToString();
    }

    public async Task ExecuteAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async Task<Guid> InsertSalaryGradeAsync(
      string code, string name, int rankOrder, string minimum, string midpoint, string maximum)
    {
      var salaryGradeId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[SalaryGrades]
          ([SalaryGradeId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [RankOrder],
           [MinimumAmount], [MidpointAmount], [MaximumAmount], [Status], [StatusChangedUtc],
           [StatusChangedBy], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{salaryGradeId}', '{Tenant}', '{CompanyA}', N'{code}', N'{code.ToUpperInvariant()}', N'{name}',
           {rankOrder}, {minimum}, {midpoint}, {maximum}, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return salaryGradeId;
    }

    public async Task<Guid> InsertJobGradeAsync(
      string code, string name, int rankOrder, Guid? salaryGradeId = null)
    {
      var jobGradeId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[JobGrades]
          ([JobGradeId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [RankOrder],
           [SalaryGradeId], [Status], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{jobGradeId}', '{Tenant}', '{CompanyA}', N'{code}', N'{code.ToUpperInvariant()}', N'{name}',
           {rankOrder}, {(salaryGradeId is null ? "NULL" : $"'{salaryGradeId}'")},
           N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return jobGradeId;
    }

    public async Task<Guid> InsertPositionAsync(
      string code, string title, Guid companyId, Guid? jobGradeId = null)
    {
      var positionId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Positions]
          ([PositionId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Title], [JobGradeId],
           [Status], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{positionId}', '{Tenant}', '{companyId}', N'{code}', N'{code.Trim().ToUpperInvariant()}',
           N'{title}', {(jobGradeId is null ? "NULL" : $"'{jobGradeId}'")},
           N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return positionId;
    }

    public Task InsertPositionAssignmentAsync(
      Guid employeeId, Guid? sourcePositionId, Guid destinationPositionId) =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeePositionAssignments]
          ([EmployeePositionAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourcePositionId],
           [DestinationPositionId], [EffectiveFromUtc], [ChangedBy], [CreatedUtc], [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{CompanyA}', '{employeeId}',
           {(sourcePositionId is null ? "NULL" : $"'{sourcePositionId}'")},
           '{destinationPositionId}', SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    // Created on first use with a reserved code no test names, so it satisfies FP-007's required
    // Employee.DepartmentId without appearing in any assertion about the positions under test.
    private Guid? holdingDepartment;

    private async Task<Guid> HoldingDepartmentAsync()
    {
      if (holdingDepartment is { } existing)
      {
        return existing;
      }

      var departmentId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Departments]
          ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name],
           [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{departmentId}', '{Tenant}', '{CompanyA}', N'ZZ-POSITION-HOME', N'ZZ-POSITION-HOME',
           N'Position Home', NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      holdingDepartment = departmentId;

      return departmentId;
    }

    public async Task<Guid> InsertEmployeeAsync(string employeeNumber)
    {
      var employeeId = Guid.NewGuid();
      var homeDepartment = await HoldingDepartmentAsync();

      // NOTE FOR PHASE 3. This insert has no PositionId because the column does not exist yet. When Phase 3
      // makes it NOT NULL, every raw Employees insert in the Integration suite — this one included — needs a
      // position, exactly as every one of them needed a department when FP-007 Phase 3 landed.
      await ExecuteAsync($"""
        INSERT INTO [tenant].[Employees]
          ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [EmployeeNumber],
           [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [Status], [StatusChangeReasonCode],
           [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{employeeId}', '{Tenant}', '{CompanyA}', '{BranchA}', '{homeDepartment}', N'{employeeNumber}',
           N'{employeeNumber.ToUpperInvariant()}', N'Person {employeeNumber}', SYSDATETIMEOFFSET(),
           N'Active', N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      await ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeBranchAssignments]
          ([EmployeeBranchAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourceBranchId],
           [DestinationBranchId], [EffectiveFromUtc], [TransferredBy], [ReasonCode], [CreatedUtc],
           [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{CompanyA}', '{employeeId}', NULL, '{BranchA}',
           SYSDATETIMEOFFSET(), N'{Actor}', N'InitialAssignment', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return employeeId;
    }

    public async ValueTask DisposeAsync()
    {
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
      tenantCatalog = $"SSAS_FP008_Tenant_{token}";

      await MasterAsync($"CREATE DATABASE [{tenantCatalog}]");

      await MigrateAsync();

      await SeedCompanyAsync(CompanyA, "CMPA");
      await SeedCompanyAsync(CompanyB, "CMPB");
      await SeedBranchAsync(BranchA, "BRA");
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

    private Task SeedBranchAsync(Guid branchId, string code) =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[Branches]
          ([BranchId], [TenantId], [BranchCode], [NormalizedBranchCode], [BranchName],
           [IsMainBranch], [IsActive], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{branchId}', '{Tenant}', N'{code}', N'{code}', N'Branch {code}',
           1, 1, SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    private static async Task MasterAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    private static string Configured() =>
      Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

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

    // CARRIES A REAL TENANT, unlike the department fixture this was copied from — that one may hold a null
    // tenant only because it never queries an entity through EF, and this one does. A null here throws
    // `Nullable object must have a value` out of `PersistenceDbContext`'s tenant filter before any SQL is
    // sent: EF evaluates both operands of `CurrentTenantId.HasValue && ... == CurrentTenantId.Value` while
    // extracting query parameters, because `&&` short-circuits in C# and not in an expression tree.
    private sealed class FixtureTenant(Guid tenantId) : ICurrentTenant
    {
      public Guid? TenantId => tenantId;
    }

    private sealed class FixtureClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
  }
}
