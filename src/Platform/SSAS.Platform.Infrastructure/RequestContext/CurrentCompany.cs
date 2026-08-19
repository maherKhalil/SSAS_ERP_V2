using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Companies;

namespace SSAS.Platform.Infrastructure.RequestContext;

// THE TRUSTED COMPANY FOR THIS REQUEST, ESTABLISHED ONCE AND ONLY BY VALIDATION (FP-006C1, ADR-025
// decision 2).
//
// IT IS SCOPED, AND IT STARTS EMPTY. `CompanyId` is null until EstablishAsync has run the five-step live
// validation, which is what makes "no company selected" and "a company the caller may not use" the same
// observable state from here: neither produces a value.
//
// IT IS A REPORTER, NOT AN AUTHORITY. Handlers and read models use it to know which company the request is
// operating within; the WRITE boundary deliberately does not, because a value established at the start of a
// request is exactly what must not be trusted at save time. Saves go through ICompanyWriteAuthorizer, which
// re-asks the resolver — the same division the branch dimension makes between ICurrentBranch and
// IBranchWriteAuthorizer.
//
// ESTABLISHING TWICE IS IDEMPOTENT BY RE-VALIDATION, not by memoisation: each call runs the validation
// again, so a company revoked mid-request cannot be re-established from a cached answer.
public sealed class CurrentCompany(ICompanyContextResolver contextResolver) : ICurrentCompany, ICompanyContextEstablisher
{
  private Guid? established;

  public Guid? CompanyId => established;

  // Runs the five-step validation and, on success, makes the company visible to ICurrentCompany consumers
  // for the rest of the request. On failure the context is CLEARED rather than left holding a previously
  // established value, so a revocation observed mid-request cannot leave a stale company readable.
  public async Task<Result<Guid>> EstablishAsync(CancellationToken cancellationToken = default)
  {
    var resolved = await contextResolver.ResolveTrustedCompanyAsync(cancellationToken);

    established = resolved.IsSuccess ? resolved.Value : null;

    return resolved;
  }
}
