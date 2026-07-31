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
}
