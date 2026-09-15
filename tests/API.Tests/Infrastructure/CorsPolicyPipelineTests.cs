using System.Net.Http.Headers;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// THE CORS POLICY IS APPLIED TO THE PIPELINE, AND IT NAMES EXACT ORIGINS (T-147).
// ==================================================================================================
//
// `app.UseCors(...)` was deleted and the whole API suite stayed green. **The policy itself is built in
// `AddHostAuthenticationTransport`: `.WithOrigins(options.AllowedOrigins).AllowCredentials().AllowAnyMethod()
// .AllowAnyHeader()` — exact origins, credentials permitted.**
//
// ---- ⚠⚠⚠ THIS IS AN AVAILABILITY GUARD, NOT A SECURITY ONE. SAID PLAINLY SO IT IS NOT MISREAD.
//
// ***REMOVING `UseCors` FAILS CLOSED.*** With no CORS middleware no `Access-Control-Allow-Origin` header is
// emitted, so a browser BLOCKS the cross-origin call. **The failure mode is "the SPA stops working", not
// "anyone may call this API".** *A file in this area invites being read as a security guard, and it is not
// one: the security-relevant half is the ORIGIN LIST, which the second test below pins.*
//
// ---- ⚠ WHY THE SECOND TEST IS NOT OPTIONAL.
//
// A policy of `AllowAnyOrigin` would satisfy the first test perfectly. **The disallowed-origin case is what
// makes "exact origins" a measured claim rather than a restatement of the configuration**, and it is the
// half that would fail if somebody loosened the policy while keeping the middleware.
// ---- ⚠⚠ THE SHARED HOST FIXTURE, AND IN THIS SUITE THAT IS A CORRECTNESS REQUIREMENT.
//
// These tests need no service replacement and no extra setting, so they take the collection's host rather
// than booting their own. **A class that boots a second `Program` host concurrently hits Serilog's global
// `Log.Logger`: `System.InvalidOperationException : The logger is already frozen`, from
// `ReloadableLogger.Freeze()`.** *That is what these tests did on their first full-suite run, and it is not a
// port collision — it is process-wide state.* ⚠ **Any test class that must boot its own host needs
// `[Collection(HostIntegrationTestGroup.Name)]` to serialise it; a class that needs no customisation should
// take the fixture instead and boot nothing.**
[Collection(HostIntegrationTestGroup.Name)]
public sealed class CorsPolicyPipelineTests(HostWebApplicationFactory factory)
{
  // From `appsettings.Development.json`, which the test host runs on. ⚠ Read from configuration rather than
  // invented: an origin this file made up would be refused by a CORRECT policy and by a BROKEN one alike.
  private const string AllowedOrigin = "https://localhost:4200";

  private const string ForeignOrigin = "https://attacker.example.test";

  [Fact]
  public async Task A_request_from_a_configured_origin_is_answered_with_cors_headers()
  {
    using var client = factory.CreateClient();

    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/", UriKind.Relative));
    request.Headers.Add("Origin", AllowedOrigin);
    using var response = await client.SendAsync(request);

    Assert.True(
      response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed),
      "the response carries no Access-Control-Allow-Origin header, so a browser will block every " +
      "cross-origin call from the configured front end. The likeliest cause is that app.UseCors is absent " +
      "from the pipeline: the policy can be registered and still never applied.");
    Assert.Equal(AllowedOrigin, allowed.Single());
    Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
  }

  // ---- ⚠⚠ THE COMPANION. `WithOrigins` IS ONE EDIT AWAY FROM `AllowAnyOrigin`, AND CREDENTIALS ARE ALLOWED.
  //
  // A policy that echoed any origin while permitting credentials would let any site issue authenticated
  // requests from a logged-in user's browser. **That is the one genuinely security-relevant property here,
  // and it is invisible to the test above.**
  //
  // ⚠⚠ **AND IT IS VACUOUS ON ITS OWN — MEASURED.** Deleting `app.UseCors` reddens the two positive tests and
  // leaves this one GREEN, because with no CORS middleware NOTHING is granted and "not granted" is trivially
  // true. ***ITS SUBJECT IS THE SHAPE OF A POLICY THE OTHER TWO PROVE IS APPLIED.*** *Neither substitutes for
  // the other, and this one must not be read as evidence that the middleware is installed.*
  [Fact]
  public async Task A_request_from_an_unconfigured_origin_is_not_granted_cors_access()
  {
    using var client = factory.CreateClient();

    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/", UriKind.Relative));
    request.Headers.Add("Origin", ForeignOrigin);
    using var response = await client.SendAsync(request);

    Assert.False(
      response.Headers.Contains("Access-Control-Allow-Origin"),
      $"an origin that is not configured ({ForeignOrigin}) was granted cross-origin access. The policy " +
      "permits credentials, so any site could then issue authenticated requests from a signed-in user's " +
      "browser.");
  }

  // ⚠ A PREFLIGHT IS A DIFFERENT CODE PATH FROM A SIMPLE REQUEST — the middleware short-circuits it and never
  // reaches an endpoint. Asserted separately because a pipeline that answered simple requests correctly and
  // dropped preflights would break every non-GET call from the browser while both tests above passed.
  [Fact]
  public async Task A_preflight_from_a_configured_origin_is_answered_without_reaching_an_endpoint()
  {
    using var client = factory.CreateClient();

    using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/", UriKind.Relative));
    request.Headers.Add("Origin", AllowedOrigin);
    request.Headers.Add("Access-Control-Request-Method", "POST");
    using var response = await client.SendAsync(request);

    Assert.True(
      response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed),
      "the preflight was not answered with CORS headers, so every non-simple cross-origin request will fail.");
    Assert.Equal(AllowedOrigin, allowed.Single());
  }
}
