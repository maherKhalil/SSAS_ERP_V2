namespace SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

// THE TRUSTED CURRENT COMPANY, SERVER-SIDE (FP-006C1, ADR-025 decision 2).
//
// SEPARATE FROM ICurrentTenant, AND RESOLVED LATER. The tenant must be known before a company can be,
// because companies live in the tenant's own database and cannot be read until routing has resolved. So
// there is a legitimate authenticated state with a tenant and no company.
//
// NULL IS NOT AN ERROR AT THIS LAYER. It is the answer to "has a company been established yet", and the
// write boundary is what turns it into a refusal for company-owned data. Tenant-global and branch-only work
// is unaffected.
//
// WHAT IS EXPOSED HERE HAS ALREADY BEEN VALIDATED. A caller-supplied company identifier expresses INTENT
// only (ICompanySelection); it becomes visible here only after the five-step live validation in
// ICompanyContextResolver has passed — exists, belongs to the trusted tenant, is Active, and the caller is
// currently authorized for it.
//
// IT IS NOT THE WRITE BOUNDARY'S SOURCE OF AUTHORITY, deliberately. A synchronous property can only ever
// report a value someone else already checked, and company access is revocable inside a request. Saves go
// through ICompanyWriteAuthorizer, which re-asks against live state — the same reason the branch write
// boundary uses IBranchWriteAuthorizer rather than a plain ICurrentBranch.
public interface ICurrentCompany
{
  Guid? CompanyId { get; }
}
