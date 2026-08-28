using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.GL.Application.Calendar;
using SSAS.GL.Application.Reads;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// GL'S FISCAL-YEAR OVERLAP, AGAINST A REAL DATABASE (T-147).
// ==================================================================================================
//
// **The third of the product's three invariants that no schema constraint backs**, and the mechanism is
// stated once in `AttendanceOverlapChainSqlServerTests` — **this is one of its instances, in a third module.**
//
// ---- ⚠ AND GL'S AUTHOR ALREADY KNEW. THE COMMENT IS AT `CalendarCommandHandlers.cs:75`.
//
// > *"no unique index can express this: overlap is a range predicate across rows, not an equality on a key.
// > So there is no database backstop, and two concurrent definitions of adjacent-but-overlapping years could
// > both pass. Recorded rather than papered over — the exposure is small (defining a fiscal year is rare and
// > deliberate) and the alternative is a lock held across a human-scale operation."*
//
// **That is the mechanism, the consequence, and a weighed decision not to solve it — written before anyone
// went looking.** This file does not dispute it. **It tests the guard that the decision leaves as the only
// enforcement**, which is exactly what a recorded, accepted exposure needs and had never had.
//
// ---- ⚠ GL'S INTERVALS ARE HALF-OPEN. ATTENDANCE'S ARE CLOSED. DO NOT CARRY ONE ACROSS.
//
// ```
// GL          GetCoveringAsync      StartUtc <= instant && EndUtc >  instant     [start, end)
//             OverlapsExistingAsync StartUtc <  end     && start   <  EndUtc     [start, end)
//
// Attendance  GetCoveringAsync      StartDate <= onDate && EndDate >= onDate     [start, end]
//             OverlapsAsync         StartDate <= endDate && EndDate >= startDate [start, end]
// ```
//
// **Each module is internally consistent, and the two conventions differ correctly for their types.** A
// `DateOnly` period ending on the 30th contains the 30th; a `DateTimeOffset` year ending at midnight on
// 1 January does NOT contain that instant — it belongs to the year beginning then.
//
// **So the boundary assertion below is the opposite of Attendance's and both are right.** A reader moving
// between the two modules who carries a convention across will write an off-by-one that looks correct.
public sealed class GlFiscalYearOverlapChainSqlServerTests
{
  private static readonly DateTimeOffset Y2026 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Y2027 = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Y2028 = new(2028, 1, 1, 0, 0, 0, TimeSpan.Zero);

  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task A_fiscal_year_overlapping_an_existing_one_is_refused()
  {
    await using var fixture = await GlFixture.CreateAsync();
    await using var context = fixture.CreateContext();

    Assert.True((await HandlerFor(context, fixture).HandleAsync(Year(fixture, "FY2026", Y2026, Y2027)))
      .IsSuccess);

    // Starts six months INTO the existing year.
    var overlapping = await HandlerFor(context, fixture).HandleAsync(
      Year(fixture, "FY2026H2", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), Y2028));

    Assert.True(overlapping.IsFailure);

    // The SPECIFIC refusal. A duplicate code, an unauthorised actor or an invalid period list would each
    // produce a failure too, and none would say anything about overlap.
    Assert.Equal(CalendarErrors.OverlappingYear, overlapping.Error);
  }

  // ---- THE BOUNDARY, AND IT IS THE OPPOSITE OF ATTENDANCE'S BECAUSE THE INTERVAL IS HALF-OPEN.
  //
  // A year ending at midnight on 1 January and the next beginning at that instant do NOT overlap, and the
  // instant itself belongs to the LATER year. **A journal posted at midnight on 1 January lands in the new
  // fiscal year** — which is the behaviour this asserts, against a real engine rather than against the
  // reading that the two predicates agree.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task The_boundary_instant_belongs_to_the_later_year_and_only_to_it()
  {
    await using var fixture = await GlFixture.CreateAsync();
    await using var context = fixture.CreateContext();

    var first = await HandlerFor(context, fixture).HandleAsync(Year(fixture, "FY2026", Y2026, Y2027));
    var second = await HandlerFor(context, fixture).HandleAsync(Year(fixture, "FY2027", Y2027, Y2028));

    Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Code : null);

    // Adjacent, sharing an instant, and legal — because the interval is half-open.
    Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Code : null);

    // ---- ⚠ TWO INSTRUMENTS, AND EACH SEES WHAT THE OTHER CANNOT. NEITHER IS REDUNDANT.
    //
    // **The repository assertions prove OBSERVABLE BEHAVIOUR and are plantable**: making the start
    // exclusive fails them deterministically.
    //
    // **The row count proves EXACTLY ONE MATCHED, and only it can.** `GetCoveringAsync` uses
    // `FirstOrDefaultAsync`, so an ambiguous state — two years covering one instant — returns an
    // arbitrary row rather than an error. **It cannot report its own ambiguity.**
    //
    // ⚠ AND THE COUNT DUPLICATES THE PREDICATE DELIBERATELY, WHICH IS A REAL COST STATED PLAINLY:
    // a plant on the repository's `WHERE` does not reach it. **Measured rather than assumed — making the
    // END inclusive reddens NEITHER assertion**, because the resulting ambiguity happened to return the
    // right row. **So the end-exclusive boundary is CHARACTERISED here, not proven**, and no assertion
    // over a non-deterministic result could prove it. **That is precisely why the overlap guard matters
    // and why its refusal test is the load-bearing one.**
    var covering = await context.Set<FiscalYear>()
      .Where(year => year.CompanyId == fixture.CompanyA)
      .Where(year => year.StartUtc <= Y2027 && year.EndUtc > Y2027)
      .CountAsync();

    Assert.Equal(1, covering);

    var calendar = new FiscalCalendarRepository(new SingleContext(context));

    // START-INCLUSIVE: the first instant of a year belongs to it.
    var atStart = await calendar.GetCoveringAsync(fixture.CompanyA, Y2026);
    Assert.NotNull(atStart);
    Assert.Equal(first.Value, atStart!.Id);

    // END-EXCLUSIVE: the instant a year ends belongs to the NEXT one. A journal posted at midnight on
    // 1 January lands in the new fiscal year, and that is the behaviour money depends on.
    var atBoundary = await calendar.GetCoveringAsync(fixture.CompanyA, Y2027);
    Assert.NotNull(atBoundary);
    Assert.Equal(second.Value, atBoundary!.Id);
  }

  private static DefineFiscalYearCommand Year(
    GlFixture fixture, string code, DateTimeOffset start, DateTimeOffset end) =>
    new(fixture.CompanyA, code, start, end, [new FiscalPeriodDefinition(code, start, end)]);

  private static DefineFiscalYearCommandHandler HandlerFor(TenantDbContext context, GlFixture fixture) =>
    new(new FiscalCalendarRepository(new SingleContext(context)),
      new GrantingScope(),
      new SingleContextUnitOfWork(context),
      new FixtureTenant(fixture.Tenant),
      new DefiningUser());

  private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  private sealed class SingleContextUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
  {
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) =>
      Result.Success(await context.SaveChangesAsync(cancellationToken));

    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      new EfTransaction(await context.Database.BeginTransactionAsync(cancellationToken));

    private sealed class EfTransaction(IDbContextTransaction transaction) : ITransaction
    {
      public Task CommitAsync(CancellationToken cancellationToken = default) =>
        transaction.CommitAsync(cancellationToken);

      public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        transaction.RollbackAsync(cancellationToken);

      public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
  }

  private sealed class FixtureTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  // Grants. The subject is overlap and boundary resolution; authorisation has its own coverage, and wiring
  // the real resolver would make it this fixture's subject — as in T-142 and T-146.
  private sealed class GrantingScope : IGlScopeResolver
  {
    public Result RequirePermission(string permissionName) => Result.Success();

    public Task<Result<GlReadScope>> ResolveAsync(
      string permissionName, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException("Defining a fiscal year resolves no read scope.");

    public Task<Result> AuthorizeAsync(
      string permissionName, Guid companyId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success());
  }

  private sealed class DefiningUser : ICurrentUser
  {
    public string? UserId => "gl-fiscal-year-chain";

    public string? UserName => "gl-fiscal-year-chain";

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }
}
