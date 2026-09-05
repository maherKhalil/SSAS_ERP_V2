using System.Reflection;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;

namespace SSAS.Platform.Tests.Authentication;

// Phase 3C-1 platform token claims/profile foundation (ADR-015 / ADR-016 / DEC-TEN-0022).
public sealed class PlatformAccessTokenClaimsTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
  private const long IdentityId = 42;
  private const long SessionId = 7;

  private static readonly AuthenticationClientId Client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

  // ---- SecurityPlane / claim-name constants ----

  [Fact]
  public void Security_plane_claim_name_and_values_are_exact()
  {
    Assert.Equal("security_plane", SSAS.BuildingBlocks.Application.Abstractions.Identity.JwtClaimTypes.SecurityPlane);
    Assert.Equal("platform", SecurityPlane.Platform);
    Assert.Equal("tenant", SecurityPlane.Tenant);
  }

  // ---- Platform claims model shape ----

  [Fact]
  public void Platform_claims_model_has_no_tenant_shaped_fields()
  {
    var properties = typeof(PlatformAccessTokenClaims).GetProperties().Select(property => property.Name).ToArray();

    foreach (var forbidden in new[] { "TenantId", "TenantUserId", "CompanyId", "Roles", "Role", "Status", "SecurityPlane" })
    {
      Assert.DoesNotContain(forbidden, properties);
    }

    Assert.Contains("Subject", properties);
    Assert.Contains("IdentityId", properties);
    Assert.Contains("AuthenticationSessionId", properties);
    Assert.Contains("SecurityVersion", properties);
    Assert.Contains("Permissions", properties);
  }

  // ---- Claims provider eligibility + sourcing ----

  [Fact]
  [Trait("Acceptance", "AC-TEN-0074")]
  // ***SUPPORTING (STRENGTH) AND NARROWEST-BUT-ONE (BREADTH) SITE FOR `AC-TEN-0074`'s POSITIVE HALF. The
  // LOAD-BEARING one is
  // `JwtInfrastructureTests.Platform_token_issuer_emits_the_platform_profile_and_no_tenant_claims`***, which
  // asserts the ISSUED TOKEN carries `security_plane=platform` exactly once. This test asserts the CLAIMS
  // RECORD is populated, which is a PRECONDITION for that token rather than the criterion's subject.
  //
  // ⚠ THE RANKING IS WRITTEN DOWN BECAUSE A TRAIT CANNOT CARRY IT. **Two sites of unequal strength look
  // identical to any census; deleting the load-bearing one leaves this id here, still green, covering less.**
  //
  // *"it carries … `identity_id`, `session_id`, `client_id`, `security_version`, and one or more active
  // catalog-valid PlatformSupport permission claims."* The ban half is on `PlatformPlaneAuthorizationArchi
  // tectureTests.Platform_access_token_claims_carry_no_tenant_or_role_shaped_fields`, same trait.
  //
  // ⚠ **THE TWO HALVES CANNOT LIVE IN ONE TEST**: a ban is a claim about the TYPE's members and the positive
  // is a claim about a produced INSTANCE's values. Splitting them across a structural and a behavioural site
  // is not duplication — **neither could make the other's assertion** — and the id is on both so that
  // deleting either leaves the criterion visibly half-carried rather than silently so.
  public async Task Eligible_active_principal_with_permissions_prepares_platform_claims()
  {
    var provider = Build(
      account: EligibleAccount(),
      principal: ActivePrincipal(),
      permissions: [PlatformPermissionNames.ViewTenants, PlatformPermissionNames.AdministerPlatformSupport]);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), SessionId, Client);

    Assert.True(result.IsSuccess);
    var claims = result.Value;
    Assert.Equal("local:platform-operator", claims.Subject);
    Assert.Equal(IdentityId, claims.IdentityId);
    Assert.Equal(SessionId, claims.AuthenticationSessionId);
    Assert.Equal(AuthenticationClientId.V1Web, claims.ClientId.Value);
    Assert.True(claims.SecurityVersion > 0);
    // Sourced from the read service, deduped and ordinally ordered.
    Assert.Equal(
      new[] { PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewTenants },
      claims.Permissions);
  }

  [Fact]
  public async Task Permissions_are_sourced_from_the_read_service_and_deterministically_ordered()
  {
    // Unordered, with a duplicate — the provider must dedupe and ordinally order.
    var provider = Build(
      account: EligibleAccount(),
      principal: ActivePrincipal(),
      permissions: [PlatformPermissionNames.ViewTenants, PlatformPermissionNames.ManageTenants, PlatformPermissionNames.ViewTenants]);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), SessionId, Client);

    Assert.True(result.IsSuccess);
    Assert.Equal(new[] { PlatformPermissionNames.ManageTenants, PlatformPermissionNames.ViewTenants }, result.Value.Permissions);
  }

  [Fact]
  public async Task Missing_principal_denies()
  {
    var provider = Build(EligibleAccount(), principal: null, permissions: []);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), SessionId, Client);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalNotFound, result.Error);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0063")]
  [Trait("Acceptance", "AC-TEN-0039")]
  // ⚠⚠⚠ `AC-TEN-0039` AND `AC-TEN-0063` ARE THE SAME CRITERION WRITTEN TWICE, IN TWO BLOCKS.
  //   `0039` *"Platform token issuance performs a LIVE PRINCIPAL-STATUS CHECK and denies issuance for a
  //          `Disabled` principal; NO TOKEN-CARRIED STATUS IS AUTHORITATIVE."*
  //   `0063` *"At issuance, a LIVE STATUS CHECK denies a platform token when `...Status == Disabled`;
  //          NO TOKEN-CARRIED STATUS IS AUTHORITATIVE."*
  // Same property, same trailing clause verbatim; `0039` sits in the principal-lifecycle block and `0063` in
  // the token-profile block. **One test satisfies both because there is only one property.**
  //
  // ⚠ THAT MATTERS FOR THE DENOMINATOR, NOT JUST FOR THE TRAITS: **a package of 93 criteria containing a
  // duplicated property describes fewer than 93 distinct behaviours**, so criterion-coverage and
  // property-coverage are different numbers. Recorded rather than silently double-counted.
  // `AC-TEN-0063` — *"At issuance, a LIVE STATUS CHECK denies a platform token when
  // `PlatformSupportPrincipal.Status == Disabled`; no token-carried status is authoritative."*
  //
  // ⚠ THE *LIVE* IN THIS CRITERION IS EXPRESSIBLE AND `AC-TEN-0070`'s IS NOT, WHICH IS A DISTINCTION WORTH
  // KEEPING. Here *live* means the decision is taken from the PRINCIPAL RECORD rather than from the token —
  // a claim about the SOURCE, and the fake can present a disabled principal. `0070`'s *re-derived live on
  // refresh* is a claim about REPEATING the read, and this file's permission fake answers identically on
  // every call, so a cache and a re-read are indistinguishable. **A constant-returning double can express a
  // STATE and cannot express a CHANGE.**
  public async Task Disabled_principal_denies()
  {
    var disabled = ActivePrincipal();
    Assert.True(disabled.Disable("actor", Now).IsSuccess);
    var provider = Build(EligibleAccount(), disabled, [PlatformPermissionNames.AdministerPlatformSupport]);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), SessionId, Client);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.PrincipalDisabled, result.Error);
  }

  [Fact]
  public async Task Ineligible_account_denies()
  {
    // PendingSetup account is not authentication-eligible.
    var provider = Build(IneligibleAccount(), ActivePrincipal(), [PlatformPermissionNames.AdministerPlatformSupport]);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), SessionId, Client);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.AccountIneligible, result.Error);
  }

  [Fact]
  [Trait("Acceptance", "AC-TEN-0061")]
  // `AC-TEN-0061` — *"A principal with ZERO active catalog-valid `PermissionScope.PlatformSupport`
  // permissions is not eligible; platform token issuance is DENIED."* Expressible for the same reason as
  // `0063`: the fake's array is fixed per test but CHOSEN per test, so an empty set is a state it can hold.
  public async Task Zero_valid_permissions_fails_closed()
  {
    var provider = Build(EligibleAccount(), ActivePrincipal(), permissions: []);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), SessionId, Client);

    Assert.True(result.IsFailure);
    Assert.Equal(PlatformSupportErrors.NoUsablePlatformAuthority, result.Error);
  }

  [Fact]
  public async Task A_non_positive_session_id_is_rejected()
  {
    var provider = Build(EligibleAccount(), ActivePrincipal(), [PlatformPermissionNames.AdministerPlatformSupport]);

    var result = await provider.GetClaimsAsync(new VerifiedIdentity(IdentityId, 5), 0, Client);

    Assert.True(result.IsFailure);
  }

  // ⚠ CITES `AC-TEN-0071` — *"Platform token issuance reads ONLY `Identity`, `AuthenticationAccount`,
  // `PlatformSupportPrincipal`, and `PlatformPermissionAssignment`; bootstrap subject lists/configuration
  // NEVER participate."* **The criterion's four sources are this constructor's four parameters, in order, and
  // the second half is the absence the exact list enforces.**
  //
  // ⚠⚠ AN EXACT LIST IS A BIND, NOT A BAN, WHICH IS WHY IT DISCHARGES BOTH HALVES AT ONCE: *a
  // `DoesNotContain("Bootstrap…")` would pass on any newly-invented configuration type nobody thought to
  // name* — **here an ADDED dependency of any kind reddens as loudly as a removed one.**
  //
  // ⚠⚠⚠ AND THE FAILURE IT GUARDS IS TEMPTING RATHER THAN HYPOTHETICAL, WHICH IS WHAT EARNS A GUARD.
  // ***THE BOOTSTRAP PARADOX — "how does the FIRST platform administrator obtain claims before a principal row
  // exists?" — HAS AN OBVIOUS WRONG ANSWER: consult the bootstrap subject list here.*** *That would make
  // issuance depend on configuration rather than on persisted state, which is exactly what this forbids.*
  //
  // ⚠ The compiler notices such an addition (the tests below construct this provider directly) — **but only
  // until the call sites are repaired, which is ordinary work that looks like nothing.** *This is what still
  // objects afterwards.*
  // ---- ⚠⚠⚠ THE LIMIT OF EVERY DEPENDENCY-LIST GUARD, INCLUDING THIS ONE.
  //
  // ***A DEPENDENCY GUARD WATCHES WHO YOU CAN REACH. IT IS BLIND TO WHAT ARRIVES THROUGH WHAT YOU
  // ALREADY REACH.*** The dependency set stays constant while the payload grows, so a field added to a
  // type this provider already receives could carry the configuration this list is asserted to deny,
  // and the list would not move.
  //
  // The remedy, where it matters enough to spend the assertion, is to pin the CONTRACT TYPE'S SHAPE
  // POSITIVELY -- *"every property is a `Guid`"* rather than *"no property is a date"*, because a ban
  // names only the shapes its author thought of. Worked example:
  // `AttendanceArchitectureTests.No_attendance_read_path_can_learn_that_an_employee_was_terminated`.
  [Fact]
  [Trait("Criterion", "AC-TEN-0071")]
  public void Provider_consumes_no_bootstrap_or_options_configuration()
  {
    // Durable: the platform claims provider must not depend on bootstrap subjects/options or the
    // system-wide authority-state service — only per-identity persistence + the permission read service.
    var parameterTypes = typeof(PlatformAccessTokenClaimsProvider)
      .GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType.Name).ToArray();

    Assert.Equal(
      new[]
      {
        nameof(IIdentityRepository),
        nameof(IAuthenticationAccountRepository),
        nameof(IPlatformSupportPrincipalRepository),
        nameof(IPlatformSupportPermissionReadService)
      },
      parameterTypes);
  }

  // ---- Builders / fakes ----

  private static PlatformAccessTokenClaimsProvider Build(
    AuthenticationAccount account,
    PlatformSupportPrincipal? principal,
    string[] permissions) =>
    new(
      new FakeIdentityRepository(),
      new FakeAccountRepository(account),
      new FakePrincipalRepository(principal),
      new FakePermissionReadService(permissions));

  private static AuthenticationAccount EligibleAccount()
  {
    var account = AuthenticationAccount.CreatePending(IdentityId, LoginEmail.Create("operator@example.com").Value);
    Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), Now).IsSuccess);
    Assert.True(account.IsAuthenticationEligible);
    return account;
  }

  private static AuthenticationAccount IneligibleAccount()
  {
    var account = AuthenticationAccount.CreatePending(IdentityId, LoginEmail.Create("operator@example.com").Value);
    Assert.False(account.IsAuthenticationEligible);
    return account;
  }

  private static PlatformSupportPrincipal ActivePrincipal()
  {
    var principal = PlatformSupportPrincipal.Register(IdentityId).Value;
    SetId(principal, 100);
    return principal;
  }

  private static Identity IdentityFor()
  {
    var identity = Identity.Create(AuthenticationSubject.Create("local:platform-operator").Value);
    SetId(identity, IdentityId);
    return identity;
  }

  private static void SetId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field!.SetValue(entity, id);
  }

  // ==================================================================================================
  // ⚠⚠⚠ WHAT THE DOUBLES BELOW CANNOT EXPRESS — READ BEFORE CITING A CRITERION HERE.
  // ==================================================================================================
  //
  //   LIVENESS / RE-DERIVATION   `FakePermissionReadService` returns a FIXED array and IGNORES the principal
  //                              id it is asked about. Every call answers identically, so **a provider that
  //                              CACHED permissions and one that RE-READ them are indistinguishable.**
  //                              `AC-TEN-0070` — *"platform refresh RE-DERIVES permission claims LIVE; no
  //                              stale snapshot is reused"* — is therefore NOT CITABLE HERE. Its witness
  //                              needs a double whose answer MOVES between two calls.
  //
  //   WHAT THE PROVIDER READS    `AC-TEN-0071` — *"issuance reads only Identity, Account, Principal and
  //                              Assignment; BOOTSTRAP CONFIGURATION NEVER PARTICIPATES"* — is not citable
  //                              either, for the opposite reason: **there is no configuration double here to
  //                              record an access**, so a provider that also read configuration would be
  //                              invisible. A capability no double offers is one no test can prove is unused.
  //
  //   LOCK / FOR-UPDATE          `GetByIdentityIdForUpdateAsync` returns exactly the unlocked result.
  //
  //   EVERYTHING ELSE            every other repository member throws `NotSupportedException`.
  //
  // ⚠ NONE OF THIS IS A DEFECT — these are correct choices for a claims-provider test. **And note the two
  // failures are OPPOSITE: `0070` fails because a double answers too CONSTANTLY, `0071` because a double is
  // ABSENT.** Too much stability and too little presence both produce a criterion that cannot be witnessed
  // in the file that looks like its home.
  private sealed class FakeIdentityRepository : IIdentityRepository
  {
    public Task<Identity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<Identity?>(identityId == IdentityId ? IdentityFor() : null);

    public Task<Identity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(Identity identity, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakeAccountRepository(AuthenticationAccount account) : IAuthenticationAccountRepository
  {
    public Task<AuthenticationAccount?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<AuthenticationAccount?>(identityId == IdentityId ? account : null);

    public Task<AuthenticationAccount?> GetByIdAsync(long authenticationAccountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByIdForUpdateAsync(long authenticationAccountId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(string normalizedLoginEmail, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakePrincipalRepository(PlatformSupportPrincipal? principal) : IPlatformSupportPrincipalRepository
  {
    public Task<PlatformSupportPrincipal?> GetByIdentityIdAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(identityId == IdentityId ? principal : null);

    public Task<PlatformSupportPrincipal?> GetByIdentityIdForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult(identityId == IdentityId ? principal : null);

    public Task<PlatformSupportPrincipal?> GetByIdAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<PlatformSupportPrincipal?> GetByIdForUpdateAsync(long platformSupportPrincipalId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task<bool> ExistsForIdentityAsync(long identityId, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();

    public Task AddAsync(PlatformSupportPrincipal principal, CancellationToken cancellationToken = default) =>
      throw new NotSupportedException();
  }

  private sealed class FakePermissionReadService(string[] permissions) : IPlatformSupportPermissionReadService
  {
    public Task<IReadOnlyCollection<string>> GetActivePermissionsAsync(
      long platformSupportPrincipalId,
      CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyCollection<string>>(permissions);
  }
}
