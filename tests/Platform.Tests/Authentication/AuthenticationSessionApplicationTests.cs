using System.Reflection;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Queries;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;

namespace SSAS.Platform.Tests.Authentication;

[Trait("Scenario", "TS-AUTH-0020")]
[Trait("Scenario", "TS-AUTH-0021")]
[Trait("Scenario", "TS-AUTH-0024")]
[Trait("Scenario", "TS-AUTH-0031")]
[Trait("Scenario", "TS-AUTH-0032")]
[Trait("Scenario", "TS-AUTH-0038")]
[Trait("Scenario", "TS-AUTH-0042")]
[Trait("Scenario", "TS-AUTH-0075")]
[Trait("Scenario", "TS-AUTH-0076")]
[Trait("Scenario", "TS-AUTH-0083")]
[Trait("Scenario", "TS-AUTH-0088")]
[Trait("Scenario", "TS-AUTH-0089")]
[Trait("Acceptance", "AC-AUTH-0024")]
[Trait("Acceptance", "AC-AUTH-0032")]
public sealed class AuthenticationSessionApplicationTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
  private static readonly AuthenticationClientId Client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;
  private static readonly string[] VerifiedIdentityPropertyNames = ["IdentityId", "SecurityVersion"];

  [Fact]
  public void Verified_identity_is_a_narrow_internal_capability()
  {
    var publicConstructors = typeof(VerifiedIdentity).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
    var properties = typeof(VerifiedIdentity).GetProperties().Select(property => property.Name).Order().ToArray();

    Assert.Empty(publicConstructors);
    Assert.Equal(VerifiedIdentityPropertyNames, properties);
    Assert.DoesNotContain("17", new VerifiedIdentity(17, 3).ToString(), StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData(" ssas-erp-web", false)]
  [InlineData("ssas-erp-web", true)]
  public void Client_id_syntax_rejects_blank_or_whitespace_padded_values(string? value, bool expected)
  {
    Assert.Equal(expected, AuthenticationClientId.Create(value).IsSuccess);
  }

  [Fact]
  public void V1_client_allowlist_is_ordinal_and_maximum_length_is_enforced()
  {
    var registry = new AllowedClientRegistry();

    Assert.True(registry.IsAllowed(AuthenticationClientId.Create("ssas-erp-web").Value));
    Assert.False(registry.IsAllowed(AuthenticationClientId.Create("SSAS-ERP-WEB").Value));
    Assert.False(registry.IsAllowed(AuthenticationClientId.Create("unknown-client").Value));
    Assert.True(AuthenticationClientId.Create(new string('a', AuthenticationClientId.MaximumLength + 1)).IsFailure);
  }

  [Fact]
  public async Task Begin_tenant_access_returns_no_membership_without_creating_authentication_state()
  {
    var fixture = new Fixture();

    var result = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.True(result.IsSuccess);
    Assert.IsType<NoEligibleMembership>(result.Value);
    Assert.Empty(fixture.Sessions.Values);
    Assert.Empty(fixture.Selections.Values);
  }

  [Fact]
  public async Task Begin_tenant_access_automatically_selects_one_revalidated_membership()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();

    var result = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.True(result.IsSuccess);
    var automatic = Assert.IsType<TenantSelectedAutomatically>(result.Value);
    Assert.Equal(membership.TenantId, automatic.Session.TenantId);
    Assert.Single(fixture.Sessions.Values);
    Assert.Single(fixture.Sessions.Values[0].RefreshTokenRecords);
  }

  [Fact]
  public async Task Begin_tenant_access_creates_single_use_selection_proof_for_multiple_memberships()
  {
    var fixture = new Fixture();
    fixture.AddEligibleMembership("Tenant One");
    fixture.AddEligibleMembership("Tenant Two");

    var result = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));

    Assert.True(result.IsSuccess);
    var selection = Assert.IsType<TenantSelectionRequired>(result.Value);
    Assert.Equal(2, selection.Memberships.Count);
    Assert.Single(fixture.Selections.Values);
    Assert.True(selection.SelectionProof.IsAvailable);
    Assert.Empty(fixture.Sessions.Values);
  }

  [Fact]
  public async Task Tenant_selection_creates_one_session_and_cannot_be_replayed()
  {
    var fixture = new Fixture();
    var selected = fixture.AddEligibleMembership("Tenant One");
    fixture.AddEligibleMembership("Tenant Two");
    var begin = await fixture.BeginHandler().HandleAsync(new BeginTenantAccessCommand(fixture.Capability, Client));
    var required = Assert.IsType<TenantSelectionRequired>(begin.Value);
    var rawProof = required.SelectionProof.RevealOnce().Value;
    var handler = fixture.SelectHandler();

    var first = await handler.HandleAsync(new SelectTenantCommand(
      new SensitiveAuthenticationTokenInput(rawProof), Client, selected.TenantUserId, selected.TenantId));
    var replay = await handler.HandleAsync(new SelectTenantCommand(
      new SensitiveAuthenticationTokenInput(rawProof), Client, selected.TenantUserId, selected.TenantId));

    Assert.True(first.IsSuccess);
    Assert.True(replay.IsFailure);
    Assert.Single(fixture.Sessions.Values);
    Assert.NotNull(fixture.Selections.Values.Single().ConsumedUtc);
  }

  [Fact]
  public async Task Stale_verified_identity_is_rejected_before_tenant_state_is_created()
  {
    var fixture = new Fixture();
    fixture.AddEligibleMembership();

    var result = await fixture.BeginHandler().HandleAsync(
      new BeginTenantAccessCommand(new VerifiedIdentity(fixture.Account.IdentityId, fixture.Account.SecurityVersion + 1), Client));

    Assert.True(result.IsFailure);
    Assert.Equal("Authentication.Failed", result.Error.Code);
    Assert.Empty(fixture.Sessions.Values);
    Assert.Empty(fixture.Selections.Values);
  }

  [Fact]
  public async Task Eleventh_session_revokes_deterministic_oldest_and_leaves_other_identity_unchanged()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    for (var index = 0; index < 10; index++)
    {
      fixture.Sessions.Values.Add(NewPersistedSession(100 + index, fixture.Account.IdentityId, membership, Now.AddMinutes(index)));
    }

    var unrelated = NewPersistedSession(999, fixture.Account.IdentityId + 1, membership with { IdentityId = fixture.Account.IdentityId + 1 }, Now.AddMinutes(-1));
    fixture.Sessions.Values.Add(unrelated);

    var created = await fixture.Creator.CreateAsync(fixture.Account, membership, Client, Now.AddHours(1), default);

    Assert.True(created.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Revoked, fixture.Sessions.Values.Single(session => session.Id == 100).Status);
    Assert.Equal(AuthenticationSessionRevocationReason.SessionLimitExceeded, fixture.Sessions.Values.Single(session => session.Id == 100).RevocationReason);
    Assert.Equal(AuthenticationSessionStatus.Active, unrelated.Status);
    Assert.Equal(10, fixture.Sessions.Values.Count(session => session.IdentityId == fixture.Account.IdentityId && session.Status == AuthenticationSessionStatus.Active));
  }

  [Fact]
  public async Task Successful_refresh_rotates_once_and_verified_predecessor_reuse_compromises_session()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    var session = NewPersistedSession(501, fixture.Account.IdentityId, membership, Now);
    var generated = fixture.TokenService.GenerateRefreshToken(session.Id, session.TokenFamilyId, Client);
    var raw = generated.SensitiveToken.RevealOnce().Value;
    var predecessor = session.CreateInitialRefreshToken(generated.PublicId, generated.SecretHash, Now, Guid.NewGuid());
    SetId(predecessor, 601);
    fixture.Sessions.Values.Add(session);
    fixture.Sessions.Locator = new RefreshTokenSessionLocator(session.Id, session.IdentityId, session.TenantUserId, session.TenantId);
    var handler = fixture.RefreshHandler();
    var command = new RefreshAuthenticationSessionCommand(new SensitiveAuthenticationTokenInput(raw), Client);

    var first = await handler.HandleAsync(command);
    var reuse = await handler.HandleAsync(command);

    Assert.True(first.IsSuccess);
    Assert.True(reuse.IsFailure);
    Assert.Equal("AuthenticationSession.RefreshFailed", reuse.Error.Code);
    Assert.Equal(AuthenticationSessionStatus.Compromised, session.Status);
    Assert.Equal(2, session.RefreshTokenRecords.Count);
    Assert.NotNull(session.RefreshTokenRecords.Single(token => token.PublicId != predecessor.PublicId).RevokedUtc);
  }

  [Fact]
  public async Task Current_session_logout_revokes_only_the_bound_session_and_is_terminally_idempotent()
  {
    var fixture = new Fixture();
    var membership = fixture.AddEligibleMembership();
    var session = NewPersistedSession(701, fixture.Account.IdentityId, membership, Now);
    fixture.Sessions.Values.Add(session);
    var current = new FakeCurrentAuthenticationSession(new CurrentAuthenticationSession(
      fixture.Account.IdentityId, membership.TenantId, membership.TenantUserId,
      session.Id, Client, fixture.Account.SecurityVersion));
    var handler = new RevokeCurrentAuthenticationSessionCommandHandler(
      current, fixture.Accounts, fixture.Sessions, fixture.UnitOfWork, new TestClock());

    var first = await handler.HandleAsync(new RevokeCurrentAuthenticationSessionCommand());
    var second = await handler.HandleAsync(new RevokeCurrentAuthenticationSessionCommand());

    Assert.True(first.IsSuccess);
    Assert.True(second.IsSuccess);
    Assert.Equal(AuthenticationSessionStatus.Revoked, session.Status);
    Assert.Equal(AuthenticationSessionRevocationReason.UserLogout, session.RevocationReason);
  }

  private static AuthenticationSession NewPersistedSession(
    long id,
    long identityId,
    EligibleTenantMembership membership,
    DateTimeOffset createdUtc)
  {
    var session = AuthenticationSession.Create(
      identityId,
      membership.TenantUserId,
      membership.TenantId,
      Client.Value,
      Guid.NewGuid(),
      1,
      createdUtc,
      createdUtc.AddDays(30),
      createdUtc.AddDays(90));
    SetId(session, id);
    return session;
  }

  private sealed class Fixture
  {
    public Fixture()
    {
      Account = AuthenticationAccount.CreatePending(17, LoginEmail.Create("session.user@example.com").Value);
      SetId(Account, 7);
      Assert.True(Account.CompleteInitialSetup("test-password-hash", Guid.NewGuid(), Now).IsSuccess);
      Accounts.Value = Account;
      Capability = new VerifiedIdentity(Account.IdentityId, Account.SecurityVersion);
      Creator = new AuthenticationSessionCreator(Sessions, UnitOfWork, TokenService,
        new FakeClaimsProvider(), new FakeAccessTokenIssuer(), Policy);
    }

    public AuthenticationAccount Account { get; }
    public VerifiedIdentity Capability { get; }
    public FakeAccountRepository Accounts { get; } = new();
    public FakeSelectionRepository Selections { get; } = new();
    public FakeMembershipService Memberships { get; } = new();
    public FakeSessionRepository Sessions { get; } = new();
    public FakeUnitOfWork UnitOfWork { get; } = new();
    public AuthenticationTokenService TokenService { get; } = new();
    public AuthenticationPolicy Policy { get; } = new();
    public AuthenticationSessionCreator Creator { get; }

    public EligibleTenantMembership AddEligibleMembership(string name = "Tenant")
    {
      var membership = new EligibleTenantMembership(Account.IdentityId, 30 + Memberships.Values.Count, Guid.NewGuid(), name);
      Memberships.Values.Add(membership);
      return membership;
    }

    public BeginTenantAccessCommandHandler BeginHandler() => new(
      Accounts,
      Selections,
      Memberships,
      new AllowedClientRegistry(),
      TokenService,
      Creator,
      UnitOfWork,
      Policy,
      new TestClock());

    public RefreshAuthenticationSessionCommandHandler RefreshHandler() => new(
      Accounts,
      Sessions,
      Memberships,
      new AllowedClientRegistry(),
      TokenService,
      new FakeClaimsProvider(),
      new FakeAccessTokenIssuer(),
      UnitOfWork,
      Policy,
      new TestClock());

    public SelectTenantCommandHandler SelectHandler() => new(
      Accounts,
      Selections,
      Memberships,
      new AllowedClientRegistry(),
      TokenService,
      Creator,
      UnitOfWork,
      new TestClock());
  }

  private sealed class FakeAccountRepository : IAuthenticationAccountRepository
  {
    public AuthenticationAccount? Value { get; set; }
    public Task<AuthenticationAccount?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Value?.Id == id ? Value : null);
    public Task<AuthenticationAccount?> GetByIdentityIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Value?.IdentityId == id ? Value : null);
    public Task<AuthenticationAccount?> GetByIdForUpdateAsync(long id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
    public Task<AuthenticationAccount?> GetByIdentityIdForUpdateAsync(long id, CancellationToken cancellationToken = default) => GetByIdentityIdAsync(id, cancellationToken);
    public Task<AuthenticationAccount?> GetByNormalizedLoginEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(Value?.NormalizedLoginEmail == email ? Value : null);
    public Task ReloadAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddAsync(AuthenticationAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class FakeMembershipService : IIdentityTenantMembershipReadService
  {
    public List<EligibleTenantMembership> Values { get; } = [];
    public Task<IReadOnlyList<EligibleTenantMembership>> ListEligibleMembershipsAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<EligibleTenantMembership>>(Values.Where(value => value.IdentityId == identityId).ToArray());
    public Task<IdentityTenantMembershipEligibility> GetMembershipEligibilityForUpdateAsync(long identityId, long tenantUserId, Guid tenantId, CancellationToken cancellationToken = default)
    {
      var membership = Values.SingleOrDefault(value => value.IdentityId == identityId && value.TenantUserId == tenantUserId && value.TenantId == tenantId);
      return Task.FromResult(new IdentityTenantMembershipEligibility(membership, membership is not null));
    }
  }

  private sealed class FakeSessionRepository : IAuthenticationSessionRepository
  {
    public List<AuthenticationSession> Values { get; } = [];
    public RefreshTokenSessionLocator? Locator { get; set; }
    public Task<AuthenticationSession?> GetByIdForUpdateAsync(long id, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(value => value.Id == id));
    public Task<RefreshTokenSessionLocator?> GetRefreshTokenLocatorAsync(Guid publicId, CancellationToken cancellationToken = default) => Task.FromResult(Locator);
    public Task<AuthenticationSession?> GetByRefreshTokenForUpdateAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Values.SingleOrDefault(value => value.Id == id));
    public Task<IReadOnlyList<AuthenticationSession>> ListActiveUnexpiredByIdentityForUpdateAsync(long identityId, DateTimeOffset utcNow, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<AuthenticationSession>>(Values.Where(value => value.IdentityId == identityId && value.IsUsable(utcNow)).ToArray());
    public Task<IReadOnlyList<AuthenticationSession>> ListActiveByIdentityForUpdateAsync(long identityId, CancellationToken cancellationToken = default) =>
      Task.FromResult<IReadOnlyList<AuthenticationSession>>(Values.Where(value => value.IdentityId == identityId && value.Status == AuthenticationSessionStatus.Active).ToArray());
    public Task AddAsync(AuthenticationSession session, CancellationToken cancellationToken = default)
    {
      SetId(session, Values.Count == 0 ? 1 : Values.Max(value => value.Id) + 1);
      Values.Add(session);
      return Task.CompletedTask;
    }
  }

  private sealed class FakeSelectionRepository : ITenantSelectionTransactionRepository
  {
    public List<TenantSelectionTransaction> Values { get; } = [];
    public Task<long?> GetIdentityIdByPublicIdAsync(Guid publicId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(value => value.PublicId == publicId)?.IdentityId);
    public Task<TenantSelectionTransaction?> GetByPublicIdForUpdateAsync(Guid publicId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Values.SingleOrDefault(value => value.PublicId == publicId));
    public Task AddAsync(TenantSelectionTransaction transaction, CancellationToken cancellationToken = default)
    {
      SetId(transaction, Values.Count + 1);
      Values.Add(transaction);
      return Task.CompletedTask;
    }
  }

  private sealed class AllowedClientRegistry : IAuthenticationClientRegistry
  {
    public bool IsAllowed(AuthenticationClientId clientId) => clientId == Client;
  }

  private sealed class FakeClaimsProvider : IAccessTokenClaimsProvider
  {
    public Task<Result<AccessTokenClaims>> GetClaimsAsync(long authenticationSessionId, long identityId,
      long tenantUserId, Guid tenantId, AuthenticationClientId clientId, long securityVersion,
      CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(new AccessTokenClaims(
        "test-subject", identityId, tenantId, tenantUserId, authenticationSessionId, clientId, securityVersion, [], [])));
  }

  private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
  {
    public Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Success(new IssuedAccessToken(new SensitiveAccessToken("test-access-token"), issuedUtc.AddMinutes(15)));
  }

  private sealed class FakeCurrentAuthenticationSession(CurrentAuthenticationSession value)
    : ICurrentAuthenticationSession
  {
    public CurrentAuthenticationSession? Value { get; } = value;
  }

  private sealed class FakeUnitOfWork : IPlatformUnitOfWork
  {
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Result.Success(1));
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.FromResult<ITransaction>(new FakeTransaction());
  }

  private sealed class FakeTransaction : ITransaction
  {
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => Now;
  }

  private static void SetId(object entity, long id)
  {
    var field = typeof(Entity<long>).GetField("<Id>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.NotNull(field);
    field.SetValue(entity, id);
  }
}
