using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.Payroll.API;

// ==================================================================================================
// PAYROLL'S MODULE KEY, FOR ENABLEMENT (FP-014, OD-SUB-0005).
// ==================================================================================================
//
// ---- THE UNIT IS THE MODULE, NOT THE ROUTE GROUP.
//
// This module mounts one route group covering compensation, pay elements, periods, the run lifecycle and payslips.
// `OD-SUB-0005` ruled the gateable unit is the thing carrying exactly one `IPermissionCatalogContributor`
// and one `Add*Module()` registration, which is this module — so one key, applied to every group it owns.
//
// ---- THE KEY IS STABLE AND IS NEVER DERIVED FROM A ROUTE.
//
// Once per-tenant subscription assignment exists, this string is what a tenant's entitlement rows point at.
// Changing it would silently un-entitle every tenant holding the old value, which is why it is declared
// here as a value rather than computed from the route prefix it happens to share a name with today.
//
// ---- WHY A TYPE RATHER THAN A CONSTANT.
//
// An architecture test enumerates `IModuleEnablementDescriptor` implementations to assert there are exactly
// four, that each key is unique, and that no platform-plane assembly declares one. A bare `const string`
// would be invisible to that check, and the check is what stops a fifth module being added ungated.
public sealed class PayrollModuleEnablement : IModuleEnablementDescriptor
{
  public const string Key = "Payroll";

  public string ModuleKey => Key;
}
