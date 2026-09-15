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
// Passing the message as RFC 7807 `detail` fixes that, and it is safe because of the RULE below, not
// because of anything true of today's messages: **`ShowsDetail` is a positive allowlist on 4xx minus
// 401/403.** A 4xx message is addressed to the caller by construction — it tells them what THEY did
// wrong — so **it would be exactly as safe if every message in the product interpolated a runtime
// value.** Nothing here depends on a census of the messages that happen to exist.
//
// ⚠ **WRITTEN AS THE RULE ON PURPOSE.** An earlier version of this paragraph licensed the change with a
// MEASUREMENT — *no message carries a runtime value, zero interpolations across `src/`* — and see the
// note beside `ShowsDetail` for what became of it. **A rule is checkable forever; a measurement is true
// until somebody commits.**
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
  int StatusCode, string Code, bool DetailAllowed = false, string? Detail = null,
  string? Field = null)
{
  // A refusal that is not an authorization decision may always explain itself. Authorization refusals
  // must opt in, one code at a time, with the reason at the declaration.
  // ⚠ AN ALLOWLIST, NOT A BLOCKLIST -- A CLIENT ERROR EXPLAINS ITSELF AND NOTHING ELSE DOES.
  //
  // This began as `not (401 or 403)` and **an existing test found the hole**: `A45_A_real_storage_failure`
  // injects `TenantStorage.Unavailable` — *"no route to the tenant database"* — and asserts the body never
  // says `tenant database`. A 500 sailed straight through the 401/403 check and leaked it.
  //
  // ---- ⚠⚠ THE MEASUREMENT THAT LICENSED THIS IS NOW FALSE, AND IT NEVER CARRIED THE GUARANTEE.
  //
  // The original licence was *no message carries a runtime value — zero interpolations, zero
  // concatenations, zero variables across `src/`.* **It was true when it was written. It is false now:
  // SEVEN interpolated domain messages exist, across three `*Errors.cs` files** — and they are not
  // homogeneous, which matters more than the count.
  //
  // **THREE interpolate an internal status enum** — `PayrollErrors`'s recalculate, approve and post
  // transition messages. ⚠ **FOUR interpolate a CALLER-SUPPLIED IDENTIFIER**: an account code, a pay
  // element code (twice), and a fiscal period name.
  //
  // ⚠ **The population is domain `Error` messages, which is what `Explaining` carries.** Two interpolated
  // `InvalidOperationException` texts also exist in `TenantStorageBootstrapService`; they are outside it
  // because they are startup failures that reach a caller only as a 500, and a 500 shows no detail.
  //
  // **Kept rather than deleted, because it is part of why this design is what it is** — and because the
  // recount is the evidence for the paragraph above. Two of those three are asserted end to end at the
  // wire precisely BECAUSE the value travels, which is the feature working.
  //
  // ⚠ **AND THE MEASUREMENT ANSWERED THE WRONG QUESTION EVEN WHILE IT WAS TRUE.** The risk is not data
  // interpolated into a message, it is a message that describes our own infrastructure — and a
  // hand-written constant does that perfectly well.
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
  // ⚠ TWO PRIMITIVES, AND NEITHER IS OPTIONAL (T-269).
  //
  // It cannot take the domain `Error`: **this project deliberately references nothing** -- no Domain, no
  // Application, no module -- because a single ProjectReference would let module vocabulary leak into
  // every module's transport, and the dependency tests pin that. So the two values cross as primitives.
  //
  // **`field` has no default on purpose.** An optional second parameter would let a mapper carry the
  // message and silently drop the field, which is the failure this whole item exists to prevent -- a
  // caller unable to tell which input was wrong. Every call site has to decide, and `null` is a decision.
  public ApiError Explaining(string? message, string? field) =>
    this with { Detail = message, Field = field };
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
        ["resourceKey"] = resourceKey,

        // ⚠ ONLY PRESENT WHEN THERE IS ONE. An always-present `field: null` would invite a client
        // to bind to it, and most refusals name no single input -- a precondition, a conflict, a server
        // fault. Absent means *mark nothing*, which is different from *mark the field called null*.
        // A JSON PATH into the request body, absent when no single input is at fault. `name`,
        // `assignments[].payElementId`. See `Error.Field` for the full semantics.
        ["field"] = error.Field
      }.Where(entry => entry.Value is not null)
        .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));
  }
}
