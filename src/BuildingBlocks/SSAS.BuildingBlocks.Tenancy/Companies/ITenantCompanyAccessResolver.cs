using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Tenancy.Companies;

// IT LIVES IN THE MODULE-FACING TENANT CONTRACT SET, not in Platform.Application, for the same reason the
// branch resolver does (ADR-012 v1.1, FP-006C3): a MODULE must call it and PLATFORM must implement it, and
// SSAS.Platform.* is itself a module, so a module referencing it there would be a module-to-module
// reference. It was moved here in FP-006C4, when HR employee reads became the first module caller.
//
// WHICH COMPANIES A USER MAY ACT WITHIN, AND THE ONLY PLACE THAT DECIDES IT (FP-006C1, ADR-025 decision 6).
//
// TWO SOURCES OF SCOPE, ONE ANSWER. A tenant administrator's scope is every ACTIVE company in the tenant,
// derived from Platform.Tenant.Administer; everyone else's is their UserCompanyAccess rows intersected with
// the companies that are still Active. Both are resolved here so no caller has to remember which rule
// applies — a read path and a write path disagreeing about that is precisely how a user ends up able to
// write somewhere they cannot see, or vice versa.
//
// IT ALWAYS INTERSECTS WITH ACTIVE COMPANIES. An assignment row naming an Inactive or Archived company is
// not access. The row survives deactivation deliberately, so that reactivating a company restores the access
// that existed before it, and filtering here is what stops that retained row from granting entry meanwhile.
//
// IT GRANTS SCOPE, NEVER OPERATIONS. Holding Platform.Tenant.Administer widens the company set and nothing
// else; which operations are permitted remains the ordinary functional permission check (ADR-025 decision 8).
public interface ITenantCompanyAccessResolver
{
  // Every company this user may currently act within, Active only. Empty is a legitimate answer — it means
  // the user has no company-owned access, and the caller must fail closed rather than fall back to "all".
  // Unlike branches there is no minimum-one invariant: no authority requires a user to hold a company.
  Task<Result<IReadOnlyList<CompanyAccessSummary>>> GetPermittedCompaniesAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default);

  // THE AUTHORITATIVE SINGLE-COMPANY CHECK, re-asked when the request context is established and again at
  // every company-owned write. Deliberately NOT answered from a set captured at login: access can be revoked
  // and a company can be deactivated inside a session's lifetime, and a write admitted on a stale set is the
  // failure this exists to prevent.
  Task<Result> AuthorizeCompanyAsync(
    Guid tenantId,
    long tenantUserId,
    Guid companyId,
    CancellationToken cancellationToken = default);
}

// What a caller needs to render a company picker or auto-select, and nothing more.
public sealed record CompanyAccessSummary(Guid CompanyId, string CompanyCode, string CompanyName);
