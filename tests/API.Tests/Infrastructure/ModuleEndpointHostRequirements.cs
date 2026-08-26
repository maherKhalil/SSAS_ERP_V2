using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.API.Tests.Infrastructure;

// WHAT A TEST HOST OWES A MODULE ROUTE GROUP, IN ONE PLACE (T-034).
//
// ---- THE DECLARATION AND THE SATISFACTION ARE DELIBERATELY TWO DIFFERENT THINGS.
//
// `ModuleEndpointRequirements.RequireModuleEnablementServices` is the DECLARATION — it lives beside the
// module's own mapping and refuses to map without the service. This is the SATISFACTION, and it is here
// rather than in production because these are test hosts: the product Host registers the real thing in
// `Program.cs`, and a shared test helper that reached into production composition would make the two
// drift together in ways neither could see.
//
// **Neither half makes the other redundant.** Without the declaration, a sixth host forgets this call and
// nothing says so until a request arrives. Without this, five hosts each spell the same registration by
// hand and the next required service means five more edits — which is precisely how the original defect
// arrived.
//
// ---- WHAT THIS DOES NOT CONSOLIDATE, AND WHY.
//
// The five hosts share **ten** registrations: this contract, `ICurrentTenant`, `ICurrentTenantUser`,
// `ICurrentCompany`, `ICompanyContextEstablisher`, `ITenantCompanyAccessResolver`,
// `IRequestTenantEligibility`, `ITenantAuthenticationEligibilityReadService`, `IDateTimeProvider` and
// `ITenantUnitOfWork`.
//
// Only this one is consolidated here, because only this one is a requirement of the SEAM every module
// route group passes through. The other nine are satisfied with per-host fakes that read that host's own
// `TenantId` and company context — the classes are duplicated, but the identities they return are
// deliberately different, and folding them into a shared helper would either erase that difference or
// force every host to pass its identity back in, which is the same five edits wearing a parameter list.
//
// That distinction is recorded rather than acted on: **five hosts differing deliberately is not the same
// problem as five hosts differing by accident**, and here they do both, in different places.
public static class ModuleEndpointHostRequirements
{
  // ---- A TEST DOUBLE, AND IT IS NOT THE PRODUCTION DEFAULT WEARING A TEST NAME (T-040).
  //
  // The Host now resolves entitlement from the Platform database; the transitional grant-everything
  // implementation is DELETED, not moved here. These five hosts exist to exercise module ENDPOINTS —
  // their suites assert routing, validation, permissions and transport — and none of them has a
  // database, a subscription record or any reason to care what entitlement answers.
  //
  // So the double grants everything, which restores exactly the behaviour those suites were written
  // against and asserts nothing new. **Entitlement itself is tested where it can be: the snapshot in
  // `Platform.Tests`, the refusal end-to-end in `ModuleEnablementGateTests`, and the real read against
  // real SQL in `Integration.Tests`.**
  //
  // Scoped, matching the Host's registration for the real resolver.
  public static IServiceCollection AddModuleEndpointRequirements(this IServiceCollection services)
  {
    ArgumentNullException.ThrowIfNull(services);

    services.AddScoped<ITenantModuleEntitlement, AlwaysEntitled>();

    return services;
  }
}

// The double itself. Named for what it does, so a reader of a failing test is not left wondering whether
// entitlement was in play: in these five suites it never is.
internal sealed class AlwaysEntitled : ITenantModuleEntitlement
{
  public ValueTask<bool> IsEnabledAsync(string moduleKey, CancellationToken cancellationToken)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

    return ValueTask.FromResult(true);
  }
}
