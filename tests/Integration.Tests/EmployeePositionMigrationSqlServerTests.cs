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

// THE FAIL-LOUD MIGRATION (TS-POS-0043, DEC-POS-0026, OD-POS-001).
//
// ==================================================================================================
// THIS IS THE TEST THAT MAKES THE ONE-WAY DOOR SAFE.
// ==================================================================================================
//
// `OD-POS-001` ruled `Employee.PositionId` NOT NULL with no backfill, on the operational fact that no
// production employees existed. `DEC-POS-0026` required the migration to ASSERT that fact rather than assume
// it — per tenant database, every run, before any DDL.
//
// So the interesting case is not the happy one. It is a database that holds employees when the migration
// arrives: a tenant provisioned after the ruling, a restore, a demo catalog, or a customer-managed database
// under `ADR-021`. What must happen there is a loud, transactional stop with an actionable message — and
// what must NOT happen is any of the four accommodations, above all the silent one where a default is
// supplied and every existing employee acquires a position nobody chose.
//
// These tests run the real migration against a real database at exactly the migration before it, which is
// the only place that state exists.
[Trait("Category", "SqlServer")]
public sealed class EmployeePositionMigrationSqlServerTests
{
  // The migration immediately before the one under test. Reaching this target leaves Positions present and
  // Employees without a PositionId column — the state a customer database is in when the upgrade lands.
  private const string BeforeMigration = "20260821140615_AddHrSearchNormalizedLabels";

  private const string PositionMigration = "20260821161613_AddEmployeePosition";

  // ---- THE EMPTY DATABASE: THE MIGRATION APPLIES, AND THE COLUMN IS REQUIRED.
  //
  // The control. Without it, a test that only proves the refusal would also pass if the migration refused
  // everything — including the case it is meant to allow.
  [Fact]
  [Trait("Decision", "DEC-POS-0026")]
  public async Task An_empty_employee_table_admits_the_migration_and_gets_a_required_column()
  {
    await using var fixture = await PositionMigrationFixture.CreateAsync();

    await fixture.MigrateAsync(PositionMigration);

    // NOT NULL, and no default constraint: the scaffolded `defaultValue` form would have left one behind,
    // and its absence is what proves the accommodation was removed rather than merely unused.
    Assert.Equal(0, await fixture.NullableColumnCountAsync("Employees", "PositionId"));
    Assert.Equal(0, await fixture.DefaultConstraintCountAsync("Employees", "PositionId"));

    // And the foreign key exists, which is what finally orders Positions before Employees in the cutover.
    Assert.Equal(1, await fixture.ForeignKeyCountAsync("FK_Employees_Positions_PositionId"));
  }

  // ---- THE POPULATED DATABASE: IT STOPS, AND IT SAYS WHY.
  [Fact]
  [Trait("Scenario", "TS-POS-0043")]
  [Trait("Decision", "DEC-POS-0026")]
  public async Task A_database_holding_employees_refuses_the_migration_with_the_recorded_decision()
  {
    await using var fixture = await PositionMigrationFixture.CreateAsync();

    await fixture.SeedLegacyEmployeeAsync("EMP-LEGACY-1");

    var failure = await Assert.ThrowsAnyAsync<SqlException>(
      () => fixture.MigrateAsync(PositionMigration));

    // ---- THE MESSAGE IS PART OF THE CONTRACT, NOT DECORATION.
    //
    // An operator reading a failed deployment log has to be able to ACT. `DEC-POS-0026` specifies exactly
    // what it must carry, and each clause is asserted rather than trusted to a reviewer.
    Assert.Contains("FP-008", failure.Message, StringComparison.Ordinal);

    // The database, because an operator upgrading forty tenants needs to know WHICH one stopped.
    Assert.Contains(fixture.Catalog, failure.Message, StringComparison.Ordinal);

    // The row count found.
    Assert.Contains("1 row(s)", failure.Message, StringComparison.Ordinal);

    // The recorded decision, so the reasoning is readable rather than guessable from a constraint name.
    Assert.Contains("DEC-POS-0009", failure.Message, StringComparison.Ordinal);
    Assert.Contains("OD-POS-001", failure.Message, StringComparison.Ordinal);

    // The one remedy — and explicitly NOT "edit the migration".
    Assert.Contains("REMEDY", failure.Message, StringComparison.Ordinal);
    Assert.Contains("Do NOT edit this migration", failure.Message, StringComparison.Ordinal);
  }

  // ---- AND IT WRITES NOTHING (DEC-POS-0026: "fails loudly and transactionally, and writes nothing").
  //
  // The column must be absent afterwards. This is the assertion that would catch the worst version of this
  // migration: one that added the column with a default, THEN discovered the rows, and left a partially
  // migrated database behind for someone to reconcile by hand.
  [Fact]
  [Trait("Scenario", "TS-POS-0043")]
  [Trait("Decision", "DEC-POS-0026")]
  public async Task A_refused_migration_leaves_the_schema_and_the_employees_exactly_as_they_were()
  {
    await using var fixture = await PositionMigrationFixture.CreateAsync();

    var employeeId = await fixture.SeedLegacyEmployeeAsync("EMP-LEGACY-2");

    await Assert.ThrowsAnyAsync<SqlException>(() => fixture.MigrateAsync(PositionMigration));

    // NO COLUMN. Not a nullable one, not one with a default — none at all.
    Assert.Equal(0, await fixture.ColumnCountAsync("Employees", "PositionId"));
    Assert.Equal(0, await fixture.ForeignKeyCountAsync("FK_Employees_Positions_PositionId"));

    // AND NO ROW WAS TOUCHED. The employee is still there, unmodified — nothing was deleted to make room
    // for the constraint, which is the second forbidden accommodation.
    Assert.Equal(1, await fixture.EmployeeCountAsync());
    Assert.Equal("EMP-LEGACY-2", await fixture.EmployeeNumberAsync(employeeId));

    // The migration is not recorded as applied, so a corrected run can still apply it.
    Assert.Equal(0, await fixture.AppliedMigrationCountAsync(PositionMigration));
  }

  // ---- THE CHECK RUNS EVERY TIME, NOT ONCE.
  //
  // `DEC-POS-0026` scopes it "per tenant database, each time the migration runs". A database that is emptied
  // after a refusal must then be able to migrate — proving the refusal was a state check rather than a latch
  // that permanently condemns the database.
  [Fact]
  [Trait("Decision", "DEC-POS-0026")]
  public async Task A_database_emptied_after_a_refusal_migrates_on_the_next_run()
  {
    await using var fixture = await PositionMigrationFixture.CreateAsync();

    await fixture.SeedLegacyEmployeeAsync("EMP-LEGACY-3");

    await Assert.ThrowsAnyAsync<SqlException>(() => fixture.MigrateAsync(PositionMigration));

    // The operator resolves it — here by removing the rows, which stands in for whatever the architect
    // actually rules for a tenant in that state. The point is that the migration re-evaluates.
    await fixture.ExecuteAsync("DELETE FROM [tenant].[EmployeeBranchAssignments];");
    await fixture.ExecuteAsync("DELETE FROM [tenant].[Employees];");

    await fixture.MigrateAsync(PositionMigration);

    Assert.Equal(1, await fixture.ColumnCountAsync("Employees", "PositionId"));
    Assert.Equal(1, await fixture.AppliedMigrationCountAsync(PositionMigration));
  }

  private sealed class PositionMigrationFixture : IAsyncDisposable
  {
    private const string Actor = "position-migration-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string catalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid BranchA { get; } = Guid.NewGuid();

    public string Catalog => catalog;

    public static async Task<PositionMigrationFixture> CreateAsync()
    {
      var fixture = new PositionMigrationFixture();
      await fixture.InitializeAsync();
      return fixture;
    }

    // Through IMigrator with an explicit target rather than MigrateAsync(), so the database sits at exactly
    // the state a customer's does when this migration reaches it.
    public async Task MigrateAsync(string target)
    {
      await using var context = NewContext();

      await context.GetService<IMigrator>().MigrateAsync(target);
    }

    // Raw SQL because at this point in the chain the Employee ENTITY has a PositionId the TABLE does not,
    // so EF cannot insert one. That mismatch is the whole situation being tested — the same reason the
    // FP-007 department migration tests seed this way.
    public async Task<Guid> SeedLegacyEmployeeAsync(string number)
    {
      var employeeId = Guid.NewGuid();
      var departmentId = await SeedDepartmentAsync();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Employees]
          ([EmployeeId], [TenantId], [CompanyId], [BranchId], [DepartmentId], [EmployeeNumber],
           [NormalizedEmployeeNumber], [FullName], [EmploymentDate], [TerminationDate], [Status],
           [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc], [CreatedBy],
           [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{employeeId}', '{Tenant}', '{CompanyA}', '{BranchA}', '{departmentId}', N'{number}',
           N'{number.ToUpperInvariant()}', N'Person {number}', SYSDATETIMEOFFSET(), NULL, N'Active',
           N'Created', SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      await ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeBranchAssignments]
          ([EmployeeBranchAssignmentId], [TenantId], [CompanyId], [EmployeeId], [SourceBranchId],
           [DestinationBranchId], [EffectiveFromUtc], [TransferredBy], [ReasonCode], [CreatedUtc],
           [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{CompanyA}', '{employeeId}', NULL,
           '{BranchA}', SYSDATETIMEOFFSET(), N'{Actor}', N'InitialAssignment', SYSDATETIMEOFFSET(),
           N'{Actor}');
        """);

      return employeeId;
    }

    public Task<int> ColumnCountAsync(string table, string column) =>
      ScalarAsync<int>($"""
        SELECT COUNT(*) FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'
        """);

    public Task<int> NullableColumnCountAsync(string table, string column) =>
      ScalarAsync<int>($"""
        SELECT COUNT(*) FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}' AND c.is_nullable = 1
        """);

    public Task<int> DefaultConstraintCountAsync(string table, string column) =>
      ScalarAsync<int>($"""
        SELECT COUNT(*) FROM sys.default_constraints d
        JOIN sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'
        """);

    public Task<int> ForeignKeyCountAsync(string name) =>
      ScalarAsync<int>($"SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'{name}'");

    public Task<int> EmployeeCountAsync() =>
      ScalarAsync<int>("SELECT COUNT(*) FROM [tenant].[Employees]");

    public Task<string> EmployeeNumberAsync(Guid employeeId) =>
      ScalarAsync<string>(
        $"SELECT [EmployeeNumber] FROM [tenant].[Employees] WHERE [EmployeeId] = '{employeeId}'");

    public Task<int> AppliedMigrationCountAsync(string migrationId) =>
      ScalarAsync<int>($"""
        SELECT COUNT(*) FROM [{TenantPersistenceConstants.MigrationHistorySchema}].[{TenantPersistenceConstants.MigrationHistoryTable}]
        WHERE [MigrationId] = N'{migrationId}'
        """);

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

    private async Task<Guid> SeedDepartmentAsync()
    {
      var departmentId = Guid.NewGuid();

      await ExecuteAsync($"""
        INSERT INTO [tenant].[Departments]
          ([DepartmentId], [TenantId], [CompanyId], [Code], [NormalizedCode], [Name], [NormalizedName],
           [ParentDepartmentId], [Status], [StatusChangedUtc], [StatusChangedBy], [CreatedUtc],
           [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{departmentId}', '{Tenant}', '{CompanyA}', N'DEP', N'DEP', N'Department',
           N'DEPARTMENT', NULL, N'Active', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

      return departmentId;
    }

    private async Task InitializeAsync()
    {
      catalog = $"SSAS_FP008M_Tenant_{token}";

      await MasterAsync($"CREATE DATABASE [{catalog}]");

      // ---- STOP AT THE MIGRATION BEFORE THE ONE UNDER TEST.
      //
      // Positions exist, Employees does not yet have PositionId. That is precisely the state a customer
      // database is in when this migration reaches it.
      await using (var context = NewContext())
      {
        await context.GetService<IMigrator>().MigrateAsync(BeforeMigration);
      }

      await SeedCompanyAsync();
      await SeedBranchAsync();
    }

    private Task SeedCompanyAsync() =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[Companies]
          ([CompanyId], [TenantId], [CompanyCode], [NormalizedCompanyCode], [CompanyName],
           [BaseCurrencyCode], [Status], [StatusChangeReasonCode], [StatusChangedUtc], [StatusChangedBy],
           [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{CompanyA}', '{Tenant}', N'CMPA', N'CMPA', N'Company A',
           'SAR', N'Active', N'Created', SYSDATETIMEOFFSET(), N'{Actor}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    private Task SeedBranchAsync() =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[Branches]
          ([BranchId], [TenantId], [BranchCode], [NormalizedBranchCode], [BranchName],
           [IsMainBranch], [IsActive], [CreatedUtc], [CreatedBy], [ModifiedUtc], [ModifiedBy])
        VALUES
          ('{BranchA}', '{Tenant}', N'BRA', N'BRA', N'Branch A',
           1, 1, SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    private async Task<T> ScalarAsync<T>(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;

      var value = await command.ExecuteScalarAsync();

      return value is null or DBNull ? default! : (T)value;
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

    private static string Configured() =>
      IntegrationSqlEnvironment.BaseConnectionString;

    private static string ConnectionFor(string name) =>
      new SqlConnectionStringBuilder(Configured()) { InitialCatalog = name }.ConnectionString;

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
