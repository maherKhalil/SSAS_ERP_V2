using System.Reflection;
using SSAS.Host.API.Authorization;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Application.Authentication;

namespace SSAS.Architecture.Tests;

// Phase 4B durable invariants (DEC-TEN-0023). The platform-support authentication HTTP surface is structurally
// separate from the tenant surface, its current-session logout is bound only from the trusted access token, and
// it does NOT pull in the Phase 4E plane-authentication policy taxonomy (RequirePlatformAuthenticatedUser / D2).
public sealed class PlatformSupportAuthenticationSurfaceArchitectureTests
{
  private const string TenantAuthPrefix = "/api/platform/auth";

  [Fact]
  public void Platform_support_auth_surface_is_structurally_separate_from_the_tenant_surface()
  {
    // Distinct route prefix — neither is a segment-prefix of the other, so the two planes never share a route.
    Assert.Equal("/api/platform/support/auth", PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RoutePrefix);
    Assert.NotEqual(TenantAuthPrefix, PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RoutePrefix);
    Assert.False(PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RoutePrefix
      .StartsWith(TenantAuthPrefix + "/", StringComparison.Ordinal));

    // Distinct HttpOnly refresh cookie name — a tenant refresh cookie is never presentable to the platform surface
    // (different name AND different path scope), and vice versa.
    Assert.NotEqual(
      AuthenticationEndpointRouteBuilderExtensions.RefreshCookieName,
      PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName);
    Assert.Equal("__Secure-ssas-platform-refresh", PlatformSupportAuthenticationEndpointRouteBuilderExtensions.RefreshCookieName);

    // The map extension exists (the surface is wired, not latent).
    Assert.NotNull(typeof(PlatformSupportAuthenticationEndpointRouteBuilderExtensions)
      .GetMethod("MapPlatformSupportAuthenticationEndpoints", BindingFlags.Public | BindingFlags.Static));
  }

  [Fact]
  public void Platform_logout_command_binds_only_trusted_token_claims()
  {
    // Both fields are the trusted session_id + identity_id claims; there is no request-body/secret-shaped field a
    // caller could use to target another session. The transport binds them from the validated token, never the body.
    var properties = typeof(RevokeCurrentPlatformAuthenticationSessionCommand).GetProperties();

    Assert.Equal(2, properties.Length);
    Assert.All(properties, property => Assert.Equal(typeof(long), property.PropertyType));
    Assert.Contains(properties, property => property.Name == "PlatformAuthenticationSessionId");
    Assert.Contains(properties, property => property.Name == "IdentityId");
    Assert.DoesNotContain(properties, property =>
      property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
      property.Name.Contains("Principal", StringComparison.OrdinalIgnoreCase));
  }

  // ==================================================================================================
  // ⚠⚠⚠ TWO OF `AC-TEN-0082`'s CLAUSES ARE ENFORCED BY WHAT THIS HANDLER CANNOT REACH, AND UNTIL NOW
  // NOTHING WATCHED THAT.
  // ==================================================================================================
  //
  // *"`AuthenticationAccount.SecurityVersion` is unchanged"* and *"the already-issued access JWT remains valid
  // until natural expiry"* are not behaviours the handler performs — **they are behaviours it is INCAPABLE of.**
  // It takes a session repository, a unit of work and a clock. *It cannot touch an account because it cannot
  // reach one; it cannot invalidate a token because it has no token service.*
  //
  // ⚠⚠ SO NEITHER CLAUSE HAS A FIXTURE, AND NEITHER CAN: ***you cannot break the absence of a line, and adding
  // the dependency IS the change the clause forbids.*** **The guarantee lives in the constructor, so the
  // constructor is what has to be guarded.**
  //
  // ⚠⚠⚠ AND THE FAILURE IS EXPRESSIBLE AND TEMPTING, WHICH IS WHAT EARNS A GUARD RATHER THAN A COMMENT.
  // ***"LOGOUT SHOULD INVALIDATE THE TOKEN" IS THE MOST OBVIOUS THING A COMPETENT PERSON WOULD THINK, AND
  // INJECTING A TOKEN SERVICE IS EXACTLY HOW THEY WOULD DO IT*** — which is precisely what clause 6 forbids,
  // for a deliberate reason: the access JWT is short-lived and stateless, and revoking it early would require
  // exactly the shared mutable state (`SecurityVersion`) that clause 5 protects the TENANT plane from.
  //
  // ⚠ AN EXACT LIST, NOT A BAN, SO IT IS A BIND RATHER THAN A FLOOR: **an ADDED dependency reddens this as
  // loudly as a removed one.** *A `DoesNotContain("IToken…")` would pass on any dependency nobody thought to
  // name, which is every dependency that has not been invented yet.*
  [Fact]
  [Trait("Criterion", "AC-TEN-0082")]
  public void The_platform_logout_handler_can_reach_no_account_and_no_token_service()
  {
    var constructor = Assert.Single(
      typeof(RevokeCurrentPlatformAuthenticationSessionCommandHandler).GetConstructors());

    Assert.Equal(
      ["IPlatformAuthenticationSessionRepository", "IPlatformUnitOfWork", "IDateTimeProvider"],
      constructor.GetParameters().Select(parameter => parameter.ParameterType.Name));
  }

  // ⚠⚠⚠ THIS GUARD IS THE EVIDENCE THAT `AC-TEN-0084`, `0085` AND `0086` ARE **DEFERRED, NOT UNCOVERED**,
  // AND IT CORRECTS A PARTITION I PUBLISHED. I classified `0021`-`0091` by cluster and put `0084`-`0091` in
  // the LIVE bucket on the strength of routes and policies existing. **The Phase-4E plane-authentication
  // POLICY TAXONOMY does not exist**, and this test is what says so: no type named `PlatformAuthenticatedUser`
  // is present in Host.API or Platform.API, and the platform logout route is secured by a narrow inline
  // `security_plane=platform` check rather than by the deferred policy infrastructure (`DEC-TEN-0024`).
  //
  //   `AC-TEN-0084` *"tenant-authenticated, PLATFORM-AUTHENTICATED … and plane-neutral policies EXIST"*
  //   `AC-TEN-0085` *"an architecture guard rejects a plane-specific endpoint using a bare
  //                  `RequireAuthenticatedUser`"* — needs the taxonomy to have something to reject
  //   `AC-TEN-0086` *"…require the TENANT-AUTHENTICATED POLICY"* — names a policy that is not built
  //
  // **All three name the taxonomy this guard proves absent**, so their subjects are Phase 4E. My partition's
  // deferred bucket was 6 and is 9. ⚠ I flagged this risk when I published it — *"I classified at CLUSTER
  // granularity and did not open all 62 individually"* — and this is that risk landing.
  //
  // ⚠⚠ `AC-TEN-0085` IS ALSO THE ONLY CRITERION IN THE PACKAGE WHOSE SUBJECT IS A TEST. It does not describe
  // product behaviour; it requires that an ARCHITECTURE GUARD exist. **When 4E lands, satisfying `0085` means
  // writing a guard — and the criterion is closed by the guard's existence, not by anything the guard finds.**
  //
  // ==================================================================================================
  // ⚠⚠⚠ READ THIS BEFORE WRITING THAT GUARD: ***A CRITERION SATISFIED BY A GUARD'S EXISTENCE IS SATISFIED
  // BY A VACUOUS GUARD.***
  // ==================================================================================================
  //
  // If closure is *"the architecture guard exists"*, then a guard whose matcher matches nothing, or whose
  // walk enumerates an empty set, closes `AC-TEN-0085` completely — **green forever, criterion cited,
  // nothing observed.** Every other criterion in this package is closed by BEHAVIOUR, which makes an
  // anti-vacuity control a quality improvement. **HERE THE CONTROL *IS* THE SATISFACTION CONDITION**, so
  // the one criterion in the package whose subject is a test is the one most likely to be closed by a test
  // that cannot fail.
  //
  // ⚠ THE REMEDY IS IN THIS VERY METHOD AND SHOULD BE COPIED, NOT REDESIGNED. The body below carries both
  // halves already, each recorded against the incident that motivated it:
  //   * a FLOOR on the scanned population — `scanned.Length >= 20` — because **three tests here once passed
  //     over an EMPTY type set (T-258)**; the floor is on what was scanned, not on what was found, since an
  //     empty result is this test's success condition.
  //   * a MATCHER CONTROL — the same `Name.Contains` run for a term that MUST be present (T-263) — because
  //     **a ban whose matcher matches nothing is green for the wrong reason.**
  //
  // So when the 4E guard is written: give it a floor, give it a known-positive, and PLANT it — a
  // plane-specific endpoint with a bare `RequireAuthenticatedUser` must redden it. **Without that, `0085`
  // is met by a test that has never been able to fail, and it will be cited exactly like the others.**
  // ==================================================================================================
  // ⚠⚠⚠ THIS IS A TRIPWIRE, NOT A CONSTRAINT. IT IS SUPPOSED TO REDDEN ONE DAY, AND WHEN IT DOES THE
  // CORRECT RESPONSE IS TO DELETE IT — AFTER REVISITING THE SEVEN DISPOSITIONS THAT DEPEND ON IT.
  // ==================================================================================================
  //
  // **Nothing here forbids a tenant-administration surface.** `DEC-TEN-0024` schedules it; when Phase 4D
  // lands, `/api/platform/tenants` is correct and this test is obsolete. ***THE RED IS THE SIGNAL, NOT THE
  // OBSTACLE.***
  //
  // ---- WHY IT EXISTS: SEVEN CRITERIA ARE RECORDED AS UNBUILT ON THE STRENGTH OF A MEASUREMENT.
  //
  // `AC-TEN-0012`, `0021`, `0022`, `0023`, `0026`, `0027` and `0028` all describe AUTHORIZATION on
  // `/api/platform/tenants` routes. **They are uncited because that surface does not exist** — measured over
  // a closed population: every `"/api/platform/…"` literal in `src/` resolves to `auth`, `companies`,
  // `localization`, `roles` or `support`, and none to `tenants`.
  //
  // ⚠⚠ ***BUT AN UNBUILT-BY-MEASUREMENT DISPOSITION DECAYS SILENTLY. IT BECOMES FALSE THE DAY SOMEONE
  // BUILDS THE THING, AND NOTHING TELLS THE PEOPLE HOLDING THE LEDGER.*** *That is the same rot we record
  // against stale specification notes elsewhere in this repository — a claim that was true when written and
  // is quietly wrong afterwards.* **Its sibling below does not have that problem: it REDDENS if the Phase-4E
  // taxonomy appears, so those three dispositions announce their own expiry. These seven did not.**
  //
  // ⚠ AND THE SIBLING WOULD NOT HAVE CAUGHT THIS ONE: it bans two TYPE NAMES — `PlatformAuthorityEndpoint`
  // and `PlatformSessionEndpoint` — so a tenant-administration file called anything else walks past it. *The
  // ROUTE LITERAL is the thing a new surface cannot avoid writing, whatever its types are named.*
  //
  // ---- ⚠ WRITE THE PURPOSE ON A TRIPWIRE OR LOSE IT. The guard next door records the reason:
  // *"a stale criterion misleads; a stale RATIONALE invites a DELETION."* **A reader who cannot see why this
  // exists will remove it — correctly, for the wrong reason — and the seven dispositions will go on reading
  // as measured fact.**
  [Fact]
  public void No_tenant_administration_route_surface_exists_yet()
  {
    var sources = Directory
      .EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
      .Where(path =>
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .ToArray();

    // FLOOR on the walk. A source scan that found nothing would satisfy the ban below in silence.
    Assert.True(sources.Length >= 300,
      $"only {sources.Length} source files were walked; the scan has broken and the ban below judges nothing.");

    var texts = sources.Select(File.ReadAllText).ToArray();

    // MATCHER CONTROL: the same search, run for a route literal that MUST be present. Without it a change to
    // how routes are written would silence the ban rather than trip it.
    Assert.Contains(texts, text => text.Contains("\"/api/platform/support/auth", StringComparison.Ordinal));

    // ⚠ THE OPENING QUOTE IS LOAD-BEARING: it matches a STRING LITERAL rather than any mention. Without it
    // a comment saying *"`/api/platform/tenants` arrives in Phase 4D"* trips this — **a red for prose, on a
    // guard whose every red is supposed to mean the surface now exists.** *A tripwire that cries wolf is a
    // tripwire someone deletes, which is the exact failure the header warns about.*
    Assert.DoesNotContain(texts, text => text.Contains("\"/api/platform/tenants", StringComparison.Ordinal));
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }

  [Fact]
  public void Phase_4E_plane_authentication_policy_taxonomy_is_not_pulled_into_phase_4B()
  {
    // Boundary marker: the platform logout route is secured by a narrow inline security_plane=platform check, NOT by
    // the deferred RequirePlatformAuthenticatedUser policy infrastructure (DEC-TEN-0024 / Phase 4E). When 4E lands,
    // it introduces those types and this guard is updated deliberately — it must not appear as a 4B side effect.
    var assemblies = new[]
    {
      typeof(PlatformPermissionAuthorizationHandler).Assembly,                      // Host.API
      typeof(PlatformSupportAuthenticationEndpointRouteBuilderExtensions).Assembly  // Platform.API
    };

    // ⚠ THREE TESTS HERE PASSED OVER AN EMPTY TYPE SET (T-258). The floor is on the scanned types,
    // not on the deferred ones — an empty deferred list is the success condition.
    var scanned = assemblies.SelectMany(assembly => assembly.GetTypes()).ToArray();
    Assert.True(scanned.Length >= 20,
      $"only {scanned.Length} types were scanned across the assemblies; the enumeration collapsed.");

    // ⚠ THE CONTROL ON THE MATCHER (T-263). The floor proves types were scanned. It cannot prove the name
    // test still selects anything, and a ban whose matcher matches nothing is green for the wrong reason.
    // So the same `Name.Contains` is run over the same collection for a term that MUST be present.
    Assert.Contains(scanned, type => type.Name.Contains("PlatformSupport", StringComparison.Ordinal));

    var deferred = scanned
      .Where(type => type.Name.Contains("PlatformAuthenticatedUser", StringComparison.Ordinal))
      .Select(type => type.FullName)
      .ToArray();

    Assert.Empty(deferred);
  }
}
