namespace SSAS.BuildingBlocks.Tenancy;

// WHICH TENANT USER IS ACTING, FOR SCOPE AUTHORIZATION (FP-006C3, ADR-012).
//
// ITenantBranchAccessResolver and ITenantCompanyAccessResolver take the acting user EXPLICITLY, because
// their earliest caller is authentication itself — which must answer "which branches may this user enter"
// while deciding whether a login completes, before any ambient user context exists.
//
// A business module is in the opposite position: it always has an ambient caller, and would otherwise have
// to reach into Platform's authentication contracts to name them. This is the narrow module-facing view of
// that identity, and deliberately nothing more — no roles, no permissions, no session, no claims. A module
// needs to say WHO is asking so scope can be resolved; deciding what they may do stays with the ordinary
// permission pipeline.
//
// NULL IS NOT AN ERROR AT THIS LAYER. Background, maintenance and migration compositions have no acting
// user, and the boundary that needs one turns the absence into a refusal rather than a fallback.
public interface ICurrentTenantUser
{
  long? TenantUserId { get; }
}
