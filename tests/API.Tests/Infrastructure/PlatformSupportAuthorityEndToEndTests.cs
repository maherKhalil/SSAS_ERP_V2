using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Host.API.Authentication;
using SSAS.Host.API.Authorization;
using SSAS.Host.API.Configuration;
using SSAS.Host.API.Diagnostics;
using SSAS.Platform.API;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.API.PlatformSupport;
using SSAS.Platform.Application.Authentication;
using SSAS.Platform.Application.Permissions;
using SSAS.Platform.Application.PlatformSupport;
using SSAS.Platform.Domain;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Enums;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.PlatformSupport;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure;
using SSAS.Platform.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.RequestContext;

namespace SSAS.API.Tests.Infrastructure;

// Phase-4D positive authority-administration E2E (ADR-016 §5 slice 4D). Real HTTPS requests hit the real
// authority routes on a host wired to real platform persistence (SQL Server), authorized by a REAL signed
// platform JWT carrying Platform.Support.Administer through the committed Phase-4A policy.
//
// The release-critical cases are the DEC-TEN-0026 ones: self-revoke of Administer, revoking the LAST usable
// Administer, self-disable, and last-admin self-disable must all SUCCEED. There is no preventive guard; the
// committed Phase-4D-0 recovery is the safety mechanism, and these tests assert the resulting live authority
// state rather than expecting the mutation to be refused.
[Collection(PlatformSupportAuthorityEndToEndGroup.Name)]
public sealed class PlatformSupportAuthorityEndToEndTests(PlatformSupportAuthorityEndToEndHost host)
{
  private const string Prefix = "/api/platform/support/principals";
  // The single cached Web (camelCase) serializer instance lives on the host and is shared by both classes.
  private static readonly JsonSerializerOptions JsonOptions = PlatformSupportAuthorityEndToEndHost.JsonOptions;

  // ---- Register ----

  [Fact]
  public async Task Register_creates_exactly_one_principal_and_creates_no_identity_or_account()
  {
    var identityId = await host.SeedIdentityAsync();
    var identitiesBefore = await host.ScalarAsync("SELECT COUNT(*) FROM [platform].[Identities]");
    var accountsBefore = await host.ScalarAsync("SELECT COUNT(*) FROM [platform].[AuthenticationAccounts]");

    var response = await host.SendAsync(HttpMethod.Post, Prefix, new { identityId });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    Assert.Equal(1, Convert.ToInt32(await host.ScalarAsync(
      $"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE [IdentityId] = {identityId}"), CultureInfo.InvariantCulture));
    // Registration never creates identities/accounts, and never auto-grants any permission.
    Assert.Equal(identitiesBefore, await host.ScalarAsync("SELECT COUNT(*) FROM [platform].[Identities]"));
    Assert.Equal(accountsBefore, await host.ScalarAsync("SELECT COUNT(*) FROM [platform].[AuthenticationAccounts]"));
    var principalId = Convert.ToInt64(await host.ScalarAsync(
      $"SELECT [PlatformSupportPrincipalId] FROM [platform].[PlatformSupportPrincipals] WHERE [IdentityId] = {identityId}"), CultureInfo.InvariantCulture);
    Assert.Equal(0, Convert.ToInt32(await host.ScalarAsync(
      $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE [PlatformSupportPrincipalId] = {principalId}"), CultureInfo.InvariantCulture));
  }

  [Fact]
  public async Task Register_for_an_already_registered_identity_conflicts_without_creating_a_duplicate()
  {
    var identityId = await host.SeedIdentityAsync();
    Assert.Equal(HttpStatusCode.Created, (await host.SendAsync(HttpMethod.Post, Prefix, new { identityId })).StatusCode);

    var response = await host.SendAsync(HttpMethod.Post, Prefix, new { identityId });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    await AssertCodeAsync(response, "platform_support.principal_conflict");
    Assert.Equal(1, Convert.ToInt32(await host.ScalarAsync(
      $"SELECT COUNT(*) FROM [platform].[PlatformSupportPrincipals] WHERE [IdentityId] = {identityId}"), CultureInfo.InvariantCulture));
  }

  // ---- Grant / Revoke ----

  [Fact]
  public async Task Grant_persists_one_active_assignment_and_rejects_invalid_or_disabled_targets()
  {
    var principalId = await host.SeedPrincipalAsync();

    var granted = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/grant",
      new { permissionName = PlatformPermissionNames.ViewTenants });
    Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
    Assert.Equal(1, await host.ActiveAssignmentCountAsync(principalId, PlatformPermissionNames.ViewTenants));

    // Tenant-scoped permission and unknown permission are caller-input errors, not authorization failures.
    var tenantScoped = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/grant",
      new { permissionName = PlatformPermissionNames.ViewCompanies });
    Assert.Equal(HttpStatusCode.BadRequest, tenantScoped.StatusCode);

    var unknown = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/grant",
      new { permissionName = "Platform.Support.DoesNotExist" });
    Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

    // Duplicate active grant is refused by the filtered uniqueness contract.
    var duplicate = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/grant",
      new { permissionName = PlatformPermissionNames.ViewTenants });
    Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    Assert.Equal(1, await host.ActiveAssignmentCountAsync(principalId, PlatformPermissionNames.ViewTenants));

    // Grant to a Disabled principal is rejected (DEC-TEN-0020).
    await host.DisableDirectAsync(principalId);
    var onDisabled = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/grant",
      new { permissionName = PlatformPermissionNames.ManageTenants });
    Assert.Equal(HttpStatusCode.Conflict, onDisabled.StatusCode);
    await AssertCodeAsync(onDisabled, "platform_support.principal_disabled");
  }

  [Fact]
  public async Task Revoke_soft_removes_the_assignment_and_is_allowed_on_a_disabled_principal()
  {
    var principalId = await host.SeedPrincipalAsync(PlatformPermissionNames.ViewTenants);

    var revoked = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/revoke",
      new { permissionName = PlatformPermissionNames.ViewTenants });

    Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
    Assert.Equal(0, await host.ActiveAssignmentCountAsync(principalId, PlatformPermissionNames.ViewTenants));
    // History is retained, not physically deleted, with revoke audit metadata.
    Assert.Equal(1, Convert.ToInt32(await host.ScalarAsync(
      $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE [PlatformSupportPrincipalId] = {principalId} AND [RemovedUtc] IS NOT NULL AND [RemovedBy] IS NOT NULL"), CultureInfo.InvariantCulture));

    // Revoke remains ALLOWED on a Disabled principal (cleanup semantics).
    var second = await host.SeedPrincipalAsync(PlatformPermissionNames.ManageTenants);
    await host.DisableDirectAsync(second);
    var onDisabled = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{second}/revoke",
      new { permissionName = PlatformPermissionNames.ManageTenants });
    Assert.Equal(HttpStatusCode.NoContent, onDisabled.StatusCode);
    Assert.Equal(0, await host.ActiveAssignmentCountAsync(second, PlatformPermissionNames.ManageTenants));
  }

  // ---- DEC-TEN-0026 : self-mutation and last-admin ----

  [Fact]
  public async Task Self_revoke_of_administer_succeeds_and_the_issued_token_keeps_its_claim_until_expiry()
  {
    // The caller revokes their OWN Administer. This MUST succeed: authorization is evaluated from the valid
    // incoming JWT and is never re-evaluated against persisted state after the mutation.
    var actor = await host.SeedActorAsync(PlatformPermissionNames.AdministerPlatformSupport);

    var response = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{actor.PrincipalId}/revoke",
      new { permissionName = PlatformPermissionNames.AdministerPlatformSupport }, actor.Token);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal(0, await host.ActiveAssignmentCountAsync(actor.PrincipalId, PlatformPermissionNames.AdministerPlatformSupport));

    // Approved stateless consequence: the already-issued JWT still carries Administer, so the SAME token is
    // still accepted by the claim-based policy. This is expected and must not be "fixed" into an immediate 403.
    var stillAuthorized = await host.SendAsync(HttpMethod.Get, $"{Prefix}/{actor.PrincipalId}", token: actor.Token);
    Assert.Equal(HttpStatusCode.OK, stillAuthorized.StatusCode);
  }

  [Fact]
  public async Task Revoking_the_last_administer_succeeds_and_makes_administrative_recovery_eligible()
  {
    // Actor is the ONLY usable Administer and also holds View, so general authority survives the revoke while
    // administrative authority does not — the exact DEC-TEN-0026 lockout state.
    var actor = await host.SeedActorAsync(
      PlatformPermissionNames.AdministerPlatformSupport, PlatformPermissionNames.ViewTenants);
    // Shared-database premise: make the actor the ONLY usable administrator, independent of test order.
    await host.EnsureOnlyAdministrativePrincipalAsync(actor.PrincipalId);
    Assert.True(await host.HasAdministrativeAuthorityAsync());

    var response = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{actor.PrincipalId}/revoke",
      new { permissionName = PlatformPermissionNames.AdministerPlatformSupport }, actor.Token);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.True(await host.HasGeneralAuthorityAsync());        // View survives
    Assert.False(await host.HasAdministrativeAuthorityAsync()); // nobody can administer -> recovery eligible
  }

  [Fact]
  public async Task Self_disable_succeeds_revokes_platform_sessions_and_leaves_security_version_untouched()
  {
    var actor = await host.SeedActorAsync(PlatformPermissionNames.AdministerPlatformSupport);
    var sessionId = await host.SeedPlatformSessionAsync(actor.IdentityId, actor.PrincipalId);
    // Shared-database premise: make the actor the ONLY usable administrator, independent of test order.
    await host.EnsureOnlyAdministrativePrincipalAsync(actor.PrincipalId);
    Assert.True(await host.HasAdministrativeAuthorityAsync());
    var securityVersionBefore = await host.ScalarAsync(
      $"SELECT [SecurityVersion] FROM [platform].[AuthenticationAccounts] WHERE [IdentityId] = {actor.IdentityId}");

    var response = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{actor.PrincipalId}/disable",
      new { expectedRowVersion = await host.RowVersionAsync(actor.PrincipalId) }, actor.Token);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal("Disabled", Convert.ToString(await host.ScalarAsync(
      $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {actor.PrincipalId}"), CultureInfo.InvariantCulture));
    // Platform sessions are revoked proactively...
    Assert.Equal("Revoked", Convert.ToString(await host.ScalarAsync(
      $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"), CultureInfo.InvariantCulture));
    // Tenant-session isolation for Disable is proven authoritatively by the Phase-4B SQL test that seeds a real
    // Tenant/TenantUser/AuthenticationSession graph; it is not duplicated here.
    // ...and the global account SecurityVersion is unchanged (platform Disable is plane-local).
    Assert.Equal(securityVersionBefore, await host.ScalarAsync(
      $"SELECT [SecurityVersion] FROM [platform].[AuthenticationAccounts] WHERE [IdentityId] = {actor.IdentityId}"));
    // Administrative authority is gone -> recovery becomes eligible. No preventive guard blocked the mutation.
    Assert.False(await host.HasAdministrativeAuthorityAsync());
  }

  [Fact]
  public async Task Re_enable_restores_retained_administer_without_resurrecting_revoked_sessions()
  {
    var actor = await host.SeedActorAsync(PlatformPermissionNames.AdministerPlatformSupport);
    var sessionId = await host.SeedPlatformSessionAsync(actor.IdentityId, actor.PrincipalId);
    await host.SendAsync(HttpMethod.Post, $"{Prefix}/{actor.PrincipalId}/disable",
      new { expectedRowVersion = await host.RowVersionAsync(actor.PrincipalId) }, actor.Token);
    Assert.False(await host.HasAdministrativeAuthorityAsync());

    var response = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{actor.PrincipalId}/reenable",
      new { expectedRowVersion = await host.RowVersionAsync(actor.PrincipalId) }, actor.Token);

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    Assert.Equal("Active", Convert.ToString(await host.ScalarAsync(
      $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {actor.PrincipalId}"), CultureInfo.InvariantCulture));
    // Retained Administer is usable again...
    Assert.True(await host.HasAdministrativeAuthorityAsync());
    // ...but sessions revoked by Disable stay revoked; re-enable never resurrects authentication state.
    Assert.Equal("Revoked", Convert.ToString(await host.ScalarAsync(
      $"SELECT [Status] FROM [platform].[PlatformAuthenticationSessions] WHERE [PlatformAuthenticationSessionId] = {sessionId}"), CultureInfo.InvariantCulture));
  }

  [Fact]
  public async Task A_stale_row_version_is_rejected_for_both_lifecycle_transitions_without_mutating_state()
  {
    var principalId = await host.SeedPrincipalAsync();
    var stale = await host.RowVersionAsync(principalId);
    // Move the row on so the captured token is stale.
    await host.DisableDirectAsync(principalId);

    var reenableStale = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/reenable",
      new { expectedRowVersion = stale });

    Assert.Equal(HttpStatusCode.Conflict, reenableStale.StatusCode);
    await AssertCodeAsync(reenableStale, "concurrency.conflict");
    Assert.Equal("Disabled", Convert.ToString(await host.ScalarAsync(
      $"SELECT [Status] FROM [platform].[PlatformSupportPrincipals] WHERE [PlatformSupportPrincipalId] = {principalId}"), CultureInfo.InvariantCulture));

    var disableStale = await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/disable",
      new { expectedRowVersion = stale });
    Assert.Equal(HttpStatusCode.Conflict, disableStale.StatusCode);
  }

  // ---- Reads (DEC-TEN-0025: same Administer permission) ----

  [Fact]
  public async Task Authority_reads_project_transport_dtos_for_an_administer_caller()
  {
    var principalId = await host.SeedPrincipalAsync(PlatformPermissionNames.ViewTenants);
    await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/revoke",
      new { permissionName = PlatformPermissionNames.ViewTenants });
    await host.SendAsync(HttpMethod.Post, $"{Prefix}/{principalId}/grant",
      new { permissionName = PlatformPermissionNames.ManageTenants });

    var list = await host.SendAsync(HttpMethod.Get, $"{Prefix}?pageNumber=1&pageSize=50");
    Assert.Equal(HttpStatusCode.OK, list.StatusCode);

    var get = await host.SendAsync(HttpMethod.Get, $"{Prefix}/{principalId}");
    Assert.Equal(HttpStatusCode.OK, get.StatusCode);

    // History keeps the revoked record alongside the active one.
    var assignments = await host.SendAsync(HttpMethod.Get, $"{Prefix}/{principalId}/assignments");
    Assert.Equal(HttpStatusCode.OK, assignments.StatusCode);
    var history = JsonSerializer.Deserialize<PlatformPermissionAssignmentResponse[]>(
      await assignments.Content.ReadAsStringAsync(), JsonOptions)!;
    Assert.Contains(history, item => item.PermissionName == PlatformPermissionNames.ViewTenants && !item.IsActive);
    Assert.Contains(history, item => item.PermissionName == PlatformPermissionNames.ManageTenants && item.IsActive);

    // The active projection is the current catalog-filtered set only.
    var permissions = await host.SendAsync(HttpMethod.Get, $"{Prefix}/{principalId}/permissions");
    Assert.Equal(HttpStatusCode.OK, permissions.StatusCode);
    var active = JsonSerializer.Deserialize<PlatformSupportActivePermissionsResponse>(
      await permissions.Content.ReadAsStringAsync(), JsonOptions)!;
    Assert.Equal([PlatformPermissionNames.ManageTenants], active.PermissionNames);
  }

  private static async Task AssertCodeAsync(HttpResponseMessage response, string expected)
  {
    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.Equal(expected, document.RootElement.GetProperty("code").GetString());
  }
}

// One real host + one real database for the authority E2E collection.
public sealed class PlatformSupportAuthorityEndToEndHost : IAsyncLifetime
{
  private const string Issuer = "https://platform-authority-e2e.tests";
  private const string Audience = "platform-authority-e2e-tests";
  private const string Origin = "https://localhost:4200";
  private static readonly DateTimeOffset Now = new(2026, 8, 13, 11, 0, 0, TimeSpan.Zero);
  // Web (camelCase) naming: the strict transport reader enforces an exact camelCase property allow-list.
  // Cached once (CA1869) and shared with the test class.
  internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private WebApplication? application;
  private HttpClient? client;
  private string connectionString = string.Empty;
  private string adminToken = string.Empty;

  public sealed record Actor(long IdentityId, long PrincipalId, string Token);

  public async Task InitializeAsync()
  {
    var databaseName = $"SSAS_ERP_FP003_4D_{Guid.NewGuid():N}";
    var configured = Environment.GetEnvironmentVariable("SSAS_TEST_SQLSERVER") ??
      "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False";
    // TEST-ONLY timeouts on the test's own connection string (production configuration is untouched). This
    // host creates and migrates a database while other suites hammer the local SQL Server, where both the
    // pre-login handshake and migration commands have been observed to exceed the 15s/30s defaults.
    connectionString = new SqlConnectionStringBuilder(configured)
    {
      InitialCatalog = databaseName,
      CommandTimeout = 120,
      ConnectTimeout = 120
    }.ConnectionString;

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
    builder.WebHost.UseTestServer();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
      ["ConnectionStrings:Platform"] = connectionString,
      ["Jwt:Issuer"] = Issuer,
      ["Jwt:Audience"] = Audience,
      ["Jwt:ClockSkewSeconds"] = "30",
      ["AuthenticationTransport:AllowedOrigins:0"] = Origin,
      ["AuthenticationTransport:RateLimitHmacSecret"] = "platform-authority-e2e-rate-limit-secret-0123456789"
    });
    builder.Services
      .AddPlatformInfrastructure(builder.Configuration)
      .AddPlatformRequestContext()
      .AddPlatformModule()
      .AddHostJwtAuthentication(builder.Configuration, builder.Environment)
      .AddHostPermissionAuthorization()
      .AddHostAuthenticationTransport(builder.Configuration, builder.Environment)
      .AddHostProblemDetails();

    application = builder.Build();
    await using (var scope = application.Services.CreateAsyncScope())
    {
      await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
    }

    application.UseCorrelationId();
    application.UseAuthentication();
    application.UseAuthorization();
    application.MapPlatformSupportAuthorityEndpoints();

    await application.StartAsync();
    client = application.GetTestClient();
    client.BaseAddress = new Uri("https://localhost");
    adminToken = SignPlatformToken("platform-e2e-admin", 1, PlatformPermissionNames.AdministerPlatformSupport);
  }

  public async Task DisposeAsync()
  {
    client?.Dispose();
    if (application is not null)
    {
      await using (var scope = application.Services.CreateAsyncScope())
      {
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.EnsureDeletedAsync();
      }

      await application.DisposeAsync();
    }
  }

  private HttpClient Client => client ?? throw new InvalidOperationException("The test host has not started.");

  private WebApplication App => application ?? throw new InvalidOperationException("The test host has not started.");

  public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? token = null)
  {
    var request = new HttpRequestMessage(method, path);
    request.Headers.Add("Origin", Origin);
    request.Headers.Authorization = new("Bearer", token ?? adminToken);
    if (body is not null)
    {
      // Web (camelCase) naming: the strict transport reader enforces an exact camelCase property allow-list.
      request.Content = new StringContent(
        JsonSerializer.Serialize(body, JsonOptions),
        Encoding.UTF8,
        "application/json");
    }

    return Client.SendAsync(request);
  }

  // ---- Seeding ----

  public async Task<long> SeedIdentityAsync()
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    await context.SaveChangesAsync();

    var account = AuthenticationAccount.CreatePending(
      identity.Id, LoginEmail.Create($"op-{Guid.NewGuid():N}@example.test").Value);
    Assert.True(account.CompleteInitialSetup("e2e-password-hash", Guid.NewGuid(), Now).IsSuccess);
    context.AuthenticationAccounts.Add(account);
    await context.SaveChangesAsync();
    return identity.Id;
  }

  public async Task<long> SeedPrincipalAsync(params string[] permissions)
  {
    var identityId = await SeedIdentityAsync();
    return await SeedPrincipalForAsync(identityId, permissions);
  }

  public async Task<Actor> SeedActorAsync(params string[] permissions)
  {
    var identityId = await SeedIdentityAsync();
    var principalId = await SeedPrincipalForAsync(identityId, permissions);
    // The actor's own token: platform plane + Administer, subject/identity bound to the seeded operator.
    var token = SignPlatformToken($"platform-actor-{identityId}", identityId, PlatformPermissionNames.AdministerPlatformSupport);
    return new Actor(identityId, principalId, token);
  }

  private async Task<long> SeedPrincipalForAsync(long identityId, IReadOnlyCollection<string> permissions)
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var principal = PlatformSupportPrincipal.Register(identityId).Value;
    context.PlatformSupportPrincipals.Add(principal);
    await context.SaveChangesAsync();

    var catalog = new PlatformPermissionCatalog();
    foreach (var permission in permissions)
    {
      Assert.True(catalog.TryGet(permission, out var definition));
      Assert.True(principal.GrantPermission(definition, "seed", Now).IsSuccess);
    }

    await context.SaveChangesAsync();
    return principal.Id;
  }

  public async Task<long> SeedPlatformSessionAsync(long identityId, long principalId)
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var account = await context.AuthenticationAccounts.AsNoTracking().SingleAsync(a => a.IdentityId == identityId);
    var session = PlatformAuthenticationSession.Create(
      identityId, principalId, AuthenticationClientId.V1Web, Guid.NewGuid(), account.SecurityVersion,
      Now, Now.AddDays(1), Now.AddDays(7));
    context.PlatformAuthenticationSessions.Add(session);
    await context.SaveChangesAsync();
    return session.Id;
  }

  public async Task DisableDirectAsync(long principalId)
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var principal = await context.PlatformSupportPrincipals.SingleAsync(p => p.Id == principalId);
    Assert.True(principal.Disable("seed", Now).IsSuccess);
    await context.SaveChangesAsync();
  }

  // Setup-only premise builder for the two scenarios that assert GLOBAL absence of administrative authority.
  // The collection fixture shares one database across the class, so sibling tests leave other usable Administer
  // holders behind; without this the "last administrator" premise would depend on test execution order. Disables
  // every OTHER active principal directly (never through the API under test), leaving the actor as the only
  // usable administrator, so the post-act assertion is meaningful rather than accidental.
  public async Task EnsureOnlyAdministrativePrincipalAsync(long actorPrincipalId)
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var others = await context.PlatformSupportPrincipals
      .Where(principal => principal.Id != actorPrincipalId && principal.Status == PlatformSupportPrincipalStatus.Active)
      .ToListAsync();
    foreach (var principal in others)
    {
      Assert.True(principal.Disable("seed", Now).IsSuccess);
    }

    await context.SaveChangesAsync();
  }

  // ---- Live authority state (the committed Phase-4D-0 predicates) ----

  public async Task<bool> HasGeneralAuthorityAsync()
  {
    await using var scope = App.Services.CreateAsyncScope();
    return await scope.ServiceProvider.GetRequiredService<IPlatformSupportAuthorityStateReadService>()
      .HasUsablePlatformAuthorityAsync();
  }

  public async Task<bool> HasAdministrativeAuthorityAsync()
  {
    await using var scope = App.Services.CreateAsyncScope();
    return await scope.ServiceProvider.GetRequiredService<IPlatformSupportAuthorityStateReadService>()
      .HasUsablePlatformAdministrativeAuthorityAsync();
  }

  // ---- Raw SQL helpers ----

  public async Task<object?> ScalarAsync(string sql)
  {
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return await command.ExecuteScalarAsync();
  }

  public async Task<int> ActiveAssignmentCountAsync(long principalId, string permissionName) =>
    Convert.ToInt32(await ScalarAsync(
      $"SELECT COUNT(*) FROM [platform].[PlatformPermissionAssignments] WHERE [PlatformSupportPrincipalId] = {principalId} AND [PermissionName] = N'{permissionName}' AND [RemovedUtc] IS NULL"),
      CultureInfo.InvariantCulture);

  public async Task<string> RowVersionAsync(long principalId)
  {
    await using var scope = App.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var principal = await context.PlatformSupportPrincipals.AsNoTracking().SingleAsync(p => p.Id == principalId);
    return Convert.ToBase64String(principal.RowVersion);
  }

  private string SignPlatformToken(string subject, long identityId, string permission)
  {
    var key = App.Services.GetRequiredService<ISigningKeyProvider>().Snapshot.ActiveSigningKey;
    var now = DateTimeOffset.UtcNow;
    var claims = new List<Claim>
    {
      new(JwtClaimTypes.Subject, subject),
      new(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("N")),
      new("iat", now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
      new(JwtClaimTypes.IdentityId, identityId.ToString(CultureInfo.InvariantCulture)),
      new(JwtClaimTypes.SessionId, "9001"),
      new(JwtClaimTypes.ClientId, AuthenticationClientId.V1Web),
      new(JwtClaimTypes.SecurityVersion, "1"),
      new(JwtClaimTypes.SecurityPlane, SecurityPlane.Platform),
      new(JwtClaimTypes.Permission, permission)
    };
    var token = new JwtSecurityToken(
      issuer: Issuer, audience: Audience, claims: claims,
      notBefore: now.AddMinutes(-1).UtcDateTime, expires: now.AddMinutes(10).UtcDateTime,
      signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlatformSupportAuthorityEndToEndGroup : ICollectionFixture<PlatformSupportAuthorityEndToEndHost>
{
  public const string Name = "Platform support authority E2E";
}
