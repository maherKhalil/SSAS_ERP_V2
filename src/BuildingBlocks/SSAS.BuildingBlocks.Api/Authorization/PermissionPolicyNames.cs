namespace SSAS.BuildingBlocks.Api.Authorization;

// ==================================================================================================
// THE ONE CANONICAL POLICY-NAMING CONTRACT (FP-006C5, ADR-015 §8).
// ==================================================================================================
//
// A permission requirement travels from an endpoint to the Host's policy provider as a POLICY NAME, and both
// ends must agree on how that name is spelled. The prefixes were previously written out in two places — the
// Host's policy defaults and Platform.API's endpoint convention — and the Platform copy carried a comment
// admitting it was a duplicate kept in sync by hand.
//
// Adding HR as a third endpoint author would have made it three copies, so the literal now lives here, once,
// and both the Host and every module API read it from this one place. This project is the natural home: the
// name is transport vocabulary shared by everything that maps an endpoint, and it names no permission, no
// module and no policy.
//
// ---- WHY THE TWO PLANES ARE STRUCTURALLY SEPARATE.
//
// "PlatformPermission:" is not a prefix of "Permission:", nor the reverse, so the two can never collide in
// the shared policy provider and a platform-support permission can never be satisfied through the tenant
// path. That property is load-bearing, not cosmetic — it is why these are two constants and not one with a
// parameter.
public static class PermissionPolicyNames
{
  // The TENANT plane: a permission held by a tenant user, evaluated against the trusted tenant and live
  // eligibility.
  public const string TenantPrefix = "Permission:";

  // The PLATFORM-SUPPORT plane: a permission held by a platform principal. Deliberately never mixed with the
  // tenant prefix above.
  public const string PlatformPrefix = "PlatformPermission:";
}
