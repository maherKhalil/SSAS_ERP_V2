namespace SSAS.BuildingBlocks.Tenancy;

// ==================================================================================================
// WHICH EMPLOYEE A TENANT USER IS, ASKED BY A MODULE AND ANSWERED BY PLATFORM (ADR-030, T-084).
// ==================================================================================================
//
// ---- WHY IT LIVES HERE AND NOT IN `SSAS.Platform.Contracts`.
//
// `BuildingBlocks.Tenancy` is already the module-to-Platform seam and has been since `ADR-012`: it holds
// `ModulePermissionDefinition` and `IPermissionCatalogContributor` — types modules produce and Platform
// consumes — and `ICurrentTenantUser`, whose implementation lives in `Platform.Infrastructure`. **The edge
// exists, is load-bearing, and this is the one the product uses.**
//
// `SSAS.Platform.Contracts` exists and is empty — one project file, no types, and no module references it.
// Adopting it here would open the first module-to-Platform project reference in the product, which is a
// structural precedent rather than a defect fix. If it is later adopted deliberately, moving one interface
// is cheap; adding an architectural edge and finding it wrong is not.
//
// ---- IT ASKS ONE QUESTION AND TAKES ITS SUBJECT EXPLICITLY.
//
// Not an identity service, and deliberately not "tell me about the current user". The tenant user is a
// PARAMETER rather than ambient state, for the reason `ITenantModuleEntitlement` gives about the current
// tenant: a caller that reads its subject from ambient context cannot be asked about anyone else, and one
// that takes it explicitly cannot accidentally answer about the wrong person. The caller already holds the
// id — `ICurrentTenantUser` is right beside this file.
//
// ---- ABSENCE IS AN ORDINARY ANSWER, NOT AN ERROR (`ADR-030` DECISION 5).
//
// `null` means this tenant user has no linked employee, and that is a normal state on both sides: platform
// support staff, and users created before their employee record exists. **A caller must not treat it as a
// fault** — `ADR-030` puts it plainly: *"a support administrator opening a self-service page is not a fault
// condition; it is Tuesday."*
public interface IUserEmployeeResolver
{
  // Returns the linked employee, or null when this tenant user has none. Never throws for an unknown user:
  // not-linked and not-a-user are the same answer to this question, and distinguishing them would let a
  // caller probe for the existence of a user id.
  Task<Guid?> ResolveEmployeeIdAsync(long tenantUserId, CancellationToken cancellationToken = default);
}
