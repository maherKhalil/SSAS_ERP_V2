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

// ==================================================================================================
// THE SHARED GL INTEGRATION FIXTURE (promoted in T-142).
// ==================================================================================================
//
// **Promoted from a private nested class in `GlSchemaSqlServerTests` rather than copied.** A second
// fixture creating catalogs the same way is `DEC-L-080` in test infrastructure — **and test infrastructure
// is where duplication is least visible, because nobody reviews a fixture twice.**
//
// It carries nothing of the schema tests' assumptions: a catalog, a `TenantDbContext` with GL's
// contributor, and seeding. **If a caller needs setup specific to its own subject, that belongs in the
// caller** — a fixture shaped by its first caller is not a shared fixture.
internal sealed class GlFixture : IAsyncDisposable
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
