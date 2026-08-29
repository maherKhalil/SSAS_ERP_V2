using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Domain.Calendar;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE FISCAL-YEAR DEFINITION LOCK, ACTUALLY CONTENDED (T-190).
// ==================================================================================================
//
// ---- ⚠ THIS FILE EXISTS BECAUSE THE LOCK HAD NO BEHAVIOURAL EVIDENCE AT ALL, AND THAT WAS MEASURED.
//
// `GlFiscalYearOverlapChainSqlServerTests` composes the REAL `SqlServerFiscalYearDefinitionLock` against
// real SQL Server. It still could not see it: **planting the lock to grant unconditionally — never calling
// `sp_getapplock` at all — left every one of those tests green.** `sp_getapplock` could have been deleted
// outright and the suite would not have noticed.
//
// **That is not a defect in those tests. It is a limit on their SHAPE.** They call the handler sequentially
// on ONE connection, so the lock is never contended — and an uncontended lock is indistinguishable from no
// lock. No single-connection test can redden that plant however it is written, which is what makes a second
// connection the only instrument that can prove the lock exists.
//
// The general form, which is worth more than this file: **A TEST CANNOT PROVE A GUARD IT NEVER TRIGGERS.**
// Composing the real implementation is not the same as exercising it.
//
// ---- WHAT T-184 COULD AND COULD NOT CLAIM BEFORE THIS.
//
// `FiscalYearDefinitionOrderTests` proves the ORDER — that the overlap check sits inside the lock rather
// than beside it — by reading source with comments stripped. That was true and remains true. It says
// nothing about whether the acquisition happens, because source order is not behaviour.
//
// So the honest statement was "the ordering is proven and the acquisition is not". These four tests are
// what make the second half true.
public sealed class GlFiscalYearDefinitionLockContentionSqlServerTests
{
  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public async Task A_second_connection_is_refused_while_the_first_holds_the_lock()
  {
    await using var fixture = await GlFixture.CreateAsync();
    await using var holder = fixture.CreateContext();
    await using var rival = fixture.CreateContext();

    // TWO CONNECTIONS, WHICH IS THE WHOLE POINT. `CreateContext` builds fresh options each call, so these
    // are genuinely separate connections — `sp_getapplock` owned by a transaction is scoped to one of them,
    // and a single shared connection would re-enter its own lock and prove nothing.
    await using var holderTransaction = await holder.Database.BeginTransactionAsync();
    await using var rivalTransaction = await rival.Database.BeginTransactionAsync();

    var granted = await LockOn(holder).AcquireAsync(fixture.Tenant, fixture.CompanyA);
    Assert.True(granted.IsSuccess);

    // Waits out the five-second acquisition timeout, so this test is deliberately the slowest in the file.
    // Shortening it would mean not testing the thing.
    var refused = await LockOn(rival).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(refused.IsFailure);
    Assert.Equal(CalendarErrors.CalendarDefinitionBusy, refused.Error);
  }

  // ⚠ THE ANTI-VACUITY CONTROL, AND IT IS NOT OPTIONAL HERE. A lock that refused EVERYONE — a wrong
  // `@Result` reading, a key that never matches itself — would satisfy the test above perfectly. Only
  // showing that the same acquisition SUCCEEDS once the holder lets go distinguishes a working lock from a
  // permanently closed door.
  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public async Task The_lock_is_released_when_the_holding_transaction_ends()
  {
    await using var fixture = await GlFixture.CreateAsync();
    await using var holder = fixture.CreateContext();
    await using var successor = fixture.CreateContext();

    var holderTransaction = await holder.Database.BeginTransactionAsync();
    var granted = await LockOn(holder).AcquireAsync(fixture.Tenant, fixture.CompanyA);
    Assert.True(granted.IsSuccess);

    // Rolling back rather than committing, because release must not depend on the outcome: a handler that
    // fails its overlap check and abandons the transaction has to free the lock exactly as one that commits
    // does. `@LockOwner = 'Transaction'` is what makes that automatic and is the reason it was chosen.
    await holderTransaction.RollbackAsync();
    await holderTransaction.DisposeAsync();

    await using var successorTransaction = await successor.Database.BeginTransactionAsync();
    var second = await LockOn(successor).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(second.IsSuccess);
  }

  // The key is per COMPANY, not per tenant and not global. Defining next year for one company must not
  // stall an unrelated company on the same tenant for five seconds.
  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public async Task Two_companies_do_not_contend_with_each_other()
  {
    await using var fixture = await GlFixture.CreateAsync();
    await using var first = fixture.CreateContext();
    await using var second = fixture.CreateContext();

    await using var firstTransaction = await first.Database.BeginTransactionAsync();
    await using var secondTransaction = await second.Database.BeginTransactionAsync();

    Assert.True((await LockOn(first).AcquireAsync(fixture.Tenant, fixture.CompanyA)).IsSuccess);

    var otherCompany = Guid.NewGuid();
    Assert.True((await LockOn(second).AcquireAsync(fixture.Tenant, otherCompany)).IsSuccess);
  }

  // The property the chain test's comment already CLAIMED — "the lock refuses when there is no open
  // transaction, which only the real implementation can demonstrate" — while exercising nothing. It is a
  // true claim, and this is the first thing that checks it.
  //
  // `sp_getapplock` with `@LockOwner = 'Transaction'` errors outright without one, so the guard is what
  // turns a caller's sequencing bug into an immediate refusal rather than an exception much later.
  [Fact]
  [Trait("Decision", "DEC-L-084")]
  public async Task Acquiring_without_an_open_transaction_is_refused()
  {
    await using var fixture = await GlFixture.CreateAsync();
    await using var context = fixture.CreateContext();

    var result = await LockOn(context).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(result.IsFailure);
    Assert.Equal(CalendarErrors.CalendarDefinitionBusy, result.Error);
  }

  private static SqlServerFiscalYearDefinitionLock LockOn(TenantDbContext context) =>
    new(new SingleContext(context));

  private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }
}
