using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.Platform.Tests.Localization;

public sealed class RequestTenantEligibilityTests
{
  [Fact]
  public async Task Same_scope_reuses_each_tenant_lookup_without_cross_tenant_reuse()
  {
    var firstTenant = Guid.NewGuid();
    var secondTenant = Guid.NewGuid();
    var source = new MutableEligibility();
    var eligibility = new RequestTenantEligibility(source);

    var first = await eligibility.GetEligibilityAsync(firstTenant);
    var repeated = await eligibility.GetEligibilityAsync(firstTenant);
    var different = await eligibility.GetEligibilityAsync(secondTenant);

    Assert.True(first.IsAuthenticationEligible);
    Assert.Same(first, repeated);
    Assert.True(different.IsAuthenticationEligible);
    Assert.Equal(2, source.Calls);
  }

  [Fact]
  public async Task New_scope_observes_suspension_after_an_active_request()
  {
    var tenantId = Guid.NewGuid();
    var source = new MutableEligibility();
    var firstScope = new RequestTenantEligibility(source);
    Assert.True((await firstScope.GetEligibilityAsync(tenantId)).IsAuthenticationEligible);

    source.Status = TenantStatus.Suspended;
    Assert.True((await firstScope.GetEligibilityAsync(tenantId)).IsAuthenticationEligible);
    var secondScope = new RequestTenantEligibility(source);
    var suspended = await secondScope.GetEligibilityAsync(tenantId);

    Assert.False(suspended.IsAuthenticationEligible);
    Assert.Equal(TenantStatus.Suspended, suspended.TenantStatus);
    Assert.Equal(2, source.Calls);
  }

  [Theory]
  [InlineData(TenantStatus.Provisioning)]
  [InlineData(TenantStatus.Suspended)]
  [InlineData(TenantStatus.Archived)]
  [InlineData(null)]
  public async Task Non_active_or_missing_tenant_remains_denied(TenantStatus? status)
  {
    var source = new MutableEligibility { Status = status };
    var result = await new RequestTenantEligibility(source).GetEligibilityAsync(Guid.NewGuid());

    Assert.False(result.IsAuthenticationEligible);
  }

  [Fact]
  public async Task Cancellation_is_preserved_before_a_lookup()
  {
    var source = new MutableEligibility();
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(() =>
      new RequestTenantEligibility(source).GetEligibilityAsync(Guid.NewGuid(), cancellation.Token));
    Assert.Equal(0, source.Calls);
  }

  private sealed class MutableEligibility : ITenantAuthenticationEligibilityReadService
  {
    public TenantStatus? Status { get; set; } = TenantStatus.Active;
    public int Calls { get; private set; }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      return Task.FromResult(TenantAuthenticationEligibilityResult.FromStatus(tenantId, Status));
    }

    public Task<TenantAuthenticationEligibilityResult> GetEligibilityForUpdateAsync(
      Guid tenantId,
      CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException("Request eligibility must never replace the locked mutation check.");
  }
}
