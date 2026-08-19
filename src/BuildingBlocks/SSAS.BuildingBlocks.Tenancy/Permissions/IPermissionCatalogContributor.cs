namespace SSAS.BuildingBlocks.Tenancy.Permissions;

// ==================================================================================================
// A BUSINESS MODULE'S OWN PERMISSION DEFINITIONS, OFFERED TO THE ONE CATALOG (ADR-012 r1.2).
// ==================================================================================================
//
// ---- THE DEFECT THIS EXISTS TO CLOSE.
//
// Functional permissions are code-owned: a role may only be granted a name the catalog defines, because
// `AssignPermissionToRoleCommandHandler` refuses anything else and `Role.AssignPermission` needs a
// definition only the catalog can produce. That is correct, and it left business modules with nowhere to
// put theirs. HR declared five `HR.Employees.*` constants that no catalog knew, so no tenant role could be
// granted one and every Employee endpoint answered 403 to every caller — while every test passed, because
// tests mint permission claims directly and never travel the assignment path.
//
// ---- WHY A CONTRIBUTOR RATHER THAN AN ENTRY IN PLATFORM'S CATALOG.
//
// `SSAS.Platform.*` is a module under ADR-012, so it may not reference HR — and writing `HR.Employees.View`
// into Platform's catalog would be the same coupling with the project reference filed off. Platform must be
// able to compose a module's permissions without knowing which module, or how many, exist.
//
// This contract is the same shape as `ITenantModelContributor`: neither side owns it, the Host registers
// the set EXPLICITLY, and there is no reflection-based discovery. A module that is not registered
// contributes nothing, which is a loud, reviewable omission rather than a silent one.
//
// ---- IT CARRIES DATA, NOT BEHAVIOUR.
//
// A contributor states which permissions its module defines. It does not validate them, does not decide
// their scope, and cannot reach the composed catalog: the composer applies the SAME canonical name
// validation Platform's own definitions go through, and stamps the scope itself. A contributor therefore
// cannot grant itself a laxer rule than Platform lives under.
public interface IPermissionCatalogContributor
{
  // Enumerated once at composition and never re-read. Implementations must be deterministic: the same set
  // every time, with no dependence on tenant, request or ambient state, exactly as tenant-model
  // contributors are required to be.
  IReadOnlyCollection<ModulePermissionDefinition> Permissions { get; }
}
