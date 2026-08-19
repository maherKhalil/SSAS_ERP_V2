using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Companies;

// THE AUTHORITATIVE COMPANY FOR A COMPANY-OWNED WRITE (FP-006C1, ADR-025 decision 9).
//
// ONE CALL ANSWERS BOTH QUESTIONS — which company, and may this user still write within it — because
// answering them separately is how they drift apart. A "current company" read from one place and an
// authorization check made against another is exactly the shape in which a revoked user keeps writing.
// This mirrors IBranchWriteAuthorizer for the sibling dimension, deliberately and for the same reason.
//
// EVERY INPUT IS RE-READ FROM AUTHORITATIVE STATE:
//
//   * the SELECTED COMPANY comes from ICompanySelection, which carries caller INTENT and no authority;
//   * the VALIDATION comes from ICompanyContextResolver, which re-reads existence, tenant ownership, Active
//     state, the assignment rows and the administrator authority — all of which can change inside a
//     request's lifetime.
//
// IT DOES NOT READ ICurrentCompany. That property reports a value established earlier in the request, and a
// value established earlier is precisely what must not be trusted at save time.
//
// IT FAILS CLOSED. No company selected, no trusted tenant, company deactivated, access revoked, authority
// revoked, resolver absent: every one of them refuses the write rather than falling back to a previously
// valid answer.
public interface ICompanyWriteAuthorizer
{
  Task<Result<Guid>> AuthorizeCurrentCompanyAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
