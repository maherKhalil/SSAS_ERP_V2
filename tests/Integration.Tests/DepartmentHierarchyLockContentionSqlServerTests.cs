using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE DEPARTMENT HIERARCHY LOCK, ACTUALLY CONTENDED (T-193).
// ==================================================================================================
//
// ---- ⚠ FOUND BY AN AUDIT, NOT BY A FAILURE, AND THE LOCK WAS IN PRODUCTION THE WHOLE TIME.
//
// `DepartmentAppFixture` composes the real `SqlServerDepartmentHierarchyLock` on a real connection and
// never opens a second one. The API host registers a `GrantingHierarchyLock` that grants unconditionally.
// The architecture tests check the interface's SHAPE — that it exists, that it takes what it should.
//
// **So planting this lock to grant without ever calling `sp_getapplock` would have gone unnoticed**, which
// is exactly the state GL's fiscal-year lock was in until T-190 found it the same way.
//
// **A test cannot prove a guard it never triggers.** Sequential calls on one connection never contend, and
// an uncontended lock is indistinguishable from no lock — so composing the real implementation, which this
// module already did, is not the same as exercising it.
//
// ---- THE PATTERN WAS NOT MISSING. IT WAS INCONSISTENTLY APPLIED, AND THAT IS THE MORE USEFUL FINDING.
//
// Enumerating every `sp_getapplock` site rather than trusting anyone's memory of them turned up NINE, and
// Attendance's `SqlServerLeaveSubmissionLock` already had this exact shape — a holder, a contender on a
// second connection, and a different-key control — before either GL or HR did. `TenantDatabaseMigration
// Ownership`, `TenantDatabaseBackupOwnership` and `TenantCutoverOperationLock` each hold on a separate real
// connection too.
//
// So the habit existed and was applied in four places out of nine. This file is the eighth.
public sealed class DepartmentHierarchyLockContentionSqlServerTests
{
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task A_second_connection_is_refused_while_the_first_holds_the_lock()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var holder = fixture.CreateContext();
    await using var rival = fixture.CreateContext();

    await using var holderTransaction = await holder.Database.BeginTransactionAsync();
    await using var rivalTransaction = await rival.Database.BeginTransactionAsync();

    var granted = await LockOn(holder).AcquireAsync(fixture.Tenant, fixture.CompanyA);
    Assert.True(granted.IsSuccess);

    // Sits out the five-second acquisition timeout. Shortening it would mean not testing the thing.
    var refused = await LockOn(rival).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(refused.IsFailure);
    Assert.Equal(DepartmentErrors.HierarchyMutationBusy, refused.Error);
  }

  // ⚠ THE CONTROL, AND WITHOUT IT THE TEST ABOVE PROVES NOTHING. A lock that had failed SHUT — a misread
  // `@Result`, a key that never matches itself — refuses the contender perfectly and would pass. Only
  // showing the SAME acquisition succeeding once the holder lets go separates a working lock from a
  // permanently closed door, and a closed door here stops every hierarchy move in the company.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task The_lock_is_released_when_the_holding_transaction_ends()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var holder = fixture.CreateContext();
    await using var successor = fixture.CreateContext();

    var holderTransaction = await holder.Database.BeginTransactionAsync();
    Assert.True((await LockOn(holder).AcquireAsync(fixture.Tenant, fixture.CompanyA)).IsSuccess);

    // Rolled back, not committed: a handler whose ancestry walk finds a cycle abandons its transaction, and
    // that path must free the lock exactly as the committing one does. `@LockOwner = 'Transaction'` is what
    // makes it automatic, and a test that only ever commits never walks it.
    await holderTransaction.RollbackAsync();
    await holderTransaction.DisposeAsync();

    await using var successorTransaction = await successor.Database.BeginTransactionAsync();

    Assert.True((await LockOn(successor).AcquireAsync(fixture.Tenant, fixture.CompanyA)).IsSuccess);
  }

  // The key is per COMPANY. Reparenting a department in one company must not stall an unrelated company on
  // the same tenant for five seconds.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task Two_companies_do_not_contend_with_each_other()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var first = fixture.CreateContext();
    await using var second = fixture.CreateContext();

    await using var firstTransaction = await first.Database.BeginTransactionAsync();
    await using var secondTransaction = await second.Database.BeginTransactionAsync();

    Assert.True((await LockOn(first).AcquireAsync(fixture.Tenant, fixture.CompanyA)).IsSuccess);
    Assert.True((await LockOn(second).AcquireAsync(fixture.Tenant, fixture.CompanyB)).IsSuccess);
  }

  // `sp_getapplock` with `@LockOwner = 'Transaction'` errors outright without one, so the guard turns a
  // caller's sequencing bug into an immediate refusal rather than an exception much later.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public async Task Acquiring_without_an_open_transaction_is_refused()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var context = fixture.CreateContext();

    var result = await LockOn(context).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(result.IsFailure);
    Assert.Equal(DepartmentErrors.HierarchyMutationBusy, result.Error);
  }

  private static SqlServerDepartmentHierarchyLock LockOn(TenantDbContext context) =>
    new(new SingleContext(context));

  private sealed class SingleContext(TenantDbContext context) : ITenantDbContextAccessor
  {
    public Task<DbContext> GetRequiredAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult<DbContext>(context);
  }
}
