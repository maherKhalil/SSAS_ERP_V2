using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Api.Authorization;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// WHAT THE GATE DOES WHEN ENTITLEMENT IS REFUSED (FP-014, OD-SUB-0006).
// ==================================================================================================
//
// ---- WHY THIS TEST BUILDS ITS OWN HOST INSTEAD OF USING THE REAL ONE.
//
// **The real host cannot demonstrate a refusal today, and a test pretending otherwise would assert
// nothing.** The only registered resolver is `TransitionalGrantsEveryModuleEntitlement`, which grants every
// module to every tenant because there is no subscription data to answer from. Against the real host, "a
// tenant without the module gets 403" has no tenant that lacks a module — the test would either be
// unwritable or would pass for a reason unrelated to the gate.
//
// So the resolver is the thing varied, on a two-route host built here. That is honest about what is being
// checked: **the seam refuses when entitlement says no, and admits when it says yes.** Whether any real
// tenant is ever refused depends on data this task deliberately does not add.
//
// When the commercial plane's schema lands, the end-to-end version of this test becomes writable against
// the real host — a tenant with no assignment for a module, refused on that module's route. This test does
// not replace that one; it makes the mechanism testable before the data exists.
public sealed class ModuleEnablementGateTests
{
  private const string ModuleKey = "Payroll";

  private static async Task<WebApplication> StartAsync(ITenantModuleEntitlement entitlement)
  {
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseTestServer();
    builder.Services.AddSingleton(entitlement);

    var application = builder.Build();

    // A gated route and an ungated one, on the same host, so the difference observed is the gate and not
    // the host configuration.
    application.MapGet("/gated", () => Results.Ok("reached")).RequireModule(ModuleKey);
    application.MapGet("/ungated", () => Results.Ok("reached"));

    await application.StartAsync();
    return application;
  }

  private sealed class Answers(bool answer) : ITenantModuleEntitlement
  {
    public string? AskedFor { get; private set; }

    public ValueTask<bool> IsEnabledAsync(string moduleKey, CancellationToken cancellationToken)
    {
      AskedFor = moduleKey;
      return ValueTask.FromResult(answer);
    }
  }

  // ---- REFUSED: 403, AND THE HANDLER NEVER RUNS.
  //
  // 403 rather than 404 is `OD-SUB-0006`, and the owner took it knowing the cost: a tenant can enumerate
  // the product surface by probing. That was weighed against support being able to answer "why can't I
  // reach payroll" from the response rather than from server logs, and the answerable one won.
  //
  // "The handler never runs" is the half that matters for correctness — a gate that refused *after* the
  // work would still have done the work.
  [Fact]
  public async Task A_route_of_a_module_the_tenant_does_not_have_is_refused_with_403()
  {
    var entitlement = new Answers(false);
    await using var application = await StartAsync(entitlement);

    var response = await application.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Empty(await response.Content.ReadAsStringAsync());
    Assert.Equal(ModuleKey, entitlement.AskedFor);
  }

  // ---- ENTITLED: THE REQUEST PROCEEDS UNCHANGED.
  //
  // The gate must be invisible to an entitled caller. This is what makes the transitional resolver safe to
  // ship: every tenant is entitled today, so mounting the seam changes no observable behaviour.
  [Fact]
  public async Task A_route_of_a_module_the_tenant_has_is_reached_normally()
  {
    await using var application = await StartAsync(new Answers(true));

    var response = await application.GetTestClient().GetAsync(new Uri("/gated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("reached", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
  }

  // ---- AN UNGATED ROUTE IS NEVER ASKED (REQ-SUB-0013).
  //
  // The platform plane is exempt, and the mechanism by which it is exempt is simply that its groups do not
  // carry the convention. Asserting the resolver is not even CONSULTED — rather than that the request
  // succeeded — is the stronger claim: it holds whatever the resolver would have answered.
  [Fact]
  public async Task An_ungated_route_does_not_consult_entitlement_at_all()
  {
    var entitlement = new Answers(false);
    await using var application = await StartAsync(entitlement);

    var response = await application.GetTestClient().GetAsync(new Uri("/ungated", UriKind.Relative));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Null(entitlement.AskedFor);
  }
}
