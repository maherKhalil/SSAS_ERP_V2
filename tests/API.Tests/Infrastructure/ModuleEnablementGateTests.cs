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
// **Because the resolver is the thing being varied, and varying it is the whole experiment.** This host
// mounts one gated route and one ungated route and swaps the `ITenantModuleEntitlement` between them, so
// the difference observed is the seam and nothing else: **it refuses when entitlement says no, and admits
// when it says yes.**
//
// A test comment describes the present, so this one is CORRECTED rather than dated (`DEC-L-039` — the
// migration beside it got the opposite treatment for the opposite reason). It used to say the real host
// could not demonstrate a refusal at all, because the only registered resolver was
// `TransitionalGrantsEveryModuleEntitlement` and it granted every module to every tenant. **That is no
// longer true.** T-040 deleted that type and registered `TenantModuleEntitlement`, which reads real
// subscription data; T-041 seeds the trial that data now consists of.
//
// The end-to-end version this file once anticipated therefore exists: `ExpiredTenantGateTests` and
// `TrialTenantGateTests` run the real resolver and demonstrate an actual refusal. **This test is not
// superseded by them** — they show what the resolver answers, and this shows what the seam does with the
// answer, which stays worth isolating however entitlement comes to be decided.
// ==================================================================================================
// ⚠⚠⚠ AND `AC-SUB-0019` IS SATISFIED **VACUOUSLY** — UNDECLARED, WHICH IS WHAT MAKES IT THE DANGEROUS ONE.
// ==================================================================================================
//
// *"That problem type is IDENTICAL on every gated route of every module. A per-module variant is a failure
// of this criterion even if each variant is individually correct."*
//
// **There is no problem type on any gated route, so all of them are trivially identical.** The criterion is
// met, and met by the absence of the thing it constrains.
//
// ⚠ `TS-SUB-0019` PRESCRIBES THE TEST THAT WOULD MISS THIS: *"The same problem type comes back from a GL
// route, a Payroll route, an Attendance route AND an HR route. Four modules, because a per-module variant
// would pass a single-module test."* **The scenario author anticipated a per-module VARIANT and not a
// per-module ABSENCE — and four empty bodies are identical.** Written as prescribed, that test passes today
// over nothing at all, and would keep passing if the type were added to three modules and forgotten in one,
// only failing once someone made two DIFFERENT types. It is the collective-predicate shape: *the same on
// all four* is true of the empty set.
//
// ⚠⚠ WHY THIS ONE MATTERS MORE THAN THE OTHER VACUOUS CRITERIA IN THIS PACKAGE. `AC-SUB-0008` carries an
// explicit **"satisfied vacuously as at 2026-08-30"** note in its own criterion text, and `AC-SUB-0020`
// carries a correction saying the counts were stale. **Both declare their own weakness. `AC-SUB-0019`
// declares nothing** — it reads as an ordinary consistency requirement, and a reader auditing this package
// would tick it off from the criterion text alone.
//
// **A vacuous criterion that announces itself is a known gap; one that does not is a false green.**
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
  //
  // ⚠ CITES `AC-SUB-0018` FOR TWO OF ITS THREE CLAUSES — *"…refused BEFORE THE HANDLER RUNS, with `403`
  // and problem type `module-not-enabled`."* The `403` is asserted; *before the handler runs* is asserted
  // by `Assert.Empty` on the body, since the handler's only observable act is writing `"reached"`, and by
  // `AskedFor`, which proves the gate was consulted rather than the route merely being unmapped.
  //
  // ⚠⚠⚠ THE THIRD CLAUSE IS NOT MET BY THE PRODUCT, AND **THE ASSERTION THAT PROVES CLAUSE 1 IS THE
  // EVIDENCE FOR CLAUSE 3 FAILING.** `Assert.Empty(body)` says there is no body — so there is no problem
  // document, and therefore no problem type. That is not an artefact of this minimal host:
  //
  //   `ModuleEnablement.cs:117` returns `Results.StatusCode(StatusCodes.Status403Forbidden)` — a BARE
  //   status with no `ProblemDetails`. The real host registers `AddProblemDetails` but no
  //   `UseStatusCodePages`, so nothing converts a bare status result into a document. `ExpiredTenantGateTests`
  //   and `TrialTenantGateTests` run the real host and assert STATUS CODES ONLY — no body assertion exists
  //   on any gate refusal anywhere. And the literal `module-not-enabled` appears **nowhere outside
  //   `docs/`.**
  //
  // ⚠ STATED FAIRLY, BECAUSE THE NARROW READING WOULD BE UNFAIR: this is not one forgotten slug. **No
  // `problems/` type URI exists anywhere in `src/`** — the whole typed-problem convention `api-contracts.md`
  // documents is unbuilt, and `module-not-enabled` is one instance of that, not a singular omission.
  [Fact]
  [Trait("Criterion", "AC-SUB-0018")]
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
