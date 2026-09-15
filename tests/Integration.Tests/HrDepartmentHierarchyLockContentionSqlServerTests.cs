using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

public sealed class HrDepartmentHierarchyLockContentionSqlServerTests
{
  [Fact]
  public async Task A_second_connection_is_refused_while_the_first_holds_the_lock()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var holder = fixture.CreateContext();
    await using var rival = fixture.CreateContext();

    await using var holderTransaction = await holder.Database.BeginTransactionAsync();
    await using var rivalTransaction = await rival.Database.BeginTransactionAsync();

    var granted = await LockOn(holder).AcquireAsync(fixture.Tenant, fixture.CompanyA);
    Assert.True(granted.IsSuccess);

    var refused = await LockOn(rival).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(refused.IsFailure);
    Assert.Equal(DepartmentErrors.HierarchyMutationBusy, refused.Error);
  }

  [Fact]
  public async Task The_lock_is_released_when_the_holding_transaction_ends()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var holder = fixture.CreateContext();
    await using var successor = fixture.CreateContext();

    var holderTransaction = await holder.Database.BeginTransactionAsync();
    var granted = await LockOn(holder).AcquireAsync(fixture.Tenant, fixture.CompanyA);
    Assert.True(granted.IsSuccess);

    await holderTransaction.RollbackAsync();
    await holderTransaction.DisposeAsync();

    await using var successorTransaction = await successor.Database.BeginTransactionAsync();
    var second = await LockOn(successor).AcquireAsync(fixture.Tenant, fixture.CompanyA);

    Assert.True(second.IsSuccess);
  }

  [Fact]
  public async Task Two_companies_do_not_contend_with_each_other()
  {
    await using var fixture = await DepartmentAppFixture.CreateAsync();
    await using var first = fixture.CreateContext();
    await using var second = fixture.CreateContext();

    await using var firstTransaction = await first.Database.BeginTransactionAsync();
    await using var secondTransaction = await second.Database.BeginTransactionAsync();

    Assert.True((await LockOn(first).AcquireAsync(fixture.Tenant, fixture.CompanyA)).IsSuccess);

    var otherCompany = Guid.NewGuid();
    Assert.True((await LockOn(second).AcquireAsync(fixture.Tenant, otherCompany)).IsSuccess);
  }

  [Fact]
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
