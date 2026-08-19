using SSAS.BuildingBlocks.Tenancy.Persistence;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;

namespace SSAS.Platform.Application.Common;

internal static class PersistenceResult
{
  public static async Task<Result> SaveAsync(IPlatformUnitOfWork unitOfWork, CancellationToken cancellationToken)
  {
    var result = await unitOfWork.SaveChangesAsync(cancellationToken);
    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
  }

  // Tenant ERP overload (ADR-017). The two units of work stay separate types rather than sharing an
  // interface, because the plane a handler commits to is exactly the distinction that must remain visible
  // once the Platform and Tenant databases are physically apart and no single transaction spans them.
  public static async Task<Result> SaveAsync(ITenantUnitOfWork unitOfWork, CancellationToken cancellationToken)
  {
    var result = await unitOfWork.SaveChangesAsync(cancellationToken);
    return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
  }
}
