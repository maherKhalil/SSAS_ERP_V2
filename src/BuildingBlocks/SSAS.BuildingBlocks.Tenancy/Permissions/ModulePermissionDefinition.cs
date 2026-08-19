namespace SSAS.BuildingBlocks.Tenancy.Permissions;

// ONE PERMISSION A MODULE DEFINES (ADR-012 r1.2).
//
// ---- THERE IS NO SCOPE ON THIS TYPE, AND THAT IS THE POINT.
//
// Permission scope separates what a TENANT administers from what a PLATFORM OPERATOR administers
// (`PermissionScope.PlatformSupport`, ADR-015 §8): cross-tenant authority that is never tenant-assignable.
// A business module's functional permissions are tenant authority by definition, so the composer stamps
// `Tenant` and a module has no way to ask for anything else.
//
// Making it unrepresentable rather than validated is deliberate. A `Scope` property here would be a field
// a future module could set to `PlatformSupport` and a reviewer would have to notice; with no property
// there is nothing to review, and the escalation cannot be expressed.
//
// ---- THE NAME IS A PLAIN STRING BECAUSE THE GRAMMAR IS PLATFORM'S TO ENFORCE.
//
// `PermissionName` and its three-segment grammar live in Platform's Domain and stay there. Handing modules
// a pre-validated name type would either move that grammar into shared code or give contributors a second
// validation path; instead the composer runs the one canonical `PermissionName.Create` over every
// contributed name and refuses the whole composition if any fails.
public sealed record ModulePermissionDefinition(string Name, string Description);
