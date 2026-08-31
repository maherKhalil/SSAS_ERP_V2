using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.Payroll.Application.Permissions;
using SSAS.Payroll.Application.Reads;

namespace SSAS.Payroll.Tests.Reads;

// ================================================================================================
// THE PAYROLL SCOPE RESOLVER — `AC-PAY-0028`, `DEC-PAY-0006`.
// ================================================================================================
//
// ---- ⚠ WHY THIS FILE DID NOT EXIST, WHICH IS THE FINDING THAT PRODUCED IT.
//
// `DepartmentScopeResolverTests` and `PositionScopeResolverTests` both assert
// `A_company_outside_the_authorized_set_is_refused`; `GlEndpointTests` and `GlJournalDraftReadTests` both
// assert `A_caller_with_no_authorized_company_is_refused_rather_than_served_an_empty_page`. **Four tests
// across two modules for one mechanism, and none for Payroll** — the module whose own scope type says
// *"everywhere else a forgeable scope is an authorization defect; for compensation it is a personal-data
// breach."*
//
// **The behaviour was there the whole time.** `AuthorizeAsync` has refused an unpermitted company since it
// was written and `ResolveAsync` has refused an empty set since it was written. What was missing is the
// assertion — so a change to either would have been caught by nothing in this module.
//
// ---- WHAT IS STUBBED AND WHAT IS NOT.
//
// The Platform authorities are stubbed so a test can state exactly what a caller may reach. The resolver
// is real. Whether `ITenantCompanyAccessResolver` answers correctly is proven against SQL elsewhere.
public sealed class PayrollScopeResolverTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");

  // ================================================================================================
  // ⚠ THE CRITERION'S SECOND CLAUSE: A REQUEST WIDENING ITS OWN SCOPE IS REFUSED.
  // ================================================================================================

  // The permission IS held and the company is NOT, so the refusal can only come from the company
  // dimension. Without that the test would pass on a `WritePermissionDenied` and prove nothing at all
  // about scope.
  [Fact]
  [Trait("Criterion", "AC-PAY-0028")]
  public async Task A_company_outside_the_authorized_set_is_refused()
  {
    var resolver = Resolver(
      permissions: [PayrollPermissionNames.ManageCompensation], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(
      PayrollPermissionNames.ManageCompensation, CompanyB);

    Assert.True(authorized.IsFailure);
    Assert.Equal(PayrollScopeErrors.CompanyScopeDenied, authorized.Error);
  }

  // ---- ⚠ AND THE CONTROL, WITHOUT WHICH THE TEST ABOVE IS SATISFIED BY A RESOLVER THAT REFUSES
  // ---- EVERYTHING.
  //
  // Same resolver, same permission, the one company that IS permitted. A guard never observed to permit
  // anything is indistinguishable from a guard that is broken shut, and only the pair separates
  // "refuses the widened company" from "refuses".
  [Fact]
  [Trait("Criterion", "AC-PAY-0028")]
  public async Task A_company_inside_the_authorized_set_is_permitted()
  {
    var resolver = Resolver(
      permissions: [PayrollPermissionNames.ManageCompensation], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(
      PayrollPermissionNames.ManageCompensation, CompanyA);

    Assert.True(authorized.IsSuccess);
  }

  // ---- ⚠ AN EMPTY AUTHORIZED SET REFUSES, RATHER THAN DEGRADING TO AN EMPTY PAGE.
  //
  // `PayrollReadScope` states the reason where it is enforced: *an empty page says "there is nothing
  // here", a claim about the DATA; a refusal says "you cannot see", a claim about the CALLER. Only the
  // second is true, and only the second stays true when someone later grants the caller a company.*
  //
  // The check lives in `PayrollReadScope.Create`, not in the resolver, so it holds for every future
  // caller of the factory rather than for the one that exists today — and this asserts it through the
  // resolver, which is the only route a caller can take to it.
  [Fact]
  [Trait("Criterion", "AC-PAY-0028")]
  public async Task An_empty_authorized_company_set_is_refused_rather_than_served_as_an_empty_page()
  {
    var scope = await Resolver(companies: []).ResolveAsync(PayrollPermissionNames.ViewCompensation);

    Assert.True(scope.IsFailure);
    Assert.Equal(PayrollScopeErrors.CompanyScopeDenied, scope.Error);
  }

  // ================================================================================================
  // ⚠ THE CRITERION'S FIRST CLAUSE: A READ SCOPE CANNOT BE SUPPLIED BY THE CALLER.
  // ================================================================================================
  //
  // The structural half is usually argued from `PayrollReadScope`'s private constructor and `internal`
  // factory. This says something the constructor cannot: **the resolver takes no `ICurrentCompany` at
  // all**, so the caller's company SELECTION is not an input to scope resolution even indirectly. A
  // resolver that took one could narrow or widen by it; this one cannot see it.
  [Fact]
  [Trait("Criterion", "AC-PAY-0028")]
  public void The_resolver_cannot_see_the_callers_company_selection()
  {
    var parameters = typeof(PayrollScopeResolver)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();

    Assert.DoesNotContain(parameters, name => name.Contains("CurrentCompany", StringComparison.Ordinal));

    // The control: the authority it DOES take. Without this the assertion above is satisfied by a
    // resolver that takes no company dependency of any kind, including the one that makes it work.
    Assert.Contains(parameters, name => name.Contains("CompanyAccessResolver", StringComparison.Ordinal));
  }

  // ---- IT RE-ASKS EVERY TIME RATHER THAN CACHING.
  //
  // Company access is revocable inside a request's lifetime, and a payslip served from a set captured
  // earlier in the same request is the failure the live resolution exists to prevent.
  [Fact]
  public async Task The_company_authority_is_consulted_on_every_resolution()
  {
    var access = new RecordingCompanyAccess([CompanyA]);
    var resolver = Resolver(
      permissions: [PayrollPermissionNames.ViewCompensation, PayrollPermissionNames.ManageCompensation],
      companyAccess: access);

    await resolver.ResolveAsync(PayrollPermissionNames.ViewCompensation);
    await resolver.ResolveAsync(PayrollPermissionNames.ViewCompensation);
    await resolver.AuthorizeAsync(PayrollPermissionNames.ManageCompensation, CompanyA);

    Assert.Equal(3, access.Calls);
  }

  // ---- THE TWO AXES ARE INDEPENDENT AND NEITHER WIDENS THE OTHER.
  //
  // `Platform.Tenant.Administer` widens the COMPANY dimension to every active company in the tenant and
  // grants no operation. The resolver's own comment says so; nothing asserted it for payroll, which is
  // the surface where the mistake would disclose pay.
  [Fact]
  public async Task Tenant_administration_alone_reads_no_compensation()
  {
    var resolver = Resolver(
      permissions: ["Platform.Tenant.Administer"],
      // Deliberately generous: the administrator's company scope resolves to everything.
      companies: [CompanyA, CompanyB]);

    var scope = await resolver.ResolveAsync(PayrollPermissionNames.ViewCompensation);

    Assert.True(scope.IsFailure);
    Assert.Equal(PayrollScopeErrors.ReadPermissionDenied, scope.Error);
  }

  private static PayrollScopeResolver Resolver(
    IReadOnlyCollection<string>? permissions = null,
    IReadOnlyList<Guid>? companies = null,
    ITenantCompanyAccessResolver? companyAccess = null) =>
    new(
      companyAccess ?? new RecordingCompanyAccess(companies ?? [CompanyA]),
      new StubCurrentTenant(),
      new StubCurrentTenantUser(),
      new StubCurrentUser(permissions ?? [PayrollPermissionNames.ViewCompensation]));

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
    public Guid? TenantId => PayrollScopeResolverTests.TenantId;
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
