using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.Platform.Tests.IdentityAccess;

// ==================================================================================================
// AC-SS-0012 — TERMINATION CLOSES SELF-SERVICE, ASSERTED AT THE SEAM (T-090).
// ==================================================================================================
//
// ---- WHY AT THE SEAM AND NOT AT A ROUTE.
//
// There are two self-service scope resolvers today (`Payroll`, `Attendance`), leave balances would make a
// third, and each one is a place that could forget. **This asserts the ONE method they all go through**, so
// a self-service surface built next year inherits the property without its author knowing it exists.
//
// A per-route assertion would have to be written again for every route, which is the same failure the
// production check would have had — the reason the refusal is not in the reads.
//
// ---- IT RUNS THE REAL RESOLVER AGAINST A REAL `PlatformDbContext`.
//
// SQLite in memory, one table, the real `UserEmployeeLink` mapping. A hand-rolled fake of the resolver
// would assert that a fake returns what it was told; what needs proving is that the production class reads
// the link, asks about the employment, and refuses on the answer.
//
// The one stub is `IEmploymentStandingDirectory`, which is HR's side of the seam and is not reachable from
// a Platform test — that is the boundary demonstrated rather than asserted.
public sealed class UserEmployeeResolverSeamTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid EmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private const long TenantUserId = 42;

  // ---- THE CONTROL. Without it every refusal below would also pass against a resolver that returns null
  // ---- unconditionally, which is a guard that cannot fail.
  [Fact]
  [Trait("Criterion", "AC-SS-0012")]
  public async Task A_current_employment_resolves()
  {
    await using var scope = await SeamScope.CreateAsync(EmploymentStanding.Current);

    Assert.Equal(EmployeeId, await scope.Resolver.ResolveEmployeeIdAsync(TenantUserId));

    // The standing WAS asked about the employee the link named — not about some other identifier, and not
    // skipped. A resolver that never asked would pass the assertion above and fail the whole point.
    Assert.Equal([EmployeeId], scope.Standing.Asked);
  }

  // ---- AC-SS-0012 ITSELF.
  [Fact]
  [Trait("Criterion", "AC-SS-0012")]
  public async Task An_ended_employment_does_not_resolve()
  {
    await using var scope = await SeamScope.CreateAsync(EmploymentStanding.Ended);

    Assert.Null(await scope.Resolver.ResolveEmployeeIdAsync(TenantUserId));
    Assert.Equal([EmployeeId], scope.Standing.Asked);
  }

  // ---- FAIL CLOSED. `Unknown` is `default(EmploymentStanding)`, so a directory that returned nothing at
  // ---- all — a bug, a new status nobody mapped — refuses rather than admits.
  [Fact]
  [Trait("Criterion", "AC-SS-0012")]
  public async Task An_unknown_employment_does_not_resolve()
  {
    await using var scope = await SeamScope.CreateAsync(EmploymentStanding.Unknown);

    Assert.Null(await scope.Resolver.ResolveEmployeeIdAsync(TenantUserId));
  }

  // ================================================================================================
  // REQ-SS-0006 / AC-SS-0011 / TS-SS-0009 — THE LINK SURVIVES THE REFUSAL.
  // ================================================================================================
  //
  // **The naive fix for `AC-SS-0012` is to delete or filter the link, and it satisfies the criterion while
  // destroying the attributability `REQ-SS-0006` exists to protect** — a terminated employee's retained
  // payslips stop being attributable to anyone.
  //
  // So the row is read back from the DATABASE after a refusal, through a second context, rather than
  // trusted from the tracked instance.
  [Fact]
  [Trait("Criterion", "AC-SS-0011")]
  [Trait("Criterion", "AC-SS-0010")]
  public async Task The_link_is_untouched_by_a_refusal()
  {
    await using var scope = await SeamScope.CreateAsync(EmploymentStanding.Ended);

    Assert.Null(await scope.Resolver.ResolveEmployeeIdAsync(TenantUserId));

    var surviving = await scope.ReadLinksBackAsync();

    Assert.Equal([(TenantUserId, EmployeeId)], surviving);
  }

  // ---- AND A CALLER WITH NO LINK NEVER REACHES HR AT ALL.
  //
  // Not an optimisation: the standing directory is a cross-database read, and asking it about an employee
  // nobody named would be asking a question with no subject.
  [Fact]
  public async Task An_unlinked_user_is_answered_without_asking_about_any_employee()
  {
    await using var scope = await SeamScope.CreateAsync(EmploymentStanding.Current);

    Assert.Null(await scope.Resolver.ResolveEmployeeIdAsync(TenantUserId + 1));
    Assert.Empty(scope.Standing.Asked);
  }

  // ================================================================================================
  // NO DIRECTORY AT ALL STILL REFUSES — THE FAIL-CLOSED HALF OF AN OPTIONAL DEPENDENCY.
  // ================================================================================================
  //
  // The standing directory is an optional constructor parameter because Platform must stand up without any
  // module registered — fourteen API tests proved it by failing DI validation when it was required.
  //
  // **An optional dependency is a fail-open waiting to happen unless the absent case is asserted.** A host
  // with no HR module has no employees, so nobody has a standing and nobody resolves; without this test
  // that reasoning lives only in a comment, and the first refactor to "simplify" the null check would take
  // the safe branch out with nothing turning red.
  [Fact]
  [Trait("Criterion", "AC-SS-0012")]
  public async Task A_resolver_with_no_standing_directory_refuses()
  {
    await using var scope = await SeamScope.CreateAsync(EmploymentStanding.Current, withDirectory: false);

    Assert.Null(await scope.Resolver.ResolveEmployeeIdAsync(TenantUserId));
  }

  private sealed class SeamScope : IAsyncDisposable
  {
    private readonly SqliteConnection connection;
    private readonly PlatformDbContext context;

    private SeamScope(
      SqliteConnection connection,
      PlatformDbContext context,
      UserEmployeeResolver resolver,
      RecordingStanding standing)
    {
      this.connection = connection;
      this.context = context;
      Resolver = resolver;
      Standing = standing;
    }

    public UserEmployeeResolver Resolver { get; }

    public RecordingStanding Standing { get; }

    public static async Task<SeamScope> CreateAsync(
      EmploymentStanding standing, bool withDirectory = true)
    {
      var connection = new SqliteConnection("Data Source=:memory:");
      await connection.OpenAsync();

      var context = NewContext(connection);

      // ---- ONLY THE ONE TABLE, NOT THE WHOLE PLATFORM SCHEMA.
      //
      // `EnsureCreated` would translate every Platform configuration into SQLite — a different provider
      // from the one they were written for — and a provider mismatch would be reported here as a seam
      // failure. The column names are the ones `UserEmployeeLinkConfiguration` maps.
      //
      // UNQUALIFIED, because SQLite has no schemas: the `platform` schema the configuration names is
      // dropped by the provider, so the table is plain `UserEmployeeLink` here and `[platform]` in SQL
      // Server. `UserEmployeeLinkSqlServerTests` is where the real schema is asserted.
      await context.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE UserEmployeeLink (
          UserEmployeeLinkId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
          TenantId TEXT NOT NULL,
          TenantUserId INTEGER NOT NULL,
          EmployeeId TEXT NOT NULL,
          CreatedUtc TEXT NOT NULL,
          CreatedBy TEXT NULL,
          ModifiedUtc TEXT NULL,
          ModifiedBy TEXT NULL,
          RowVersion BLOB NULL);
        """);

      await context.Database.ExecuteSqlRawAsync(
        """
        INSERT INTO UserEmployeeLink
          (TenantId, TenantUserId, EmployeeId, CreatedUtc, CreatedBy)
        VALUES ({0}, {1}, {2}, '2026-08-27T12:00:00Z', 'seed');
        """
          .Replace("{0}", $"'{TenantId}'", StringComparison.Ordinal)
          .Replace("{1}", TenantUserId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
          .Replace("{2}", $"'{EmployeeId}'", StringComparison.Ordinal));

      var recording = new RecordingStanding(standing);

      return new SeamScope(
        connection,
        context,
        new UserEmployeeResolver(context, new FixedTenant(), withDirectory ? recording : null),
        recording);
    }

    // Read through a SECOND context so the assertion sees the DATABASE, not a tracked instance.
    //
    // Projected to the two columns under assertion rather than materialized whole: the audit timestamps are
    // `DateTimeOffset`, which SQLite stores as text and reads back differently from SQL Server. Widening the
    // read would make this test fail on a provider difference that has nothing to do with the link
    // surviving — `UserEmployeeLinkSqlServerTests` is where the full row is asserted against the real one.
    public async Task<IReadOnlyList<(long TenantUserId, Guid EmployeeId)>> ReadLinksBackAsync()
    {
      await using var reader = NewContext(connection);

      var rows = await reader.UserEmployeeLinks.AsNoTracking()
        .Select(link => new { link.TenantUserId, link.EmployeeId })
        .ToListAsync();

      return [.. rows.Select(row => (row.TenantUserId, row.EmployeeId))];
    }

    public async ValueTask DisposeAsync()
    {
      await context.DisposeAsync();
      await connection.DisposeAsync();
    }

    private static PlatformDbContext NewContext(SqliteConnection connection) =>
      new(
        new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options,
        new NoUser(),
        new FixedTenant(),
        new FixedClock());
  }

  // Records what it was asked about, because "did the resolver actually consult HR" is the half of this
  // that a return value cannot show.
  private sealed class RecordingStanding(EmploymentStanding standing) : IEmploymentStandingDirectory
  {
    public List<Guid> Asked { get; } = [];

    public Task<EmploymentStanding> GetStandingAsync(
      Guid employeeId, CancellationToken cancellationToken = default)
    {
      Asked.Add(employeeId);
      return Task.FromResult(standing);
    }
  }

  private sealed class FixedTenant : ICurrentTenant
  {
    public Guid? TenantId => UserEmployeeResolverSeamTests.TenantId;
  }

  private sealed class NoUser : ICurrentUser
  {
    public string? UserId => "seam-tests";

    public string? UserName => null;

    public string? Email => null;


    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class FixedClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
  }
}
