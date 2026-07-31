using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Common;

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

    IReadOnlyCollection<PermissionDto> permissions = permissionCatalog.All
      .OrderBy(permission => permission.Name.Value, StringComparer.Ordinal)
      .Select(permission => new PermissionDto(permission.Name.Value, permission.Scope, permission.Description))
      .ToArray();
    return Task.FromResult(Result.Success(permissions));
  }
}
