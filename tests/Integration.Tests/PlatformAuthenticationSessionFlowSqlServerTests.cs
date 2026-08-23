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

  // ---- Phase 4B: current-session logout (DEC-TEN-0023) ----

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task Logout_revokes_the_current_platform_session_with_UserLogout()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);
    await CreateSessionAsync(db, identityId);
    var sessionId = await SingleSessionIdAsync(db);

    await using (var context = db.CreateContext())
    {
      var result = await BuildLogout(context).HandleAsync(new RevokeCurrentPlatformAuthenticationSessionCommand(sessionId, identityId));
      Assert.True(result.IsSuccess);
    }

    await using var verify = db.CreateContext();
    Assert.Equal("Revoked", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    Assert.Equal("UserLogout", await ReadStringAsync(verify, $"SELECT [RevocationReason] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task Logout_is_idempotent_and_fail_closed_for_a_foreign_or_already_revoked_session()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _) = await SeedAuthorityAsync(db);
    var (otherIdentityId, _) = await SeedAuthorityAsync(db);
    await CreateSessionAsync(db, identityId);
    var sessionId = await SingleSessionIdAsync(db);

    // A different identity can never revoke this session — the ownership check makes it a silent no-op success.
    await using (var context = db.CreateContext())
    {
      Assert.True((await BuildLogout(context).HandleAsync(new RevokeCurrentPlatformAuthenticationSessionCommand(sessionId, otherIdentityId))).IsSuccess);
    }
    await using (var stillActive = db.CreateContext())
    {
      Assert.Equal("Active", await ReadStringAsync(stillActive, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    }

    // The owner logs out, then logs out again: the second call is an idempotent no-op success with no state change.
    await using (var first = db.CreateContext())
    {
      Assert.True((await BuildLogout(first).HandleAsync(new RevokeCurrentPlatformAuthenticationSessionCommand(sessionId, identityId))).IsSuccess);
    }
    await using (var second = db.CreateContext())
    {
      Assert.True((await BuildLogout(second).HandleAsync(new RevokeCurrentPlatformAuthenticationSessionCommand(sessionId, identityId))).IsSuccess);
    }

    await using var verify = db.CreateContext();
    Assert.Equal("Revoked", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    Assert.Equal("UserLogout", await ReadStringAsync(verify, $"SELECT [RevocationReason] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task Logout_revokes_the_platform_session_without_touching_the_tenant_session()
  {
    // Platform-store-only: logout revokes the platform session and leaves the identity's tenant session Active.
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    var (identityId, _, accountVersion) = await SeedAuthorityWithVersionAsync(db);
    await CreateSessionAsync(db, identityId);
    var tenantSessionId = await SeedTenantSessionAsync(db, identityId, accountVersion);
    var sessionId = await SingleSessionIdAsync(db);

    await using (var context = db.CreateContext())
    {
      Assert.True((await BuildLogout(context).HandleAsync(new RevokeCurrentPlatformAuthenticationSessionCommand(sessionId, identityId))).IsSuccess);
    }

    await using var verify = db.CreateContext();
    Assert.Equal("Revoked", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"));
    Assert.Equal("Active", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[AuthenticationSessions] WHERE [AuthenticationSessionId] = {tenantSessionId}"));
  }

  // ---- Phase 4B: L1 create-vs-disable serialization (DEC-TEN-0023) ----

  // The L1 invariant (DEC-TEN-0023): platform-session creation serializes its Active-eligibility decision against
  // a concurrent principal Disable on a transactionally-effective lock. The GLOBAL LOCK ORDER is
  // account -> principal -> session(s): both flows take the principal lock FIRST, so the principal row is the
  // single first-contended resource and no flow ever holds a session lock while waiting for the principal lock.
  //
  // Regression history: an earlier ordering (create: session -> principal; disable: session -> principal-at-save)
  // deadlocked under production-representative volume, because the two session range reads use DIFFERENT
  // nonclustered indexes (IdentityId,Status,... vs PlatformSupportPrincipalId,Status) and therefore do not
  // serialize each other, while creation's INSERT had to write both. These tests seed enough rows to reproduce
  // that seek-based regime, and drive each interleaving deterministically with a SQL lock gate.

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task L1_create_first_commits_the_session_and_the_disable_then_revokes_it()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    await SeedSeekVolumeAsync(db);
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var rowVersion = await PrincipalRowVersionAsync(db, principalId);
    var seeksBefore = await ReadSessionIndexSeeksAsync(db);

    // Gate creation at its SESSION-range step; by then it already holds the account and principal locks.
    await using var gate = await LockGate.HoldAsync(db.ConnectionString,
      $"SELECT * FROM [platform].[PlatformAuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId} AND [Status] = N'Active'");

    await using var createContext = db.CreateContext();
    var createTask = BuildCreator(createContext, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now);
    await Task.Delay(TimeSpan.FromSeconds(2));
    Assert.False(createTask.IsCompleted); // creation is parked on the gate while HOLDING the principal lock

    await using var disableContext = db.CreateContext();
    var disableTask = BuildDisable(disableContext).HandleAsync(
      new SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommand(principalId, rowVersion));
    await Task.Delay(TimeSpan.FromSeconds(2));
    Assert.False(disableTask.IsCompleted); // Disable is serialized on the principal lock — it cannot overtake

    await gate.ReleaseAsync();
    var created = await createTask;
    var disabled = await disableTask;

    Assert.True(created.IsSuccess);  // create-first genuinely won the race
    Assert.True(disabled.IsSuccess); // and no deadlock victim was chosen
    await AssertDisabledWithNoUsableContinuationAsync(db, identityId, principalId, created.Value.RefreshToken.RevealOnce().Value);
    await AssertBothSessionIndexesWereSoughtAsync(db, seeksBefore);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task L1_disable_first_makes_the_concurrent_creation_fail_closed()
  {
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    await SeedSeekVolumeAsync(db);
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var rowVersion = await PrincipalRowVersionAsync(db, principalId);
    var seeksBefore = await ReadSessionIndexSeeksAsync(db);

    // Gate Disable at its SESSION-range step; by then it already holds the principal lock.
    await using var gate = await LockGate.HoldAsync(db.ConnectionString,
      $"SELECT * FROM [platform].[PlatformAuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Active'");

    await using var disableContext = db.CreateContext();
    var disableTask = BuildDisable(disableContext).HandleAsync(
      new SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommand(principalId, rowVersion));
    await Task.Delay(TimeSpan.FromSeconds(2));
    Assert.False(disableTask.IsCompleted); // Disable is parked on the gate while HOLDING the principal lock

    await using var createContext = db.CreateContext();
    var createTask = BuildCreator(createContext, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now);
    await Task.Delay(TimeSpan.FromSeconds(2));
    Assert.False(createTask.IsCompleted); // creation is serialized on the principal lock

    await gate.ReleaseAsync();
    var disabled = await disableTask;
    var created = await createTask;

    Assert.True(disabled.IsSuccess);
    Assert.True(created.IsFailure); // creation observed the committed Disable and failed closed
    Assert.Equal(PlatformSupportErrors.PrincipalDisabled, created.Error);
    await AssertDisabledWithNoUsableContinuationAsync(db, identityId, principalId, refreshToken: null);
    await AssertBothSessionIndexesWereSoughtAsync(db, seeksBefore);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task L1_holds_when_read_committed_snapshot_isolation_is_disabled()
  {
    // The guarantee must come from the UPDLOCK/HOLDLOCK reads, NOT the deployment isolation level. EF Core's SQL
    // Server database creator turns RCSI ON for the databases it creates, so every other test here already runs
    // with RCSI ON; this one proves the opposite regime — RCSI OFF, where readers take shared locks — behaves
    // identically. Both isolation regimes are therefore covered.
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    Assert.True(await db.ReadCommittedSnapshotEnabledAsync()); // original (EF-created) state: ON
    await db.SetReadCommittedSnapshotAsync(false);
    Assert.False(await db.ReadCommittedSnapshotEnabledAsync()); // RCSI is OFF for this run

    await SeedSeekVolumeAsync(db);
    var (identityId, principalId) = await SeedAuthorityAsync(db);
    var rowVersion = await PrincipalRowVersionAsync(db, principalId);

    await using var gate = await LockGate.HoldAsync(db.ConnectionString,
      $"SELECT * FROM [platform].[PlatformAuthenticationSessions] WITH (UPDLOCK, HOLDLOCK) WHERE [IdentityId] = {identityId} AND [Status] = N'Active'");

    await using var createContext = db.CreateContext();
    var createTask = BuildCreator(createContext, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now);
    await Task.Delay(TimeSpan.FromSeconds(2));
    Assert.False(createTask.IsCompleted);

    await using var disableContext = db.CreateContext();
    var disableTask = BuildDisable(disableContext).HandleAsync(
      new SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommand(principalId, rowVersion));
    await Task.Delay(TimeSpan.FromSeconds(2));
    Assert.False(disableTask.IsCompleted); // U-lock serialization is unaffected by snapshot reads

    await gate.ReleaseAsync();
    var created = await createTask;
    var disabled = await disableTask;

    Assert.True(created.IsSuccess);
    Assert.True(disabled.IsSuccess);
    await AssertDisabledWithNoUsableContinuationAsync(db, identityId, principalId, created.Value.RefreshToken.RevealOnce().Value);
  }

  [Fact]
  [Trait("Decision", "DEC-TEN-0023")]
  public async Task L1_concurrent_create_and_disable_stress_never_deadlocks_on_seeded_volume()
  {
    // Supplementary unsynchronised stress on seek-producing data: whichever flow wins, the terminal invariant
    // holds and NEITHER side may fail from a deadlock (1205 surfaces as WriteFailure through the UnitOfWork).
    await using var db = await PlatformFlowSqlDatabase.CreateAsync();
    await SeedSeekVolumeAsync(db);
    var seeksBefore = await ReadSessionIndexSeeksAsync(db);

    var authorities = new List<(long IdentityId, long PrincipalId)>();
    for (var index = 0; index < 8; index++)
    {
      authorities.Add(await SeedAuthorityAsync(db));
    }

    foreach (var (identityId, principalId) in authorities)
    {
      var rowVersion = await PrincipalRowVersionAsync(db, principalId);
      await using var createContext = db.CreateContext();
      await using var disableContext = db.CreateContext();
      var createTask = BuildCreator(createContext, new CapturingAccessTokenIssuer()).CreateAsync(Verified(identityId), Client, PlatformFlowSqlDatabase.Now);
      var disableTask = BuildDisable(disableContext).HandleAsync(
        new SSAS.Platform.Application.PlatformSupport.DisablePlatformSupportPrincipalCommand(principalId, rowVersion));
      var createResult = await createTask;
      var disableResult = await disableTask;

      // Disable always commits: creation only READS the principal under lock, so the optimistic token stays valid.
      Assert.True(disableResult.IsSuccess);
      // A deadlock would surface as a write failure on either side — none is tolerated.
      Assert.NotEqual(IdentityAccessErrors.WriteFailure, disableResult.Error);
      if (createResult.IsFailure)
      {
        Assert.NotEqual(IdentityAccessErrors.WriteFailure, createResult.Error);
      }

      await using var verify = db.CreateContext();
      Assert.Equal("Disabled", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
      Assert.Equal(0, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Active'"));
    }

    await AssertBothSessionIndexesWereSoughtAsync(db, seeksBefore);
  }

  // Terminal state after a committed Disable: principal Disabled, zero active platform sessions, and any refresh
  // token minted by the racing creation is unusable.
  private static async Task AssertDisabledWithNoUsableContinuationAsync(
    PlatformFlowSqlDatabase db, long identityId, long principalId, string? refreshToken)
  {
    await using var verify = db.CreateContext();
    Assert.Equal("Disabled", await ReadStringAsync(verify, $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"));
    Assert.Equal(0, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformSupportPrincipalId] = {principalId} AND [Status] = N'Active'"));
    Assert.Equal(0, await ReadInt32Async(verify, $"SELECT COUNT(*) FROM [platform].[PlatformAuthenticationSessions] WHERE [IdentityId] = {identityId} AND [Status] = N'Active'"));

    if (refreshToken is not null)
    {
      await using var refreshContext = db.CreateContext();
      Assert.True((await BuildRefresh(refreshContext, new CapturingAccessTokenIssuer()).HandleAsync(Refresh(refreshToken))).IsFailure);
    }
  }

  // Index-usage counters are cumulative, so they are sampled BEFORE and AFTER the racing operations: the delta
  // attributes the seeks to the flows under test rather than to seeding.
  private static async Task<(long Identity, long Principal)> ReadSessionIndexSeeksAsync(PlatformFlowSqlDatabase db)
  {
    await using var context = db.CreateContext();
    return (
      await ReadInt64Async(context, SeekCountSql("IdentityId")),
      await ReadInt64Async(context, SeekCountSql("PlatformSupportPrincipalId")));
  }

  // Evidence that the seeded volume really produced index SEEKS on the two distinct session indexes — the regime
  // in which the two range locks are independent, and in which the previous ordering deadlocked.
  private static async Task AssertBothSessionIndexesWereSoughtAsync(
    PlatformFlowSqlDatabase db, (long Identity, long Principal) before)
  {
    var after = await ReadSessionIndexSeeksAsync(db);
    Assert.True(after.Identity > before.Identity, "Expected an index SEEK on the (IdentityId, Status, ...) session index.");
    Assert.True(after.Principal > before.Principal, "Expected an index SEEK on the (PlatformSupportPrincipalId, Status) session index.");
  }

  private static string SeekCountSql(string leadingColumn) => $"""
    SELECT ISNULL(SUM(usage.user_seeks), 0)
    FROM sys.indexes AS ix
    JOIN sys.index_columns AS ic ON ic.object_id = ix.object_id AND ic.index_id = ix.index_id AND ic.key_ordinal = 1
    JOIN sys.columns AS col ON col.object_id = ix.object_id AND col.column_id = ic.column_id
    LEFT JOIN sys.dm_db_index_usage_stats AS usage ON usage.object_id = ix.object_id
      AND usage.index_id = ix.index_id AND usage.database_id = DB_ID()
    WHERE ix.object_id = OBJECT_ID('[platform].[PlatformAuthenticationSessions]')
      AND ix.type_desc = 'NONCLUSTERED' AND col.name = N'{leadingColumn}'
    """;

  // Seeds enough principals/sessions that the optimizer chooses index SEEKS on the two session indexes rather
  // than scanning a nearly empty table (a scan would serialize the flows for the wrong reason and hide the very
  // regime the L1 fix has to survive). The threshold was measured, not guessed: at 50 principals / 250 sessions
  // the principal/status query still SCANS, while from ~100 principals / 500 sessions both queries seek. 120
  // principals / 600 sessions keeps a margin above that boundary at a fraction of the earlier setup cost.
  private static async Task SeedSeekVolumeAsync(PlatformFlowSqlDatabase db, int principalCount = 120, int sessionsEach = 5)
  {
    await using var context = db.CreateContext();
    var unitOfWork = new TestPlatformUnitOfWork(context);

    var identities = Enumerable.Range(0, principalCount)
      .Select(_ => Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value))
      .ToList();
    context.Identities.AddRange(identities);
    Assert.True((await unitOfWork.SaveChangesAsync()).IsSuccess);

    var principals = identities.Select(identity => PlatformSupportPrincipal.Register(identity.Id).Value).ToList();
    context.PlatformSupportPrincipals.AddRange(principals);
    Assert.True((await unitOfWork.SaveChangesAsync()).IsSuccess);

    var sessions = principals
      .SelectMany(principal => Enumerable.Range(0, sessionsEach).Select(_ => PlatformAuthenticationSession.Create(
        principal.IdentityId, principal.Id, Client.Value, Guid.NewGuid(), 1,
        PlatformFlowSqlDatabase.Now, PlatformFlowSqlDatabase.Now.AddDays(1), PlatformFlowSqlDatabase.Now.AddDays(7))))
      .ToList();
    context.PlatformAuthenticationSessions.AddRange(sessions);
    Assert.True((await unitOfWork.SaveChangesAsync()).IsSuccess);

    await context.Database.ExecuteSqlRawAsync("UPDATE STATISTICS [platform].[PlatformAuthenticationSessions]; UPDATE STATISTICS [platform].[PlatformSupportPrincipals];");
  }

  private static async Task<long> SingleSessionIdAsync(PlatformFlowSqlDatabase db)
  {
    await using var read = db.CreateContext();
    return (await read.PlatformAuthenticationSessions.AsNoTracking().SingleAsync()).Id;
  }

  private static RevokeCurrentPlatformAuthenticationSessionCommandHandler BuildLogout(PlatformDbContext context) =>
    new(new PlatformAuthenticationSessionRepository(context), new TestPlatformUnitOfWork(context), new TestClock());

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

  // Holds a real UPDLOCK/HOLDLOCK on a chosen range from an INDEPENDENT connection/transaction, so a test can
  // deterministically stop a production flow at a known step (no production timing hooks, no sleeps in
  // production code). Releasing the gate lets the blocked flow continue.
  private sealed class LockGate : IAsyncDisposable
  {
    private readonly SqlConnection connection;
    private SqlTransaction? transaction;

    private LockGate(SqlConnection connection, SqlTransaction transaction)
    {
      this.connection = connection;
      this.transaction = transaction;
    }

    public static async Task<LockGate> HoldAsync(string connectionString, string sql)
    {
      var connection = new SqlConnection(connectionString);
      await connection.OpenAsync();
      var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
      await using var command = connection.CreateCommand();
      command.Transaction = transaction;
      command.CommandText = sql;
      await using var reader = await command.ExecuteReaderAsync();
      while (await reader.ReadAsync())
      {
      }

      return new LockGate(connection, transaction);
    }

    // Releases the held locks (rollback: the gate never mutates state).
    public async Task ReleaseAsync()
    {
      if (transaction is null)
      {
        return;
      }

      await transaction.RollbackAsync();
      await transaction.DisposeAsync();
      transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
      await ReleaseAsync();
      await connection.DisposeAsync();
    }
  }

  private sealed class PlatformFlowSqlDatabase(string connectionString) : IAsyncDisposable
  {
    public static readonly DateTimeOffset Now = new(2026, 8, 11, 11, 0, 0, TimeSpan.Zero);
    private const int SetupCommandTimeoutSeconds = 120;

    public string ConnectionString => connectionString;

    public static async Task<PlatformFlowSqlDatabase> CreateAsync()
    {
      var databaseName = $"SSAS_ERP_FP003_FLOW_{Guid.NewGuid():N}";
      var configured = IntegrationSqlEnvironment.BaseConnectionString;
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

    // TEST-ONLY command timeout. Integration test classes run as parallel xUnit collections and each test creates
    // and migrates its own database, so a CREATE DATABASE/migration can legitimately exceed the 30s EF default
    // under local SQL Server contention (observed as a MigrateAsync timeout in an unrelated sibling test). 120s
    // absorbs that contention while still failing a genuine hang. Production configuration is untouched.
    public PlatformDbContext CreateContext(Guid? tenantId = null)
    {
      var options = new DbContextOptionsBuilder<PlatformDbContext>()
        .UseSqlServer(connectionString, sql => sql
          .MigrationsHistoryTable("__EFMigrationsHistory", "platform")
          .CommandTimeout(SetupCommandTimeoutSeconds))
        .Options;
      return new PlatformDbContext(options, new TestCurrentUser(), new TestCurrentTenant(tenantId ?? Guid.NewGuid()), new TestClock());
    }

    // Sets READ_COMMITTED_SNAPSHOT for this isolated test database, run from a master connection with
    // ROLLBACK IMMEDIATE so any idle pooled connection cannot block the ALTER. Note: EF Core's SQL Server
    // database creator turns RCSI ON for databases it creates, so the migrated default here is ON.
    public async Task SetReadCommittedSnapshotAsync(bool enabled)
    {
      var target = new SqlConnectionStringBuilder(connectionString);
      var databaseName = target.InitialCatalog;
      target.InitialCatalog = "master";
      SqlConnection.ClearAllPools();
      await using var connection = new SqlConnection(target.ConnectionString);
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = $"ALTER DATABASE [{databaseName}] SET READ_COMMITTED_SNAPSHOT {(enabled ? "ON" : "OFF")} WITH ROLLBACK IMMEDIATE;";
      await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ReadCommittedSnapshotEnabledAsync()
    {
      await using var context = CreateContext();
      var connection = context.Database.GetDbConnection();
      await connection.OpenAsync();
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID();";
      return Convert.ToBoolean(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
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
