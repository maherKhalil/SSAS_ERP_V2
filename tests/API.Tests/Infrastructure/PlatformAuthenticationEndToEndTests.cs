using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.API.Authentication;
using SSAS.Platform.Domain.Authentication;
using SSAS.Platform.Domain.Identities;
using SSAS.Platform.Domain.Tenants;
using SSAS.Platform.Domain.TenantUsers;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Infrastructure.Persistence;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// ⚠⚠⚠ THE POSITIVE HALF OF THE TENANT AUTH SURFACE. IT DID NOT EXIST UNTIL `280`.
// ==================================================================================================
//
// `POST /api/platform/auth/login` had FIVE test references and **every one asserted a REJECTION** — an
// HTTP-not-HTTPS 403, an unknown-field 400, two OpenAPI document reads and a permission-catalogue row.
// `POST /api/platform/auth/select-tenant` had NONE OF ANY KIND. The handlers underneath are well covered
// at the application layer (`AuthenticationSessionApplicationTests`) and against real persistence
// (`PlatformAuthenticationPersistenceTests`) — **it is the HTTP BINDING layer that nothing exercised.**
//
// ⚠ A SUITE THAT ONLY EVER ASSERTS REFUSALS CANNOT DISTINGUISH *the route correctly rejects bad input*
// FROM *the route rejects everything*. All five prior tests stay green if login is broken outright.
//
// ---- WHY THIS IS A GUARD AND NOT COVERAGE, WHICH IS THE WHOLE POINT OF THE ITEM.
//
// These two routes bind through `AuthenticationEndpointRouteBuilderExtensions.ReadJsonAsync<T>`, a PRIVATE
// copy of `StrictRequestReader` that deserializes with `new JsonSerializerOptions(JsonSerializerDefaults.Web)`
// — **case-INsensitive**. `AuthenticationLoginRequest` and `AuthenticationTenantSelectionRequest` therefore
// carry no `[JsonPropertyName]`, and `StrictRequestBindingArchitectureTests` EXCLUDES them for that reason:
// they are unreachable from any `ReadStrictJsonAsync<...>` call site, so they fall outside its closure.
//
// ⚠⚠ THAT EXCLUSION IS CORRECT AND IT IS SILENTLY LOAD-BEARING ON THE `Web` OPTIONS. Convert these readers
// to the shared `StrictRequestReader` — which uses `JsonSerializerOptions.Default`, case-SENSITIVE — and
// camelCase JSON stops binding to PascalCase properties, every field arrives null, and both routes answer
// `400 request.invalid` forever. **That is FP-011 exactly, on the login route.**
//
// ⚠⚠⚠ AND THE HEADER OF `StrictRequestReader` USED TO TELL THE NEXT READER THAT EVERY ROUTE GROUP ALREADY
// BINDS THROUGH IT — an invitation to perform precisely that conversion, on the file a converter opens
// first. The header is corrected (`351274c`). **These tests are what makes the conversion LOUD**: after
// this file, converting `ReadJsonAsync` reddens the gate the way converting the support-login copy already
// did. Before it, the tenant login route would have broken in silence.
//
// **Do not delete these as duplicating the application-layer tests. Those construct the command directly
// and never serialize a byte; the defect these exist to catch lives entirely in the JSON binding.**
[Collection(PlatformSupportAuthenticationEndToEndGroup.Name)]
public sealed class PlatformAuthenticationEndToEndTests(PlatformSupportAuthenticationEndToEndHost host)
{
  private const string Origin = PlatformSupportAuthenticationEndToEndHost.Origin;
  private const string Password = PlatformSupportAuthenticationEndToEndHost.Password;
  private const string Prefix = "/api/platform/auth";
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private static readonly DateTimeOffset Now = new(2026, 8, 12, 11, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Tenant_login_binds_a_camel_case_body_and_returns_the_single_eligible_tenant_session()
  {
    var (email, _, _) = await SeedTenantMemberAsync(tenantCount: 1);

    var response = await LoginAsync(email);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await ReadAsync<AuthenticatedResponse>(response);

    // ⚠ ASSERTING THE BOUND VALUES, NOT ONLY THE STATUS. A 200 alone would survive a reader that bound
    // nothing and a handler that happened to succeed anyway; the tenant and session identifiers can only
    // be non-empty if the credentials in the BODY were actually read.
    Assert.Equal("Authenticated", body.Outcome);
    Assert.Equal("Bearer", body.TokenType);
    Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    Assert.NotEqual(Guid.Empty, body.TenantId);
    Assert.True(body.TenantUserId > 0);
    Assert.True(body.AuthenticationSessionId > 0);
  }

  [Fact]
  public async Task Tenant_selection_binds_a_camel_case_body_and_establishes_the_chosen_tenant_session()
  {
    var (email, tenantIds, tenantUserIds) = await SeedTenantMemberAsync(tenantCount: 2);

    // Two eligible memberships, so login cannot auto-select and must hand back a reveal-once proof.
    var selection = await ReadAsync<TenantSelectionRequiredResponse>(await LoginAsync(email));
    Assert.Equal("TenantSelectionRequired", selection.Outcome);
    Assert.Equal(2, selection.Memberships.Count);

    var chosen = selection.Memberships[0];
    Assert.Contains(chosen.TenantId, tenantIds);
    Assert.Contains(chosen.TenantUserId, tenantUserIds);

    var response = await PostAsync("/select-tenant", JsonSerializer.Serialize(new
    {
      selectionProof = selection.SelectionProof,
      tenantId = chosen.TenantId,
      tenantUserId = chosen.TenantUserId
    }));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await ReadAsync<AuthenticatedResponse>(response);
    Assert.Equal("Authenticated", body.Outcome);

    // ⚠ THE DISCRIMINATING ASSERTION: the session is for the tenant the BODY named. A reader that bound
    // `tenantId` to `Guid.Empty` could not produce this, and a handler ignoring the request could not
    // either — this is the leg that fails if the JSON never reached the command.
    Assert.Equal(chosen.TenantId, body.TenantId);
    Assert.Equal(chosen.TenantUserId, body.TenantUserId);
    Assert.True(body.AuthenticationSessionId > 0);
  }

  [Fact]
  public async Task Tenant_selection_rejects_unknown_input_fields_without_echoing_the_body()
  {
    var (email, _, _) = await SeedTenantMemberAsync(tenantCount: 2);
    var selection = await ReadAsync<TenantSelectionRequiredResponse>(await LoginAsync(email));
    var chosen = selection.Memberships[0];

    // The same body as the happy path plus one field the contract does not declare. `select-tenant` has
    // never had this test; its sibling on `/login` is `HostEndpointTests:254`.
    var response = await PostAsync("/select-tenant", JsonSerializer.Serialize(new
    {
      selectionProof = selection.SelectionProof,
      tenantId = chosen.TenantId,
      tenantUserId = chosen.TenantUserId,
      clientId = "caller-value"
    }));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("request.invalid", body, StringComparison.Ordinal);
    Assert.DoesNotContain("caller-value", body, StringComparison.Ordinal);
  }

  // ---- SEEDING.
  //
  // ⚠ `TenantUser` is tenant-owned, and `PersistenceDbContext.AssignTenant` REFUSES to save one without a
  // trusted tenant context — `CurrentTenant` reads the tenant claim off `IHttpContextAccessor`, which is
  // null outside a request. So the seeding scope installs an authenticated principal carrying the tenant
  // claim. That is the production accessor doing its real job, not a bypass: the write still has to match.
  private async Task<(string Email, Guid[] TenantIds, long[] TenantUserIds)> SeedTenantMemberAsync(int tenantCount)
  {
    await using var scope = host.Application.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var hashing = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
    var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
    var email = $"member-{Guid.NewGuid():N}@example.test";

    var identity = Identity.Create(AuthenticationSubject.Create($"local:{Guid.NewGuid():N}").Value);
    context.Identities.Add(identity);
    await context.SaveChangesAsync();

    var account = AuthenticationAccount.CreatePending(identity.Id, LoginEmail.Create(email).Value);
    Assert.True(account.CompleteInitialSetup(hashing.HashPassword(Password), Guid.NewGuid(), Now).IsSuccess);
    context.AuthenticationAccounts.Add(account);
    await context.SaveChangesAsync();

    var tenantIds = new List<Guid>();
    var tenantUserIds = new List<long>();
    for (var index = 0; index < tenantCount; index++)
    {
      var code = $"T{Guid.NewGuid():N}"[..8].ToUpperInvariant();
      var tenant = Tenant.Create(
        TenantCode.Create(code).Value,
        TenantName.Create($"Tenant {code}").Value,
        "e2e-seed",
        Guid.NewGuid(),
        Now).Value;
      context.Tenants.Add(tenant);
      await context.SaveChangesAsync();
      // ⚠ ACTIVATION IS LOAD-BEARING AND WAS PROVEN SO. An inactive tenant is not an eligible membership
      // (`IdentityTenantMembershipReadService` joins on `TenantStatus.Active`), so removing these two
      // lines reddens ALL THREE tests — measured, because all three passed on their first run in 370ms and
      // a happy-path test on a route nothing had ever exercised positively is where a vacuous test is born.
      Assert.True(tenant.Activate("e2e-seed", Guid.NewGuid(), Now.AddMinutes(1)).IsSuccess);
      await context.SaveChangesAsync();

      accessor.HttpContext = TenantContext(tenant.Id);
      var membership = TenantUser.CreateActive(
        identity.Id,
        tenant.Id,
        EmailAddress.Create(email).Value,
        UserDisplayName.Create($"Member {code}").Value,
        Guid.NewGuid(),
        Now);
      context.TenantUsers.Add(membership);
      await context.SaveChangesAsync();
      accessor.HttpContext = null;

      tenantIds.Add(tenant.Id);
      tenantUserIds.Add(membership.Id);
    }

    return (email, [.. tenantIds], [.. tenantUserIds]);
  }

  private static DefaultHttpContext TenantContext(Guid tenantId) => new()
  {
    User = new ClaimsPrincipal(new ClaimsIdentity(
      [new Claim(JwtClaimTypes.TenantId, tenantId.ToString())], "e2e-seed"))
  };

  private Task<HttpResponseMessage> LoginAsync(string loginEmail) =>
    PostAsync("/login", JsonSerializer.Serialize(new { loginEmail, password = Password }));

  private Task<HttpResponseMessage> PostAsync(string path, string payload)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, $"{Prefix}{path}")
    {
      Content = new StringContent(payload, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Origin", Origin);
    return host.Client.SendAsync(request);
  }

  private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
  {
    var body = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(body, JsonOptions)
      ?? throw new InvalidOperationException($"the response body did not deserialize to {typeof(T).Name}: {body}");
  }
}
