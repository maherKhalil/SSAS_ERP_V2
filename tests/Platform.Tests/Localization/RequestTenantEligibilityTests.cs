using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Tenants;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Infrastructure.Persistence.Queries;

namespace SSAS.Platform.Tests.Localization;

// ⚠ EXAMINED FOR CITATION AND DELIBERATELY LEFT UNCITED. Nine route criteria (`AC-LOC-0042` through
// `-0050`) each carry a *"current live Tenant"* or *"trusted live Tenant"* clause, and this file is the
// MECHANISM BENEATH ALL NINE — but it drives `RequestTenantEligibility` directly and touches no route. A
// clause that says a ROUTE ENFORCES something is not satisfied by showing the thing it enforces works.
//
// **Citing nine ids here would put nine criteria's worth of apparent coverage on a test that exercises no
// endpoint** — the failure mode a criterion id is most prone to, because an id reads later as PROVEN.
//
// ⚠⚠ WHAT THIS FILE DOES ESTABLISH, AND IT IS WORTH RECORDING EVEN WITHOUT A CITATION:
//
//   *live* really means live. `New_scope_observes_suspension_after_an_active_request` suspends the tenant
//   BETWEEN requests and shows the next scope observing it, while the in-flight scope keeps its answer.
//   That is the difference between *live* and *decided once at login*, and no route test states it.
//
//   The mutation path cannot be served from this cache: `GetEligibilityForUpdateAsync` throws
//   *"Request eligibility must never replace the locked mutation check."* A read-scoped cache silently
//   answering a locked check is exactly how a suspended tenant would keep writing.
//
// ⚠⚠⚠ AND THE POPULATION OF `Non_active_or_missing_tenant_remains_denied` IS COMPLETE — MEASURED, NOT
// ASSUMED. `TenantStatus` declares exactly FOUR members (Provisioning, Active, Suspended, Archived); the
// theory names the three non-Active ones plus `null` for the missing tenant. **So this is that rare thing,
// a hand-enumerated `[InlineData]` set that is exhaustive over its domain rather than merely plausible.**
//
// ⚠ THE RESIDUAL IS THE USUAL ONE AND IT IS NOT FIXED HERE: exhaustive TODAY. A fifth `TenantStatus`
// member would be denied by nothing and named by no test, and nothing in this file pins the enum's size.
// A one-line count control would close it; that is a logic change and is left for a separate decision.
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
