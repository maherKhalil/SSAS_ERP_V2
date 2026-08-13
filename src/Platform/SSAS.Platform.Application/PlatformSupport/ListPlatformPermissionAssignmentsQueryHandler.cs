using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Common;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.PlatformSupport;

// Full permission-assignment history (active + revoked) for one principal (DEC-TEN-0025, Phase 4C). A missing
// principal returns not-found; an existing principal with no assignments returns an empty history. History is
// never filtered by the current catalog — retired-permission rows remain visible as persisted evidence.
public sealed class ListPlatformPermissionAssignmentsQueryHandler(
  IPlatformSupportAuthorityReadService readService,
  ICurrentUser currentUser)
{
  public async Task<Result<IReadOnlyList<PlatformPermissionAssignmentDto>>> HandleAsync(
    ListPlatformPermissionAssignmentsQuery query,
    CancellationToken cancellationToken = default)
  {
    var actor = ApplicationExecutionContext.GetPlatformActor(currentUser);
    if (actor.IsFailure)
    {
      return Result.Failure<IReadOnlyList<PlatformPermissionAssignmentDto>>(actor.Error);
    }

    var assignments = await readService.ListAssignmentsAsync(query.PlatformSupportPrincipalId, cancellationToken);
    return assignments is null
      ? Result.Failure<IReadOnlyList<PlatformPermissionAssignmentDto>>(PlatformSupportErrors.PrincipalNotFound)
      : Result.Success(assignments);
  }
}
