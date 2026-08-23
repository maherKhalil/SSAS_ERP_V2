using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.GL.Domain.Accounts;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Domain.Journals;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// GL AGAINST REAL SQL (FP-011).
//
// ================================================================================================
// THIS IS WHERE THE GUARANTEES LIVE. The API tests could not prove any of them.
// ================================================================================================
//
// `IAppendOnlyEntity` is enforced by `TenantDbContext.PreventAppendOnlyMutation`, not by `JournalEntry`.
// The posting transaction is real only against a real database. `nvarchar` and `decimal(19,4)` are
// properties of COLUMNS, asserted from `sys.columns` rather than from the EF model — because asserting
// from the model tests the model's opinion of the database, and FP-009 established that the catalog views
// are the only version that catches a hand-written migration.
//
// Deliberately NOT in `TenantBackupSerialSuites`: this class creates one Guid-named disposable catalog and
// shares nothing across databases. The admission rule is explicit that "it needs real SQL" is an argument
// for being an integration test, not for being serial.
public sealed class GlSchemaSqlServerTests
{
  // ================================================================================================
  // SCHEMA — asserted from the catalog views
  // ================================================================================================

  [Theory]
  [Trait("Decision", "DEC-GL-0006")]
  [InlineData("GlAccounts", "Code")]
  [InlineData("GlAccounts", "NormalizedCode")]
  [InlineData("GlAccounts", "Name")]
  [InlineData("GlAccounts", "NormalizedName")]
  [InlineData("GlFiscalYears", "Code")]
  [InlineData("GlFiscalPeriods", "Name")]
  [InlineData("GlFiscalPeriods", "Status")]
  [InlineData("GlJournalDrafts", "Description")]
  [InlineData("GlJournalDrafts", "Reference")]
  [InlineData("GlJournalEntries", "JournalNumber")]
  [InlineData("GlJournalEntries", "Description")]
  [InlineData("GlJournalLines", "Description")]
  public async Task Every_gl_string_column_is_nvarchar(string table, string column)
  {
    await using var fixture = await GlFixture.CreateAsync();

    var type = await fixture.StringAsync(
      "SELECT ty.name FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "JOIN sys.types ty ON ty.user_type_id = c.user_type_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'");

    Assert.Equal("nvarchar", type);
  }

  [Theory]
  [Trait("Decision", "DEC-GL-0001")]
  [InlineData("GlJournalLines", "Debit")]
  [InlineData("GlJournalLines", "Credit")]
  [InlineData("GlJournalDraftLines", "Debit")]
  [InlineData("GlJournalDraftLines", "Credit")]
  public async Task Every_monetary_column_is_decimal_19_4(string table, string column)
  {
    await using var fixture = await GlFixture.CreateAsync();

    var precision = await fixture.ScalarAsync(
      "SELECT c.precision FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'");

    var scale = await fixture.ScalarAsync(
      "SELECT c.scale FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'{column}'");

    Assert.Equal(19, precision);
    Assert.Equal(4, scale);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0003")]
  public async Task The_accounts_table_has_no_company_column()
  {
    // The ruling, asserted where it would actually be broken — in the database, not in the model.
    await using var fixture = await GlFixture.CreateAsync();

    var columns = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.columns c " +
      "JOIN sys.tables t ON t.object_id = c.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'GlAccounts' AND c.name = N'CompanyId'");

    Assert.Equal(0, columns);
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0006")]
  public async Task No_gl_table_has_a_foreign_key_leaving_the_tenant_database()
  {
    // Cross-database foreign keys are not expressible in SQL Server, so this asserts the weaker thing that
    // IS checkable and is the real risk: every GL foreign key resolves inside this catalog.
    await using var fixture = await GlFixture.CreateAsync();

    var dangling = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.foreign_keys fk " +
      "JOIN sys.tables t ON t.object_id = fk.parent_object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name LIKE N'Gl%' " +
      "AND fk.referenced_object_id NOT IN (SELECT object_id FROM sys.tables)");

    Assert.Equal(0, dangling);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0005")]
  public async Task Journal_numbers_are_unique_within_company_and_fiscal_year()
  {
    await using var fixture = await GlFixture.CreateAsync();

    var indexed = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.indexes i " +
      "JOIN sys.tables t ON t.object_id = i.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'GlJournalEntries' " +
      "AND i.name = N'UX_GlJournalEntries_Tenant_Company_Year_Number' AND i.is_unique = 1");

    Assert.Equal(1, indexed);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0006")]
  public async Task Only_one_reversal_per_original_is_permitted_by_the_database()
  {
    // The aggregate refuses the second reversal, but two concurrent requests can both read "not yet
    // reversed". The FILTERED unique index is what makes the race unwinnable.
    await using var fixture = await GlFixture.CreateAsync();

    var filtered = await fixture.ScalarAsync(
      "SELECT COUNT(*) FROM sys.indexes i " +
      "JOIN sys.tables t ON t.object_id = i.object_id " +
      "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
      "WHERE s.name = N'tenant' AND t.name = N'GlJournalEntries' " +
      "AND i.name = N'UX_GlJournalEntries_OneReversalPerOriginal' " +
      "AND i.is_unique = 1 AND i.has_filter = 1");

    Assert.Equal(1, filtered);
  }

  [Fact]
  [Trait("Decision", "DEC-GL-0007")]
  public async Task Posted_journal_tables_carry_no_row_version_column()
  {
    await using var fixture = await GlFixture.CreateAsync();

    foreach (var table in new[] { "GlJournalEntries", "GlJournalLines" })
    {
      var rowVersions = await fixture.ScalarAsync(
        "SELECT COUNT(*) FROM sys.columns c " +
        "JOIN sys.tables t ON t.object_id = c.object_id " +
        "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
        $"WHERE s.name = N'tenant' AND t.name = N'{table}' AND c.name = N'RowVersion'");

      Assert.Equal(0, rowVersions);
    }
  }

  // ================================================================================================
  // THE APPEND-ONLY GUARANTEE — THROUGH THE REAL WRITE BOUNDARY
  // ================================================================================================

  [Fact]
  [Trait("Decision", "BR-GL-0002")]
  public async Task A_posted_journal_cannot_be_modified_by_attaching_it_directly_to_the_context()
  {
    // ---- THIS IS THE TEST THAT MATTERS, AND IT IS WHY THE INTERFACE EXISTS.
    //
    // Going through a repository proves only that "there is no repository method for it" — which
    // `IAppendOnlyEntity`'s own comment says is insufficient, because it protects only the callers who go
    // through the repository. Attaching directly and calling SaveChangesAsync is the path a future
    // developer takes, and the write boundary must refuse it.
    await using var fixture = await GlFixture.CreateAsync();
    var journalId = await fixture.SeedPostedJournalAsync();

    await using var context = fixture.CreateContext();

    var entry = await context.Set<JournalEntry>().FirstAsync(candidate => candidate.Id == journalId);
    context.Entry(entry).State = EntityState.Modified;

    var refused = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", refused.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0002")]
  public async Task A_posted_journal_line_cannot_be_deleted()
  {
    await using var fixture = await GlFixture.CreateAsync();
    var journalId = await fixture.SeedPostedJournalAsync();

    await using var context = fixture.CreateContext();

    var line = await context.Set<JournalLine>().FirstAsync(candidate => candidate.JournalEntryId == journalId);
    context.Set<JournalLine>().Remove(line);

    var refused = await Assert.ThrowsAsync<InvalidOperationException>(
      () => context.SaveChangesAsync());

    Assert.Contains("Append-only", refused.Message, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  [Trait("Decision", "OD-GL-0007")]
  public async Task A_draft_by_contrast_can_be_edited_and_deleted()
  {
    // The other half of the two-aggregate ruling. If this failed, the draft would be useless and
    // `OD-GL-0007` option 3 would have bought nothing.
    await using var fixture = await GlFixture.CreateAsync();

    Guid draftId;
    await using (var context = fixture.CreateContext())
    {
      var draft = JournalDraft.Create(fixture.EntryDate, "Draft", null).Value;
      draft.CompanyId = fixture.CompanyA;
      context.Set<JournalDraft>().Add(draft);
      await context.SaveChangesAsync();
      draftId = draft.Id;
    }

    await using (var context = fixture.CreateContext())
    {
      var draft = await context.Set<JournalDraft>().FirstAsync(candidate => candidate.Id == draftId);
      Assert.True(draft.Update(fixture.EntryDate, "Edited", "REF").IsSuccess);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      var draft = await context.Set<JournalDraft>().FirstAsync(candidate => candidate.Id == draftId);
      Assert.Equal("Edited", draft.Description);

      context.Set<JournalDraft>().Remove(draft);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      Assert.Empty(await context.Set<JournalDraft>().Where(candidate => candidate.Id == draftId).ToListAsync());
    }
  }

  // ================================================================================================
  // ROUND TRIPS
  // ================================================================================================

  [Fact]
  [Trait("Decision", "AC-GL-0003")]
  public async Task Amounts_round_trip_at_four_decimal_places()
  {
    await using var fixture = await GlFixture.CreateAsync();
    var journalId = await fixture.SeedPostedJournalAsync(debit: 1234.5678m);

    await using var context = fixture.CreateContext();
    var line = await context.Set<JournalLine>()
      .Where(candidate => candidate.JournalEntryId == journalId && candidate.Debit > 0m)
      .FirstAsync();

    Assert.Equal(1234.5678m, line.Debit);
  }

  [Fact]
  [Trait("Decision", "AC-GL-0019")]
  public async Task Arabic_text_round_trips_unchanged()
  {
    const string arabic = "حساب المدينون التجاريون";

    await using var fixture = await GlFixture.CreateAsync();

    Guid accountId;
    await using (var context = fixture.CreateContext())
    {
      var account = Account.Create("4100", arabic).Value;
      context.Set<Account>().Add(account);
      await context.SaveChangesAsync();
      accountId = account.Id;
    }

    await using (var context = fixture.CreateContext())
    {
      var account = await context.Set<Account>().FirstAsync(candidate => candidate.Id == accountId);
      Assert.Equal(arabic, account.Name.Value);
    }
  }

  [Fact]
  [Trait("Decision", "OD-GL-0003")]
  public async Task Two_accounts_cannot_share_a_code_within_a_tenant()
  {
    // Tenant-wide, not company-wide — the direct consequence of the chart being tenant-level. The index is
    // what makes the handler's check true under concurrency.
    await using var fixture = await GlFixture.CreateAsync();

    await using (var context = fixture.CreateContext())
    {
      context.Set<Account>().Add(Account.Create("4100", "First").Value);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      context.Set<Account>().Add(Account.Create("4100", "Second").Value);

      await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
  }

  [Fact]
  public async Task Codes_differing_only_in_case_collide_because_the_column_is_binary_collated()
  {
    await using var fixture = await GlFixture.CreateAsync();

    await using (var context = fixture.CreateContext())
    {
      context.Set<Account>().Add(Account.Create("ab-100", "Lower").Value);
      await context.SaveChangesAsync();
    }

    await using (var context = fixture.CreateContext())
    {
      // The NORMALIZED column carries the upper-cased form, so these are the same code even though the
      // display values differ. The binary collation is what makes the comparison ordinal rather than
      // dependent on the server's default.
      context.Set<Account>().Add(Account.Create("AB-100", "Upper").Value);

      await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
  }

  // ================================================================================================
  // THE FIXTURE
  // ================================================================================================

  private sealed class GlFixture : IAsyncDisposable
  {
    private const string Actor = "fp011-gl-tests";

    private readonly string token = Guid.NewGuid().ToString("N")[..12];

    private string catalog = string.Empty;

    public Guid Tenant { get; } = Guid.NewGuid();

    public Guid CompanyA { get; } = Guid.NewGuid();

    public DateTimeOffset EntryDate { get; } = new(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

    public static async Task<GlFixture> CreateAsync()
    {
      var fixture = new GlFixture();
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
        modelContributors: [new GlTenantModelContributor()]);
    }

    // Seeds a posted journal the way POSTING does — through the internal factory, from a balanced draft —
    // so the row under test is the row the product would have written.
    public async Task<Guid> SeedPostedJournalAsync(decimal debit = 100m)
    {
      await using var context = CreateContext();

      var debitAccount = Account.Create("1000", "Cash").Value;
      var creditAccount = Account.Create("4100", "Receivables").Value;
      context.Set<Account>().AddRange(debitAccount, creditAccount);
      await context.SaveChangesAsync();

      var year = FiscalYear.Create(
        "FY2026",
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
        [("FY2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
          new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero))]).Value;
      year.CompanyId = CompanyA;
      context.Set<FiscalYear>().Add(year);
      await context.SaveChangesAsync();

      var draft = JournalDraft.Create(EntryDate, "Seeded", "SEED").Value;
      draft.CompanyId = CompanyA;
      draft.ReplaceLines([(debitAccount.Id, debit, 0m, "debit"), (creditAccount.Id, 0m, debit, "credit")]);

      var period = year.Periods.First();
      var entry = JournalEntry.Post(draft, year.Id, period.Id, "1");

      context.Set<JournalEntry>().Add(entry);
      await context.SaveChangesAsync();

      return entry.Id;
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

    public async Task<string?> StringAsync(string sql)
    {
      await using var connection = new SqlConnection(ConnectionFor(catalog));
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = sql;
      return (await command.ExecuteScalarAsync())?.ToString();
    }

    private async Task InitializeAsync()
    {
      catalog = $"SSAS_FP011_Tenant_{token}";

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
        modelContributors: [new GlTenantModelContributor()]);

      await context.Database.MigrateAsync();
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
      new SqlConnectionStringBuilder(IntegrationSqlEnvironment.BaseConnectionString)
      {
        InitialCatalog = name
      }.ConnectionString;

    public async ValueTask DisposeAsync()
    {
      if (string.IsNullOrEmpty(catalog))
      {
        return;
      }

      try
      {
        await MasterAsync(
          $"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}];");
      }
      catch (SqlException)
      {
        // The gate reaps to zero before every configuration, so a catalog that outlives its test is
        // collected there rather than failing a test on teardown.
      }
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

    // ---- A GRANTING AUTHORIZER, AND WHAT THAT DOES AND DOES NOT WEAKEN.
    //
    // `FiscalYear` is `ICompanyOwnedEntity` (`OD-GL-0004`), so `TenantDbContext.ApplyCompanyRulesAsync`
    // demands a trusted company context before ANY company-owned row is written. Without one these tests
    // failed with "A trusted company context is required" — which is the ruling working, and is worth
    // recording because it was the first proof that closing a period really is a company-scoped write.
    //
    // The production authorizer needs a Platform database, an access resolver and a live session. That
    // graph belongs to the company-ownership tests, which own that property and assert it against the real
    // resolver. Substituting it HERE narrows nothing these tests claim: the write boundary still runs, and
    // what is under test is the APPEND-ONLY refusal, which the boundary applies before any of this.
    //
    // The same shape as HR's `GrantingHierarchyLock` in the API host, for the same reason.
    private sealed class GrantingCompanyAuthorizer(Guid companyId) : ICompanyWriteAuthorizer
    {
      public Task<SSAS.BuildingBlocks.Domain.Result<Guid>> AuthorizeCurrentCompanyAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SSAS.BuildingBlocks.Domain.Result.Success(companyId));
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
