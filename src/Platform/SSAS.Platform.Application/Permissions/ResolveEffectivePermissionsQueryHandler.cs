using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.Permissions;

public sealed class ResolveEffectivePermissionsQueryHandler(
  ITenantUserRepository tenantUserRepository,
  IRoleRepository roleRepository,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<IReadOnlyCollection<string>>> HandleAsync(
    ResolveEffectivePermissionsQuery query,
    CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<IReadOnlyCollection<string>>(execution.Error);
    }

    var tenantUser = await tenantUserRepository.GetByIdAsync(query.TenantUserId, cancellationToken);
    if (tenantUser is null)
    {
      return Result.Failure<IReadOnlyCollection<string>>(IdentityAccessErrors.NotFound);
    }

    if (tenantUser.Status != TenantUserStatus.Active)
    {
      return Result.Success<IReadOnlyCollection<string>>([]);
    }

    var roleIds = tenantUser.ActiveRoleIds.Distinct().ToArray();
    var roles = await roleRepository.GetByIdsAsync(roleIds, cancellationToken);
    IReadOnlyCollection<string> permissions = roles
      .SelectMany(role => role.ActivePermissions)
      .Select(permission => permission.Value)
      .Distinct(StringComparer.Ordinal)
      .OrderBy(permission => permission, StringComparer.Ordinal)
      .ToArray();
    return Result.Success(permissions);
  }
}
