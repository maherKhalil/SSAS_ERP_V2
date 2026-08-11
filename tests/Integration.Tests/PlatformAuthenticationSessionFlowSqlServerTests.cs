using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Permissions;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Identity;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.Queries;
using SSAS.Platform.Infrastructure.Persistence.Repositories;

namespace SSAS.Integration.Tests;

// Phase 3C-4 platform session creation / refresh / revocation / proactive-Disable SQL verification
// (ADR-016 / DEC-TEN-0022). Closes F3C-1 (SecurityVersion non-mutation), F3C-2 (Disable-vs-refresh race),
// F3C-3 (revoke-all), and the 3C-3 LOW-1 lock-serialization gap. Every test drives the real Application flow.
public sealed class PlatformAuthenticationSessionFlowSqlServerTests
{
  private static readonly AuthenticationClientId Client = AuthenticationClientId.Create(AuthenticationClientId.V1Web).Value;

  // ---- Creation ----

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Creation_persists_session_and_initial_refresh_and_issues_a_token()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedAuthorityAsync(db);

    await using var context = db.CreateContext();
    var issuer = new CapturingAccessTokenIssuer();
    var result = await BuildCreator(context, issuer).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now);

    Assert.True(result.IsSuccess);
    Assert.Equal(identityId, result.Value.IdentityId);
    Assert.Equal(principalId, result.Value.PlatformSupportPrincipalId);
    Assert.Contains(PlatformPermissionNames.AdministerPlatformSupport, issuer.LastClaims!.Permissions);

    await using var verify = db.CreateContext();
    var session = await verify.PlatformAuthenticationSessions.Include(s => s.RefreshTokenRecords).AsNoTracking().SingleAsync();
    Assert.Equal(AuthenticationSessionStatus.Active, session.Status);
    Assert.Equal(principalId, session.PlatformSupportPrincipalId);
    Assert.Single(session.RefreshTokenRecords);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Creation_fails_closed_for_disabled_principal_ineligible_account_or_zero_permissions()
  {
    // Disabled principal.
    await using (var db = await PlatformFlowSqlDatabase.CreateAsync())
    {
      var (identityId, _) = await SeedAuthorityAsync(db, principalActive: false);
      await using var context = db.CreateContext();
      Assert.True((await BuildCreator(context, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now)).IsFailure);
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions]"));
    }

    // Ineligible account.
    await using (var db = await PlatformFlowSqlDatabase.CreateAsync())
    {
      var (identityId, _) = await SeedAuthorityAsync(db, accountEligible: false);
      await using var context = db.CreateContext();
      Assert.True((await BuildCreator(context, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now)).IsFailure);
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions]"));
    }

    // Zero permissions.
    await using (var db = await PlatformFlowSqlDatabase.CreateAsync())
    {
      var (identityId, _) = await SeedAuthorityAsync(db, grantPermission: false);
      await using var context = db.CreateContext();
      Assert.True((await BuildCreator(context, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now)).IsFailure);
      Assert.Equal(0, await ReadInt32Async(context, "SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions]"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Session_limit_revokes_oldest_platform_sessions_only()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);

    await CreateSessionAsync(db, identityId, maxSessions: 1);
    await CreateSessionAsync(db, identityId, maxSessions: 1);

    await using var verify = db.CreateContext();
    Assert.Equal(1, await ReadInt32Async(verify, "SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [Status] = N'Active'"));
    Assert.Equal(1, await ReadInt32Async(verify, "SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [Status] = N'Revoked' AND [RevocationReason] = N'SessionLimitExceeded'"));
    Assert.Equal(0, await ReadInt32Async(verify, "SELECT COUNT(*) FROM [platform].[AuthenticationSessions]")); // tenant table untouched
  }

  // ---- Refresh ----

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Refresh_rotates_the_token_and_reissues_with_live_permissions()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var raw = await CreateSessionAsync(db, identityId);

    // Change the live permission set: revoke Administer... no — Administer must stay for eligibility; add ViewTenants.
    await GrantPermissionAsync(db, principalId, PlatformPermissionNames.ViewTenants);

    await using var context = db.CreateContext();
    var issuer = new CapturingAccessTokenIssuer();
    var result = await BuildRefresh(context, issuer).HandleAsync(Refresh(raw));

    Assert.True(result.IsSuccess);
    Assert.Contains(PlatformPermissionNames.ViewTenants, issuer.LastClaims!.Permissions); // fresh permission reflected
    Assert.Contains(PlatformPermissionNames.AdministerPlatformSupport, issuer.LastClaims.Permissions);

    await using var verify = db.CreateContext();
    var session = await verify.PlatformAuthenticationSessions.Include(s => s.RefreshTokenRecords).AsNoTracking().SingleAsync();
    Assert.Equal(AuthenticationSessionStatus.Active, session.Status);
    Assert.Equal(2, session.RefreshTokenRecords.Count);
    Assert.Single(session.RefreshTokenRecords, r => r.ConsumedUtc != null);     // predecessor consumed
    Assert.Single(session.RefreshTokenRecords, r => r.ConsumedUtc == null);     // exactly one active replacement
    Assert.NotNull(session.LastRefreshedUtc);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Refresh_reuse_of_a_consumed_token_compromises_the_session()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);
    var raw = await CreateSessionAsync(db, identityId);

    await using (var context = db.CreateContext())
    {
      Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsSuccess);
    }

    // Present the ORIGINAL (now consumed) token again.
    await using (var context = db.CreateContext())
    {
      Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsFailure);
    }

    await using var verify = db.CreateContext();
    Assert.Equal("Compromised", await ReadStringAsync(verify, "SELECT TOP 1 [Status] FROM [platform].[PlatformAuthenticationSessions]"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Refresh_denies_and_revokes_on_security_version_mismatch()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);
    var raw = await CreateSessionAsync(db, identityId);

    await using (var bump = db.CreateContext())
    {
      await bump.Database.ExecuteSqlRawAsync("UPDATE [platform].[AuthenticationAccounts] SET [SecurityVersion] = [SecurityVersion] + 1");
    }

    await using var context = db.CreateContext();
    Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsFailure);
    Assert.Equal("SecurityStateChanged", await ReadStringAsync(context, "SELECT TOP 1 [RevocationReason] FROM [platform].[PlatformAuthenticationSessions]"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Refresh_fails_closed_when_principal_disabled_account_ineligible_or_zero_permissions()
  {
    // Principal Disabled at the DB level (backstop independent of proactive revocation).
    await using (var db = await PlatformFlowSqlDatabase.CreateAsync())
    {
      var (identityId, _) = await SeedAuthorityAsync(db);
      var raw = await CreateSessionAsync(db, identityId);
      await using (var s = db.CreateContext())
      {
        await s.Database.ExecuteSqlRawAsync("UPDATE [platform].[PlatformSupportPrincipals] SET [Status] = N'Disabled'");
      }

      await using var context = db.CreateContext();
      Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsFailure);
      Assert.Equal("PlatformPrincipalIneligible", await ReadStringAsync(context, "SELECT TOP 1 [RevocationReason] FROM [platform].[PlatformAuthenticationSessions]"));
    }

    // Zero permissions (revoke Administer).
    await using (var db = await PlatformFlowSqlDatabase.CreateAsync())
    {
      var (identityId, principalId) = await SeedAuthorityAsync(db);
      var raw = await CreateSessionAsync(db, identityId);
      await RevokePermissionAsync(db, principalId, PlatformPermissionNames.AdministerPlatformSupport);

      await using var context = db.CreateContext();
      Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsFailure);
      Assert.Equal("PlatformPrincipalIneligible", await ReadStringAsync(context, "SELECT TOP 1 [RevocationReason] FROM [platform].[PlatformAuthenticationSessions]"));
    }

    // Account ineligible (disable the account → increments version AND makes ineligible; version check fires first).
    await using (var db = await PlatformFlowSqlDatabase.CreateAsync())
    {
      var (identityId, _) = await SeedAuthorityAsync(db);
      var raw = await CreateSessionAsync(db, identityId);
      await using (var a = db.CreateContext())
      {
        await a.Database.ExecuteSqlRawAsync("UPDATE [platform].[AuthenticationAccounts] SET [Status] = N'Disabled', [SecurityVersion] = [SecurityVersion] + 1");
      }

      await using var context = db.CreateContext();
      Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsFailure);
      // Either IdentityIneligible or SecurityStateChanged is an acceptable fail-closed reason; the session is revoked.
      Assert.NotEqual("Active", await ReadStringAsync(context, "SELECT TOP 1 [Status] FROM [platform].[PlatformAuthenticationSessions]"));
    }
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Refresh_denies_a_token_that_is_not_in_the_platform_store()
  {
    // A syntactically valid but unknown token (e.g. a tenant token, or a fabricated one) resolves nowhere in
    // the platform store — proving a tenant refresh token can never mint platform access.
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    await SeedAuthorityAsync(db);
    var foreign = new AuthenticationTokenService().GenerateRefreshToken(999999, Guid.NewGuid(), Client);

    await using var context = db.CreateContext();
    var command = new RefreshPlatformAuthenticationSessionCommand(
      new SensitiveAuthenticationTokenInput(foreign.SensitiveToken.RevealOnce().Value), Client);
    Assert.True((await BuildRefresh(context, new CapturingAccessTokenIssuer()).HandleAsync(command)).IsFailure);
  }

  // ---- Proactive Disable (F3C-1 / F3C-3) ----

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Disable_revokes_all_active_platform_sessions_of_the_principal_only()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var (otherIdentityId, otherPrincipalId) = await SeedAuthorityAsync(db);
    await CreateSessionAsync(db, identityId, maxSessions: 10);
    await CreateSessionAsync(db, identityId, maxSessions: 10);
    await CreateSessionAsync(db, identityId, maxSessions: 10);
    await CreateSessionAsync(db, otherIdentityId, maxSessions: 10);

    await DisablePrincipalAsync(db, principalId);

    await using var verify = db.CreateContext();
    Assert.Equal(3, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Revoked' AND [RevocationReason] = N'PlatformPrincipalIneligible'"));
    Assert.Equal(0, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Active'"));
    Assert.Equal(1, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {otherPrincipalId} AND [Status] = N'Active'"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Disable_revokes_platform_sessions_without_touching_tenant_session_or_account_security_version()
  {
    // F3C-1 consolidated: platform Disable must not increment the global account SecurityVersion and must not
    // affect the identity's tenant session.
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, principalId, accountVersion) = await SeedAuthorityWithVersionAsync(db);
    await CreateSessionAsync(db, identityId, maxSessions: 10);
    await CreateSessionAsync(db, identityId, maxSessions: 10);
    var tenantSessionId = await SeedTenantSessionAsync(db, identityId, accountVersion);

    await DisablePrincipalAsync(db, principalId);

    await using var verify = db.CreateContext();
    Assert.Equal(0, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Active'"));
    Assert.Equal("Active", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[AuthenticationSessions] WHERE [AuthenticationSessionId] = {tenantSessionId}"));
    Assert.Equal(accountVersion, await ReadInt64Async(verify, $"SELECT [SecurityVersion] FROM [platform].[AuthenticationAccounts] WHERE [IdentityId] = {identityId}"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  [Trait("AcceptanceCriteria", "AC-TEN-0066")]
  [Trait("Scenario", "TS-TEN-0104")]
  public async Task Disable_then_reenable_does_not_resurrect_revoked_platform_session()
  {
    // AC-TEN-0066 / TS-TEN-0104: a platform session revoked by principal Disable stays revoked after the
    // principal is Re-enabled, and the original refresh token can never resume it. Re-enabling restores the
    // ability to authenticate anew — it does NOT reactivate old session authority. Driven entirely through the
    // real Disable / Re-enable / refresh handlers against SQL Server.
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var raw = await CreateSessionAsync(db, identityId);

    long sessionId;
    await using (var read = db.CreateContext())
    {
      sessionId = (await read.PlatformAuthenticationSessions.AsNoTracking().SingleAsync()).Id;
    }

    // ACT 1 — real Disable revokes the active platform session.
    await DisablePrincipalAsync(db, principalId);
    await using (var afterDisable = db.CreateContext())
    {
      Assert.Equal("Disabled", await ReadStringAsync(afterDisable, $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
      Assert.Equal("Revoked", await ReadStringAsync(afterDisable, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
      Assert.Equal("PlatformPrincipalIneligible", await ReadStringAsync(afterDisable, $"SELECT [RevocationReason] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    }

    // ACT 2 — real Re-enable restores the principal but must NOT resurrect the revoked session.
    await ReenablePrincipalAsync(db, principalId);
    await using (var afterReenable = db.CreateContext())
    {
      Assert.Equal("Active", await ReadStringAsync(afterReenable, $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
      Assert.Equal("Revoked", await ReadStringAsync(afterReenable, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
      Assert.Equal("PlatformPrincipalIneligible", await ReadStringAsync(afterReenable, $"SELECT [RevocationReason] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    }

    // ACT 3 — the original refresh token cannot resume the revoked session even though the principal is Active.
    await using (var refreshContext = db.CreateContext())
    {
      Assert.True((await BuildRefresh(refreshContext, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw))).IsFailure);
    }

    // Terminal invariant: principal usable again, but the old session stays revoked and the failed refresh minted
    // no continuation (still exactly the one initial refresh record — no rotation/replacement was created).
    await using var verify = db.CreateContext();
    Assert.Equal("Active", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
    Assert.Equal("Revoked", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    Assert.Equal("PlatformPrincipalIneligible", await ReadStringAsync(verify, $"SELECT [RevocationReason] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    Assert.Equal(1, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformRefreshTokenRecords] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
  }

  // ---- Concurrency (F3C-2 + lock serialization) ----

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Concurrent_disable_and_refresh_leave_no_usable_platform_continuation()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var raw = await CreateSessionAsync(db, identityId);

    await using var refreshContext = db.CreateContext();
    await using var disableContext = db.CreateContext();
    var refreshTask = BuildRefresh(refreshContext, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw));
    var disableTask = BuildDisable(disableContext).HandleAsync(new SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommand(principalId, await PrincipalRowVersionAsync(db, principalId)));
    await Task.WhenAll(refreshTask, disableTask);

    // Terminal invariant: after both complete, the principal is Disabled and no active platform session remains.
    await using var verify = db.CreateContext();
    Assert.Equal("Disabled", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
    Assert.Equal(0, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Active'"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Concurrent_refresh_of_the_same_token_yields_at_most_one_continuation()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);
    var raw = await CreateSessionAsync(db, identityId);

    await using var contextA = db.CreateContext();
    await using var contextB = db.CreateContext();
    var results = await Task.WhenAll(
      BuildRefresh(contextA, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw)),
      BuildRefresh(contextB, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(raw)));

    Assert.True(results.Count(r => r.IsSuccess) <= 1);
    await using var verify = db.CreateContext();
    // No two sibling active replacements: at most one active (non-consumed) refresh record on the session.
    var active = await ReadInt32Async(verify, "SELECT COUNT(*) FROM [platform].[PlatformRefreshTokenRecords] WHERE [ConsumedUtc] IS NULL AND [RevokedUtc] IS NULL");
    Assert.True(active <= 1);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0022")]
  public async Task Concurrent_session_creation_respects_the_platform_session_limit()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);

    await using var contextA = db.CreateContext();
    await using var contextB = db.CreateContext();
    await Task.WhenAll(
      BuildCreator(contextA, new CapturingAccessTokenIssuer(), maxSessions: 1).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now),
      BuildCreator(contextB, new CapturingAccessTokenIssuer(), maxSessions: 1).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now));

    await using var verify = db.CreateContext();
    // The UPDLOCK/HOLDLOCK session-limit read serializes the two creators; the limit is not bypassed.
    Assert.True(await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [IdentityId] = {identityId} AND [Status] = N'Active'") <= 1);
  }

  // ---- Builders / seeding ----

  private static VerifiedIdentity Verified(long identityId) => new(identityId, 1);

  private static RefreshPlatformAuthenticationSessionCommand Refresh(string rawRefreshToken) =>
    new(new SensitiveAuthenticationTokenInput(rawRefreshToken), Client);

  private static AuthenticationPolicy Policy(int maxSessions) => new(
    12, 64, 5, TimeSpan.FromMinutes(15), 1, TimeSpan.FromDays(7), TimeSpan.FromHours(1),
    TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), TimeSpan.FromMinutes(5), maxSessions);

  private static PlatformAuthenticationSessionCreator BuildCreator(PlatformDbContext context, CapturingAccessTokenIssuer issuer, int maxSessions = 5) =>
    new(
      new PlatformAuthenticationSessionRepository(context),
      new AuthenticationAccountRepository(context),
      new PlatformSupportPrincipalRepository(context),
      new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog()),
      new PlatformAccessTokenClaimsProvider(
        new IdentityRepository(context), new AuthenticationAccountRepository(context),
        new PlatformSupportPrincipalRepository(context),
        new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog())),
      issuer,
      new AuthenticationTokenService(),
      new TestPlatformUnitOfWork(context),
      Policy(maxSessions));

  private static RefreshPlatformAuthenticationSessionCommandHandler BuildRefresh(PlatformDbContext context, CapturingAccessTokenIssuer issuer) =>
    new(
      new AuthenticationAccountRepository(context),
      new PlatformAuthenticationSessionRepository(context),
      new PlatformSupportPrincipalRepository(context),
      new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog()),
      new AllowAllClientRegistry(),
      new AuthenticationTokenService(),
      new PlatformAccessTokenClaimsProvider(
        new IdentityRepository(context), new AuthenticationAccountRepository(context),
        new PlatformSupportPrincipalRepository(context),
        new PlatformSupportPermissionReadService(context, new PlatformPermissionCatalog())),
      issuer,
      new TestPlatformUnitOfWork(context),
      Policy(5),
      new TestClock());

  private static SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommandHandler BuildDisable(PlatformDbContext context) =>
    new(
      new PlatformSupportPrincipalRepository(context),
      new PlatformAuthenticationSessionRepository(context),
      new TestPlatformUnitOfWork(context),
      new TestCurrentUser(),
      new TestClock());

  // Returns the raw refresh token string (revealed once here, reusable across refresh attempts).
  private static async Task<string> CreateSessionAsync(PlatformFlowSqlDatabase db, long identityId, int maxSessions = 5)
  {
    await using var context = db.CreateContext();
    var result = await BuildCreator(context, new CapturingAccessTokenIssuer(), maxSessions).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now);
    Assert.True(result.IsSuccess);
    return result.Value.RefreshToken.RevealOnce().Value;
  }

  private static async Task DisablePrincipalAsync(PlatformFlowSqlDatabase db, long principalId)
  {
    await using var context = db.CreateContext();
    var version = await PrincipalRowVersionAsync(db, principalId);
    Assert.True((await BuildDisable(context).HandleAsync(new SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommand(principalId, version))).IsSuccess);
  }

  private static SSAS.Platform.Application.PlatformSupport.ReenablePlatformSupportPrincipalCommandHandler BuildReenable(PlatformDbContext context) =>
    new(
      new PlatformSupportPrincipalRepository(context),
      new TestPlatformUnitOfWork(context),
      new TestCurrentUser(),
      new TestClock());

  private static async Task ReenablePrincipalAsync(PlatformFlowSqlDatabase db, long principalId)
  {
    await using var context = db.CreateContext();
    var version = await PrincipalRowVersionAsync(db, principalId);
    Assert.True((await BuildReenable(context).HandleAsync(new SSAS.Platform.Application.PlatformSupport.ReenablePlatformSupportPrincipalCommand(principalId, version))).IsSuccess);
  }

  private static async Task<byte[]> PrincipalRowVersionAsync(PlatformFlowSqlDatabase db, long principalId)
  {
    await using var context = db.CreateContext();
    return (await context.PlatformSupportPrincipals.AsNoTracking().SingleAsync(p => p.Id == principalId)).RowVersion;
  }

  private static async Task<(long IdentityId, long PrincipalId)> SeedAuthorityAsync(
    PlatformFlowSqlDatabase db, bool accountEligible = true, bool principalActive = true, bool grantPermission = true)
  {
    var seeded = await SeedAuthorityWithVersionAsync(db, accountEligible, principalActive, grantPermission);
    return (seeded.IdentityId, seeded.PrincipalId);
  }

  private static async Task<(long IdentityId, long PrincipalId, long AccountVersion)> SeedAuthorityWithVersionAsync(
    PlatformFlowSqlDatabase db, bool accountEligible = true, bool principalActive = true, bool grantPermission = true)
  {
    long identityId;
    long accountVersion;
    await using (var context = db.CreateContext())
    {
      var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
      context.Identities.Add(identity);
      Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
      identityId = identity.Id;

      var account = AuthenticationAccount.CreatePending(identityId, LoginEmail.Create($"{identityId}.op@example.com").Value);
      if (accountEligible)
      {
        Assert.True(account.CompleteInitialSetup("integration-password-hash", Guid.NewGuid(), PlatformFlowSqlDatabase.Now).IsSuccess);
      }

      context.AuthenticationAccounts.Add(account);
      Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
      accountVersion = account.SecurityVersion;
    }

    long principalId;
    await using (var context = db.CreateContext())
    {
      var principal = PlatformSupportPrincipal.Register(identityId).Value;
      await context.PlatformSupportPrincipals.AddAsync(principal);
      Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
      principalId = principal.Id;
    }

    if (grantPermission)
    {
      await GrantPermissionAsync(db, principalId, PlatformPermissionNames.AdministerPlatformSupport);
    }

    if (!principalActive)
    {
      await using var context = db.CreateContext();
      var principal = await context.PlatformSupportPrincipals.SingleAsync(p => p.Id == principalId);
      Assert.True(principal.Disable("seed", PlatformFlowSqlDatabase.Now).IsSuccess);
      Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    }

    return (identityId, principalId, accountVersion);
  }

  private static async Task GrantPermissionAsync(PlatformFlowSqlDatabase db, long principalId, string permissionName)
  {
    await using var context = db.CreateContext();
    var principal = await context.PlatformSupportPrincipals.Include(p => p.PermissionAssignments).SingleAsync(p => p.Id == principalId);
    Assert.True(new PlatformPermissionCatalog().TryGet(permissionName, out var definition));
    Assert.True(principal.GrantPermission(definition, "seed", PlatformFlowSqlDatabase.Now).IsSuccess);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
  }

  private static async Task RevokePermissionAsync(PlatformFlowSqlDatabase db, long principalId, string permissionName)
  {
    await using var context = db.CreateContext();
    var principal = await context.PlatformSupportPrincipals.Include(p => p.PermissionAssignments).SingleAsync(p => p.Id == principalId);
    Assert.True(principal.RevokePermission(PermissionName.Create(permissionName).Value, "seed", PlatformFlowSqlDatabase.Now).IsSuccess);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
  }

  private static async Task<long> SeedTenantSessionAsync(PlatformFlowSqlDatabase db, long identityId, long accountVersion)
  {
    var tenant = Tenant.Create(TenantCode.Create("AUTH1").Value, TenantName.Create("Auth Tenant").Value, "seed", Guid.NewGuid(), PlatformFlowSqlDatabase.Now).Value;
    await using (var global = db.CreateContext())
    {
      global.Tenants.Add(tenant);
      Assert.True((await new TestPlatformUnitOfWork(global).SaveChangesAsync()).IsSuccess);
      Assert.True(tenant.Activate("seed", Guid.NewGuid(), PlatformFlowSqlDatabase.Now.AddMinutes(1)).IsSuccess);
      Assert.True((await new TestPlatformUnitOfWork(global).SaveChangesAsync()).IsSuccess);
    }

    long tenantUserId;
    await using (var tenantContext = db.CreateContext(tenant.Id))
    {
      var membership = TenantUser.CreateActive(identityId, tenant.Id,
        EmailAddress.Create("member@example.com").Value, UserDisplayName.Create("Member").Value, Guid.NewGuid(), PlatformFlowSqlDatabase.Now);
      tenantContext.TenantUsers.Add(membership);
      Assert.True((await new TestPlatformUnitOfWork(tenantContext).SaveChangesAsync()).IsSuccess);
      tenantUserId = membership.Id;
    }

    await using var context = db.CreateContext(tenant.Id);
    var session = AuthenticationSession.Create(identityId, tenantUserId, tenant.Id, Client.Value, Guid.NewGuid(), accountVersion,
      PlatformFlowSqlDatabase.Now, PlatformFlowSqlDatabase.Now.AddDays(30), PlatformFlowSqlDatabase.Now.AddDays(90));
    context.AuthenticationSessions.Add(session);
    Assert.True((await new TestPlatformUnitOfWork(context).SaveChangesAsync()).IsSuccess);
    return session.Id;
  }

  private static async Task<int> ReadInt32Async(PlatformDbContext context, string sql) =>
    Convert.ToInt32(await ScalarAsync(context, sql), System.Globalization.CultureInfo.InvariantCulture);

  private static async Task<long> ReadInt64Async(PlatformDbContext context, string sql) =>
    Convert.ToInt64(await ScalarAsync(context, sql), System.Globalization.CultureInfo.InvariantCulture);

  private static async Task<string> ReadStringAsync(PlatformDbContext context, string sql) =>
    Convert.ToString(await ScalarAsync(context, sql), System.Globalization.CultureInfo.InvariantCulture)!;

  private static async Task<object?> ScalarAsync(PlatformDbContext context, string sql)
  {
    var connection = context.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
      await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return await command.ExecuteScalarAsync();
  }

  // ---- Harness ----

  private sealed class CapturingAccessTokenIssuer : IAccessTokenIssuer
  {
    public PlatformAccessTokenClaims? LastClaims { get; private set; }

    public Result<IssuedAccessToken> Issue(AccessTokenClaims claims, DateTimeOffset issuedUtc) =>
      Result.Success(new IssuedAccessToken(new SensitiveAccessToken("tenant-token"), issuedUtc.AddMinutes(15)));

    public Result<IssuedAccessToken> Issue(PlatformAccessTokenClaims claims, DateTimeOffset issuedUtc)
    {
      LastClaims = claims;
      return Result.Success(new IssuedAccessToken(new SensitiveAccessToken("platform-token"), issuedUtc.AddMinutes(15)));
    }
  }

  private sealed class AllowAllClientRegistry : IAuthenticationClientRegistry
  {
    public bool IsAllowed(AuthenticationClientId clientId) => true;
  }

  private sealed class TestPlatformUnitOfWork(PlatformDbContext context)
    : SSAS.Platform.Application.Abstractions.Persistence.IPlatformUnitOfWork
  {
    private readonly PlatformUnitOfWork inner = new(context, new NoOpDomainEventDispatcher());

    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);

    public Task<SSAS.BuildingBlocks.Application.Abstractions.Persistence.ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
      inner.BeginTransactionAsync(cancellationToken);
  }

  private sealed class PlatformFlowSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 11, 11, 0, 0, TimeSpan.Zero);

    public static async Task<PlatformFlowSqlDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP003_FLOW_{Guid.NewGuid():N}";
      var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
        "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
      var builder = new SqlConnectionStringBuilder(configured) { InitialCatalog = databaseName };
      var database = new PlatformFlowSqlDatabase(builder.ConnectionString);
      try
      {
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
      }
      catch
      {
        await database.DisposeAsync();
        throw;
      }
    }

    public PlatformDbContext CreateContext(Guid? tenantId = null)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(tenantId ?? Guid.NewGuid()), new TestClock());
    }

    public async ValueTask DisposeAsync()
    {
      await using var context = CreateContext();
      await context.Database.EnsureDeletedAsync();
    }
  }

  private sealed class NoOpDomainEventDispatcher : IDomainEventDispatcher
  {
    public Task DispatchAsync(IReadOnlyCollection<DomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

  private sealed class TestCurrentUser : ICurrentUser
  {
    public string? UserId => "integration-actor";
    public string? UserName => null;
    public string? Email => null;
    public Guid? CompanyId => null;
    public string? SessionId => null;
    public string? TokenId => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class TestCurrentTenant(Guid tenantId) : ICurrentTenant
  {
    public Guid? TenantId { get; } = tenantId;
  }

  private sealed class TestClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => PlatformFlowSqlDatabase.Now;
  }
}
