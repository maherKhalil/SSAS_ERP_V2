namespace SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

// THE ACTIVE BRANCH, SERVER-SIDE (Branch foundation B0/B1).
//
// SEPARATE FROM ICurrentTenant, AND RESOLVED LATER. The tenant must be known BEFORE a branch can be, because
// branches live in the tenant's own database and cannot even be enumerated until routing has resolved. So
// there is a legitimate authenticated state with a tenant and no branch — which is exactly what null means
// here, and why this is not folded into the tenant context.
//
// NULL IS NOT AN ERROR AT THIS LAYER. It is the answer to "has a branch been selected yet", and the write
// boundary is what turns it into a refusal for branch-owned data. A tenant-global write is still perfectly
// legal without one.
public interface ICurrentBranch
{
  Guid? BranchId { get; }
}
