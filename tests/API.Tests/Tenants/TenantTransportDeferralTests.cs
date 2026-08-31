using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.API.Tests.Infrastructure;

namespace SSAS.API.Tests.Tenants;

// ==================================================================================================
// THE TENANT REGISTRY TRANSPORT IS DEFERRED, NOT FORGOTTEN (`AC-TEN-0020`, item 202).
// ==================================================================================================
//
// `src/Platform/SSAS.Platform.API/Tenants/` does not exist, while **every sibling module has a transport**
// -- Authentication, Companies, IdentityAccess, Localization, PlatformSupport, TenantUsers. The hole is
// deliberate: `AC-TEN-0020`, FP-003's first-milestone scope statement, lists tenant endpoints among eleven
// deferred concerns, and `item-152-route-table.md` records `GET /api/platform/tenants` as **DEFERRED**.
//
// ⚠ **UNTIL THIS TEST, NOTHING FAILED IF SOMEBODY BUILT IT.** The deferral was recorded in three documents
// and enforced by nothing. `agent/T-155-tenant-transport` is a complete implementation sitting on a branch,
// one merge away, whose own commit subject says `BLOCKED on AC-TEN-0020` -- and no gate would have noticed.
//
// ---- ⚠ WHY THE PREVIOUS GUARD WENT, AND WHY THIS ONE IS SHAPED DIFFERENTLY.
//
// `Milestone_contains_no_deferred_tenant_endpoint_or_post_session_implementation` was retired under
// `DEC-L-030`. It scanned Platform SOURCE for four declaration SPELLINGS -- `TenantController`,
// `Subscription`, `Billing`, `CompanyProvision` -- and **passed for the wrong reason**: it looked for
// `CompanyProvision` while `Company` had shipped, and `TenantController` could never have fired because
// this codebase declares no controllers at all.
//
// **So this asserts over ROUTES ACTUALLY MAPPED BY THE RUNNING HOST, not over names in source.** A
// transport that exists is a route in `EndpointDataSource`, whatever its types are called.
//
// ---- ⚠ AND THE DISCRIMINATOR IS A SEGMENT, NOT A PREFIX. `/api/platform/tenant-users` IS LIVE.
//
// `TenantUserEndpointRouteBuilderExtensions` maps `/api/platform/tenant-users/{id}/…` and those routes
// SHIPPED. A `StartsWith("/api/platform/tenant")` check would redden the gate on working code -- the exact
// class of error that retired the last guard, arriving from the other side.
[Collection(HostIntegrationTestGroup.Name)]
public sealed class TenantTransportDeferralTests(HostWebApplicationFactory factory)
{
  // The deferred surface, from `AC-TEN-0020` by way of `item-152-route-table.md`: the tenant registry
  // collection and its item routes.
  private const string DeferredSegment = "/api/platform/tenants";

  private const string PlatformPrefix = "/api/platform";

  [Fact]
  // ⚠ CITES `AC-TEN-0092`, NOT ONLY `AC-TEN-0020` (item 213). `0020` is the milestone SCOPE statement and
  // is broad; **`0092` is the criterion this test actually pins** -- *"`Platform.Tenants.View`/`Manage`/
  // `Lifecycle` HTTP endpoints remain Phase 5 and are not exposed merely because..."*. It was uncited until
  // item 213 read FP-003's criteria and found the guard and the criterion had been written independently.
  [Trait("Acceptance", "AC-TEN-0092")]
  [Trait("Acceptance", "AC-TEN-0020")]
  public void The_deferred_tenant_registry_transport_is_not_mapped()
  {
    var mapped = Routes()
      .Where(route => IsDeferredTenantRegistryRoute(route.RoutePattern.RawText))
      .Select(route => $"{FirstMethodOf(route)} {route.RoutePattern.RawText}")
      .OrderBy(route => route, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      mapped.Length == 0,
      $"""
      A TENANT REGISTRY TRANSPORT IS NOW MAPPED, AND IT IS DEFERRED BY AC-TEN-0020 -- NOT FORGOTTEN.

      Routes found: {string.Join(", ", mapped)}

      AC-TEN-0020 is FP-003's first-milestone scope statement; it lists tenant endpoints among eleven
      deferred concerns, and item-152-route-table.md records this surface as DEFERRED. A complete
      implementation already exists on agent/T-155-tenant-transport, whose own commit says
      "BLOCKED on AC-TEN-0020".

      IF YOU JUST BUILT THIS, THE WORK IS PROBABLY FINE AND THE DEFERRAL IS THE THING TO SETTLE.
      Retire AC-TEN-0020's tenant-endpoint row consciously -- update the scope statement and the route
      table -- and then DELETE THIS TEST in the same commit. It exists only to make the deferral fail
      loudly instead of resting on a document being read, and it has no purpose the day the work lands.
      """);
  }

  // ==================================================================================================
  // ⚠ THE CONTROL THE RETIRED GUARD LACKED: THE MATCHER MUST FIND WHAT DOES EXIST.
  // ==================================================================================================
  //
  // Its predecessor passed vacuously for months because it searched for spellings nothing used. **A ban
  // whose enumeration returns nothing is indistinguishable from a ban that holds** -- if the host failed to
  // boot, or `EndpointDataSource` came back empty, or the prefix were mistyped, the assertion above would
  // pass while proving nothing at all.
  //
  // The known positives are drawn from LIVE CODE, not planted: these routes are mapped today by
  // `AuthenticationEndpointRouteBuilderExtensions`, `CompanyEndpointRouteBuilderExtensions` and
  // `TenantUserEndpointRouteBuilderExtensions`.
  [Fact]
  [Trait("Acceptance", "AC-TEN-0092")]
  public void The_enumeration_sees_the_platform_transports_that_do_exist()
  {
    var patterns = Routes()
      .Select(route => route.RoutePattern.RawText ?? string.Empty)
      .ToArray();

    Assert.NotEmpty(patterns);

    foreach (var live in new[] { "/api/platform/auth", "/api/platform/companies", "/api/platform/tenant-users" })
    {
      Assert.Contains(
        patterns,
        pattern => pattern.StartsWith(live, StringComparison.Ordinal));
    }
  }

  // ==================================================================================================
  // ⚠ AND THE DISCRIMINATOR ITSELF, BECAUSE ITS PRECISION IS THE WHOLE GUARD.
  // ==================================================================================================
  //
  // Asserted as a pure function so it is exercised whether or not a tenant route ever appears. The first
  // two cases are what the ban must catch; **the third is what it must NOT catch, and it is live code**.
  [Theory]
  [InlineData("/api/platform/tenants", true)]
  [InlineData("/api/platform/tenants/{tenantId}", true)]
  [InlineData("/api/platform/tenant-users/{tenantUserId}/deactivation", false)]
  [InlineData("/api/platform/companies", false)]
  [InlineData(null, false)]
  public void The_discriminator_separates_the_tenant_registry_from_tenant_users(string? pattern, bool expected) =>
    Assert.Equal(expected, IsDeferredTenantRegistryRoute(pattern));

  // The registry collection itself, or something beneath it. NOT a sibling whose name merely starts with
  // the same letters -- `/api/platform/tenant-users` must fall outside, and does, because the next
  // character after the segment has to be a '/' or nothing at all.
  private static bool IsDeferredTenantRegistryRoute(string? pattern) =>
    pattern is not null
    && (string.Equals(pattern, DeferredSegment, StringComparison.Ordinal)
      || pattern.StartsWith(DeferredSegment + "/", StringComparison.Ordinal));

  private RouteEndpoint[] Routes() =>
  [
    .. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
      .OfType<RouteEndpoint>()
      .Where(endpoint =>
        endpoint.RoutePattern.RawText?.StartsWith(PlatformPrefix, StringComparison.Ordinal) ?? false)
  ];

  private static string FirstMethodOf(RouteEndpoint endpoint)
  {
    var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

    return methods is { Count: > 0 } ? methods[0] : "?";
  }
}
