using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE POSTING FENCE ACTUALLY EXCLUDES (249, and this closes 248 step 2).
// ==================================================================================================
//
// `SqlServerFiscalPeriodPostingLock` issues SQL on the exact path that carried a defect, and every other
// assertion about it is a SOURCE-TEXT assertion: `JournalPostingOrderTests` proves the string
// `AcquireForStateChangeAsync` appears in a handler, which cannot prove the lock excludes anything.
// THIS FILE IS THE ONLY THING THAT EXERCISES THE LOCK'S BEHAVIOUR.
//
// ---- ⚠⚠⚠ WHAT WAS MEASURED BEFORE THE FIX, AND WHAT THIS DOES AND DOES NOT SHOW.
//
// A probe held a poster's transaction open, read the period as OPEN, closed it from a second connection
// with `SET LOCK_TIMEOUT 3000` so blocking would surface as error 1222 — IT DID NOT BLOCK — and the
// poster committed. A JOURNAL LANDED IN A PERIOD WHOSE STATUS WAS `Closed`.
//
// THE RED DEMONSTRATED A JOURNAL LANDING IN A CLOSED PERIOD. THE GREEN BELOW DEMONSTRATES THAT THE TWO
// APPLICATION PATHS NOW EXCLUDE EACH OTHER — AND NOT THAT NO PATH CAN CLOSE A PERIOD MID-POST.
//
// ⚠⚠ AN APPLICATION LOCK IS A PROTOCOL, NOT A CONSTRAINT. It binds writers that take it and nothing
// else: raw SQL bypasses it, a migration bypasses it, a future handler that forgets it bypasses it. That
// is true of every application lock in this codebase including `TenantCutoverWriteFence`, which this is
// modelled on. The original red probe closed the period with raw SQL and STILL WOULD — which is why the
// control had to become a different interleaving rather than a re-run.
//
// ---- ⚠ AND WHY THERE ARE THREE LEGS RATHER THAN ONE.
//
// The mirror exists because a refusal proves nothing if the exclusive side is never grantable at all.
// The shared-against-shared leg exists because AN IMPLEMENTATION THAT TOOK `Exclusive` ON THE POSTER SIDE
// WOULD PASS THE FIRST TWO, CLOSE THE RACE CORRECTLY, AND SILENTLY SERIALISE THE HOT PATH — a true guard
// over the wrong property, which is the failure this whole item was built out of.
//
// ⚠ `DEC-L-007`: this suite does not run under `GATE_SCOPE=TASK`, so these merge without executing.
// They were RUN before landing — filtered, 21 seconds — rather than merged unrun.
public sealed class FiscalPeriodPostingFenceSqlServerTests
{
  // Any stable pair serves: the resource only has to be the same string on both sides.
  private static readonly Guid Tenant = new("11111111-1111-1111-1111-111111111111");
  private static readonly Guid Company = new("22222222-2222-2222-2222-222222222222");

  private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }

  private static SqlServerFiscalPeriodPostingLock LockOn(TenantDbContext context) =>
    new(new SingleContext(context));

  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  public async Task A_period_state_change_is_refused_while_a_poster_holds_the_fence()
  {
    await using var fixture = await GlFixture.CreateAsync();

    await using var poster = fixture.CreateContext();
    await using var posterTransaction = await poster.Database.BeginTransactionAsync();

    var held = await LockOn(poster).AcquireForPostingAsync(Tenant, Company);
    Assert.True(held.IsSuccess, "the poster could not take the shared fence, so this proves nothing");

    await using var closer = fixture.CreateContext();
    await using var closerTransaction = await closer.Database.BeginTransactionAsync();

    var refused = await LockOn(closer).AcquireForStateChangeAsync(Tenant, Company);

    Assert.True(
      refused.IsFailure,
      "A STATE CHANGE WAS GRANTED WHILE A POSTER HELD THE FENCE — the fence excludes nothing, and a " +
      "period can close between a poster's read and its write.");

    // The distinct error matters: the caller's correct response is to retry, and a generic refusal
    // tells them to stop.
    Assert.Equal("Gl.FiscalPeriodPostingInProgress", refused.Error.Code);
  }

  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  public async Task The_same_state_change_succeeds_when_no_poster_holds_the_fence()
  {
    await using var fixture = await GlFixture.CreateAsync();

    await using var closer = fixture.CreateContext();
    await using var transaction = await closer.Database.BeginTransactionAsync();

    var granted = await LockOn(closer).AcquireForStateChangeAsync(Tenant, Company);

    // THE MIRROR. Without it, the refusal above is equally consistent with the exclusive side never
    // being grantable — a lock that refuses everybody would pass the first test perfectly.
    Assert.True(granted.IsSuccess, "the exclusive fence is never granted, so the refusal above is vacuous");
  }

  [Fact]
  [Trait("Decision", "BR-GL-0003")]
  public async Task Two_posters_do_not_block_each_other()
  {
    await using var fixture = await GlFixture.CreateAsync();

    await using var first = fixture.CreateContext();
    await using var firstTransaction = await first.Database.BeginTransactionAsync();
    Assert.True((await LockOn(first).AcquireForPostingAsync(Tenant, Company)).IsSuccess);

    await using var second = fixture.CreateContext();
    await using var secondTransaction = await second.Database.BeginTransactionAsync();

    var concurrent = await LockOn(second).AcquireForPostingAsync(Tenant, Company);

    Assert.True(
      concurrent.IsSuccess,
      "TWO POSTERS BLOCKED EACH OTHER — the poster side is not Shared, so posting is now serialised " +
      "company-wide. The race would be closed and the hot path would be the cost.");
  }
}
