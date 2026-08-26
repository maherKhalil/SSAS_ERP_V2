using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SSAS.BuildingBlocks.Api.Authorization;

// ==================================================================================================
// MODULE ENABLEMENT — THE SEAM `BR-PLT-0008` IS ENFORCED THROUGH (FP-014, OD-SUB-0003).
// ==================================================================================================
//
// ---- WHAT THIS DOES NOT DO, STATED FIRST BECAUSE IT MATTERS MOST.
//
// **This does not yet satisfy `BR-PLT-0008`.** There is no subscription data in this product: no plan, no
// per-tenant assignment, no entitlement grant. `OD-SUB-0004` places that assignment in the Platform
// database, and the build obligation is **no backfill and no default plan** — so until that schema exists,
// the only honest resolver is one that grants everything, and this ships with exactly that.
//
// What ships here is the MECHANISM every module route passes through, and a guard that makes a module
// added later unable to skip it by omission. `OD-SUB-0003` ruled the gate goes before the next module for
// this reason: retrofitting a seam across four modules is a different job from adding one module to a seam
// that already exists, and the violation grows monotonically until it is done.
//
// ---- WHY THE UNIT IS A MODULE KEY AND NOT A ROUTE PREFIX (OD-SUB-0005).
//
// The product mounts SEVENTEEN route groups but has FOUR gateable modules — HR alone mounts seven. A
// prefix-based gate would need a second notion of "module" that disagreed with the one the permission
// catalog already uses, and `OD-SUB-0005` ruled against exactly that: the unit is the thing carrying one
// `IPermissionCatalogContributor` and one `Add*Module()` registration. HR's seven groups share one key.
//
// ---- WHY THIS LIVES HERE, IN A PROJECT THAT REFERENCES NOTHING.
//
// `SSAS.BuildingBlocks.Api` deliberately has no project references, and an architecture test pins that. The
// contract below therefore names no module, no tenant type and no persistence concept: it takes a module
// key and answers a question. The IMPLEMENTATION knows about tenancy; the seam does not, which is the same
// membership rule `RequirePermission` and `RowVersionCodec` pass.
//
// It is a sibling of `PermissionEndpointConventions`, not new authorization architecture (`DEC-SUB-0006`).

// ---- THE CONTRACT. ONE QUESTION, ASKED PER REQUEST.
//
// The resolver answers for the CURRENT request's tenant rather than taking a tenant id, deliberately. A
// tenant id parameter would let a caller ask about a tenant other than its own, which is a cross-tenant
// read dressed as an authorization check — and it would drag tenancy vocabulary into a project that must
// not have it.
//
// **Entitlement never travels in the access token** (`DEC-SUB-0005`, and `FP-002` excludes it from claim
// content). It is resolved server-side on every request, so a change takes effect without re-issuing a
// token and without restarting the host (`REQ-SUB-0009`).
public interface ITenantModuleEntitlement
{
  // True when the current request's tenant is entitled to the named module.
  //
  // Implementations must not throw for an unknown key: an unrecognised module is not entitled, and a
  // refusal is the safe answer. Throwing would turn a commercial state into a 500.
  ValueTask<bool> IsEnabledAsync(string moduleKey, CancellationToken cancellationToken);
}

// ---- THE MODULE'S OWN DECLARATION OF ITS KEY.
//
// One implementation per gateable module assembly, and none in a platform-plane assembly. It exists as a
// TYPE rather than a constant so the key is discoverable by reflection: an architecture test enumerates
// these and asserts there are exactly four, that each key is unique, and that no platform-plane assembly
// declares one. A bare `const string` would be invisible to that check.
public interface IModuleEnablementDescriptor
{
  // Stable, and never derived from a route. Changing it would silently un-entitle every tenant holding the
  // old value once real assignment data exists.
  string ModuleKey { get; }
}

// ---- THE DECLARED KEY, AS ENDPOINT METADATA.
//
// Carrying the key as metadata makes the gate READABLE from an endpoint without issuing a request, which is
// what lets the completeness guard assert coverage over the whole mapped surface rather than trusting that
// every route group remembered to call `RequireModule`.
public sealed record ModuleEnablementMetadata(string ModuleKey);

// ==================================================================================================
// THE CONVENTION.
// ==================================================================================================
//
// A sibling of `RequirePermission`, and separate from it for the same reason the two permission planes are
// two methods: entitlement and permission are different questions with different answers, and a caller must
// not be able to choose one by passing a flag to the other.
//
// **Applied to the GROUP, never to a route.** That is the whole point — a route added to an existing group
// later cannot forget the gate, which is the same reasoning the company-context and response-header filters
// already use. `RequirePermission` is per-route because permissions differ per operation; entitlement does
// not differ per operation, so it belongs one level up.
public static class ModuleEnablementEndpointConventions
{
  // ---- ORDERING: THE GATE RUNS BEFORE THE HANDLER, AFTER AUTHORIZATION.
  //
  // An endpoint filter runs after authentication and authorization have admitted the caller, which is the
  // correct order: entitlement is a commercial fact about the tenant, not a claim about the principal, and
  // asking it before we know who the tenant is would be meaningless.
  public static TBuilder RequireModule<TBuilder>(this TBuilder builder, string moduleKey)
    where TBuilder : IEndpointConventionBuilder
  {
    ArgumentNullException.ThrowIfNull(builder);
    ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

    builder.WithMetadata(new ModuleEnablementMetadata(moduleKey));

    return builder.AddEndpointFilter(async (context, next) =>
    {
      var entitlement = context.HttpContext.RequestServices.GetRequiredService<ITenantModuleEntitlement>();

      if (!await entitlement.IsEnabledAsync(moduleKey, context.HttpContext.RequestAborted))
      {
        // ---- 403, NOT 404 (OD-SUB-0006), AND THE OWNER ACCEPTED THE COST KNOWINGLY.
        //
        // A 404 would hide which modules exist; a 403 says the route exists and this tenant may not reach
        // it. The disclosure — a tenant can enumerate the product surface by probing — was weighed against
        // support being able to answer "why can't I reach payroll" from the response rather than from
        // server logs, and the owner chose the answerable one.
        return Results.StatusCode(StatusCodes.Status403Forbidden);
      }

      return await next(context);
    });
  }
}
