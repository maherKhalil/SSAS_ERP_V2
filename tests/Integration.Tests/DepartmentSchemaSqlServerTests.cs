using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// THE DEPARTMENT SCHEMA AGAINST REAL SQL SERVER (FP-007 Phase 1, ADR-026).
//
// ================================================================================================
// WHY THESE ARE RAW SQL RATHER THAN GOING THROUGH THE APPLICATION.
// ================================================================================================
//
// Phase 1 delivers a SCHEMA and a domain. It delivers no command handler, no read scope and no API, so
// there is no application path to drive — and half-wiring an authorization graph to reach the database
// would be testing Platform's save pipeline, which FP-006 already proves, rather than the constraints this
// phase adds.
//
// Every assertion below is about something only the DATABASE enforces: a unique index, a check constraint,
// a foreign key's delete behaviour, a rowversion. Those cannot be proven by an in-memory provider, and a
// test that tried would be asserting the provider's behaviour rather than SQL Server's. The application
// paths that will exercise this schema arrive in Phase 2 and are proven there.
[Trait("Category", "SqlServer")]
public sealed class DepartmentSchemaSqlServerTests
{
  // ================================================================================================
  // THE TABLES EXIST, IN THE ONE TENANT MIGRATION STREAM
  // ================================================================================================
  //
  // Created by the SAME chain as Platform's own tenant tables, which is the whole point of the single
  // tenant model (ADR-017). If HrTenantModelContributor ever stopped contributing them, the migration would
  // still run and these tables would simply not be there.
  [Fact]
  [Trait("Decision", "ADR-017")]
  public async Task The_three_department_tables_are_created_by_the_tenant_migration_chain()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    foreach (var table in new[] { "Departments", "DepartmentManagers", "EmployeeDepartmentAssignments" })
    {
      var count = await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'{table}'");

      Assert.Equal(1, count);
    }
  }

  // ---- AND Departments HAS NO BranchId COLUMN.
  //
  // Read from the server rather than from the model, so a shadow property that reached the database would
  // be caught even though the CLR type shows nothing.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_department_table_has_no_branch_column()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    var branchColumns = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'Departments' AND c.name LIKE N'%Branch%'");

    Assert.Equal(0, branchColumns);

    var ownership = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'Departments' AND c.name IN (N'TenantId', N'CompanyId')");

    Assert.Equal(2, ownership);
  }

  // ================================================================================================
  // CODE UNIQUENESS — AUTHORITATIVE, NOT ADVISORY
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_normalized_code_is_unique_within_a_company()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    await fixture.InsertDepartmentAsync(Guid.NewGuid(), fixture.CompanyA, "SALES", "Sales");

    var conflict = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertDepartmentAsync(Guid.NewGuid(), fixture.CompanyA, "SALES", "Sales Again"));

    Assert.Contains("UX_Departments_TenantId_CompanyId_NormalizedCode", conflict.Message, StringComparison.Ordinal);
  }

  // The same code is free in a different company: uniqueness is per company, never per tenant.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_same_normalized_code_is_free_in_a_different_company()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    await fixture.InsertDepartmentAsync(Guid.NewGuid(), fixture.CompanyA, "SALES", "Sales");
    await fixture.InsertDepartmentAsync(Guid.NewGuid(), fixture.CompanyB, "SALES", "Sales");

    Assert.Equal(2, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [tenant].[Departments] WHERE [NormalizedCode] = N'SALES'"));
  }

  // ---- BINARY COLLATION IS WHAT MAKES THE INDEX ORDINAL.
  //
  // Under a default case-insensitive collation, `SALES` and `sales` would collide as stored values — which
  // is not the design: the DOMAIN normalizes to upper case before storing, and the column compares
  // ordinally so two values that normalize alike are the same value and two that do not are not.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_normalized_code_column_is_binary_collated()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    var collation = await fixture.StringAsync(
      "SELECT c.collation_name FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'Departments' AND c.name = N'NormalizedCode'");

    Assert.Equal("Latin1_General_100_BIN2", collation);
  }

  // ================================================================================================
  // THE HIERARCHY
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_may_reference_a_parent_in_the_same_company()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var parentId = Guid.NewGuid();
    var childId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(parentId, fixture.CompanyA, "SALES", "Sales");
    await fixture.InsertDepartmentAsync(childId, fixture.CompanyA, "SALES-N", "Sales North", parentId);

    Assert.Equal(1, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[Departments] WHERE [DepartmentId] = '{childId}' " +
      $"AND [ParentDepartmentId] = '{parentId}'"));
  }

  // ---- THE ONE PART OF BR-HR-0008 THE DATABASE CAN ENFORCE, PROVEN AGAINST DIRECT SQL.
  //
  // The domain refuses this too, but the constraint is what makes it true for a writer that never went
  // through the domain. The general descendant-as-parent rule is transactional and arrives in Phase 2 —
  // this proves the half that has a database guarantee, and nothing more.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_cannot_be_its_own_parent_even_in_raw_sql()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var departmentId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(departmentId, fixture.CompanyA, "SALES", "Sales");

    var violation = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      $"UPDATE [tenant].[Departments] SET [ParentDepartmentId] = '{departmentId}' " +
      $"WHERE [DepartmentId] = '{departmentId}'"));

    Assert.Contains("CK_Departments_ParentIsNotSelf", violation.Message, StringComparison.Ordinal);
  }

  // ---- A PARENT CANNOT BE DELETED WHILE A CHILD REFERENCES IT.
  //
  // Departments are never deleted by the application, so this is defence for a path that should not exist.
  // The point is that RESTRICT means the database refuses rather than silently orphaning or cascading away
  // a subtree.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_parent_foreign_key_restricts_rather_than_cascades()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var parentId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(parentId, fixture.CompanyA, "SALES", "Sales");
    await fixture.InsertDepartmentAsync(Guid.NewGuid(), fixture.CompanyA, "SALES-N", "North", parentId);

    var refusal = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      $"DELETE FROM [tenant].[Departments] WHERE [DepartmentId] = '{parentId}'"));

    Assert.Contains("FK_Departments_Departments_ParentDepartmentId", refusal.Message, StringComparison.Ordinal);
  }

  // ---- EVERY DEPARTMENT FOREIGN KEY IS NO ACTION, READ FROM THE SERVER.
  //
  // Asserted from `sys.foreign_keys` rather than from the model, because a cascade introduced by a future
  // migration would be invisible to a model-level test that read the current mapping.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task No_department_foreign_key_cascades_on_the_server()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    var cascading = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.foreign_keys fk " +
      "JOIN sys.tables t ON t.object_id = fk.parent_object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name IN " +
      "(N'Departments', N'DepartmentManagers', N'EmployeeDepartmentAssignments') " +
      "AND (fk.delete_referential_action <> 0 OR fk.update_referential_action <> 0)");

    Assert.Equal(0, cascading);
  }

  // ================================================================================================
  // THE MANAGER ASSOCIATION
  // ================================================================================================

  // ---- ONE MANAGER PER DEPARTMENT IS A FACT OF THE SCHEMA.
  //
  // The primary key is the department, so a second manager is not refused by a handler — it is
  // unrepresentable. That is the difference this table's shape exists to buy.
  [Fact]
  [Trait("Decision", "ADR-026")]
  // CITED BY B18 pass 20, body-confirmed -- VERBATIM INCLUDING ITS INSTRUMENT. `AC-DEP-0024` says
  // *at most one manager, enforced by the primary key of `tenant.DepartmentManagers` RATHER THAN BY
  // A HANDLER CHECK*. This test inserts twice through the fixture, bypassing every handler, and then
  // asserts the `SqlException` message names `PK_DepartmentManagers`. The key does the refusing and
  // the assertion reads the key's own name.
  //
  // B18 pass 19 offered `Concurrent_manager_assignment_cannot_produce_two_rows` for this criterion.
  // Reading that test's body refuted it: its own comment records that BOTH callers may legitimately
  // succeed, because assignment is an upsert and the handler REASSIGNS. It is a good invariant test
  // and it cannot discriminate key-enforcement from handler-enforcement, which is the whole clause.
  [Trait("Criterion", "AC-DEP-0024")]
  public async Task A_department_can_have_at_most_one_manager()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var departmentId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(departmentId, fixture.CompanyA, "SALES", "Sales");
    var firstEmployee = await fixture.InsertEmployeeAsync("E-0001");
    var secondEmployee = await fixture.InsertEmployeeAsync("E-0002");

    await fixture.InsertManagerAsync(departmentId, firstEmployee);

    var conflict = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertManagerAsync(departmentId, secondEmployee));

    Assert.Contains("PK_DepartmentManagers", conflict.Message, StringComparison.Ordinal);
  }

  // One employee may head several departments. Nothing forbids it, and the schema says so.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task One_employee_may_manage_more_than_one_department()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(first, fixture.CompanyA, "SALES", "Sales");
    await fixture.InsertDepartmentAsync(second, fixture.CompanyA, "OPS", "Operations");
    var employeeId = await fixture.InsertEmployeeAsync("E-0001");

    await fixture.InsertManagerAsync(first, employeeId);
    await fixture.InsertManagerAsync(second, employeeId);

    Assert.Equal(2, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[DepartmentManagers] WHERE [EmployeeId] = '{employeeId}'"));
  }

  // ---- THE ROWVERSION IS REAL, AND THE SERVER CHANGES IT ON UPDATE.
  //
  // Optimistic concurrency depends on the column being server-generated. A column that never changed would
  // make every stale-token check pass.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_department_rowversion_changes_when_the_row_changes()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var departmentId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(departmentId, fixture.CompanyA, "SALES", "Sales");

    var before = await fixture.StringAsync(
      $"SELECT CONVERT(VARCHAR(64), [RowVersion], 1) FROM [tenant].[Departments] " +
      $"WHERE [DepartmentId] = '{departmentId}'");

    await fixture.ExecuteAsync(
      $"UPDATE [tenant].[Departments] SET [Name] = N'Sales Team' WHERE [DepartmentId] = '{departmentId}'");

    var after = await fixture.StringAsync(
      $"SELECT CONVERT(VARCHAR(64), [RowVersion], 1) FROM [tenant].[Departments] " +
      $"WHERE [DepartmentId] = '{departmentId}'");

    Assert.NotEqual(before, after);
  }

  // ================================================================================================
  // THE APPEND-ONLY HISTORY
  // ================================================================================================

  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task Department_history_rows_append_and_the_initial_record_has_no_source()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var first = Guid.NewGuid();
    var second = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(first, fixture.CompanyA, "SALES", "Sales");
    await fixture.InsertDepartmentAsync(second, fixture.CompanyA, "OPS", "Operations");
    var employeeId = await fixture.InsertEmployeeAsync("E-0001");

    await fixture.InsertAssignmentAsync(employeeId, sourceDepartmentId: null, destinationDepartmentId: first);
    await fixture.InsertAssignmentAsync(employeeId, sourceDepartmentId: first, destinationDepartmentId: second);

    Assert.Equal(2, await fixture.ScalarAsync(
      $"SELECT COUNT(*) FROM [tenant].[EmployeeDepartmentAssignments] WHERE [EmployeeId] = '{employeeId}'"));

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [tenant].[EmployeeDepartmentAssignments] " +
      $"WHERE [EmployeeId] = '{employeeId}' AND [SourceDepartmentId] IS NULL"));
  }

  // A record can never describe a move to the department it came from.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_history_row_cannot_name_the_same_source_and_destination()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var departmentId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(departmentId, fixture.CompanyA, "SALES", "Sales");
    var employeeId = await fixture.InsertEmployeeAsync("E-0001");

    var violation = await Assert.ThrowsAsync<SqlException>(() =>
      fixture.InsertAssignmentAsync(employeeId, departmentId, departmentId));

    Assert.Contains(
      "CK_EmployeeDepartmentAssignments_SourceDiffersFromDestination",
      violation.Message,
      StringComparison.Ordinal);
  }

  // ---- THE HISTORY TABLE CARRIES NO CONCURRENCY OR MODIFICATION STATE.
  //
  // Read from the server. A RowVersion or a ModifiedUtc column here would mean somebody expected the row to
  // change, and the whole model depends on it never doing so.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task The_history_table_has_no_rowversion_and_no_modified_columns()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();

    var unexpected = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'EmployeeDepartmentAssignments' " +
      "AND c.name IN (N'RowVersion', N'ModifiedUtc', N'ModifiedBy', N'EffectiveToUtc')");

    Assert.Equal(0, unexpected);
  }

  // ---- AND A HISTORY ROW CANNOT BE ORPHANED.
  //
  // Deleting the department a history row names is refused. History that outlived the thing it refers to
  // would be unreadable, which is the failure RESTRICT prevents.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public async Task A_department_named_by_history_cannot_be_deleted()
  {
    await using var fixture = await DepartmentFixture.CreateAsync();
    var departmentId = Guid.NewGuid();

    await fixture.InsertDepartmentAsync(departmentId, fixture.CompanyA, "SALES", "Sales");
    var employeeId = await fixture.InsertEmployeeAsync("E-0001");
    await fixture.InsertAssignmentAsync(employeeId, null, departmentId);

    var refusal = await Assert.ThrowsAsync<SqlException>(() => fixture.ExecuteAsync(
      $"DELETE FROM [tenant].[Departments] WHERE [DepartmentId] = '{departmentId}'"));

    Assert.Contains("FK_EmployeeDepartmentAssignments_Departments", refusal.Message, StringComparison.Ordinal);
  }

  // ================================================================================================
  // FIXTURE
  // ================================================================================================
  //
  // Deliberately small. It creates one tenant database, migrates it through the real chain, and seeds the
  // two Platform rows the foreign keys require. There is no authorization graph, because Phase 1 has no
  // application path to authorize.
  private sealed class DepartmentFixture : IAsyncDisposable
  {
    private const string Actor = "department-phase1-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string tenantCatalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid CompanyB { get; } = Guid.NewGuid();

    public Guid BranchA { get; } = Guid.NewGuid();

    public static async Task<DepartmentFixture> CreateAsync()
    {
      var fixture = new DepartmentFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    public async Task<int> ScalarAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(tenantCatalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
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

    public Task InsertDepartmentAsync(
      Guid departmentId, Guid companyId, string code, string name, Guid? parentDepartmentId = null) =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[Departments]
          ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [NormalizedName],
           [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{departmentId}', '{Tenant}', '{companyId}', N'{code}', N'{code.ToUpperInvariant()}', N'{name}', N'{name.ToUpperInvariant()}',
           {(parentDepartmentId is null ? "NULL" : $"'{parentDepartmentId}'")},
           N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    // Created on first use with a reserved code no test names, so it satisfies the foreign key without
    // appearing in any assertion about the departments under test.
    private Guid? holdingDepartment;

    // The position twin, added by FP-008 Phase 3 for the same reason and on the same terms: `PositionId` is
    // NOT NULL with a RESTRICT foreign key, so a seeded employee needs a real position to point at.
    private Guid? holdingPosition;

    private async Task<Guid> HoldingDepartmentAsync()
    {
      if (holdingDepartment is { } existing)
      {
        return existing;
      }

      var departmentId = Guid.NewGuid();

      await InsertDepartmentAsync(departmentId, CompanyA, "ZZ-EMPLOYEE-HOME", "Employee Home");

      holdingDepartment = departmentId;

      return departmentId;
    }

    private async Task<Guid> HoldingPositionAsync()
    {
      if (holdingPosition is { } existing)
      {
        return existing;
      }

      var positionId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Positions]
          ([PositionId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Title], [NormalizedTitle],
           [JobGradeId], [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy],
           [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{positionId}', '{Tenant}', '{CompanyA}', N'ZZ-EMPLOYEE-HOME', N'ZZ-EMPLOYEE-HOME',
           N'Employee Home', N'EMPLOYEE HOME', NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      holdingPosition = positionId;

      return positionId;
    }

    public async Task<Guid> InsertEmployeeAsync(string employeeNumber)
    {
      var employeeId = Guid.NewGuid();

      // FP-007 Phase 3 made DepartmentId NOT NULL with a real foreign key, so a seeded employee needs a
      // department. A holding department created once per fixture keeps that incidental fact out of the
      // schema assertions, which count and inspect the departments the tests themselves create.
      var homeDepartment = await HoldingDepartmentAsync();
      var homePosition = await HoldingPositionAsync();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Employees]
          ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [PositionId],
           [EmployeeNumber],
           [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [Status], [StatusChangeReasonCode],
           [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{employeeId}', '{Tenant}', '{CompanyA}', '{BranchA}', '{homeDepartment}', '{homePosition}',
           N'{employeeNumber}',
           N'{employeeNumber.ToUpperInvariant()}', N'Person {employeeNumber}', SYSDATETIMEOFFSET(),
           N'Active', N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      // Employee's own append-only branch history: the FK from EmployeeBranchAssignments is restricted, but
      // an Employee with no initial assignment would be a shape FP-006 forbids, so the fixture writes one.
      await ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeBranchAssignments]
          ([EmployeeBranchAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourceBranchId],
           [DestinationBranchId], [EffectiveFromUtc], [TransferredBy], [ReasonCode], [CreatedUtc], [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{CompanyA}', '{employeeId}', NULL, '{BranchA}',
           SYSDATETIMEOFFSET(), N'{Actor}', N'InitialAssignment', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return employeeId;
    }

    public Task InsertManagerAsync(Guid departmentId, Guid employeeId) =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[DepartmentManagers]
          ([DepartmentId], [TenantId], [CompanyId], [EmployeeId], [AssignedUtc], [AssignedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{departmentId}', '{Tenant}', '{CompanyA}', '{employeeId}', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    public Task InsertAssignmentAsync(
      Guid employeeId, Guid? sourceDepartmentId, Guid destinationDepartmentId) =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeDepartmentAssignments]
          ([EmployeeDepartmentAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourceDepartmentId],
           [DestinationDepartmentId], [EffectiveFromUtc], [ChangedBy], [CreatedUtc], [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{CompanyA}', '{employeeId}',
           {(sourceDepartmentId is null ? "NULL" : $"'{sourceDepartmentId}'")},
           '{destinationDepartmentId}', SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

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
      tenantCatalog = $"SSAS_FP007_Tenant_{token}";

      await MasterAsync($"CREATE DATABASE [{tenantCatalog}]");

      // THE REAL MIGRATION CHAIN, with the real contributor. A contributor-free context would migrate
      // Platform's tenant tables and silently create none of HR's.
      await using (var connection = new SqlConnection(ConnectionFor(tenantCatalog)))
      {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
          .UseSqlServer(connection, sql => sql.MigrationsHistoryTable(
            TenantPersistenceConstants.MigrationHistoryTable,
            TenantPersistenceConstants.MigrationHistorySchema))
          .Options;

        await using var context = new TenantDbContext(
          options, new FixtureUser(), new FixtureTenant(), new FixtureClock(),
          modelContributors: [new HrTenantModelContributor()]);

        await context.Database.MigrateAsync();
      }

      // The Platform rows the foreign keys require. Written directly because Phase 1 has no reason to build
      // a Platform application graph, and the columns needed are few and stable.
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
      IntegrationSqlEnvironment.BaseConnectionString;

    private static string ConnectionFor(string catalog) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = catalog }.ConnectionString;

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

    private sealed class FixtureTenant : ICurrentTenant
    {
      public Guid? TenantId => null;
    }

    private sealed class FixtureClock : IDateTimeProvider
    {
      public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
  }
}
