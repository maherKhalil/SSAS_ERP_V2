using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Roles;

public sealed class GetRoleByIdQueryHandler(
  IRoleReadService readService,
  ICurrentTenant currentTenant,
  ICurrentUser currentUser)
{
  public async Task<Result<RoleDto>> HandleAsync(GetRoleByIdQuery query, CancellationToken cancellationToken = default)
  {
    var execution = ApplicationExecutionContext.GetTenantActor(currentTenant, currentUser);
    if (execution.IsFailure)
    {
      return Result.Failure<RoleDto>(execution.Error);
    }

    var role = await readService.GetByIdAsync(query.RoleId, cancellationToken);
    return role is null ? Result.Failure<RoleDto>(IdentityAccessErrors.NotFound) : Result.Success(role);
  }
}
