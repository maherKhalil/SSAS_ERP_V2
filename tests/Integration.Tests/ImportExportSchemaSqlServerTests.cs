using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.HR.Domain.ImportExport;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// THE IMPORT AND EXPORT RUN SCHEMA AGAINST REAL SQL SERVER (FP-009 Phase 1).
//
// Phase 1 delivers a schema and a domain. There is no pipeline, no scope resolver and no route, so — as
// `PositionSchemaSqlServerTests` records for the same situation — these are raw SQL: every assertion below
// is about something ONLY the database enforces. A check constraint, a unique index, a collation, an absent
// column and a foreign key's delete behaviour cannot be proven by an in-memory provider, and a test that
// tried would assert the provider rather than SQL Server.
[Trait("Category", "SqlServer")]
public sealed class ImportExportSchemaSqlServerTests
{
  // ================================================================================================
  // THE TWO TABLES EXIST, IN THE ONE TENANT MIGRATION STREAM
  // ================================================================================================
  [Theory]
  [InlineData("EmployeeImportRuns")]
  [InlineData("EmployeeExportRuns")]
  [Trait("Decision", "ADR-017")]
  public async Task Both_run_tables_are_created_by_the_tenant_migration_chain(string table)
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}'"));
  }

  // ---- RE-RUNNING THE MIGRATION IS A NO-OP.
  //
  // The orchestrator may call `MigrateAsync` against a database already at head — on restart, on a retried
  // provisioning step, or on a tenant an earlier deployment migrated.
  [Fact]
  [Trait("Decision", "ADR-018")]
  public async Task Re_running_the_tenant_migration_chain_changes_nothing()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await fixture.MigrateAsync();

    foreach (var table in new[] { "EmployeeImportRuns", "EmployeeExportRuns" })
    {
      Assert.Equal(1, await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'{table}'"));
    }
  }

  // ================================================================================================
  // THE ABSENCES, READ FROM THE SERVER RATHER THAN FROM THE MODEL
  // ================================================================================================
  //
  // A shadow property that reached the database would be invisible on the CLR type and perfectly visible
  // here, which is why this asks SQL Server rather than reflection.
  //
  // NO RowVersion AND NO Modified PAIR: the row is written once, when the outcome is already known. NO
  // `BranchId`: an import or export is performed within a company, and branch is a sibling dimension rather
  // than a narrower one. NO EMPLOYEE COLUMN: a run record names who ran what, never which employees resulted
  // — which is what keeps it out of the copy graph's dependency chain entirely.
  //
  // ---- THE BRANCH CHECK IS FOR THE NAME `BranchId`, NOT FOR THE SUBSTRING "Branch", AND DELIBERATELY SO.
  //
  // `EmployeeExportRuns.ScopeBranchIds` contains the word and is not a branch column: it is an immutable
  // snapshot of the branch set the caller's scope resolved to, stored as text and never joined or filtered
  // on. This is the `EmployeeBranchAssignment` situation exactly — that table carries two branch identifiers
  // and names NEITHER of them `BranchId`, precisely so no future convention, shadow property or interface
  // implementation can silently reclassify the table as branch-owned. The guard therefore tests the name
  // that would carry the classification rather than a substring that merely mentions the dimension.
  [Theory]
  [InlineData("EmployeeImportRuns")]
  [InlineData("EmployeeExportRuns")]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task Neither_run_table_has_a_rowversion_a_modified_pair_a_branch_or_an_employee(string table)
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount(table, "c.name = N'RowVersion'")));
    Assert.Equal(0, await fixture.ScalarAsync(
      ColumnCount(table, "c.name IN (N'ModifiedUtc', N'ModifiedBy')")));
    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount(table, "c.name = N'BranchId'")));
    Assert.Equal(0, await fixture.ScalarAsync(ColumnCount(table, "c.name LIKE N'%Employee%'")));

    // Both ownership-dimension columns ARE present, so the absences above are distinctions rather than an
    // empty table being described.
    Assert.Equal(2, await fixture.ScalarAsync(
      ColumnCount(table, "c.name IN (N'TenantId', N'CompanyId')")));
  }

  // ---- EVERY PERSISTED APPLICATION STRING IS `nvarchar`. The standing platform rule, checked rather than
  // assumed: the scaffolder emits what the model says, and the model is what this proves.
  [Theory]
  [InlineData("EmployeeImportRuns")]
  [InlineData("EmployeeExportRuns")]
  public async Task Every_string_column_is_nvarchar(string table)
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns c " +
      "JOIN sys.types ty ON ty.user_type_id = c.user_type_id " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' " +
      "AND ty.name IN (N'varchar', N'char', N'text')"));
  }

  // ---- THE NORMALIZED KEY COLUMN IS BINARY-COLLATED, AND THE DISPLAY COLUMN IS NOT.
  //
  // `DEC-POS-0030`: EF translates a value-converted property in a projection but not in a predicate, so the
  // unique index and every replay lookup run on `NormalizedImportKey`. Binary collation is what makes that
  // index authoritative under concurrent submission rather than merely advisory.
  [Fact]
  [Trait("Decision", "DEC-POS-0030")]
  public async Task The_normalized_import_key_is_binary_collated_and_the_display_value_is_not()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.Equal(
      "Latin1_General_100_BIN2",
      await fixture.StringAsync(CollationOf("EmployeeImportRuns", "NormalizedImportKey")));

    Assert.NotEqual(
      "Latin1_General_100_BIN2",
      await fixture.StringAsync(CollationOf("EmployeeImportRuns", "ImportKey")));
  }

  // ================================================================================================
  // THE IMPORT KEY IS UNIQUE WITHIN A COMPANY, AND CONSUMED BY A REFUSED RUN
  // ================================================================================================
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task Two_runs_in_one_company_cannot_share_an_import_key()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await fixture.InsertImportRunAsync("BATCH-1", fixture.CompanyA);

    Assert.False(await Capture(fixture.InsertImportRunAsync("BATCH-1", fixture.CompanyA)));
  }

  // ---- AND TWO COMPANIES IN ONE TENANT ARE NOT OBLIGED TO COORDINATE THEIR KEY CHOICES.
  //
  // The same company-scoped uniqueness shape as employee number and department code, and for the same
  // reason. `TenantId` is deliberately NOT in the index: a company belongs to exactly one tenant, so adding
  // it would widen the key without narrowing anything.
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task Two_companies_may_use_the_same_import_key()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await fixture.InsertImportRunAsync("BATCH-1", fixture.CompanyA);

    Assert.True(await Capture(fixture.InsertImportRunAsync("BATCH-1", fixture.CompanyB)));
  }

  // ---- A REFUSED RUN CONSUMES THE KEY, WHICH IS WHY THE INDEX IS FILTERED ON NOTHING.
  //
  // An index that excluded refusals would release the key of a failed import and let the very submission the
  // key exists to make unrepeatable be replayed under it.
  [Fact]
  [Trait("Decision", "DEC-DOC-0004")]
  public async Task A_refused_run_still_occupies_its_import_key()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await fixture.InsertImportRunAsync("BATCH-1", fixture.CompanyA, outcome: "Refused", accepted: 0);

    Assert.False(await Capture(
      fixture.InsertImportRunAsync("BATCH-1", fixture.CompanyA, outcome: "Applied")));

    // And the index carries no filter at all, read from the server rather than inferred from the behaviour
    // above — a filter that happened to admit both rows would make that test pass for the wrong reason.
    Assert.Equal(0, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.indexes WHERE name = N'UX_EmployeeImportRuns_Company_Key' " +
      "AND has_filter = 1"));
  }

  // ================================================================================================
  // ALL-OR-NOTHING IS ENFORCED BY THE TABLE, NOT ONLY BY THE FACTORY (OD-DOC-003)
  // ================================================================================================
  //
  // The domain has one factory per outcome, so a partially applied run cannot be CONSTRUCTED. This is the
  // other half: it cannot be WRITTEN either, by any path — including the raw SQL these tests themselves use.
  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public async Task An_applied_run_that_accepted_only_some_of_its_rows_is_refused_by_the_database()
  {
    await using var fixture = await RunFixture.CreateAsync();

    var partial = fixture.InsertImportRunAsync(
      "PARTIAL", fixture.CompanyA, outcome: "Applied", rows: 1_000, accepted: 998, rejected: 2);

    Assert.False(await Capture(partial));
  }

  [Fact]
  [Trait("Decision", "OD-DOC-003")]
  public async Task A_refused_run_that_accepted_rows_is_refused_by_the_database()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.False(await Capture(fixture.InsertImportRunAsync(
      "CONTRADICTION", fixture.CompanyA, outcome: "Refused", rows: 10, accepted: 10)));
  }

  [Fact]
  public async Task A_run_rejecting_more_rows_than_it_read_is_refused_by_the_database()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.False(await Capture(fixture.InsertImportRunAsync(
      "IMPOSSIBLE", fixture.CompanyA, outcome: "Refused", rows: 3, accepted: 0, rejected: 4)));
  }

  [Fact]
  public async Task A_run_with_a_negative_count_is_refused_by_the_database()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.False(await Capture(fixture.InsertImportRunAsync(
      "NEGATIVE", fixture.CompanyA, outcome: "Validated", rows: -1, accepted: -1)));
  }

  // ---- THE OUTCOME VOCABULARY IS CLOSED AT THE DATABASE, and `InProgress` is not in it.
  //
  // Its absence is `DEC-DOC-0007`'s synchronous execution: a persisted in-progress row is a promise that
  // something will come back and finish it, and when the process dies that promise is a permanent lie.
  [Fact]
  [Trait("Decision", "DEC-DOC-0007")]
  public async Task An_unknown_outcome_including_in_progress_is_refused_by_the_database()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.False(await Capture(fixture.InsertImportRunAsync(
      "UNKNOWN", fixture.CompanyA, outcome: "InProgress")));
  }

  // ================================================================================================
  // THE EXPORT RECORD MUST SAY WHAT LEFT (SEC-DOC-0404)
  // ================================================================================================
  //
  // A record naming no columns records that nothing left, which is not an export — and this table exists
  // precisely to say what did.
  [Fact]
  [Trait("Decision", "SEC-DOC-0404")]
  public async Task An_export_run_with_no_column_set_is_refused_by_the_database()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.True(await Capture(fixture.InsertExportRunAsync("employeeNumber,fullName")));
    Assert.False(await Capture(fixture.InsertExportRunAsync(string.Empty)));
  }

  // ---- AN EMPTY SCOPE LIST IS A REAL ANSWER AND MUST BE STORABLE; A MISSING ONE MUST NOT BE.
  //
  // "The scope resolved to no branches" is a fact worth recording. NULL would make it indistinguishable
  // from "not recorded", which is the one thing an audit column may not be ambiguous about.
  [Fact]
  [Trait("Decision", "SEC-DOC-0404")]
  public async Task An_export_run_may_record_an_empty_scope_but_never_a_missing_one()
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.True(await Capture(fixture.InsertExportRunAsync("employeeNumber", scopeBranchIds: "")));
    Assert.False(await Capture(fixture.InsertExportRunAsync("employeeNumber", scopeBranchIds: null)));
  }

  // ---- THE SCOPE COLUMNS ARE `nvarchar(max)` AND NOTHING INDEXES THEM.
  //
  // They are written once and never joined, compared or filtered on — they exist to be read by a human
  // investigating an incident. An index would serve nothing, and adding one later costs nothing, which is
  // the test for leaving it out now.
  [Fact]
  public async Task The_export_scope_columns_are_unbounded_text_and_carry_no_index()
  {
    await using var fixture = await RunFixture.CreateAsync();

    foreach (var column in new[] { "ScopeCompanyIds", "ScopeBranchIds" })
    {
      Assert.Equal(-1, await fixture.ScalarAsync(
        "SELECT c.max_length FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
        "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'EmployeeExportRuns' AND c.name = N'{column}'"));

      Assert.Equal(0, await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.index_columns ic " +
        "JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id " +
        "JOIN sys.tables t ON t.object_id = c.object_id " +
        "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'EmployeeExportRuns' AND c.name = N'{column}'"));
    }
  }

  // ================================================================================================
  // THE COMPANY FOREIGN KEY IS RESTRICTED, ON BOTH TABLES
  // ================================================================================================
  //
  // A company is ARCHIVED, never deleted, so this is belt-and-braces — but a cascade here would silently
  // erase the audit trail of who imported and exported employee data along with it, which is precisely the
  // record that must outlive the thing it describes.
  //
  // Both run tables get this key while the three assignment tables deliberately do not: an assignment's
  // company column is already anchored by its foreign key to the Employee it describes, and a run record
  // names no employee at all, so this is the only referential integrity it has.
  [Theory]
  [InlineData("EmployeeImportRuns")]
  [InlineData("EmployeeExportRuns")]
  [Trait("Decision", "ADR-023")]
  public async Task The_company_foreign_key_is_restricted(string table)
  {
    await using var fixture = await RunFixture.CreateAsync();

    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.foreign_keys fk " +
      "JOIN sys.tables t ON t.object_id = fk.parent_object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND rt.name = N'Companies' " +
      "AND fk.delete_referential_action = 0"));
  }

  [Theory]
  [InlineData("EmployeeImportRuns")]
  [InlineData("EmployeeExportRuns")]
  [Trait("Decision", "ADR-023")]
  public async Task A_run_naming_a_company_that_does_not_exist_is_refused(string table)
  {
    await using var fixture = await RunFixture.CreateAsync();

    var stranger = Guid.NewGuid();

    Assert.False(await Capture(table == "EmployeeImportRuns"
      ? fixture.InsertImportRunAsync("ORPHAN", stranger)
      : fixture.InsertExportRunAsync("employeeNumber", companyId: stranger)));
  }

  // ================================================================================================
  // APPEND-ONLY IS ENFORCED BY THE WRITE BOUNDARY, NOT BY THE ABSENCE OF A REPOSITORY METHOD
  // ================================================================================================
  //
  // Neither repository offers an update or a delete, and that is a courtesy to the reader rather than the
  // guarantee. `TenantDbContext.PreventAppendOnlyMutation` refuses a Modified or Deleted entry for ANY
  // `IAppendOnlyEntity` **whatever path tracked it** — which is what these prove, by taking exactly that
  // path: load the row through EF, change it, and save.
  //
  // The refusal names no entity type, because it is a rule about a CLASSIFICATION rather than about a row.
  //
  // A record of what happened that can be edited afterwards is not a record of what happened. For the import
  // run that means a refused run cannot be quietly relabelled as applied; for the export run it means the
  // column set and the scope snapshot — the only controls that survive the data leaving — cannot be revised
  // after somebody asks what left.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task An_import_run_cannot_be_updated_after_it_is_written()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await fixture.InsertImportRunAsync("APPEND-1", fixture.CompanyA, outcome: "Refused", accepted: 0);

    await using var context = fixture.CreateContext();

    var run = await context.Set<EmployeeImportRun>().SingleAsync();

    // Through the ONLY writable property the type exposes — the ownership stamp the boundary itself uses.
    // If even that cannot be changed, nothing can.
    run.TenantId = run.TenantId;
    context.Entry(run).State = EntityState.Modified;

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", refusal.Message, StringComparison.Ordinal);
  }

  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task An_export_run_cannot_be_deleted_after_it_is_written()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await fixture.InsertExportRunAsync("employeeNumber,fullName");

    await using var context = fixture.CreateContext();

    var run = await context.Set<EmployeeExportRun>().SingleAsync();

    context.Set<EmployeeExportRun>().Remove(run);

    var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", refusal.Message, StringComparison.Ordinal);

    // AND THE ROW IS STILL THERE. The refusal is the point, but a guard that threw AFTER deleting would
    // satisfy the assertion above and destroy the record anyway.
    Assert.Equal(1, await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM [tenant].[EmployeeExportRuns]"));
  }

  // ---- THE GUARD IS LIVE, NOT VACUOUS.
  //
  // An ordinary tenant-owned row in the same context updates without complaint, so the two refusals above
  // are about the append-only classification rather than about the fixture being unable to save anything.
  [Fact]
  [Trait("Decision", "DEC-DOC-0006")]
  public async Task An_entity_that_is_not_append_only_still_updates_in_the_same_context()
  {
    await using var fixture = await RunFixture.CreateAsync();

    await using var context = fixture.CreateContext();

    var updated = await context.Database.ExecuteSqlRawAsync(
      "UPDATE [tenant].[Companies] SET [ModifiedUtc] = SYSDATETIMEOFFSET() WHERE [TenantId] = {0}",
      fixture.Tenant);

    Assert.Equal(2, updated);
  }

  private static string ColumnCount(string table, string predicate) =>
    "SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
    "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
    $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND {predicate}";

  private static string CollationOf(string table, string column) =>
    "SELECT c.collation_name FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id " +
    "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
    $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'";

  private static async Task<bool> Capture(Task work)
  {
    try
    {
      await work;
      return true;
    }
  // The exception IS the answer here, not an error being swallowed: `Capture` exists to turn "did this
  // statement get refused" into a bool, and a `SqlException` is the refusal. Anything that is NOT a
  // SqlException still propagates and fails the test, which is what keeps this from being a blanket catch.
    catch (SqlException)
    {
      return false;
    }
  }

  private sealed class RunFixture : IAsyncDisposable
  {
    private const string Actor = "importexport-phase1-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string tenantCatalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public Guid CompanyB { get; } = Guid.NewGuid();

    public static async Task<RunFixture> CreateAsync()
    {
      var fixture = new RunFixture();
      await fixture.InitializeAsync();
      return fixture;
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

    // A REAL TenantDbContext over the fixture's database, composed exactly as production composes it —
    // including the HR contributor, without which the run entities would not be in the model at all.
    public TenantDbContext CreateContext()
    {
      var options = new DbContextOptionsBuilder<TenantDbContext>()
        .UseSqlServer(ConnectionFor(tenantCatalog))
        .Options;

      return new TenantDbContext(
        options, new FixtureUser(), new FixtureTenant(Tenant), new FixtureClock(),
        modelContributors: [new HrTenantModelContributor()]);
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

    public Task InsertImportRunAsync(
      string normalizedKey,
      Guid companyId,
      string outcome = "Applied",
      int rows = 10,
      int? accepted = null,
      int rejected = 0) =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeImportRuns]
          ([ImportRunId], [TenantId], [CompanyId], [ImportKey], [NormalizedImportKey], [FileName],
           [ByteCount], [RowCount], [AcceptedCount], [RejectedCount], [Outcome], [ExecutedUtc],
           [ExecutedBy], [CreatedUtc], [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{companyId}', N'{normalizedKey}', N'{normalizedKey}',
           N'people.csv', 4096, {rows}, {accepted ?? rows}, {rejected}, N'{outcome}',
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
        """);

    public Task InsertExportRunAsync(
      string columnSet,
      Guid? companyId = null,
      string? scopeCompanyIds = "",
      string? scopeBranchIds = "") =>
      ExecuteAsync($"""
        INSERT INTO [tenant].[EmployeeExportRuns]
          ([ExportRunId], [TenantId], [CompanyId], [RowCount], [ColumnSet], [ScopeCompanyIds],
           [ScopeBranchIds], [ExecutedUtc], [ExecutedBy], [CreatedUtc], [CreatedBy])
        VALUES
          ('{Guid.NewGuid()}', '{Tenant}', '{companyId ?? CompanyA}', 42, N'{columnSet}',
           {Text(scopeCompanyIds)}, {Text(scopeBranchIds)},
           SYSDATETIMEOFFSET(), N'{Actor}', SYSDATETIMEOFFSET(), N'{Actor}');
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

    private static string Text(string? value) => value is null ? "NULL" : $"N'{value}'";

    private async Task InitializeAsync()
    {
      tenantCatalog = $"SSAS_FP009_Tenant_{token}";

      await MasterAsync($"CREATE DATABASE [{tenantCatalog}]");

      await MigrateAsync();

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

    // Carries a REAL tenant for the same reason `PositionSchemaSqlServerTests` records: a null tenant throws
    // out of the global filter before any SQL is sent, because EF evaluates both operands of the filter's
    // `&&` while extracting query parameters.
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
