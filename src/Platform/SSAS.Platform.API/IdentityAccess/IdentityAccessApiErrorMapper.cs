using SSAS.BuildingBlocks.Api.Transport;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.API.Transport;

namespace SSAS.Platform.API.IdentityAccess;

// Feature-specific mapping of Identity/Access domain error codes to transport ApiError.
// Shared transport failures (request.invalid, concurrency.conflict, ...) come from ProblemResults;
// this mapper adds only the IAM-specific translations. More entries are added as mutating
// routes land in later phases.
public static class IdentityAccessApiErrorMapper
{
  public static ApiError Map(Error error)
  {
    ArgumentNullException.ThrowIfNull(error);
    return error.Code switch
    {
      "Pagination.Invalid" => ProblemResults.RequestInvalid,
      "Persistence.ConcurrencyConflict" => ProblemResults.ConcurrencyConflict,
      "Persistence.WriteFailure" => ProblemResults.WriteFailure,
      _ => ProblemResults.RequestInvalid
    };
  }
}
