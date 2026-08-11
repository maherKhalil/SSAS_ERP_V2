using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Permissions;

public sealed class ListPermissionCatalogQueryHandler(
  IPermissionCatalog permissionCatalog,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public Task<Result<IReadOnlyCollection<PermissionDto>>> HandleAsync(
    ListPermissionCatalogQuery query,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Task.FromResult(Result.Failure<IReadOnlyCollection<PermissionDto>>(execution.Error));
    }

    // Tenant-plane query (GetTenantActor): expose only tenant-assignable permissions.
    // PlatformSupport-scoped permissions (ADR-015) are never tenant-assignable and must not appear here.
    IReadOnlyCollection<PermissionDto> permissions = permissionCatalog.All
      .Where(permission => permission.Scope == PermissionScope.Tenant)
      .OrderBy(permission => permission.Name.Value, StringComparer.Ordinal)
      .Select(permission => new PermissionDto(permission.Name.Value, permission.Scope, permission.Description))
      .ToArray();
    return Task.FromResult(Result.Success(permissions));
  }
}
