using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE LEGACY EMPLOYEE DEPARTMENT BACKFILL, AGAINST REAL SQL (FP-007 Phase 3 §22-§23, OD-DEP-001).
// ==================================================================================================
//
// Every test here migrates a real database to the migration BEFORE this one, writes employees the way they
// existed before FP-007 — with no department, because the column does not exist yet — and then applies the
// migration under test. Nothing is simulated: the SQL that runs is the SQL that will run on a customer
// database, and the failures are the failures an operator will see.
//
// ---- WHY THE COLLISION CASE IS THE MOST IMPORTANT TEST IN THIS FILE.
//
// The approved decision is that an existing customer department coded UNASSIGNED STOPS the migration. Every
// alternative silently attaches real employees to a department the customer created for their own purposes,
// and none can be undone by a later migration that cannot know what they meant. A test that only checked
// "it throws" would miss the half that matters — that the database is left exactly as it was.
[Trait("Category", "SqlServer")]
[Collection(TenantBackupSerialSuites.Name)]
public sealed class EmployeeDepartmentMigrationSqlServerTests
{
  // The migration immediately before the one under test: Departments exist, Employee has no DepartmentId.
  private const string BeforeMigration = "20260820054319_AddHrDepartment";

  private const string DepartmentMigration = "20260820140653_AddEmployeeDepartment";

  // ================================================================================================
  // §23 A — A COMPANY WITH NO EMPLOYEES GETS NOTHING.
  // ================================================================================================
  //
  // The migration is proportionate to the actual problem. Creating an empty UNASSIGNED department in every
  // company would leave a permanent artefact of a one-time migration in tenants that never needed it.
  [Fact]
  public async Task A_company_with_no_legacy_employees_gets_no_unassigned_department()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    await fixture.MigrateAsync(DepartmentMigration);

    Assert.Equal(0, await fixture.UnassignedCountAsync());
  }

  // ================================================================================================
  // §23 B — ONE LEGACY EMPLOYEE.
  // ================================================================================================
  [Fact]
  public async Task One_legacy_employee_is_mapped_to_one_new_department_with_one_history_row()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    var employee = await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    var unassigned = Assert.Single(await fixture.UnassignedDepartmentsAsync());

    Assert.Equal(fixture.CompanyA, unassigned.CompanyId);
    Assert.Equal("UNASSIGNED", unassigned.Code);
    Assert.Equal("UNASSIGNED", unassigned.NormalizedCode);
    Assert.Equal("Unassigned", unassigned.Name);
    Assert.Equal("Active", unassigned.Status);

    Assert.Equal(unassigned.DepartmentId, await fixture.DepartmentOfAsync(employee));

    // ---- EXACTLY ONE HISTORY ROW, AND IT FABRICATES NOTHING (§21).
    //
    // A null source means "there was no previous department", not "the previous one is unknown". The
    // migration does not invent a prior department or a historical move date — it records that department
    // tracking begins here.
    var history = Assert.Single(await fixture.HistoryForAsync(employee));

    Assert.Null(history.SourceDepartmentId);
    Assert.Equal(unassigned.DepartmentId, history.DestinationDepartmentId);
    Assert.Equal("fp-007-department-migration", history.ChangedBy);
    Assert.Null(history.ReasonCode);
    Assert.Null(history.ReasonText);
  }

  // ================================================================================================
  // §23 C — MANY EMPLOYEES, ONE DEPARTMENT.
  // ================================================================================================
  //
  // The failure this guards against is one UNASSIGNED per EMPLOYEE, which a per-row insert would produce
  // and which no assertion about a single employee would catch.
  [Fact]
  public async Task Many_legacy_employees_in_one_company_share_exactly_one_new_department()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    var first = await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);
    var second = await fixture.SeedLegacyEmployeeAsync("E-0002", fixture.CompanyA);
    var third = await fixture.SeedLegacyEmployeeAsync("E-0003", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    var unassigned = Assert.Single(await fixture.UnassignedDepartmentsAsync());

    Assert.Equal(unassigned.DepartmentId, await fixture.DepartmentOfAsync(first));
    Assert.Equal(unassigned.DepartmentId, await fixture.DepartmentOfAsync(second));
    Assert.Equal(unassigned.DepartmentId, await fixture.DepartmentOfAsync(third));

    // One history row EACH, not one for the batch and not three for one employee.
    Assert.Single(await fixture.HistoryForAsync(first));
    Assert.Single(await fixture.HistoryForAsync(second));
    Assert.Single(await fixture.HistoryForAsync(third));
  }

  // ================================================================================================
  // §23 D — SEPARATE DEPARTMENTS PER AFFECTED COMPANY.
  // ================================================================================================
  //
  // A department belongs to exactly one company, so a shared UNASSIGNED across companies would be a
  // cross-company reference — the boundary violation this whole model exists to prevent.
  [Fact]
  public async Task Each_affected_company_gets_its_own_unassigned_department()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    var inA = await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);
    var inB = await fixture.SeedLegacyEmployeeAsync("E-0002", fixture.CompanyB);

    await fixture.MigrateAsync(DepartmentMigration);

    var unassigned = await fixture.UnassignedDepartmentsAsync();

    Assert.Equal(2, unassigned.Count);
    Assert.Single(unassigned, department => department.CompanyId == fixture.CompanyA);
    Assert.Single(unassigned, department => department.CompanyId == fixture.CompanyB);

    var departmentInA = await fixture.DepartmentOfAsync(inA);
    var departmentInB = await fixture.DepartmentOfAsync(inB);

    Assert.NotEqual(departmentInA, departmentInB);

    // Each employee landed in THEIR OWN company's department, not merely in some department.
    Assert.Equal(
      fixture.CompanyA,
      unassigned.Single(department => department.DepartmentId == departmentInA).CompanyId);
    Assert.Equal(
      fixture.CompanyB,
      unassigned.Single(department => department.DepartmentId == departmentInB).CompanyId);
  }

  // A company with employees is affected; a company without them is not — proven together so the
  // distinction is the one being tested rather than a coincidence of the fixture.
  [Fact]
  public async Task Only_companies_with_legacy_employees_are_affected()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    var unassigned = Assert.Single(await fixture.UnassignedDepartmentsAsync());

    Assert.Equal(fixture.CompanyA, unassigned.CompanyId);
  }

  // ================================================================================================
  // §23 E — EXISTING NORMAL DEPARTMENTS ARE UNTOUCHED.
  // ================================================================================================
  [Fact]
  public async Task Departments_that_already_exist_are_left_exactly_as_they_were()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    var finance = await fixture.SeedDepartmentAsync(fixture.CompanyA, "FIN", "Finance");
    var before = await fixture.DepartmentRowAsync(finance);

    await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    var after = await fixture.DepartmentRowAsync(finance);

    Assert.Equal(before.Code, after.Code);
    Assert.Equal(before.Name, after.Name);
    Assert.Equal(before.Status, after.Status);
    Assert.Equal(before.CompanyId, after.CompanyId);

    // And the legacy employee went into the NEW department, not into the customer's Finance one.
    Assert.Equal(2, await fixture.DepartmentCountAsync(fixture.CompanyA));
  }

  // ================================================================================================
  // §23 F, H — THE FINISHED SCHEMA.
  // ================================================================================================

  // NOT NULL, with no runtime nullable grace period. The nullable window exists only inside the migration.
  [Fact]
  public async Task The_department_column_is_not_nullable_after_the_migration()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    Assert.Equal("NO", await fixture.ScalarAsync<string>("""
      SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
      WHERE TABLE_SCHEMA = 'tenant' AND TABLE_NAME = 'Employees' AND COLUMN_NAME = 'DepartmentId'
      """));
  }

  [Fact]
  public async Task The_approved_department_index_exists()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    await fixture.MigrateAsync(DepartmentMigration);

    // The scope columns LEAD, so a department-filtered search cannot be served by a plan that skipped one.
    Assert.Equal(
      "TenantId,CompanyId,DepartmentId",
      await fixture.ScalarAsync<string>("""
        SELECT STRING_AGG(CAST(col.name AS nvarchar(max)), ',') WITHIN GROUP (ORDER BY ic.key_ordinal)
        FROM sys.indexes AS ix
        JOIN sys.index_columns AS ic ON ic.object_id = ix.object_id AND ic.index_id = ix.index_id
        JOIN sys.columns AS col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
        WHERE ix.object_id = OBJECT_ID('tenant.Employees')
          AND ix.name = 'IX_Employees_TenantId_CompanyId_DepartmentId'
        """));
  }

  // ================================================================================================
  // §23 G — THE FOREIGN KEY IS REAL.
  // ================================================================================================
  [Fact]
  public async Task An_employee_cannot_reference_a_department_that_does_not_exist()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    var employee = await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    var error = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync($"""
      UPDATE [tenant].[Employees]
      SET [DepartmentId] = '{Guid.NewGuid()}'
      WHERE [EmployeeId] = '{employee}'
      """));

    Assert.Contains("FK_Employees_Departments_DepartmentId", error.Message, StringComparison.Ordinal);
  }

  // ================================================================================================
  // §22 — THE COLLISION. THE MIGRATION STOPS, AND NOTHING SURVIVES.
  // ================================================================================================
  //
  // OD-DEP-001 as approved: do not reuse, rename, delete, modify, suffix, or choose another code. Fail
  // loudly and transactionally, and tell the operator what to do.
  [Fact]
  public async Task An_existing_unassigned_department_stops_the_migration_and_changes_nothing()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    // A CUSTOMER's department that happens to be coded UNASSIGNED. It is an ordinary department with an
    // ordinary name — nothing marks it as special, which is exactly why the migration must not assume.
    var customers = await fixture.SeedDepartmentAsync(
      fixture.CompanyA, "UNASSIGNED", "Unassigned Cost Centre");

    var employee = await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);
    var before = await fixture.EmployeeRowAsync(employee);

    var error = await Assert.ThrowsAnyAsync<Exception>(
      () => fixture.MigrateAsync(DepartmentMigration));

    // ---- THE MESSAGE HAS TO BE ACTIONABLE. An operator reading a failed deployment needs the remedy, not
    // an invitation to investigate.
    var message = Flatten(error);

    Assert.Contains("FP-007 department migration STOPPED", message, StringComparison.Ordinal);
    Assert.Contains(fixture.CompanyA.ToString(), message, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("Rename the existing UNASSIGNED department", message, StringComparison.Ordinal);
    Assert.Contains("No changes have been applied", message, StringComparison.Ordinal);

    // ---- AND NOW THE HALF THAT MATTERS: NO PARTIAL STATE SURVIVED.

    // The customer's department is untouched — not renamed, not deactivated, not deleted.
    var after = await fixture.DepartmentRowAsync(customers);

    Assert.Equal("UNASSIGNED", after.Code);
    Assert.Equal("Unassigned Cost Centre", after.Name);
    Assert.Equal("Active", after.Status);

    // No SECOND department was created — no UNASSIGNED2, no suffix, no alternative code.
    Assert.Equal(1, await fixture.DepartmentCountAsync(fixture.CompanyA));

    // No backfill history rows remain.
    Assert.Empty(await fixture.HistoryForAsync(employee));

    // And the employee is exactly as it was. The column may or may not exist depending on how far the
    // failed migration's transaction unwound; what must hold is that nothing about the EMPLOYEE changed.
    var employeeAfter = await fixture.EmployeeRowAsync(employee);

    Assert.Equal(before.EmployeeNumber, employeeAfter.EmployeeNumber);
    Assert.Equal(before.CompanyId, employeeAfter.CompanyId);
    Assert.Equal(before.BranchId, employeeAfter.BranchId);
    Assert.Equal(before.Status, employeeAfter.Status);
  }

  // ---- A COLLISION IN ONE COMPANY STOPS THE WHOLE MIGRATION, INCLUDING THE COMPANIES THAT WERE FINE.
  //
  // This is why the collision check is a separate pass before any write. A migration that had already
  // created CompanyB's department and only then hit CompanyA's collision would be relying on the rollback
  // to undo real work; checking everything first means the common failure never writes at all.
  [Fact]
  public async Task A_collision_in_one_company_leaves_every_other_company_untouched()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    await fixture.SeedDepartmentAsync(fixture.CompanyA, "UNASSIGNED", "Unassigned Cost Centre");
    await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);
    await fixture.SeedLegacyEmployeeAsync("E-0002", fixture.CompanyB);

    await Assert.ThrowsAnyAsync<Exception>(() => fixture.MigrateAsync(DepartmentMigration));

    // CompanyB was blameless and got nothing — no department, no history.
    Assert.Equal(0, await fixture.DepartmentCountAsync(fixture.CompanyB));
    Assert.Equal(0, await fixture.HistoryCountAsync());
  }

  // ---- AND A COLLIDING DEPARTMENT IN A COMPANY WITH NO LEGACY EMPLOYEES IS NOT A COLLISION.
  //
  // The check is scoped to companies that actually need backfilling. A tenant that already uses the code
  // UNASSIGNED in a company with no legacy employees has nothing to collide with, and blocking their
  // migration over it would be a refusal with no cause.
  [Fact]
  public async Task An_unassigned_department_in_an_unaffected_company_does_not_block_the_migration()
  {
    await using var fixture = await MigrationFixture.CreateAsync();

    await fixture.SeedDepartmentAsync(fixture.CompanyB, "UNASSIGNED", "Unassigned Cost Centre");
    var employee = await fixture.SeedLegacyEmployeeAsync("E-0001", fixture.CompanyA);

    await fixture.MigrateAsync(DepartmentMigration);

    // CompanyA got its migration department; CompanyB's existing one is untouched and gained no employees.
    var unassigned = await fixture.UnassignedDepartmentsAsync();

    Assert.Equal(2, unassigned.Count);

    var created = unassigned.Single(department => department.CompanyId == fixture.CompanyA);

    Assert.Equal(created.DepartmentId, await fixture.DepartmentOfAsync(employee));
    Assert.Equal("Unassigned", created.Name);

    var untouched = unassigned.Single(department => department.CompanyId == fixture.CompanyB);

    Assert.Equal("Unassigned Cost Centre", untouched.Name);
  }

  // A migration failure can arrive wrapped in EF's own exception, so the assertions read the whole chain
  // rather than only the outermost message.
  private static string Flatten(Exception exception)
  {
    var message = string.Empty;

    for (var current = exception; current is not null; current = current.InnerException)
    {
      message += current.Message + Environment.NewLine;
    }

    return message;
  }

  internal sealed record UnassignedDepartment(
    Guid DepartmentId, Guid CompanyId, string Code, string NormalizedCode, string Name, string Status);

  internal sealed record DepartmentRow(Guid CompanyId, string Code, string Name, string Status);

  internal sealed record EmployeeRow(
    string EmployeeNumber, Guid CompanyId, Guid BranchId, string Status);

  internal sealed record HistoryRow(
    Guid? SourceDepartmentId,
    Guid DestinationDepartmentId,
    string ChangedBy,
    string? ReasonCode,
    string? ReasonText,
    DateTimeOffset EffectiveFromUtc);

  // ==================================================================================================
  // THE FIXTURE. A REAL DATABASE, MIGRATED TO THE POINT JUST BEFORE THE ONE UNDER TEST.
  // ==================================================================================================
  internal sealed class MigrationFixture : IAsyncDisposable
  {
    private const string Actor = "fixture";

    private static readonly DateTimeOffset Seeded = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string catalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid CompanyB { get; } = Guid.NewGuid();

    public Guid BranchA { get; } = Guid.NewGuid();

    public static async Task<MigrationFixture> CreateAsync()
    {
      var fixture = new MigrationFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    // ---- THE MIGRATION UNDER TEST, APPLIED ON ITS OWN.
    //
    // Through IMigrator with an explicit target rather than MigrateAsync(), so the database sits at exactly
    // the pre-FP-007-Phase-3 state when the employees are seeded — which is the only way "legacy employee"
    // can mean anything here.
    public async Task MigrateAsync(string target)
    {
      await using var context = NewContext();

      await context.GetService<IMigrator>().MigrateAsync(target);
    }

    // Written with raw SQL because at this point in the chain the Employee ENTITY has a DepartmentId the
    // TABLE does not, so EF cannot insert one. That mismatch is the whole situation being tested.
    public async Task<Guid> SeedLegacyEmployeeAsync(string number, Guid companyId)
    {
      var employeeId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Employees]
          ([EmployeeId], [TenantId], [CompanyId], [BranchId], [EmployeeNumber],
           [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [TerminationDate], [Status],
           [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy],
           [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{employeeId}', '{Tenant}', '{companyId}', '{BranchA}', N'{number}',
           N'{number.ToUpperInvariant()}', N'Person {number}', SYSDATETIMEOFFSET(), NULL, N'Active',
           N'Created', SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      // The branch history every employee has. Present so these rows are indistinguishable from real
      // pre-FP-007 employees rather than a thinner shape the migration might happen to tolerate.
      await ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeBranchAssignments]
          ([EmployeeBranchAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourceBranchId],
           [DestinationBranchId], [EffectiveFromUtc], [TransferredBy], [ReasonCode], [CreatedUtc],
           [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{companyId}', '{employeeId}', NULL,
           '{BranchA}', SYSDATETIMEOFFSET(), N'{Actor}', N'InitialAssignment', SYSDATETIMEOFFSET(),
           N'{Actor}');
        """);

      return employeeId;
    }

    public async Task<Guid> SeedDepartmentAsync(Guid companyId, string code, string name)
    {
      var departmentId = Guid.NewGuid();

      // ================================================================================================
      // THIS INSERT DELIBERATELY DOES **NOT** CARRY `NormalizedName`, UNLIKE EVERY OTHER DEPARTMENT SEEDER.
      // ================================================================================================
      //
      // This suite pins the database at `DepartmentMigration` — `20260820140653_AddEmployeeDepartment` —
      // through `IMigrator` with an explicit target, so that "legacy employee" can mean something. FP-008
      // Phase 2's `AddHrSearchNormalizedLabels` comes LATER in the chain, so at the moment this row is
      // written the column does not exist and naming it is an `Invalid column name` error.
      //
      // The FP-008 sweep that added the column to the other five department seeders added it here too, and
      // these four tests caught it. Left out on purpose: a seeder that writes at a PINNED migration level
      // must match the schema AT THAT LEVEL, not the head schema. The backfill in
      // `AddHrSearchNormalizedLabels` fills this row when the chain later reaches it, exactly as it fills
      // the `UNASSIGNED` department that `AddEmployeeDepartment` itself inserts.
      await ExecuteAsync($"""
        INSERT INTO [tenant].[Departments]
          ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name],
           [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc],
           [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{departmentId}', '{Tenant}', '{companyId}', N'{code}', N'{code.ToUpperInvariant()}', N'{name}',
           NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return departmentId;
    }

    public async Task<IReadOnlyList<UnassignedDepartment>> UnassignedDepartmentsAsync()
    {
      var rows = new List<UnassignedDepartment>();

      await ReadAsync(
        """
        SELECT [DepartmentId], [CompanyId], [Code], [NormalizedCode], [Name], [Status]
        FROM [tenant].[Departments]
        WHERE [NormalizedCode] = N'UNASSIGNED'
        ORDER BY [CompanyId]
        """,
        reader => rows.Add(new UnassignedDepartment(
          reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3),
          reader.GetString(4), reader.GetString(5))));

      return rows;
    }

    public Task<int> UnassignedCountAsync() =>
      ScalarAsync<int>(
        "SELECT COUNT(*) FROM [tenant].[Departments] WHERE [NormalizedCode] = N'UNASSIGNED'");

    public Task<int> DepartmentCountAsync(Guid companyId) =>
      ScalarAsync<int>(
        $"SELECT COUNT(*) FROM [tenant].[Departments] WHERE [CompanyId] = '{companyId}'");

    public Task<int> HistoryCountAsync() =>
      ScalarAsync<int>("SELECT COUNT(*) FROM [tenant].[EmployeeDepartmentAssignments]");

    public Task<Guid> DepartmentOfAsync(Guid employeeId) =>
      ScalarAsync<Guid>(
        $"SELECT [DepartmentId] FROM [tenant].[Employees] WHERE [EmployeeId] = '{employeeId}'");

    public async Task<DepartmentRow> DepartmentRowAsync(Guid departmentId)
    {
      DepartmentRow? row = null;

      await ReadAsync(
        $"""
        SELECT [CompanyId], [Code], [Name], [Status]
        FROM [tenant].[Departments]
        WHERE [DepartmentId] = '{departmentId}'
        """,
        reader => row = new DepartmentRow(
          reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

      Assert.NotNull(row);

      return row!;
    }

    public async Task<EmployeeRow> EmployeeRowAsync(Guid employeeId)
    {
      EmployeeRow? row = null;

      await ReadAsync(
        $"""
        SELECT [EmployeeNumber], [CompanyId], [BranchId], [Status]
        FROM [tenant].[Employees]
        WHERE [EmployeeId] = '{employeeId}'
        """,
        reader => row = new EmployeeRow(
          reader.GetString(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3)));

      Assert.NotNull(row);

      return row!;
    }

    public async Task<IReadOnlyList<HistoryRow>> HistoryForAsync(Guid employeeId)
    {
      var rows = new List<HistoryRow>();

      await ReadAsync(
        $"""
        SELECT [SourceDepartmentId], [DestinationDepartmentId], [ChangedBy], [ReasonCode], [ReasonText],
               [EffectiveFromUtc]
        FROM [tenant].[EmployeeDepartmentAssignments]
        WHERE [EmployeeId] = '{employeeId}'
        ORDER BY [EffectiveFromUtc], [EmployeeDepartmentAssignmentId]
        """,
        reader => rows.Add(new HistoryRow(
          reader.IsDBNull(0) ? null : reader.GetGuid(1),
          reader.GetGuid(1),
          reader.GetString(2),
          reader.IsDBNull(3) ? null : reader.GetString(3),
          reader.IsDBNull(4) ? null : reader.GetString(4),
          reader.GetDateTimeOffset(5))));

      return rows;
    }

    public async Task<T> ScalarAsync<T>(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;

      var value = await command.ExecuteScalarAsync();

      return value is null or DBNull ? default! : (T)value;
    }

    public async Task ExecuteAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
      try
      {
        await MasterAsync(
          $"IF DB_ID('{catalog}') IS NOT NULL BEGIN " +
          $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
          $"DROP DATABASE [{catalog}]; END");
      }
      catch (SqlException)
      {
        // A disposal problem must not turn a passing test red; the database is disposable either way.
      }
    }

    private async Task InitializeAsync()
    {
      catalog = $"SSAS_FP007M_Tenant_{token}";

      await MasterAsync($"CREATE DATABASE [{catalog}]");

      // ---- STOP AT THE MIGRATION BEFORE THE ONE UNDER TEST.
      //
      // Departments exist, Employee does not yet have DepartmentId. That is precisely the state a customer
      // database is in when this migration reaches it.
      await using (var context = NewContext())
      {
        await context.GetService<IMigrator>().MigrateAsync(BeforeMigration);
      }

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

    private async Task ReadAsync(string sql, Action<SqlDataReader> read)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;

      await using var reader = await command.ExecuteReaderAsync();

      while (await reader.ReadAsync())
      {
        read(reader);
      }
    }

    private TenantDbContext NewContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(catalog), sql => sql.MigrationsHistoryTable(
          TenantPersistenceConstants.MigrationHistoryTable,
          TenantPersistenceConstants.MigrationHistorySchema))
        .Options;

      return new TenantDbContext(
        options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
        modelContributors: [new HrTenantModelContributor()]);
    }

    private static async Task MasterAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor("master"));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      await command.ExecuteNonQueryAsync();
    }

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False")
      {
        InitialCatalog = catalog,
        Pooling = false
      }.ConnectionString;

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

      // Non-static deliberately: ICurrentTenant is an instance contract, and the fixture tenant is what
      // every migrated row is stamped with.
      public string? TenantCode => tenantId.ToString("N")[..8];
    }

    private sealed class FixtureClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => Seeded;
    }
  }
}
