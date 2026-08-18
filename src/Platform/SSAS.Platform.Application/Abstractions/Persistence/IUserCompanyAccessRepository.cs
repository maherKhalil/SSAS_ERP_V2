using SSAS.Platform.Domain.Companies;

namespace SSAS.Platform.Application.Abstractions.Persistence;

// The platform-side company assignment rows (FP-006C1, ADR-025 decision 5).
//
// REPLACE-SET SEMANTICS, PHYSICAL, exactly as UserBranchAccess has. Company access is a live capability
// list with no lifecycle columns, so removing access is deleting the row. The audit trail of who could act
// where belongs to the platform audit stream, not to rows that every authorization query would then have to
// filter out.
//
// NO ASSIGNMENT COMMAND SHIPS IN THIS SLICE. FP-006C1 delivers the ownership and authorization
// infrastructure; the administration surface that edits these rows arrives with the operations that need
// it. This repository exists so the resolver's data has one owner and one place it is written.
public interface IUserCompanyAccessRepository
{
  Task<IReadOnlyList<Guid>> GetCompanyIdsAsync(
    Guid tenantId,
    long tenantUserId,
    CancellationToken cancellationToken = default);

  Task AddAsync(UserCompanyAccess access, CancellationToken cancellationToken = default);

  // Removes exactly the named assignments. Called with the difference between the current and desired sets
  // so a replace touches only what actually changed.
  Task RemoveAsync(
    Guid tenantId,
    long tenantUserId,
    IReadOnlyCollection<Guid> companyIds,
    CancellationToken cancellationToken = default);
}
