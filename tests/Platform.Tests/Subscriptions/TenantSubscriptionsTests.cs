using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Subscriptions.TenantSubscriptions;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Subscriptions;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Tests.Subscriptions;

public class TenantSubscriptionsTests
{
  [Fact]
  public async Task AppendTenantSubscriptionCommandHandler_success_appends_and_invalidates_cache()
  {
    var tenantId = Guid.NewGuid();
    var planId = Guid.NewGuid();
    var repository = new StubTenantSubscriptionRepository { PlanExists = true };
    var unitOfWork = new StubUnitOfWork();
    var cache = new StubTenantEntitlementCache();
    var currentUser = new StubCurrentUser { UserId = "123" };
    
    var handler = new AppendTenantSubscriptionCommandHandler(unitOfWork, repository, cache, currentUser);
    
    var command = new AppendTenantSubscriptionCommand(
      tenantId, planId, DateTimeOffset.UtcNow, SubscriptionTermKind.Perpetual, DateTimeOffset.UtcNow, null, "USD", null, null
    );
    
    var result = await handler.HandleAsync(command, CancellationToken.None);
    
    Assert.True(result.IsSuccess);
    Assert.True(repository.AddCalled);
    Assert.Equal(1, unitOfWork.SaveCount);
    Assert.True(cache.Invalidated);
  }

  [Fact]
  public async Task AppendTenantSubscriptionCommandHandler_fails_if_plan_does_not_exist()
  {
    var tenantId = Guid.NewGuid();
    var planId = Guid.NewGuid();
    var repository = new StubTenantSubscriptionRepository { PlanExists = false };
    var unitOfWork = new StubUnitOfWork();
    var cache = new StubTenantEntitlementCache();
    var currentUser = new StubCurrentUser { UserId = "123" };
    
    var handler = new AppendTenantSubscriptionCommandHandler(unitOfWork, repository, cache, currentUser);
    
    var command = new AppendTenantSubscriptionCommand(
      tenantId, planId, DateTimeOffset.UtcNow, SubscriptionTermKind.Perpetual, DateTimeOffset.UtcNow, null, "USD", null, null
    );
    
    var result = await handler.HandleAsync(command, CancellationToken.None);
    
    Assert.True(result.IsFailure);
    Assert.Equal(SubscriptionErrors.InvalidPlan, result.Error);
  }

  [Fact]
  public async Task AppendTenantSubscriptionCommandHandler_fails_if_not_monotonic()
  {
    var tenantId = Guid.NewGuid();
    var planId = Guid.NewGuid();
    var maxEffective = DateTimeOffset.UtcNow.AddDays(1);
    var repository = new StubTenantSubscriptionRepository { PlanExists = true, MaxEffective = maxEffective };
    var unitOfWork = new StubUnitOfWork();
    var cache = new StubTenantEntitlementCache();
    var currentUser = new StubCurrentUser { UserId = "123" };
    
    var handler = new AppendTenantSubscriptionCommandHandler(unitOfWork, repository, cache, currentUser);
    
    var command = new AppendTenantSubscriptionCommand(
      tenantId, planId, DateTimeOffset.UtcNow, SubscriptionTermKind.Perpetual, DateTimeOffset.UtcNow, null, "USD", null, null
    );
    
    var result = await handler.HandleAsync(command, CancellationToken.None);
    
    Assert.True(result.IsFailure);
    Assert.Equal("Subscription.NonMonotonicAppend", result.Error.Code);
  }

  private class StubTenantSubscriptionRepository : ITenantSubscriptionRepository
  {
    public bool PlanExists { get; set; }
    public DateTimeOffset? MaxEffective { get; set; }
    public bool AddCalled { get; private set; }

    public Task<DateTimeOffset?> GreatestEffectiveFromUtcAsync(Guid tenantId, CancellationToken cancellationToken = default)
      => Task.FromResult(MaxEffective);

    public Task<bool> PlanExistsAsync(Guid subscriptionPlanId, CancellationToken cancellationToken = default)
      => Task.FromResult(PlanExists);

    public Task AddAsync(TenantSubscription subscription, CancellationToken cancellationToken = default)
    {
      AddCalled = true;
      return Task.CompletedTask;
    }
  }

  private class StubUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult<ITransaction>(null!);
  }

  private class StubTenantEntitlementCache : ITenantEntitlementCache
  {
    public bool Invalidated { get; private set; }
    public bool TryGet(Guid tenantId, out TenantEntitlementSnapshot snapshot) { snapshot = null!; return false; }
    public void Store(TenantEntitlementSnapshot snapshot) {}
    public void InvalidateTenant(Guid tenantId) => Invalidated = true;
    public void InvalidatePlan(Guid subscriptionPlanId) {}
  }

  private class StubCurrentUser : ICurrentUser
  {
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? TokenId { get; set; }
    public System.Collections.Generic.IReadOnlyCollection<string> Roles => Array.Empty<string>();
    public System.Collections.Generic.IReadOnlyCollection<string> Permissions => Array.Empty<string>();
  }
}
