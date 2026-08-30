using Microsoft.AspNetCore.Http;

namespace SSAS.BuildingBlocks.Api.Transport;

// ==================================================================================================
// A TRANSPORT-NEUTRAL (STATUS, CODE) PAIR, AND WHETHER ITS DETAIL MAY BE SHOWN (T-261).
// ==================================================================================================
//
// ---- WHY `DetailAllowed` EXISTS AND WHY IT DEFAULTS TO FALSE ONLY WHERE IT MATTERS.
//
// `Error.Message` never reached a caller: the problem document carried `code`, `correlationId` and
// `resourceKey` and nothing else, so **every message in the product was documentation for the developer
// reading the constant.** 129 distinct domain codes collapse into `request.invalid` alone, and a caller
// seeing it could not tell a bad page size from an unknown property.
//
// Passing the message as RFC 7807 `detail` fixes that, and it is safe because **no message carries a
// runtime value** — measured across `src/`: zero interpolations, zero concatenations, zero variables.
// There is nothing in a message that was not written by hand into a constant.
//
// ---- ⚠ EXCEPT ON 401 AND 403, WHERE IT FAILS CLOSED.
//
// `branch.scope_denied` has NINE domain codes behind it with nine different messages — *"the branch was
// not found"*, *"the branch is not active"*, *"not available to this user"*. **Showing those lets a
// caller separate a branch that does not exist from one that exists and is forbidden**, which is a
// scope-enumeration oracle over the tenant's structure. The single 403 is what prevents it.
//
// **So an authorization refusal shows no detail unless a code opts in**, and the default is the safe one
// because of the code nobody has written yet: a new 403 added later by someone who never read this
// would otherwise ship detail by default, and a leaked oracle looks exactly like a helpful message.
// Only a deliberate act exposes anything.
//
// Measured when this was written: five 401/403 codes exist, and only `branch.scope_denied` has more
// than one message behind it — so failing closed for all of them costs nothing today.
public sealed record ApiError(
  int StatusCode, string Code, bool DetailAllowed = false, string? Detail = null)
{
  // A refusal that is not an authorization decision may always explain itself. Authorization refusals
  // must opt in, one code at a time, with the reason at the declaration.
  // ⚠ AN ALLOWLIST, NOT A BLOCKLIST -- A CLIENT ERROR EXPLAINS ITSELF AND NOTHING ELSE DOES.
  //
  // This began as `not (401 or 403)` and **an existing test found the hole**: `A45_A_real_storage_failure`
  // injects `TenantStorage.Unavailable` — *"no route to the tenant database"* — and asserts the body never
  // says `tenant database`. A 500 sailed straight through the 401/403 check and leaked it.
  //
  // The safety measurement that licensed this change was *no message carries a runtime value*: true, and
  // **it answered the wrong question.** The risk is not data interpolated into a message, it is a message
  // that describes our own infrastructure — and a hand-written constant does that perfectly well.
  //
  // So the classes divide by **who the message is for**. A 4xx tells callers what THEY did wrong, and the
  // message is addressed to them. A 5xx says something broke on OUR side: that message is for an operator,
  // and it already reaches one through the log and the correlation id. The response is not its route.
  //
  // Written as a positive test on 4xx so that **a status class nobody has thought about yet fails closed**
  // — the blocklist form would have admitted 502 and 504 the same way it admitted 500.
  public bool ShowsDetail =>
    (StatusCode is >= 400 and < 500 and not (401 or 403)) || DetailAllowed;

  // The domain message, attached by the mapper that still had it, and shown only where allowed.
  public string? VisibleDetail => ShowsDetail ? Detail : null;

  // Used by every mapper: keep the code and status, carry this refusal's own message.
  public ApiError Explaining(string? message) => this with { Detail = message };
}

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
  // The detail travels ON the ApiError, attached by the mapper that still had the domain message --
  // 96 call sites hand a mapped ApiError straight to this method and never see the original.
  public static IResult Problem(HttpContext context, ApiError error, string resourceKey)
  {
    ArgumentNullException.ThrowIfNull(context);
    ArgumentNullException.ThrowIfNull(error);
    ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

    return Results.Problem(
      type: $"https://httpstatuses.com/{error.StatusCode}",
      statusCode: error.StatusCode,
      title: error.Code,
      detail: error.VisibleDetail,
      extensions: new Dictionary<string, object?>
      {
        ["code"] = error.Code,
        ["correlationId"] = context.Response.Headers["X-Correlation-ID"].ToString(),
        ["resourceKey"] = resourceKey
      });
  }
}
