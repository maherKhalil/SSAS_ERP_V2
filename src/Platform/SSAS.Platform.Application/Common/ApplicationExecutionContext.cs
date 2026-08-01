using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Domain;

namespace SSAS.Platform.Application.Common;

internal static class ApplicationExecutionContext
{
  public static Result<string> GetPlatformActor(ICurrentUser currentUser)
  {
    return string.IsNullOrWhiteSpace(currentUser.UserId)
      ? Result.Failure<string>(TenantLifecycleErrors.Unauthorized)
      : Result.Success(currentUser.UserId);
  }

  public static Result<(Guid TenantId, string Actor)> GetTenantActor(ICurrentTenant currentTenant, ICurrentUser currentUser)
  {
    return currentTenant.TenantId is not { } tenantId || string.IsNullOrWhiteSpace(currentUser.UserId)
      ? Result.Failure<(Guid, string)>(IdentityAccessErrors.Unauthorized)
      : Result.Success((tenantId, currentUser.UserId));
  }

  public static bool MatchesExpectedVersion(byte[] current, byte[]? expected) =>
    expected is { Length: > 0 } && current.AsSpan().SequenceEqual(expected);
}
