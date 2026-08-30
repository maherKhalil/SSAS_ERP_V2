using Microsoft.AspNetCore.Http;

namespace SSAS.BuildingBlocks.Api.Transport;

// A transport-neutral (status, code) pair for RFC 7807 ProblemDetails projection.
public sealed record ApiError(int StatusCode, string Code);

// ==================================================================================================
// THE GENERIC TRANSPORT FAILURES EVERY MODULE'S HTTP SURFACE NEEDS (FP-006C5).
// ==================================================================================================
//
// These are TRANSPORT concepts, not business ones: a body that would not parse, a rowversion that is not a
// rowversion, an authenticated caller who may not do this, a stale write, a failure the server owns. Every
// module hits all five, and none of them says anything about companies, branches, employees or tenants.
//
// ---- WHAT MUST NEVER BE ADDED HERE.
//
// Business error codes. "employee.not_found", "company.code_conflict" and their kind belong to the module
// that owns the concept, mapped by that module's own error mapper. A shared business-error catalogue would
// make every module's vocabulary a dependency of every other module's — the coupling this project exists to
// avoid — and is pinned against by the architecture tests.
public static class ApiErrors
{
  public static readonly ApiError RequestInvalid = new(400, "request.invalid");

  // ⚠ THREE CODES THAT USED TO BE `request.invalid`, AND THE REASON IS THE CALLER'S NEXT MOVE (T-260).
  //
  // 129 distinct domain codes map to `request.invalid`. That is defensible for most of them -- a
  // malformed body is a malformed body -- but **paging is the one place a caller retries in a loop**,
  // and one code for three conditions means a client that fixes the wrong parameter fails identically.
  //
  // **The code is the whole channel.** The problem document carries `code`, `correlationId` and
  // `resourceKey` and NO message field, so `Error.Message` never reaches the caller. Splitting the
  // domain code alone would have been invisible; these are what make the distinction observable.
  public static readonly ApiError PageNumberInvalid = new(400, "request.page_number_invalid");

  public static readonly ApiError PageSizeInvalid = new(400, "request.page_size_invalid");

  // Not a page of anything: a cap on the TOTAL rows an export returns. It shared the pagination code
  // until T-260, which sent a caller to look at a parameter it had not supplied.
  public static readonly ApiError ExportCeilingInvalid = new(400, "request.export_ceiling_invalid");

  public static readonly ApiError RowVersionInvalid = new(400, "platform.rowversion_invalid");

  public static readonly ApiError Forbidden = new(403, "authorization.forbidden");

  public static readonly ApiError ConcurrencyConflict = new(409, "concurrency.conflict");

  public static readonly ApiError WriteFailure = new(500, "request.failed");
}

// The RFC 7807 projection, preserving the established platform extensions (code, correlationId,
// resourceKey).
//
// THE RESOURCE KEY IS REQUIRED, deliberately. It is the caller-facing i18n key, and which one is right
// depends on the module answering — a shared default would silently label every module's failures with one
// module's key. Each API surface supplies its own.
public static class ApiProblems
{
  public static IResult Problem(HttpContext context, ApiError error, string resourceKey)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(error);
    ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

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
