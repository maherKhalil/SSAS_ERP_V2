using SSAS.BuildingBlocks.Api.Transport;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// A REFUSAL EXPLAINS ITSELF, EXCEPT WHERE THE EXPLANATION IS AN ORACLE (T-261).
// ==================================================================================================
//
// ---- WHAT THIS REPLACED.
//
// `Error.Message` never reached a caller. The problem document carried `code`, `correlationId` and
// `resourceKey` and nothing else, so **every message in the product was documentation for whoever read
// the constant.** 129 distinct domain codes collapse into `request.invalid` alone — a caller seeing it
// could not tell a bad page size from an unknown property from a stale row version.
//
// It is safe to show because **no message carries a runtime value**: measured across `src/`, zero
// interpolations, zero concatenations, zero variables. Every one was written by hand into a constant, so
// there is nothing in a message that a leak could expose.
//
// ---- ⚠ AND THE EXCEPTION IS WHY THIS FILE EXISTS RATHER THAN A ONE-LINE CHANGE.
//
// `branch.scope_denied` has **nine** domain codes behind it with nine different messages — *"the branch
// was not found"*, *"the branch is not active"*, *"the selected branch is not available to this user"*.
// **Showing those separates a branch that does not exist from one that exists and is forbidden**, which is
// a scope-enumeration oracle over the tenant's structure. The single 403 is the thing that prevents it.
//
// So 401 and 403 **fail closed**: no detail unless that code opted in. The default is the safe one because
// of the code nobody has written yet — a new 403 added later would otherwise ship detail without anyone
// deciding to, **and a leaked oracle looks exactly like a helpful error message.**
public sealed class ProblemDetailVisibilityTests
{
  [Fact]
  public void An_ordinary_refusal_explains_itself()
  {
    var explained = new ApiError(400, "request.invalid").Explaining("The page size is out of range.");

    Assert.True(explained.ShowsDetail);
    Assert.Equal("The page size is out of range.", explained.VisibleDetail);
  }

  // ⚠ THE LOAD-BEARING HALF. `Explaining` is called by every mapper on every error, so the suppression has
  // to happen at the point of display rather than at the point of attachment — a 403 arrives here carrying
  // its message and must not surrender it.
  [Theory]
  [InlineData(401)]
  [InlineData(403)]
  public void An_authorization_refusal_withholds_its_detail_unless_it_opted_in(int status)
  {
    var closed = new ApiError(status, "branch.scope_denied").Explaining("The branch was not found.");

    Assert.False(closed.ShowsDetail);
    Assert.Null(closed.VisibleDetail);

    var opened = new ApiError(status, "authorization.forbidden", DetailAllowed: true)
      .Explaining("A trusted branch context is required.");

    Assert.True(opened.ShowsDetail);
    Assert.Equal("A trusted branch context is required.", opened.VisibleDetail);
  }

  // ⚠ THE CLASS THIS RULE ORIGINALLY MISSED, AND HOW IT WAS FOUND.
  //
  // The first rule was `not (401 or 403)`, licensed by a measurement that **no message carries a runtime
  // value** — true, and the wrong question. `A45_A_real_storage_failure_is_not_mapped_to_an_authorization
  // _refusal` injects *"no route to the tenant database"* and asserts the body never says `tenant
  // database`; a 500 went through the authorization check untouched and leaked it. **The danger was never
  // interpolated data. It was a hand-written constant that describes our own infrastructure.**
  //
  // A 4xx is addressed to the caller. A 5xx is addressed to an operator, who already has the log and the
  // correlation id — the response body is not its delivery route.
  [Theory]
  [InlineData(500)]
  [InlineData(502)]  // never declared today; the point is that it is closed WITHOUT being declared
  [InlineData(503)]
  public void A_server_fault_never_surrenders_its_message(int status)
  {
    var fault = new ApiError(status, "request.failed")
      .Explaining("no route to the tenant database");

    Assert.False(fault.ShowsDetail);
    Assert.Null(fault.VisibleDetail);
  }

  // ⚠ THE DEFAULT IS THE WHOLE SAFETY ARGUMENT, SO IT IS ASSERTED RATHER THAN ASSUMED. A future 403
  // declared without thinking about detail must get the safe behaviour.
  [Fact]
  public void A_new_authorization_code_declared_without_thought_is_closed_by_default()
  {
    var accidental = new ApiError(403, "something.new").Explaining("Which of several reasons it was.");

    Assert.False(accidental.ShowsDetail);
  }

  // Every 401/403 the product actually declares, and whether it opted in. Five when written, and only
  // `branch.scope_denied` had more than one message behind it — so failing closed cost nothing.
  [Fact]
  public void No_declared_authorization_code_has_opted_in_without_being_recorded_here()
  {
    string[] optedIn = [];

    // ⚠ THE MODULE ASSEMBLIES, NOT `ApiError`'s OWN. The 401/403 codes are declared in each module's
    // mapper, not in BuildingBlocks -- scoping to the declaring assembly found ONE, and the floor below
    // is what said so rather than letting 'none opted in' pass over a population of one.
    var declared = System.IO.Directory
      .EnumerateFiles(AppContext.BaseDirectory, "SSAS.*.API.dll")
      .Select(System.IO.Path.GetFileNameWithoutExtension)
      .Distinct(StringComparer.Ordinal)
      .Select(name => System.Reflection.Assembly.Load(name!))
      .SelectMany(assembly => assembly.GetTypes())
      .SelectMany(type => type.GetFields(
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
      .Where(field => field.FieldType == typeof(ApiError))
      .Select(field => (ApiError)field.GetValue(null)!)
      .Where(error => !error.ShowsDetail || error.DetailAllowed)
      .ToArray();

    // ⚠ THE FLOOR READS THE SAME QUANTITY THE ASSERTION BELOW READS -- it counts codes that FAIL CLOSED,
    // which is what `unexpected` filters. A floor on 'assemblies loaded' or 'fields found' would survive
    // the day `ShowsDetail` starts returning true for everything, and that is the day this must fail.
    Assert.True(declared.Length >= 5,
      $"only {declared.Length} fail-closed codes were discovered; 8 exist (5 authorization, 3 server), so " +
      "the walk has degraded and 'none opted in' would mean nothing.");

    var unexpected = declared
      .Where(error => error.DetailAllowed)
      .Select(error => error.Code)
      .Except(optedIn, StringComparer.Ordinal)
      .ToArray();

    Assert.True(unexpected.Length == 0,
      "a fail-closed code (401/403 or 5xx) exposes its detail and is not recorded as deliberate:\n  " +
      string.Join("\n  ", unexpected) +
      "\n\nAdd it to `optedIn` with the reason it is safe, or remove `DetailAllowed`.");
  }
}
