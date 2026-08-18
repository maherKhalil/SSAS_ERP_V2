using SSAS.BuildingBlocks.Domain;

namespace SSAS.Platform.Application.Companies;

// THE COMPANY A CALLER ASKED FOR — INTENT ONLY, NEVER AUTHORITY (FP-006C1, ADR-025 decisions 2 and 4).
//
// FP-006 transmits the selection as the `X-Company-Id` request header. This abstraction is the whole of what
// the server layer above is allowed to contribute: a caller-stated identifier and nothing else.
//
// IT ESTABLISHES NOTHING. The identifier here has not been checked to exist, to belong to the trusted
// tenant, to be Active, or to be reachable by this caller. ICompanyContextResolver performs all four against
// live state before any of it becomes a trusted ICurrentCompany.
//
// THE HEADER IS A TRANSPORT DETAIL, NOT A CONTRACT. Nothing below the web layer knows the name of the
// header, so a later durable session selection (ADR-025 decision 11) replaces the implementation here
// without touching the resolver, the write boundary, or any handler.
//
// WHAT MUST NEVER IMPLEMENT THIS: a JWT `company_id` claim or ICurrentUser.CompanyId. A claim would be a
// client-presentable assertion of scope that survives revocation until the token expired; ADR-025 decision 4
// makes that prohibition binding rather than advisory.
public interface ICompanySelection
{
  // The identifier the caller asked for, or a failure when they supplied something that is not an identifier
  // at all. A syntax failure is distinguishable (InvalidSelectionFormat) because it discloses nothing; every
  // authorization outcome collapses into one generic refusal further down.
  //
  // Success with a null value means "no company was requested", which is legitimate: tenant-global and
  // branch-only work needs no company.
  Result<Guid?> Requested { get; }
}
