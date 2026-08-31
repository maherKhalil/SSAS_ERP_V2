using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.GL.Application.Permissions;
using SSAS.GL.Application.Reads;

namespace SSAS.Finance.Tests.Reads;

// ================================================================================================
// THE GL SCOPE RESOLVER — THE OUT-OF-SET HALF (item 227).
// ================================================================================================
//
// ---- ⚠ WHAT WAS ALREADY COVERED, AND WHY IT IS NOT THIS.
//
// `GlEndpointTests` and `GlJournalDraftReadTests` both assert
// `A_caller_with_no_authorized_company_is_refused_rather_than_served_an_empty_page` — **the EMPTY-SET
// case**, arranged with `host.CompanyAccess.Permitted = []`, asserted at the wire as 403
// `company.scope_denied`.
//
// ⚠ **That is a different claim from the one the criterion makes.** An empty set is *this caller reaches
// nothing*; an out-of-set company is *this caller reaches SOMETHING, and not THAT*. **The second is the
// one a widening attempt actually produces**, and `GlScopeErrors.CompanyScopeDenied` was asserted by
// nothing anywhere in `tests/`.
//
// **The behaviour was there the whole time** — `AuthorizeAsync` has ended in
// `companies.Value.Any(company => company.CompanyId == companyId)` since it was written.
public sealed class GlScopeResolverTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");

  // The permission IS held and the company is NOT, so the refusal can only come from the company
  // dimension. Without that the test would pass on a `WritePermissionDenied` and prove nothing about
  // scope.
  [Fact]
  public async Task A_company_outside_the_authorized_set_is_refused()
  {
    var resolver = Resolver(permissions: [GlPermissionNames.PostJournals], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(GlPermissionNames.PostJournals, CompanyB);

    Assert.True(authorized.IsFailure);
    Assert.Equal(GlScopeErrors.CompanyScopeDenied, authorized.Error);
  }

  // ---- ⚠ AND THE CONTROL. A guard never observed to permit anything is indistinguishable from one that
  // ---- is broken shut, and only the pair separates "refuses the widened company" from "refuses".
  [Fact]
  public async Task A_company_inside_the_authorized_set_is_permitted()
  {
    var resolver = Resolver(permissions: [GlPermissionNames.PostJournals], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(GlPermissionNames.PostJournals, CompanyA);

    Assert.True(authorized.IsSuccess);
  }

  // ---- THE EMPTY SET REFUSES AT THE RESOLVER, not only at the wire.
  //
  // The existing API tests prove the 403 a caller sees. This proves the layer that decides it, which is
  // the one a refactor of the endpoint cannot accidentally take with it.
  [Fact]
  public async Task An_empty_authorized_company_set_is_refused_rather_than_served_as_an_empty_page()
  {
    var scope = await Resolver(companies: []).ResolveAsync(GlPermissionNames.ViewJournals);

    Assert.True(scope.IsFailure);
    Assert.Equal(GlScopeErrors.CompanyScopeDenied, scope.Error);
  }

  // ---- IT RE-ASKS EVERY TIME RATHER THAN CACHING. Company access is revocable inside a request.
  [Fact]
  public async Task The_company_authority_is_consulted_on_every_resolution()
  {
    var access = new RecordingCompanyAccess([CompanyA]);
    var resolver = Resolver(
      permissions: [GlPermissionNames.ViewJournals, GlPermissionNames.PostJournals],
      companyAccess: access);

    await resolver.ResolveAsync(GlPermissionNames.ViewJournals);
    await resolver.ResolveAsync(GlPermissionNames.ViewJournals);
    await resolver.AuthorizeAsync(GlPermissionNames.PostJournals, CompanyA);

    Assert.Equal(3, access.Calls);
  }

  private static GlScopeResolver Resolver(
    IReadOnlyCollection<string>? permissions = null,
    IReadOnlyList<Guid>? companies = null,
    ITenantCompanyAccessResolver? companyAccess = null) =>
    new(
      companyAccess ?? new RecordingCompanyAccess(companies ?? [CompanyA]),
      new StubCurrentTenant(),
      new StubCurrentTenantUser(),
      new StubCurrentUser(permissions ?? [GlPermissionNames.ViewJournals]));

  private sealed class RecordingCompanyAccess(IReadOnlyList<Guid> permitted) : ITenantCompanyAccessResolver
  {
    public int Calls { get; private set; }

    public Task<Result<IReadOnlyList<CompanyAccessSummary>>> GetPermittedCompaniesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default)
    {
      Calls++;

      return Task.FromResult(Result.Success<IReadOnlyList<CompanyAccessSummary>>(
        permitted.Select(id => new CompanyAccessSummary(id, "CODE", "Name")).ToArray()));
    }

    public Task<Result> AuthorizeCompanyAsync(
      Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default)
    {
      Calls++;

      return Task.FromResult(permitted.Contains(companyId)
        ? Result.Success()
        : Result.Failure(new Error("Company.Denied", "Denied.")));
    }
  }

  private sealed class StubCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => GlScopeResolverTests.TenantId;
  }

  private sealed class StubCurrentTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 42;
  }

  private sealed class StubCurrentUser(IReadOnlyCollection<string> permissions) : ICurrentUser
  {
    public string? UserId => "tester";

    public string? UserName => "tester";

    public string? Email => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => permissions;
  }
}
