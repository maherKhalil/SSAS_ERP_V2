using SSAS.BuildingBlocks.Domain;

namespace SSAS.BuildingBlocks.Application.Abstractions.Tenancy;

// RUNS THE COMPANY VALIDATION FOR THIS REQUEST, ONCE, BEFORE ANY COMPANY-OWNED WORK (FP-006C5, ADR-025
// decision 2).
//
// ---- WHY IT IS SEPARATE FROM ICurrentCompany.
//
// ICurrentCompany REPORTS. It answers "which company has been established", synchronously, and every caller
// in the request may hold it. This interface ACTS: it performs the five-step live validation against the
// tenant database and the access resolver, which is asynchronous and must happen exactly where the request
// pipeline says it happens — not incidentally, the first time somebody reads a property.
//
// Splitting them keeps the many readers from being able to trigger validation, and keeps the one caller that
// should trigger it from needing anything else.
//
// ---- IT IS NOT AUTHORIZATION FOR A WRITE.
//
// Establishing succeeds against live state at the moment it runs. Company access is revocable inside a
// request, so the write boundary re-asks through ICompanyWriteAuthorizer regardless. This exists so a
// company-owned request is refused EARLY and with a precise reason, not so a later check can be skipped.
public interface ICompanyContextEstablisher
{
  Task<Result<Guid>> EstablishAsync(CancellationToken cancellationToken = default);
}
