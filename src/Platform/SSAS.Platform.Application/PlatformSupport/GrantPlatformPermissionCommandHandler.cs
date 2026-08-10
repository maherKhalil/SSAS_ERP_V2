using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Enums;

namespace SSAS.Platform.Application.PlatformSupport;

public sealed class GrantPlatformPermissionCommandHandler(
  IPlatformSupportPrincipalRepository principalRepository,
  IPermissionCatalog permissionCatalog,
  IPlatformUnitOfWork unitOfWork,
  ICurrentUser currentUser,
  IDateTimeProvider clock)
{
  public async Task<Result> HandleAsync(GrantPlatformPermissionCommand command, CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure(actor.Error);
    }

    // Scope is resolved from the code-owned catalog, never trusted from the caller. Unknown names and
    // tenant-scoped names are rejected here; the domain re-enforces the PlatformSupport-scope invariant.
    if (!permissionCatalog.TryGet(command.PermissionName, out var permission))
    {
      return Result.Failure(PlatformSupportErrors.UnknownPermission);
    }

    if (permission.Scope != PermissionScope.PlatformSupport)
    {
      return Result.Failure(PlatformSupportErrors.TenantPermissionRejected);
    }

    var principal = await principalRepository.GetByIdAsync(command.PlatformSupportPrincipalId, cancellationToken);
    if (principal is null)
    {
      return Result.Failure(PlatformSupportErrors.PrincipalNotFound);
    }

    var domainResult = principal.GrantPermission(permission, actor.Value, clock.UtcNow);
    if (domainResult.IsFailure)
    {
      return domainResult;
    }

    var saveResult = await unitOfWork.SaveChangesAsync(cancellationToken);
    if (saveResult.IsFailure)
    {
      return saveResult.Error == IdentityAccessErrors.UniqueConstraintViolation
        ? Result.Failure(PlatformSupportErrors.DuplicatePermissionAssignment)
        : Result.Failure(saveResult.Error);
    }

    return Result.Success();
  }
}
