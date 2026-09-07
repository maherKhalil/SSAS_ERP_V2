using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Localization;
using SSAS.Platform.Domain.Localization.Events;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// AN OVERRIDE EVENT DISPATCHED THROUGH THE REAL PIPELINE EVICTS THE REAL CACHE (T-143).
// ==================================================================================================
//
// ---- ⚠⚠⚠ BOTH HALVES WERE TESTED AND THE JOIN WAS NOT.
//
//     the dispatcher   `DomainEventDispatcherTests`, `DomainEventFlowTests` — against their own
//                      `RecordingConsumer` STUB, never the real consumer
//     the consumer     `LocalizationResolverTests.Post_commit_domain_event_evicts_the_tenant_generation`
//                      — which does `new LocalizationCacheDomainEventConsumer(fixture.Cache)` and calls
//                      `HandleAsync` BY HAND, touching neither the dispatcher nor the container
//
// ***THE DISPATCHER IS PROVEN TO LOOP ITS CONSUMERS. THE CONSUMER IS PROVEN TO EVICT. NOTHING ASSERTED THAT
// THIS CONSUMER IS IN THAT LOOP.*** `services.AddSingleton<IDomainEventConsumer, LocalizationCacheDomainEventConsumer>()`
// was deleted and the suites ran **API 1006, Platform 1159, Architecture 725 — 2,890 tests, all green.**
//
// ⚠⚠ AND THE CONSEQUENCE HAS NO OTHER ALARM. `LocalizationAuditReadinessTests:62` records that **`EvictTenant`
// has EXACTLY ONE CALLER in `src/`** — this consumer. *Without the registration nothing ever evicts: the cache
// serves stale translations until the process restarts, and the only thing that notices is a user reading
// them. No test, no log, no error.*
//
// ---- ⚠⚠ WHY THIS AND NOT A REGISTRATION SET, WHICH IS THE SHAPE USED THREE TIMES ALREADY.
//
// `HostedServiceRegistrationTests` and `OptionsValidatorRegistrationTests` assert a `typeof`-bound set against
// the container. **Here that set would have ONE element — which is a frozen list wearing a set's costume, and
// it would assert less than this does.** *This test subsumes it: delete the registration and the eviction
// never happens, so this reddens; and unlike a set assertion it also fails if the dispatcher stops looping,
// if the consumer stops matching the event type, or if the cache stops honouring the eviction.*
//
// ---- ⚠ THE LIMIT.
//
// **This proves THIS consumer is in the loop. It does not prove a future one will be** — a second
// `IDomainEventConsumer` added and left unregistered is invisible here. *That is the honest bound, and it is
// not an argument for building the one-element list instead: the list would not catch a second consumer
// either unless somebody remembered to add it, which is the same remembering.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class LocalizationCacheEvictionWiringTests(HostWebApplicationFactory factory)
{
  [Fact]
  public async Task An_override_event_dispatched_through_the_host_evicts_that_tenants_cache()
  {
    // The cache is a SINGLETON, so what this test primes is the same instance the consumer will evict; the
    // dispatcher is SCOPED, so it is resolved from a scope exactly as a request would.
    var cache = factory.Services.GetRequiredService<ILocalizationTenantCache>();
    var tenantId = Guid.NewGuid();
    var loads = 0;

    Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> Load(CancellationToken _)
    {
      loads++;

      return Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>([]);
    }

    await Read();
    Assert.Equal(1, loads);

    // ---- ⚠⚠⚠ THE CONTROL, AND WITHOUT IT THE ASSERTION AT THE FOOT IS SATISFIED BY A CACHE THAT NEVER
    // ---- CACHES.
    //
    // A cache that reloaded on every call would show `loads` rising after the dispatch too, and the test
    // would pass while proving nothing about eviction. **This second read establishes that a HIT is what
    // happens when nothing has been evicted** — so the third read's miss can only be the eviction.
    await Read();
    Assert.Equal(1, loads);

    using (var scope = factory.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(
      [
        new TenantLocalizationOverrideUpdated(
          Guid.NewGuid(), DateTimeOffset.UtcNow, tenantId, Guid.NewGuid(),
          "platform.common.actions.save", "en", 1, 2, 2, 1)
      ]);
    }

    await Read();

    Assert.True(
      loads == 2,
      $"the cache loaded {loads} time(s). A `TenantLocalizationOverrideUpdated` was dispatched through the " +
      "host's own IDomainEventDispatcher and this tenant's cache entry was NOT evicted, so the next read was " +
      "served stale. `EvictTenant` has one caller in src — LocalizationCacheDomainEventConsumer — reached " +
      "only through the IDomainEventConsumer collection, so the likeliest cause is that its registration is " +
      "absent and nothing else would report that.");

    Task<IReadOnlyDictionary<string, TenantLocalizationOverrideReadModel?>> Read() =>
      cache.GetOrCreateAsync(tenantId, "en", 1, 2, ["platform.common.actions.save"], Load);
  }

  // ---- ⚠ A SECOND TENANT IS NOT EVICTED, WHICH IS WHAT MAKES THE EVICTION A TENANT'S AND NOT THE CACHE'S.
  //
  // `EvictTenant(tenantId)` taking an argument is not evidence that it uses it. **A consumer that cleared the
  // whole cache would satisfy the test above and would also throw away every other tenant's entries on every
  // override anywhere** — a real performance defect, invisible to a single-tenant assertion.
  //
  // ⚠⚠ **THIS ONE IS VACUOUS ON ITS OWN AND THE PAIR IS WHAT MAKES IT MEAN ANYTHING — MEASURED, NOT ARGUED.**
  // Deleting the consumer's registration reddens the test above and leaves this one GREEN, because with
  // nothing evicting, "the other tenant was not evicted" is trivially true. ***ITS SUBJECT IS THE SCOPE OF AN
  // EVICTION THAT THE TEST ABOVE PROVES HAPPENS.*** *Neither is a substitute for the other, and this one must
  // not be read as covering the wiring.*
  [Fact]
  public async Task Dispatching_for_one_tenant_leaves_another_tenants_cache_intact()
  {
    var cache = factory.Services.GetRequiredService<ILocalizationTenantCache>();
    var evicted = Guid.NewGuid();
    var untouched = Guid.NewGuid();
    var untouchedLoads = 0;

    Task<IReadOnlyList<TenantLocalizationOverrideReadModel>> Load(CancellationToken _)
    {
      untouchedLoads++;

      return Task.FromResult<IReadOnlyList<TenantLocalizationOverrideReadModel>>([]);
    }

    await cache.GetOrCreateAsync(untouched, "en", 1, 2, ["platform.common.actions.save"], Load);
    Assert.Equal(1, untouchedLoads);

    using (var scope = factory.Services.CreateScope())
    {
      await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(
      [
        new TenantLocalizationOverrideUpdated(
          Guid.NewGuid(), DateTimeOffset.UtcNow, evicted, Guid.NewGuid(),
          "platform.common.actions.save", "en", 1, 2, 2, 1)
      ]);
    }

    await cache.GetOrCreateAsync(untouched, "en", 1, 2, ["platform.common.actions.save"], Load);

    Assert.True(
      untouchedLoads == 1,
      $"another tenant's cache entry was reloaded {untouchedLoads} time(s) after an override event for a " +
      "DIFFERENT tenant. The eviction is not scoped to the tenant named in the event, so every override " +
      "anywhere discards every tenant's cached localization.");
  }
}
