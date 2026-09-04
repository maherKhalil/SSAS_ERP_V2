using SSAS.Platform.Domain;
using SSAS.BuildingBlocks.Domain;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using SSAS.Host.API.Authentication;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Application.Authentication;

namespace SSAS.API.Tests.Infrastructure;

[Collection(HostIntegrationTestGroup.Name)]
public sealed class JwtInfrastructureTests(HostWebApplicationFactory factory)
{
  // ⚠ CITES `AC-IAM-0008` — *"Every tenant-scoped token contains EXACTLY ONE VALID tenant claim."* `:51`
  // carries both halves of that in one assertion, and the choice of `Assert.Single` is the citation:
  //
  //   EXACTLY ONE   `Assert.Single(claims, predicate)` fails on ZERO and on TWO. `Assert.Contains` would
  //                 pass with a second, contradictory `tenant_id` in the token — which is the forgery
  //                 shape the criterion exists to exclude, and the one a reader assumes is covered.
  //   VALID         the predicate compares the VALUE to the tenant the caller asked for, so a token
  //                 carrying exactly one claim naming a DIFFERENT tenant fails too.
  //
  // **`Assert.Single` with a predicate is doing two jobs and looks like one.** Weakening it to `Contains`
  // during a tidy-up would leave a passing test and delete the whole criterion.
  //
  // ⚠⚠ AND THE ANTI-VACUITY CONTROL IS THE PLATFORM PROFILE, TWO TESTS DOWN: `Assert.DoesNotContain(…
  // TenantId …)` on a platform token, plus a theory at `:283-285` that REJECTS a platform token carrying
  // `tenant_id` — **including when it is BLANK.** So *exactly one* here is a real constraint rather than a
  // description of a claim set that always looks the same: the same infrastructure is proved to emit ONE
  // in one profile and NONE in the other. **Neither alone distinguishes a working rule from a fixed
  // template.**
  //
  // Not cited: everything else in this test — RS256, the kid, the fifteen-minute lifetime, the 8 KB cap,
  // role and permission ordering and de-duplication. Those are `FP-002`'s subject and several already
  // carry `AC-AUTH` ids elsewhere in this file.
  [Fact]
  [Trait("Criterion", "AC-IAM-0008")]
  [Trait("Criterion", "AC-TEN-0060")]
  [Trait("Criterion", "AC-AUTH-0037")]
  [Trait("Criterion", "AC-AUTH-0005")]
  // `AC-AUTH-0037` — *"Every issued access token has the EXACT REQUIRED CLAIMS AND FORMATS."* This asserts
  // the issued token member by member: subject, tenant, roles and permissions in deterministic order, the
  // 15-minute lifetime, a `jti` parseable as `N`-format, the size ceiling, and the absence of email/name.
  //
  // `AC-AUTH-0005` — *"Every tenant access token has EXACTLY ONE tenant claim."* `Assert.Single` on the
  // `tenant_id` claim is the ISSUANCE half. ⚠ **The VALIDATION half is `Tenant_profile_rejects_duplicate_
  // singleton_claims`, which plants a SECOND `tenant_id` and requires rejection** — same trait. **An issuer
  // that emits one and a validator that would accept two are different failures, and *exactly one* needs
  // both: emitting one proves nothing about what is accepted.**
  
  // `AC-TEN-0060`'s ISSUANCE HALF — *"A tenant access token WITHOUT a `security_plane` claim … no
  // tenant-issuer change is required in Phase 3C."* The last assertion in this method is that the issued
  // tenant token carries NO `security_plane` claim at all, and it already names `DEC-TEN-0022` in its own
  // comment.
  //
  // ⚠ THE CRITERION'S OTHER HALF IS VALIDATION, NOT ISSUANCE — *"REMAINS VALID under the tenant profile
  // (absence => tenant)"* — and it has its own deliberate witness at `Legacy_tenant_token_without_security_
  // plane_is_accepted`, which carries the same trait.
  //
  // ⚠⚠ CORRECTED: I FIRST WROTE THAT NO SUCH TEST EXISTED AND THAT THE HALF WAS COVERED ONLY *BY
  // CONSTRUCTION*. **It is named for exactly this property and sits one method ABOVE
  // `Explicit_tenant_security_plane_is_accepted`, which I did read and cited as the adjacent case.** I was
  // one method away and asserted an absence instead of scrolling. ***AND THE SEARCH THAT WOULD HAVE FOUND IT
  // IS IN THE CRITERION'S OWN HEADING — `AC-TEN-0060` is titled "LEGACY tenant token remains valid", and the
  // test carries the word `Legacy`.*** I searched the property description and never the criterion's own
  // vocabulary.
  public void Access_token_issuer_emits_rs256_known_kid_and_exact_trusted_bindings()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var keys = scope.ServiceProvider.GetRequiredService<ISigningKeyProvider>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var now = DateTimeOffset.UtcNow;
    var tenantId = Guid.Parse("72064151-a6a5-414b-bced-43083bc88b3c");

    var issued = issuer.Issue(new AccessTokenClaims(
      "immutable-subject", 11, tenantId, 22, 33, client, 4,
      ["z-role", "a-role", "z-role"], ["z.permission", "a.permission", "z.permission"]), now);

    Assert.True(issued.IsSuccess);
    var compact = issued.Value.AccessToken.RevealOnce().Value;
    var token = new JwtSecurityTokenHandler().ReadJwtToken(compact);
    Assert.Equal(SecurityAlgorithms.RsaSha256, token.Header.Alg);
    Assert.Equal(keys.Snapshot.ActiveSigningKey.KeyId, token.Header.Kid);
    Assert.Equal(TimeSpan.FromMinutes(15), token.ValidTo - token.ValidFrom);
    Assert.True(compact.Length <= 8192);
    Assert.True(Guid.TryParseExact(token.Id, "N", out _));
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.Subject && claim.Value == "immutable-subject");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.TenantId && claim.Value == tenantId.ToString("D"));
    Assert.Equal(["a-role", "z-role"], token.Claims.Where(claim => claim.Type == JwtClaimTypes.Role).Select(claim => claim.Value));
    Assert.Equal(["a.permission", "z.permission"], token.Claims.Where(claim => claim.Type == JwtClaimTypes.Permission).Select(claim => claim.Value));
    Assert.DoesNotContain(token.Claims, claim => claim.Type is JwtClaimTypes.Email or JwtClaimTypes.Name);
    // Tenant regression (DEC-TEN-0022): the tenant profile carries NO security_plane claim (absence ⇒ tenant).
    Assert.DoesNotContain(token.Claims, claim => claim.Type == JwtClaimTypes.SecurityPlane);
  }

  [Fact]
  [Trait("Criterion", "AC-TEN-0074")]
  [Trait("Criterion", "AC-TEN-0025")]
  // ⚠ `AC-TEN-0025`'s **FIRST** HALF ONLY — *"A platform-support token CARRIES `security_plane=platform` and
  // NO `tenant_id` or `tenant_user_id`"*. Its second half — *"a token COMBINING `security_plane=platform`
  // with `tenant_id` is REJECTED as invalid"* — is `AC-TEN-0059` word for word, and is cited on the three
  // rejection sites.
  //
  // ⚠⚠ SO `0025` CONTAINS `0059` RATHER THAN DUPLICATING IT, AND THE DISTINCTION MATTERS FOR WHOEVER TIDIES
  // THE CRITERIA. **A duplicate can be deleted; a container cannot** — deleting `0025` as "redundant with
  // `0059`" would lose the issuance half, which `0059` does not mention at all. **Saying WHICH half each
  // site carries is the only thing that stops a containment being read as a duplicate later.**
  //
  // ***LOAD-BEARING (STRENGTH) AND NARROWEST (BREADTH) SITE FOR `AC-TEN-0074`'s POSITIVE HALF — it isolates
  // the ISSUER, so a failure here names the issuer and nothing else. The end-to-end login in
  // `PlatformSupportAuthenticationEndToEndTests` is BROADEST and least diagnostic; the claims record is
  // SUPPORTING. **Breadth and strength run opposite: the most realistic test is the least diagnostic**, so
  // three sites is a hierarchy on two axes and a census sees only three.*** The other site — `PlatformAccessTokenClaims
  // Tests.Eligible_active_principal_with_permissions_prepares_platform_claims` — is SUPPORTING, and the
  // distinction is written at both because **a trait says a criterion is covered and never says by how
  // much. With two sites of UNEQUAL strength, deleting the strong one leaves the id sitting on the weak one
  // and the criterion degrades silently while staying green.**
  //
  // `AC-TEN-0074`'s POSITIVE HALF AT ITS STRONGEST SITE — *"it carries `security_plane=platform` EXACTLY
  // ONCE plus `identity_id`, `session_id`, `client_id`, `security_version` …"*. `Assert.Single` on the plane
  // claim is *exactly once* literally, and the same form covers subject, identity, session and client.
  //
  // ⚠ THIS IS A BETTER SITE THAN THE ONE I CITED FIRST. `PlatformAccessTokenClaimsTests` asserts the CLAIMS
  // RECORD's members are populated; **this asserts the ISSUED TOKEN's claims, which is what the criterion is
  // actually about.** The record is a precondition for the token, so the earlier citation is a supporting
  // site rather than a wrong one — both keep the trait, and a reader who deletes either still sees the id.
  //
  // ⚠⚠ AND THE FIXTURE IS ADVERSARIAL RATHER THAN REPRESENTATIVE: the permission list passed in contains
  // `Platform.Tenants.View` TWICE. **A duplicate is the input that would break *exactly once* if the issuer
  // emitted per-item**, so the arrangement builds the state in which a plausible wrong implementation
  // succeeds and then asserts it does not.
  public void Platform_token_issuer_emits_the_platform_profile_and_no_tenant_claims()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var now = DateTimeOffset.UtcNow;

    var issued = issuer.Issue(new PlatformAccessTokenClaims(
      "immutable-subject", 11, 33, client, 4,
      ["Platform.Tenants.View", "Platform.Support.Administer", "Platform.Tenants.View"]), now);

    Assert.True(issued.IsSuccess);
    var token = new JwtSecurityTokenHandler().ReadJwtToken(issued.Value.AccessToken.RevealOnce().Value);

    // security_plane=platform exactly once.
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.SecurityPlane && claim.Value == "platform");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.Subject && claim.Value == "immutable-subject");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.IdentityId && claim.Value == "11");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.SessionId && claim.Value == "33");
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.ClientId && claim.Value == AuthenticationClientId.V1Web);
    Assert.Single(token.Claims, claim => claim.Type == JwtClaimTypes.SecurityVersion && claim.Value == "4");
    // Permissions deduped + ordinally ordered.
    Assert.Equal(
      ["Platform.Support.Administer", "Platform.Tenants.View"],
      token.Claims.Where(claim => claim.Type == JwtClaimTypes.Permission).Select(claim => claim.Value));

    // Platform profile forbids every tenant-shaped claim.
    Assert.DoesNotContain(token.Claims, claim =>
      claim.Type == JwtClaimTypes.TenantId || claim.Type == JwtClaimTypes.TenantUserId ||
      claim.Type == JwtClaimTypes.Role || claim.Type == JwtClaimTypes.CompanyId);
  }

  [Fact]
  public void Platform_token_issuer_rejects_a_claim_set_with_no_permissions()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

    var issued = issuer.Issue(new PlatformAccessTokenClaims("immutable-subject", 11, 33, client, 4, []), DateTimeOffset.UtcNow);

    Assert.True(issued.IsFailure);
  }

  [Fact]
  public void Access_token_issuer_rejects_a_token_larger_than_the_approved_ceiling()
  {
    using var scope = factory.Services.CreateScope();
    var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var permissions = Enumerable.Range(0, 500).Select(index => $"permission.{index:D4}.{new string('x', 32)}").ToArray();

    var issued = issuer.Issue(new AccessTokenClaims(
      "immutable-subject", 11, Guid.NewGuid(), 22, 33, client, 4, [], permissions), DateTimeOffset.UtcNow);

    Assert.True(issued.IsFailure);
    Assert.Equal("Authentication.AccessTokenUnavailable", issued.Error.Code);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0019")]
  // ==================================================================================================
  // `AC-AUTH-0019` — *"Invalid JWT SIGNATURE, ISSUER, AUDIENCE, EXPIRY, or CLAIMS is rejected."*
  // FIVE NAMED FAILURE MODES, AND THE FILE HAS A TEST FOR EACH — the enumeration mapped once, here:
  // ==================================================================================================
  //   SIGNATURE   this test (a token the handler cannot verify) and `Algorithm_substitution_is_rejected`
  //   ISSUER      `Wrong_issuer_or_audience_is_rejected` — a theory, one row per field
  //   AUDIENCE    the same theory's other row
  //   EXPIRY      `Expired_jwt_is_rejected_...` and `Not_yet_valid_token_is_rejected` — **both ENDS of the
  //               validity window, which *expiry* alone would not have required**
  //   CLAIMS      `Malformed_identifier_and_duplicate_claims_are_rejected`
  //
  // ⚠ **A DISJUNCTION OF FIVE NEEDS FIVE ARM-ONLY FIXTURES, AND HERE IT HAS THEM** — each test invalidates
  // exactly one property and leaves the rest well-formed, so a validator that checked four of five reddens
  // on the fifth. **Contrast the single fixture that carries several arms at once: it looks richer and
  // cannot isolate any of them.** All five carry this trait.
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Invalid_jwt_is_rejected_by_the_registered_authentication_handler()
  {
    var token = CreateToken("DifferentTestSigningKey-ForInvalidSignature-NotASecret", DateTime.UtcNow.AddMinutes(5));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0019")]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Expired_jwt_is_rejected_by_the_registered_authentication_handler()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(-1));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  // ==================================================================================================
  // `AC-AUTH-0040`, QUOTED IN FULL BECAUSE THE SHORT FORM OF IT IS A DIFFERENT CRITERION:
  //
  //   *"Strict JWT validation accepts only RS256 with known enabled `kid` and valid exact issuer,
  //   audience, signature, lifetime, `nbf`, cardinality, and formats; no symmetric path remains active."*
  //
  // ⚠⚠ THE FIRST DRAFT OF THIS COMMENT QUOTED IT AS FAR AS *"known enabled `kid`"* AND STOPPED, then
  // enumerated the four `kid`/algorithm refusals as though they discharged it. **A truncated quotation is
  // not a shorter claim, it is a NARROWER ONE — and the citation is read later against the criterion's
  // real text, not against the fragment**, so eight named properties would have counted as covered on the
  // strength of tests that never touch them. Every clause is therefore listed with its witness:
  // ==================================================================================================
  //   RS256 ONLY     `Unsigned_alg_none_token_is_rejected`, `Algorithm_substitution_is_rejected`
  //   KNOWN kid      this test
  //   ENABLED kid    `Production_key_provider_keeps_enabled_overlap_keys_and_excludes_disabled_keys` —
  //                  *enabled* is decided at the key provider, not by presenting a token
  //   (kid present)  `Missing_kid_is_rejected`
  //   ISSUER         `Wrong_issuer_or_audience_is_rejected`, first row
  //   AUDIENCE       the same theory's second row
  //   SIGNATURE      `Invalid_jwt_is_rejected_...`, `Algorithm_substitution_is_rejected`
  //   LIFETIME       `Expired_jwt_is_rejected_...`
  //   `nbf`          `Not_yet_valid_token_is_rejected` — **a clause `AC-AUTH-0019` does not have, which is
  //                  why 0019 and 0040 are not the same citation on the same set**
  //   CARDINALITY    `Tenant_profile_rejects_duplicate_singleton_claims`, `Duplicate_security_plane_is_rejected`
  //   FORMATS        `Malformed_identifier_and_duplicate_claims_are_rejected`
  //   NO SYMMETRIC   ***NOT WITNESSABLE BY ANY TOKEN IN THIS FILE.*** Refusing an HS256 token proves what
  //                  the CONFIGURED validator does; *no symmetric path REMAINS* is a claim about what the
  //                  tree still contains, and an unwired symmetric issuer sitting in `src/` satisfies every
  //                  assertion here. Witness added as an architecture guard:
  //                  `AuthenticationMilestoneArchitectureTests.No_symmetric_signing_path_remains_active_anywhere_under_src`.
  //
  // **The permitted side is `Token_from_the_access_token_issuer_passes_strict_bearer_validation`** —
  // without it, *accepts only* is satisfied by a validator that accepts nothing.
  //
  // ⚠ AND THIS TEST'S NAME CARRIES AN EXTRA CLAIM THE CRITERION DOES NOT: *without trying the active key*.
  // **That is a claim about WHAT THE VALIDATOR DID NOT DO — a stronger property than refusal, because a
  // validator that fell back to the active key would still reject a forged token and would silently accept
  // one signed with a retired key.** The criterion does not ask for it; the test provides it.
  public async Task Unknown_kid_is_rejected_without_trying_the_active_key()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), "unknown-kid");

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Missing_kid_is_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), removeKid: true);

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Unsigned_alg_none_token_is_rejected()
  {
    var now = DateTimeOffset.UtcNow;
    var token = new JwtSecurityToken(
      HostWebApplicationFactory.Issuer,
      HostWebApplicationFactory.Audience,
      CreateRequiredClaims(now),
      now.AddMinutes(-1).UtcDateTime,
      now.AddMinutes(5).UtcDateTime);

    var result = await AuthenticateAsync(new JwtSecurityTokenHandler().WriteToken(token));

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  [Trait("Criterion", "AC-AUTH-0019")]
  public async Task Algorithm_substitution_is_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), algorithm: SecurityAlgorithms.RsaSha384);

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Theory]
  [InlineData("https://wrong-issuer.test", HostWebApplicationFactory.Audience)]
  [InlineData(HostWebApplicationFactory.Issuer, "wrong-audience")]
  [Trait("Criterion", "AC-AUTH-0019")]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Wrong_issuer_or_audience_is_rejected(string issuer, string audience)
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5), issuer: issuer, audience: audience);

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Not_yet_valid_token_is_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var token = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(10), notBefore: DateTime.UtcNow.AddMinutes(5));

    var result = await AuthenticateAsync(token);

    Assert.False(result.Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0019")]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Malformed_identifier_and_duplicate_claims_are_rejected()
  {
    var key = factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var malformed = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => Replace(claims, JwtClaimTypes.IdentityId, "01"));
    var duplicateCritical = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SessionId, "4")]);
    var duplicateRole = CreateRs256Token(key, DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.Role, "role"), new Claim(JwtClaimTypes.Role, "role")]);

    Assert.False((await AuthenticateAsync(malformed)).Succeeded);
    Assert.False((await AuthenticateAsync(duplicateCritical)).Succeeded);
    Assert.False((await AuthenticateAsync(duplicateRole)).Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  // `AC-AUTH-0040`'s PERMITTED SIDE. ***A CRITERION SAYING "ACCEPTS ONLY X" IS HALF A REFUSAL LIST AND HALF
  // AN ACCEPTANCE, AND A SUITE OF REFUSALS ALONE IS SATISFIED BY A VALIDATOR THAT ACCEPTS NOTHING.*** The
  // four refusal tests carry the *only*; this carries the *accepts*.
  public async Task Token_from_the_access_token_issuer_passes_strict_bearer_validation()
  {
    var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var issued = issuer.Issue(new AccessTokenClaims(
      "validated-subject", 1, Guid.NewGuid(), 2, 3, client, 1, ["role"], ["permission"]), DateTimeOffset.UtcNow);
    var token = issued.Value.AccessToken.RevealOnce().Value;

    var result = await AuthenticateAsync(token);

    Assert.True(result.Succeeded);
  }

  // ---- Phase 3C-2: security-plane profile validation (ADR-015 / DEC-TEN-0022) ----

  [Fact]
  public async Task Platform_token_from_the_issuer_passes_strict_bearer_validation()
  {
    // Anchors 3C-1 issuer ↔ 3C-2 validator compatibility on a real signed platform token.
    var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var issued = issuer.Issue(
      new PlatformAccessTokenClaims("platform-subject", 1, 3, client, 1, ["Platform.Support.Administer", "Platform.Tenants.View"]),
      DateTimeOffset.UtcNow);

    Assert.True((await AuthenticateAsync(issued.Value.AccessToken.RevealOnce().Value)).Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-TEN-0060")]
  // `AC-TEN-0060`'s VALIDATION HALF, AND THIS IS THE LOAD-BEARING SITE FOR IT — *"A tenant access token
  // WITHOUT a `security_plane` claim REMAINS VALID under the tenant profile (absence => tenant)."* The token
  // is built with the required claims and nothing else, so the plane claim is absent by construction and the
  // test name says that is the point. **The issuance half is on `Access_token_issuer_emits_rs256_known_kid_
  // and_exact_trusted_bindings`, same trait.**
  //
  // ⚠ THE PAIR WITH `Explicit_tenant_security_plane_is_accepted` BELOW IS WHAT MAKES *absence => tenant* A
  // RULE RATHER THAN A COINCIDENCE: claim absent is accepted here, claim present and set to `tenant` is
  // accepted there, and `Unknown_or_wrong_case_security_plane_is_rejected` refuses everything else. **Three
  // adjacent tests covering absent / correct / wrong, which is the complete case analysis over a claim that
  // is optional.**
  public async Task Legacy_tenant_token_without_security_plane_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5));

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Explicit_tenant_security_plane_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Tenant)]);

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Structural_platform_token_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5), mutateClaims: ToPlatformClaims);

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Theory]
  [InlineData(JwtClaimTypes.TenantId, "b1b7c1e2-0000-4000-8000-000000000001")] // platform + tenant_id
  [InlineData(JwtClaimTypes.TenantId, "")]                                     // forbidden even when blank
  [InlineData(JwtClaimTypes.TenantUserId, "2")]                                // platform + tenant_user_id
  [InlineData(JwtClaimTypes.Role, "anything")]                                 // platform + role
  [InlineData(JwtClaimTypes.CompanyId, "b1b7c1e2-0000-4000-8000-000000000002")] // platform + company_id
  public async Task Platform_token_with_a_forbidden_claim_is_rejected(string forbiddenType, string forbiddenValue)
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => ToPlatformClaims(claims).Append(new Claim(forbiddenType, forbiddenValue)).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  // ---- THE TENANT PLANE REFUSES `company_id` TOO (ADR-025 decision 4, item 164).
  // The prohibition names this claim by hand and is binding, and until now the guard existed only on the
  // PLATFORM plane -- the plane where the claim was never plausible -- and not on the one it names.
  // This is a NAMED prohibition, not a general out-of-set rejection; the tenant profile still accepts an
  // unlisted extra claim, which is costed rather than built (item 164).
  [Fact]
  public async Task Tenant_token_carrying_company_id_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Append(new Claim(JwtClaimTypes.CompanyId, "b1b7c1e2-0000-4000-8000-000000000003")).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  // ---- THE CONTROL: the very same token WITHOUT the claim authenticates, so the refusal above is the
  // ---- claim's doing and not some unrelated defect in how this token was built.
  [Fact]
  public async Task The_same_tenant_token_without_company_id_is_accepted()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5));

    Assert.True((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_token_with_no_permission_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
        .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform))
        .ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_token_with_a_duplicate_permission_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => ToPlatformClaims(claims).Append(new Claim(JwtClaimTypes.Permission, "Platform.Support.Administer")).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_token_with_a_blank_permission_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
        .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform))
        .Append(new Claim(JwtClaimTypes.Permission, " "))
        .ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Duplicate_security_plane_is_rejected()
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => ToPlatformClaims(claims).Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform)).ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Theory]
  [InlineData("Platform")]
  [InlineData("PLATFORM")]
  [InlineData("Tenant")]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("bogus")]
  public async Task Unknown_or_wrong_case_security_plane_is_rejected(string plane)
  {
    // Even on an otherwise-valid tenant token, a non-exact security_plane value is rejected.
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SecurityPlane, plane)]);

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  [Trait("Criterion", "AC-TEN-0059")]
  [Trait("Criterion", "AC-TEN-0025")]
  // `AC-TEN-0025`'s **SECOND** half, which is `AC-TEN-0059` restated inside a larger criterion — *"a token
  // combining `security_plane=platform` with `tenant_id` is rejected as invalid."* Its first half, the
  // issued token's shape, is on `Platform_token_issuer_emits_the_platform_profile_and_no_tenant_claims`.
  //
  // `AC-TEN-0059` AT THE AUTHENTICATION LAYER AND FROM THE OTHER DIRECTION — the two sites in
  // `PlatformAuthorizationPipelineTests` and `PlatformSupportAuthorityAuthorizationTests` add `tenant_id` to
  // a PLATFORM-shaped token; this takes a TENANT-shaped token and claims the platform plane. Same criterion,
  // opposite construction, and **the rejection here is `AuthenticateAsync` failing rather than a route
  // returning 401 — the structural refusal seen without any HTTP layer in the way.**
  //
  // ⚠⚠ AND THIS SITE NARROWS THE DISJUNCTION GAP I RECORDED AT THE OTHER TWO WITHOUT CLOSING IT. The default
  // claim set carries BOTH `tenant_id` AND `tenant_user_id`, so this token violates both arms of *"any
  // `tenant_id` (or `tenant_user_id`)"* at once. **Both arms present is not each arm tested**: a validator
  // that rejects on `tenant_id` alone and ignores `tenant_user_id` passes all three sites, because no
  // fixture anywhere plants `tenant_user_id` WITHOUT `tenant_id`. ***THE ARM-ONLY CASE IS THE ONE A
  // DISJUNCTION NEEDS, and a fixture carrying both arms looks like stronger coverage while providing less.***
  public async Task Tenant_shaped_token_claiming_platform_plane_is_rejected()
  {
    // Attack: a valid tenant token (with tenant fields) sets security_plane=platform → platform profile
    // forbids tenant_id/tenant_user_id → rejected.
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform)]);

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Fact]
  public async Task Platform_shaped_token_claiming_tenant_plane_is_rejected()
  {
    // Attack: a platform-shaped token (no tenant fields) sets security_plane=tenant → tenant profile
    // requires tenant_id/tenant_user_id → rejected.
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      mutateClaims: claims => claims
        .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
        .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Tenant))
        .Append(new Claim(JwtClaimTypes.Permission, "Platform.Support.Administer"))
        .ToList());

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  [Theory]
  [InlineData(JwtClaimTypes.TenantId, "b1b7c1e2-0000-4000-8000-000000000003")]
  [InlineData(JwtClaimTypes.TenantUserId, "9")]
  [Trait("Criterion", "AC-AUTH-0005")]
  // `AC-AUTH-0005`'s VALIDATION half — a token carrying a SECOND `tenant_id` (or `tenant_user_id`) is
  // refused. **The issuance half is on `Access_token_issuer_emits_rs256_known_kid_and_exact_trusted_
  // bindings`, same trait.**
  //
  // ⚠ THE FIXTURE IS ADVERSARIAL AND ITS DUPLICATE LOOKS LIKE A MISTAKE: a token with two tenant claims is
  // malformed by construction, which is exactly the state a tidy-up removes. **It is the only state in which
  // *exactly one* can fail**, and the two rows cover both singleton claims separately rather than trusting
  // one to stand for the pair.
  [Trait("Criterion", "AC-AUTH-0040")]
  public async Task Tenant_profile_rejects_duplicate_singleton_claims(string type, string extraValue)
  {
    var token = CreateRs256Token(ActiveKey(), DateTime.UtcNow.AddMinutes(5),
      extraClaims: [new Claim(type, extraValue)]);

    Assert.False((await AuthenticateAsync(token)).Succeeded);
  }

  private X509SecurityKey ActiveKey() => factory.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;

  private static List<Claim> ToPlatformClaims(List<Claim> claims) =>
    claims
      .Where(claim => claim.Type is not (JwtClaimTypes.TenantId or JwtClaimTypes.TenantUserId))
      .Append(new Claim(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform))
      .Append(new Claim(JwtClaimTypes.Permission, "Platform.Support.Administer"))
      .ToList();

  [Fact]
  public void Jwt_options_validator_requires_a_production_signing_certificate()
  {
    var validator = new JwtOptionsValidator(new TestHostEnvironment("Production"));
    var options = new JwtOptions
    {
      Issuer = HostWebApplicationFactory.Issuer,
      Audience = HostWebApplicationFactory.Audience,
      ClockSkewSeconds = 30
    };

    var result = validator.Validate(null, options);

    Assert.True(result.Failed);
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0022")]
  [Trait("Criterion", "AC-AUTH-0039")]
  // `AC-AUTH-0039` — *"`kid` is derived from certificate DER bytes, one active signing key is selected from
  // an immutable snapshot, invalid key identifiers fail closed, and ROLLOVER RETAINS OLD VERIFICATION FOR AT
  // LEAST LIFETIME PLUS 30-SECOND SKEW."* Clause by clause:
  //
  //   DER-DERIVED `kid`   `Kid()` at the foot of this file recomputes `Base64Url(SHA256(RawData))` and the
  //                       assertions compare the provider's key ids against it. ⚠ **A REIMPLEMENTATION, NOT
  //                       A SHARED CALL — so it catches the provider switching to `Thumbprint`, a serial, or
  //                       a GUID, and it CANNOT catch the formula being wrong in both places at once.** That
  //                       is the honest strength of it: the derivation's SOURCE is pinned, its CORRECTNESS
  //                       is not.
  //   ONE ACTIVE KEY      `Snapshot.ActiveSigningKey` is singular by type. Structural, not asserted here.
  //   FAIL CLOSED         `Unknown_kid_is_rejected_without_trying_the_active_key`, `Missing_kid_is_rejected`.
  //   OVERLAP >= LIFETIME `AccessTokenLifetime` is 15 min and `ClockSkewSeconds` 30, so the criterion's
  //   + SKEW              threshold is 15m30s. This test retires at 16 min and PASSES; the refusal twin
  //                       retires at 15 min and THROWS. ⚠⚠ **THE PAIR BRACKETS THE THRESHOLD, IT DOES NOT
  //                       PIN IT** — every value in (15m, 16m] is consistent with both, so a provider
  //                       demanding a full 16 minutes satisfies this file while violating the criterion.
  //                       Recorded rather than papered over: closing it needs a 15m30s row, and the two
  //                       existing values were chosen for rotation, not for the boundary.
  //
  // `AC-AUTH-0022` — *"Signing-key OVERLAP supports CONTROLLED ROTATION."* Three real certificates on disk —
  // an active PFX with its private key, a retained public CER, and a disabled one — so the provider is
  // exercised against the artefacts a rotation actually produces rather than against a stub.
  //
  // ⚠ **OVERLAP IS A THREE-STATE CLAIM AND ALL THREE ARE PRESENT: active (signs), RETAINED (validates but
  // does not sign), disabled (neither).** A two-state fixture — active and absent — would satisfy *rotation*
  // and say nothing about *overlap*, **because overlap IS the retained state.** Its refusal twin is
  // `Production_key_provider_rejects_duplicate_kid_and_insufficient_overlap`, same trait.
  public void Production_key_provider_keeps_enabled_overlap_keys_and_excludes_disabled_keys()
  {
    var directory = Path.Combine(Path.GetTempPath(), $"ssas-jwt-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      const string password = "test-only-password";
      using var active = CreateCertificate("active");
      using var retained = CreateCertificate("retained");
      using var disabled = CreateCertificate("disabled");
      var activePath = Path.Combine(directory, "active.pfx");
      var retainedPath = Path.Combine(directory, "retained.cer");
      var disabledPath = Path.Combine(directory, "disabled.cer");
      File.WriteAllBytes(activePath, active.Export(X509ContentType.Pfx, password));
      File.WriteAllBytes(retainedPath, retained.Export(X509ContentType.Cert));
      File.WriteAllBytes(disabledPath, disabled.Export(X509ContentType.Cert));
      var options = ProductionOptions(activePath, password,
      [
        new VerificationCertificateOptions
        {
          Path = retainedPath,
          Enabled = true,
          RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(16)
        },
        new VerificationCertificateOptions { Path = disabledPath, Enabled = false }
      ]);

      using var provider = new SigningKeyProvider(Options.Create(options), new TestHostEnvironment("Production"));

      Assert.Equal(2, provider.Snapshot.EnabledVerificationKeys.Count);
      Assert.Contains(provider.Snapshot.ActiveSigningKey.KeyId, provider.Snapshot.EnabledVerificationKeys.Keys);
      Assert.Contains(Kid(retained), provider.Snapshot.EnabledVerificationKeys.Keys);
      Assert.DoesNotContain(Kid(disabled), provider.Snapshot.EnabledVerificationKeys.Keys);
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0022")]
  // `AC-AUTH-0022`'s REFUSAL twin — duplicate `kid` and insufficient overlap both rejected. **The permitted
  // side is the test above; a criterion about a supported CAPABILITY needs the success, and a criterion about
  // a CONTROLLED one needs the refusals.**
  public void Production_key_provider_rejects_duplicate_kid_and_insufficient_overlap()
  {
    var directory = Path.Combine(Path.GetTempPath(), $"ssas-jwt-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      const string password = "test-only-password";
      using var active = CreateCertificate("active");
      using var retained = CreateCertificate("retained");
      var activePath = Path.Combine(directory, "active.pfx");
      var activePublicPath = Path.Combine(directory, "active.cer");
      var retainedPath = Path.Combine(directory, "retained.cer");
      File.WriteAllBytes(activePath, active.Export(X509ContentType.Pfx, password));
      File.WriteAllBytes(activePublicPath, active.Export(X509ContentType.Cert));
      File.WriteAllBytes(retainedPath, retained.Export(X509ContentType.Cert));

      var duplicate = ProductionOptions(activePath, password,
        [new VerificationCertificateOptions { Path = activePublicPath, RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(16) }]);
      Assert.Throws<InvalidOperationException>(() =>
        new SigningKeyProvider(Options.Create(duplicate), new TestHostEnvironment("Production")));

      var insufficientOverlap = ProductionOptions(activePath, password,
        [new VerificationCertificateOptions { Path = retainedPath, RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(15) }]);
      Assert.Throws<InvalidOperationException>(() =>
        new SigningKeyProvider(Options.Create(insufficientOverlap), new TestHostEnvironment("Production")));
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  [Fact]
  [Trait("Criterion", "AC-AUTH-0038")]
  // ==================================================================================================
  // `AC-AUTH-0038` — *"Production signs only with a deployment-mounted X.509 RSA private key OF AT LEAST
  // 2048 BITS through an abstract provider; development and tests use only their approved non-production
  // key sources."*
  //
  // ⚠⚠ THE SIZE FLOOR WAS ENFORCED IN THREE PLACES IN `src/` AND EXERCISED ONLY FROM ABOVE. Every
  // certificate this file builds comes from `CreateCertificate`, which called `RSA.Create(2048)` with the
  // size hard-coded — **the boundary value itself, so the floor was satisfied by every fixture and
  // contradicted by none.** Delete all three `KeySize < 2048` checks from `SigningKeyProvider` and the
  // suite stays green. *A check that only ever sees conforming input is indistinguishable from no check.*
  //
  // ⚠ AND THE PARAMETER IS THE WHOLE FIX. The helper needed one optional argument; the reason nobody
  // supplied an undersized key is that **there was no way to ASK for one**, and a fixture builder with a
  // constant where the criterion has a threshold quietly removes the threshold from the test space.
  //
  // ⚠⚠⚠ TWO CHECKS, TWO PATHS, AND THE ORDER HIDES ONE OF THEM. `ValidateActive` runs before any
  // verification certificate is read, so an undersized ACTIVE key throws before the verification floor is
  // ever reached: **a build with the verification check deleted passes an active-key test.** The two cases
  // are therefore separated — the second pairs an undersized verification certificate with a CONFORMING
  // active one — and each asserts on the message, because both throw `InvalidOperationException` and the
  // type alone cannot say which floor fired.
  // ==================================================================================================
  public void Production_key_provider_rejects_an_rsa_key_below_the_approved_size()
  {
    var directory = Path.Combine(Path.GetTempPath(), $"ssas-jwt-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
      const string password = "test-only-password";
      using var undersizedActive = CreateCertificate("undersized-active", 1024);
      using var conformingActive = CreateCertificate("conforming-active");
      using var undersizedVerification = CreateCertificate("undersized-verification", 1024);
      var undersizedActivePath = Path.Combine(directory, "undersized-active.pfx");
      var conformingActivePath = Path.Combine(directory, "conforming-active.pfx");
      var undersizedVerificationPath = Path.Combine(directory, "undersized-verification.cer");
      File.WriteAllBytes(undersizedActivePath, undersizedActive.Export(X509ContentType.Pfx, password));
      File.WriteAllBytes(conformingActivePath, conformingActive.Export(X509ContentType.Pfx, password));
      File.WriteAllBytes(undersizedVerificationPath, undersizedVerification.Export(X509ContentType.Cert));

      var activeFailure = Assert.Throws<InvalidOperationException>(() => new SigningKeyProvider(
        Options.Create(ProductionOptions(undersizedActivePath, password, [])),
        new TestHostEnvironment("Production")));
      Assert.Contains("active JWT RSA key", activeFailure.Message, StringComparison.Ordinal);

      var verificationFailure = Assert.Throws<InvalidOperationException>(() => new SigningKeyProvider(
        Options.Create(ProductionOptions(conformingActivePath, password,
        [
          new VerificationCertificateOptions
          {
            Path = undersizedVerificationPath,
            Enabled = true,
            RetireAfterUtc = DateTimeOffset.UtcNow.AddMinutes(16)
          }
        ])),
        new TestHostEnvironment("Production")));
      Assert.Contains("verification certificate", verificationFailure.Message, StringComparison.Ordinal);
    }
    finally
    {
      Directory.Delete(directory, true);
    }
  }

  private async Task<AuthenticateResult> AuthenticateAsync(string token)
  {
    using var scope = factory.Services.CreateScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    context.Request.Headers[HeaderNames.Authorization] = $"Bearer {token}";

    return await scope.ServiceProvider
      .GetRequiredService<IAuthenticationService>()
      .AuthenticateAsync(context, JwtBearerDefaults.AuthenticationScheme);
  }

  private static string CreateToken(string signingKey, DateTime expiresAt)
  {
    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
      issuer: HostWebApplicationFactory.Issuer,
      audience: HostWebApplicationFactory.Audience,
      claims: [new Claim("sub", "test-user")],
      notBefore: expiresAt.AddMinutes(-5),
      expires: expiresAt,
      signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static string CreateRs256Token(
    SecurityKey signingKey,
    DateTime expiresAt,
    string? kid = null,
    bool removeKid = false,
    DateTime? notBefore = null,
    string? issuer = null,
    string? audience = null,
    string algorithm = SecurityAlgorithms.RsaSha256,
    IEnumerable<Claim>? extraClaims = null,
    Func<List<Claim>, List<Claim>>? mutateClaims = null)
  {
    var now = DateTimeOffset.UtcNow;
    var claims = CreateRequiredClaims(now).Concat(extraClaims ?? []).ToList();
    if (mutateClaims is not null) claims = mutateClaims(claims);
    var token = new JwtSecurityToken(
      issuer ?? HostWebApplicationFactory.Issuer,
      audience ?? HostWebApplicationFactory.Audience,
      claims,
      notBefore ?? expiresAt.AddMinutes(-5),
      expiresAt,
      new SigningCredentials(signingKey, algorithm));
    if (kid is not null) token.Header[JwtHeaderParameterNames.Kid] = kid;
    if (removeKid) token.Header.Remove(JwtHeaderParameterNames.Kid);
    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  private static IEnumerable<Claim> CreateRequiredClaims(DateTimeOffset now) =>
  [
    new Claim(JwtClaimTypes.Subject, "test-user"),
    new Claim(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
    new Claim("iat", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
    new Claim(JwtClaimTypes.IdentityId, "1"),
    new Claim(JwtClaimTypes.TenantId, Guid.NewGuid().ToString("D")),
    new Claim(JwtClaimTypes.TenantUserId, "2"),
    new Claim(JwtClaimTypes.SessionId, "3"),
    new Claim(JwtClaimTypes.ClientId, AuthenticationClientId.V1Web),
    new Claim(JwtClaimTypes.SecurityVersion, "1")
  ];

  private static List<Claim> Replace(List<Claim> claims, string type, string value) =>
    claims.Where(claim => claim.Type != type).Append(new Claim(type, value)).ToList();

  private static JwtOptions ProductionOptions(
    string activePath,
    string password,
    VerificationCertificateOptions[] verificationCertificates) => new()
    {
      Issuer = HostWebApplicationFactory.Issuer,
      Audience = HostWebApplicationFactory.Audience,
      AccessTokenLifetime = TimeSpan.FromMinutes(15),
      ClockSkewSeconds = 30,
      MaximumEncodedTokenSize = 8192,
      ActiveSigningCertificatePath = activePath,
      ActiveSigningCertificatePassword = password,
      VerificationCertificates = verificationCertificates
    };

  private static X509Certificate2 CreateCertificate(string name, int keySizeBits = 2048)
  {
    using var rsa = RSA.Create(keySizeBits);
    var request = new CertificateRequest($"CN=SSAS JWT {name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
  }

  private static string Kid(X509Certificate2 certificate) =>
    Base64UrlEncoder.Encode(SHA256.HashData(certificate.RawData));

  private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
  {
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "SSAS.API.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
  }
  // ================================================================================================
  // ⚠ A SIGNING FAILURE IS OPAQUE TO THE CALLER AND VISIBLE TO THE OPERATOR (T-241).
  // ================================================================================================
  //
  // Both `Issue` overloads wrap their body in `catch (Exception)` and return the generic
  // `AccessTokenIssuanceUnavailable`. **The response is right and the silence was not**: the file had no
  // logger at all, so a signing key that failed to load and a routine refusal were indistinguishable to
  // everyone, including the operator watching authentication fail for every user at once.
  //
  // **This test has to assert BOTH HALVES, because either alone is satisfied by the wrong fix.** Asserting
  // only the log would pass if someone also widened the response to name the cause -- which is the leak
  // the generic failure exists to prevent. Asserting only the response is what the code already did.
  [Fact]
  public void A_signing_failure_is_logged_with_its_cause_and_still_answers_the_caller_generically()
  {
    var logger = new CapturingLogger<AccessTokenIssuer>();
    var issuer = new AccessTokenIssuer(
      new ThrowingSigningKeyProvider(),
      Options.Create(new JwtOptions { Issuer = "ssas", Audience = "ssas-web" }),
      logger);

    var client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
    var issued = issuer.Issue(new AccessTokenClaims(
      "immutable-subject", 11, Guid.Parse("72064151-a6a5-414b-bced-43083bc88b3c"), 22, 33, client, 4,
      ["a-role"], ["a.permission"]), DateTimeOffset.UtcNow);

    // Outward: unchanged, and carrying nothing about the cause.
    Assert.True(issued.IsFailure);
    Assert.Equal(AuthenticationErrors.AccessTokenIssuanceUnavailable.Code, issued.Error.Code);
    Assert.DoesNotContain("signing key", issued.Error.Message, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(ThrowingSigningKeyProvider.Marker, issued.Error.Message, StringComparison.Ordinal);

    // Inward: the exception itself, not just a note that something failed. A log line without the
    // exception attached would leave the operator exactly as unable to tell the causes apart.
    var entry = Assert.Single(logger.Entries);
    Assert.Equal(LogLevel.Error, entry.Level);
    Assert.NotNull(entry.Exception);
    Assert.Contains(ThrowingSigningKeyProvider.Marker, entry.Exception!.Message, StringComparison.Ordinal);
  }

  // Throws where the real provider would hand back a key -- the shape of a certificate that failed to
  // load, which is the failure this whole path was silent about.
  private sealed class ThrowingSigningKeyProvider : ISigningKeyProvider
  {
    internal const string Marker = "signing-key-unavailable-marker";

    public SigningKeySnapshot Snapshot => throw new InvalidOperationException(Marker);
  }

  private sealed class CapturingLogger<T> : ILogger<T>
  {
    internal List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
      LogLevel logLevel, EventId eventId, TState state, Exception? exception,
      Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, exception));
  }
}
