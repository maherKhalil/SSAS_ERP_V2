using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// HTTPS REDIRECTION APPLIES EVERYWHERE EXCEPT THE TWO AUTHENTICATION PREFIXES (T-145).
// ==================================================================================================
//
// `Program.cs` wraps `UseHttpsRedirection` in a `UseWhen` whose predicate excludes `/api/platform/auth` and
// `/api/platform/support/auth`. **Deleting that block removes the redirect AND the exclusion together, and
// nothing noticed**: it was planted and the whole API suite stayed green.
//
// ---- ⚠⚠⚠ WHY NOTHING NOTICED, WHICH IS ALSO WHY THIS FILE NEEDS ITS OWN FACTORY.
//
// ***`UseHttpsRedirection` IS INERT UNDER `TestServer` UNLESS AN HTTPS PORT IS CONFIGURED.*** Measured, both
// ways, before this file was written:
//
//     no `https_port`      `/` => 200, no Location header          — the middleware cannot redirect
//     `https_port=443`     `/` => 307, Location: https://localhost/
//
// **So every existing test runs against a pipeline in which this middleware does nothing at all** — the
// shared `HostWebApplicationFactory` sets no port, and that is the whole reason deleting the block was
// invisible. *The factories below supply the port, which is the deployment setting production has and the
// test host lacked; they change no product code.*
//
// ---- ⚠⚠ THE EXCLUSION IS THE HALF NOTHING ASSERTED, AND ITS REASON IS NOT RECORDED.
//
// **`Program.cs` carries NO comment on the predicate.** *Why the two authentication prefixes are exempt is
// not written down anywhere I could find, so it is not restated here — a reason invented by a test would be
// worse than an absent one, because it would read as the decision.* ⚠ **What IS asserted is that the
// exclusion exists and behaves**, so that someone tidying the predicate away has to argue with a red test
// rather than with silence.
//
// ---- ⚠ THE LIMIT.
//
// **This proves the redirect and the exemption under a configured HTTPS port. It says nothing about what a
// real deployment does** — that depends on the load balancer, on `ConfigureTrustedForwarding`, and on the
// port the host is actually given. *Two of those are outside this process.*
[Collection(HostIntegrationTestGroup.Name)]
public sealed class HttpsRedirectionExclusionTests
{
  // A real route, not a fabricated one: `/api/platform/auth/login` exists (`AuthenticationEndpointRouteBuilderExtensions:21`).
  // ⚠ A NON-EXISTENT path under the prefix would answer 404 whether or not the exclusion worked, and a 404
  // is indistinguishable from "not redirected" at a glance — so the witness has to be a path that would
  // otherwise produce something else.
  private const string ExcludedPath = "/api/platform/auth/login";

  private const string SupportExcludedPath = "/api/platform/support/auth/login";

  [Fact]
  public async Task A_plain_http_request_outside_the_authentication_prefixes_is_redirected_to_https()
  {
    using var factory = new HttpsPortFactory();
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.GetAsync(new Uri("/", UriKind.Relative));

    Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    Assert.Equal("https", response.Headers.Location?.Scheme);
  }

  // ---- ⚠⚠⚠ THE HALF NOTHING ASSERTED. A REDIRECT HERE WOULD BE A BEHAVIOUR CHANGE NOBODY ORDERED.
  //
  // Both prefixes are checked, not one: the predicate has two clauses and a single-path test would pass with
  // either clause deleted.
  [Theory]
  [InlineData(ExcludedPath)]
  [InlineData(SupportExcludedPath)]
  public async Task A_plain_http_request_to_an_authentication_prefix_is_not_redirected(string path)
  {
    using var factory = new HttpsPortFactory();
    using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.PostAsync(new Uri(path, UriKind.Relative), new StringContent(string.Empty));

    // The endpoint's own answer is not the subject and must not be asserted — it changes when the contract
    // changes. What matters is that the request REACHED an endpoint rather than being turned round by the
    // redirect middleware, and `307` with an `https` Location is the only thing that would mean otherwise.
    Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    Assert.Null(response.Headers.Location);
  }

  // Supplies the HTTPS port the middleware needs, which `TestServer` does not provide and a deployment does.
  // Nothing else is altered: the pipeline, the environment and every registration are the product's.
  private sealed class HttpsPortFactory : WebApplicationFactory<global::Program>
  {
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      builder.UseEnvironment("Development");
      builder.UseSetting("https_port", "443");
    }
  }
}
