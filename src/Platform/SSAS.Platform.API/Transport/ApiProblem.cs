using Microsoft.AspNetCore.Http;

namespace SSAS.Platform.API.Transport;

// A transport-neutral (status, code) pair for RFC 7807 ProblemDetails projection.
public sealed record ApiError(int StatusCode, string Code);

// Narrow, reusable ProblemDetails projection preserving the established platform
// extensions (code, correlationId, resourceKey). Shared transport failures live here;
// feature-specific domain-conflict codes live in per-feature mappers.
public static class ProblemResults
{
  // Generic platform problem i18n key, matching the existing Platform transports.
  public const string DefaultResourceKey = "platform.authentication.errors.request_rejected";

  // Shared transport failures common to every admin feature.
  public static readonly ApiError RequestInvalid = new(400, "request.invalid");
  public static readonly ApiError RowVersionInvalid = new(400, "platform.rowversion_invalid");
  public static readonly ApiError Forbidden = new(403, "authorization.forbidden");
  public static readonly ApiError ConcurrencyConflict = new(409, "concurrency.conflict");
  public static readonly ApiError WriteFailure = new(500, "request.failed");

  public static IResult Problem(HttpContext context, ApiError error, string resourceKey = DefaultResourceKey)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(error);
    return Results.Problem(
      type: $"https://httpstatuses.com/{error.StatusCode}",
      statusCode: error.StatusCode,
      title: error.Code,
      extensions: new Dictionary<string, object?>
      {
        ["code"] = error.Code,
        ["correlationId"] = context.Response.Headers["X-Correlation-ID"].ToString(),
        ["resourceKey"] = resourceKey
      });
  }
}
