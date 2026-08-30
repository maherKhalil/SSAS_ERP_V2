using Microsoft.AspNetCore.Http;
using SSAS.BuildingBlocks.Api.Transport;

namespace SSAS.Platform.API.Transport;

// PLATFORM'S VIEW OF THE SHARED TRANSPORT FAILURES (FP-006C5).
//
// The (status, code) pairs and the RFC 7807 projection are now shared primitives in
// SSAS.BuildingBlocks.Api, because HR needs exactly the same five transport failures and ADR-012 forbids it
// referencing Platform to get them.
//
// WHAT STAYED BEHIND IS THE ONE PLATFORM-SPECIFIC THING: the i18n resource key. It names a Platform
// translation catalogue entry, so it is not module-neutral and did not move — a shared default would have
// labelled HR's failures with Platform's key. It remains the default HERE, which is why every existing
// Platform call site is unchanged and emits exactly the same body as before.
public static class ProblemResults
{
  // Generic platform problem i18n key, matching the existing Platform transports.
  public const string DefaultResourceKey = "platform.authentication.errors.request_rejected";

  // Shared transport failures common to every admin feature. Aliases of the shared definitions rather than
  // copies, so Platform and HR cannot drift to different codes for the same condition.
  public static readonly ApiError RequestInvalid = ApiErrors.RequestInvalid;
  public static readonly ApiError CompanySelectionRequired = ApiErrors.CompanySelectionRequired;
  public static readonly ApiError RowVersionInvalid = ApiErrors.RowVersionInvalid;
  public static readonly ApiError Forbidden = ApiErrors.Forbidden;
  public static readonly ApiError ConcurrencyConflict = ApiErrors.ConcurrencyConflict;
  public static readonly ApiError WriteFailure = ApiErrors.WriteFailure;

  public static IResult Problem(HttpContext context, ApiError error, string resourceKey = DefaultResourceKey) =>
    ApiProblems.Problem(context, error, resourceKey);
}
