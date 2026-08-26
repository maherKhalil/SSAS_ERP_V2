using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SSAS.BuildingBlocks.Api.Authorization;

// ==================================================================================================
// A MODULE'S ROUTE GROUPS DECLARE WHAT THEY NEED, AND SAY SO AT STARTUP (T-034).
// ==================================================================================================
//
// ---- THE INCIDENT THIS EXISTS TO PREVENT, AND WHY DOCUMENTING IT WAS NOT ENOUGH.
//
// T-032 put every module route group behind `RequireModule`, whose filter resolves
// `ITenantModuleEntitlement` as a REQUIRED service. Five API test hosts build their own
// `WebApplication` and map module endpoints by hand; none of them registered it, because nothing told
// them to. **They started cleanly and then answered 500 to 171 requests** — a per-request failure that
// reads like a product bug, in five suites at once.
//
// ---- WHY IT FAILS AT MAP TIME RATHER THAN AT FIRST REQUEST.
//
// `IPermissionCatalogContributor` is the precedent, and it is a good one: registration is explicit,
// never discovered, so a module that is not registered contributes nothing and that is a loud,
// reviewable omission. **But it is only loud because the Host composes the catalog eagerly at startup**
// — `_ = app.Services.GetRequiredService<IPermissionCatalog>();` — rather than letting it build lazily
// on whichever request authorizes first.
//
// This is the same move one level out. The check runs when a module's endpoints are MAPPED, which is
// startup for every host that has one, so a missing registration is a startup exception naming the
// service, the module and the remedy — instead of a 500 per request, forever.
//
// ---- WHY IT LIVES ON THE MODULE'S MAPPING RATHER THAN ON THE HOST.
//
// A host cannot forget a check that is not its to make. Every host that mounts a module's routes calls
// that module's `Map*Endpoints`, so putting the assertion there means **a sixth test host inherits it
// for free and cannot opt out**, while a host that mounts no module routes is never asked for a service
// it does not need. Asking each host to call a validator would have reproduced the original defect
// exactly: a step a new host can omit in silence.
public static class ModuleEndpointRequirements
{
  // ---- PRESENCE IS PROBED, NOT RESOLVED, AND THAT IS DELIBERATE.
  //
  // `ITenantModuleEntitlement` is registered **scoped** — the real resolver reads per-request tenant
  // state — so resolving it from the root provider at map time would throw for the wrong reason under
  // scope validation, and would construct a service nobody asked for. `IServiceProviderIsService`
  // answers "is this registered" without instantiating anything, which is the question actually being
  // asked.
  public static IEndpointRouteBuilder RequireModuleEnablementServices(
    this IEndpointRouteBuilder endpoints, string moduleKey)
  {
    ArgumentNullException.ThrowIfNull(endpoints);
    ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

    var probe = endpoints.ServiceProvider.GetService<IServiceProviderIsService>();

    // ---- A CONTAINER THAT CANNOT BE PROBED IS NOT TREATED AS A FAILURE, AND THE REASON IS STATED.
    //
    // `IServiceProviderIsService` is registered by `Microsoft.Extensions.DependencyInjection` itself, so
    // a null probe means a third-party container that does not implement it. Throwing then would refuse
    // to start a host whose composition may be perfectly correct, on the strength of a diagnostic that
    // is unavailable. The check is skipped and the original per-request failure remains the backstop —
    // which is the pre-T-034 behaviour, not a new hole.
    if (probe is null || probe.IsService(typeof(ITenantModuleEntitlement)))
    {
      return endpoints;
    }

    throw new InvalidOperationException(
      $"Module '{moduleKey}' maps gated route groups, but no {nameof(ITenantModuleEntitlement)} is " +
      "registered. Every module route group passes through RequireModule, whose filter resolves that " +
      "contract as a required service, so without it every route below answers 500 at request time " +
      "rather than failing here. " +
      $"Register one — the Host registers {nameof(TransitionalGrantsEveryModuleEntitlement)} until the " +
      "commercial plane's resolver replaces it. This is a composition defect, not a runtime condition: " +
      "a host that mounts a gated surface and cannot say whether the tenant is entitled is " +
      "misconfigured, and admitting the request instead would be a gate that does nothing.");
  }
}
