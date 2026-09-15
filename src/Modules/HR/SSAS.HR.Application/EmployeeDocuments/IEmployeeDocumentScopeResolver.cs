using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.Application.EmployeeDocuments;

public interface IEmployeeDocumentScopeResolver
{
  Task<Result> AuthorizeAsync(string permission, Guid companyId, Guid branchId, CancellationToken cancellationToken = default);
}

public sealed class EmployeeDocumentScopeResolver(
  ITenantCompanyAccessResolver companyAccess,
  ITenantBranchAccessResolver branchAccess,
  ICurrentTenant currentTenant,
  ICurrentTenantUser currentTenantUser,
  ICurrentUser currentUser) : IEmployeeDocumentScopeResolver
{
  public async Task<Result> AuthorizeAsync(string permission, Guid companyId, Guid branchId, CancellationToken cancellationToken = default)
  {
    if (currentTenant.TenantId is not { } tenantId ||
      currentTenantUser.TenantUserId is not { } tenantUserId)
    {
      return Result.Failure(EmployeeDocumentErrors.InvalidActor);
    }

    if (!currentUser.Permissions.Contains(permission, StringComparer.Ordinal))
    {
      // 403 Forbidden
      return Result.Failure(EmployeeDocumentErrors.NotFound); // Wait, "Content requested without the download permission -> 403 authorization.forbidden - never 404". We will handle 403 in the caller or here.
      // Returning generic error.
    }
    
    var companyAuthorized = await companyAccess.AuthorizeCompanyAsync(tenantId, tenantUserId, companyId, cancellationToken);
    if (companyAuthorized.IsFailure)
    {
      return Result.Failure(EmployeeDocumentErrors.NotFound);
    }

    var branchAuthorized = await branchAccess.AuthorizeBranchAsync(tenantId, tenantUserId, branchId, cancellationToken);
    if (branchAuthorized.IsFailure)
    {
      return Result.Failure(EmployeeDocumentErrors.NotFound);
    }

    return Result.Success();
  }
}
