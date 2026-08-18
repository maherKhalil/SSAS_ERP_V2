using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Companies;
using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Infrastructure.Companies;

// Resolves and re-authorizes the request's selected company on every company-owned write (FP-006C1,
// ADR-025 decision 9).
//
// IT DELEGATES RATHER THAN DUPLICATES. The five-step validation lives in ICompanyContextResolver and is
// asked again here, on every save. A second copy of "is this company usable" is precisely how a request path
// and a write path come to disagree, so this type deliberately contains no validation logic of its own —
// its whole job is to run the one validation at the right moment and to check the tenant it was given.
//
// THE TENANT IS CONFIRMED, NOT ASSUMED. The write boundary passes the tenant it is about to write as, and
// this refuses when that disagrees with the tenant the trusted company was resolved within. Otherwise a
// context routed to one tenant could stamp rows with a company authorized inside another.
internal sealed class CompanyWriteAuthorizer(
  ICompanyContextResolver contextResolver,
  ICurrentTenant currentTenant) : ICompanyWriteAuthorizer
{
  public async Task<Result<Guid>> AuthorizeCurrentCompanyAsync(
    Guid tenantId,
    CancellationToken cancellationToken = default)
  {
    if (tenantId == Guid.Empty)
    {
      return Result.Failure<Guid>(CompanyAccessErrors.ContextRequired);
    }

    // THE CONTEXT THE COMPANY IS VALIDATED WITHIN MUST BE THE CONTEXT BEING WRITTEN. The resolver validates
    // against ICurrentTenant; the caller states the tenant whose database this save is landing in. When they
    // differ, nothing here is trustworthy — refuse rather than pick one.
    if (currentTenant.TenantId is not { } trustedTenantId || trustedTenantId != tenantId)
    {
      return Result.Failure<Guid>(CompanyAccessErrors.ContextRequired);
    }

    // ---- AND THE AUTHORIZATION IS ASKED AGAIN, NOW. Assignment revoked, administrator authority revoked,
    // company deactivated — each of these leaves the requested company identifier perfectly readable and no
    // longer usable, which is precisely why an earlier answer is not reused.
    return await contextResolver.ResolveTrustedCompanyAsync(cancellationToken);
  }
}
