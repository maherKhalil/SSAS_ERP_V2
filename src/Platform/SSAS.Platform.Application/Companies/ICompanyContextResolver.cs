using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Companies;

// THE FIVE-STEP VALIDATION THAT TURNS INTENT INTO A TRUSTED COMPANY (FP-006C1, ADR-025 decision 3).
//
// ONE PLACE PERFORMS IT, AND BOTH CALLERS USE THAT PLACE. The request path establishes ICurrentCompany
// through this; the write boundary re-asks through this on every save. Two implementations of "is this
// company usable" is exactly how a read path and a write path come to disagree, so there is one.
//
// THE ORDER IS PART OF THE CONTRACT, and it fails closed at every step:
//
//   1. a trusted current tenant exists;
//   2. the caller requested a syntactically valid company identifier;
//   3. the company exists;
//   4. the company belongs to that tenant;
//   5. the company is Active, AND the caller is currently authorized for it.
//
// Steps 3 to 5 are resolved by ITenantCompanyAccessResolver against LIVE state on every call. Nothing is
// cached from login, and nothing is cached from an earlier call in the same request: company access is
// revocable and a company is deactivatable inside a request's lifetime.
//
// EVERY FAILURE FROM STEPS 3 TO 5 IS THE SAME ERROR. Nonexistent, cross-tenant, inactive and unauthorized
// are indistinguishable to the caller, so a company identifier cannot be probed for existence.
public interface ICompanyContextResolver
{
  Task<Result<Guid>> ResolveTrustedCompanyAsync(CancellationToken cancellationToken = default);
}
