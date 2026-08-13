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

  [Fact]
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
