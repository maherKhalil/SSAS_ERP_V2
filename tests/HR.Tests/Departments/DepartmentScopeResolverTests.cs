using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Departments.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Departments;

namespace SSAS.HR.Tests.Departments;

// THE DEPARTMENT SCOPE RESOLVER (FP-007 Phase 2, ADR-025 decisions 8 and 10).
//
// The decision tests: which requests are refused, which are permitted, and — just as important — what is
// NOT consulted. Whether the composed predicate actually restricts what the database returns is a different
// question, answered against real SQL in Integration.Tests.
public sealed class DepartmentScopeResolverTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");

  // ================================================================================================
  // THE FUNCTIONAL DIMENSION
  // ================================================================================================

  [Fact]
  public async Task A_user_without_the_view_permission_is_refused()
  {
    var scope = await Resolver(permissions: []).ResolveAsync(new DepartmentScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(DepartmentErrors.PermissionDenied, scope.Error);
  }

  // ---- THE ONE THAT MATTERS MOST (ADR-025 decision 8).
  //
  // `Platform.Tenant.Administer` widens the COMPANY dimension — an administrator reaches every active
  // company in the tenant — and grants NO operation. An administrator who was never given the HR permission
  // cannot read a department, and the scope they would have had is irrelevant to that.
  [Fact]
  public async Task Tenant_administration_alone_does_not_grant_the_department_read()
  {
    var resolver = Resolver(
      permissions: ["Platform.Tenant.Administer"],
      // Deliberately generous: the administrator's scope resolves to everything.
      companies: [CompanyA, CompanyB]);

    var scope = await resolver.ResolveAsync(new DepartmentScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(DepartmentErrors.PermissionDenied, scope.Error);
  }

  // ---- AND THE SAME HOLDS FOR EVERY WRITE PERMISSION, not only the read.
  [Theory]
  [InlineData(HrPermissionNames.CreateDepartments)]
  [InlineData(HrPermissionNames.UpdateDepartments)]
  [InlineData(HrPermissionNames.DeactivateDepartments)]
  public async Task Tenant_administration_alone_grants_no_department_write(string permission)
  {
    var resolver = Resolver(permissions: ["Platform.Tenant.Administer"], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(permission, CompanyA);

    Assert.True(authorized.IsFailure);
    Assert.Equal(DepartmentErrors.PermissionDenied, authorized.Error);
  }

  // Holding the employee permissions says nothing about departments. The two sets are independent.
  [Fact]
  public async Task Employee_permissions_do_not_grant_department_reads()
  {
    var resolver = Resolver(permissions:
      [HrPermissionNames.ViewEmployees, HrPermissionNames.UpdateEmployees]);

    var scope = await resolver.ResolveAsync(new DepartmentScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(DepartmentErrors.PermissionDenied, scope.Error);
  }

  // ================================================================================================
  // THE COMPANY DIMENSION
  // ================================================================================================

  [Fact]
  public async Task The_current_company_is_resolved_and_materialized()
  {
    var scope = await Resolver().ResolveAsync(new DepartmentScopeRequest());

    Assert.True(scope.IsSuccess);
    Assert.Equal(TenantId, scope.Value.TenantId);
    Assert.Equal([CompanyA], scope.Value.Companies.CompanyIds);
  }

  [Fact]
  public async Task All_authorized_companies_resolves_to_the_whole_permitted_set()
  {
    var resolver = Resolver(companies: [CompanyA, CompanyB]);

    var scope = await resolver.ResolveAsync(
      new DepartmentScopeRequest(DepartmentCompanyScopeMode.AllAuthorizedCompanies));

    Assert.True(scope.IsSuccess);
    Assert.Equal([CompanyA, CompanyB], scope.Value.Companies.CompanyIds);
  }

  // ---- AN EMPTY AUTHORIZED SET REFUSES. It never degrades to unfiltered.
  [Fact]
  // CITED BY B18 pass 18: a caller whose authorized company set resolves empty is refused rather
  // than served an unfiltered read. An empty result claims something about the DATA; a refusal
  // claims something about the CALLER, and only the second is true here.
  [Trait("Criterion", "AC-DEP-0007")]
  public async Task An_empty_authorized_company_set_is_refused_rather_than_unfiltered()
  {
    var resolver = Resolver(companies: []);

    var scope = await resolver.ResolveAsync(
      new DepartmentScopeRequest(DepartmentCompanyScopeMode.AllAuthorizedCompanies));

    Assert.True(scope.IsFailure);
    Assert.Equal(DepartmentErrors.CompanyScopeDenied, scope.Error);
  }

  [Fact]
  public async Task An_unestablished_company_context_is_refused()
  {
    var resolver = Resolver(companyEstablished: false);

    var scope = await resolver.ResolveAsync(new DepartmentScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(DepartmentErrors.CompanyScopeDenied, scope.Error);
  }

  [Fact]
  public async Task A_company_outside_the_authorized_set_is_refused()
  {
    // The permission IS held, so the refusal below can only come from the company dimension. Without this
    // the test would pass on a PermissionDenied and prove nothing about scope.
    var resolver = Resolver(
      permissions: [HrPermissionNames.UpdateDepartments],
      selectedCompany: CompanyB,
      companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(HrPermissionNames.UpdateDepartments, CompanyB);

    Assert.True(authorized.IsFailure);
    Assert.Equal(DepartmentErrors.CompanyScopeDenied, authorized.Error);
  }

  // ---- IT RE-ASKS, EVERY TIME, RATHER THAN CACHING.
  //
  // Company access is revocable inside a request's lifetime, and a read served from a set captured earlier
  // is precisely the failure the live resolution exists to prevent.
  [Fact]
  public async Task The_company_authority_is_consulted_on_every_resolution()
  {
    var access = new RecordingCompanyAccess([CompanyA]);
    var resolver = Resolver(
      permissions: [HrPermissionNames.ViewDepartments, HrPermissionNames.UpdateDepartments],
      companyAccess: access);

    await resolver.ResolveAsync(new DepartmentScopeRequest());
    await resolver.ResolveAsync(new DepartmentScopeRequest());
    await resolver.AuthorizeAsync(HrPermissionNames.UpdateDepartments, CompanyA);

    Assert.Equal(3, access.Calls);
  }

  // ================================================================================================
  // WHAT THE RESOLVER DELIBERATELY DOES NOT CONSULT
  // ================================================================================================
  //
  // There is no branch dimension. A department is not branch-owned, so branch scope does not decide whether
  // one is visible — and the resolver takes no branch resolver at all, which is a stronger statement than
  // "it does not call one".
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_resolver_takes_no_branch_dependency()
  {
    var parameters = typeof(DepartmentScopeResolver)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();

    Assert.DoesNotContain(parameters, name => name.Contains("Branch", StringComparison.Ordinal));
  }

  private static DepartmentScopeResolver Resolver(
    IReadOnlyCollection<string>? permissions = null,
    Guid? selectedCompany = null,
    bool companyEstablished = true,
    IReadOnlyList<Guid>? companies = null,
    ITenantCompanyAccessResolver? companyAccess = null) =>
    new(
      companyAccess ?? new RecordingCompanyAccess(companies ?? [CompanyA]),
      new StubCurrentCompany(companyEstablished ? selectedCompany ?? CompanyA : null),
      new StubCurrentTenant(),
      new StubCurrentTenantUser(),
      new StubCurrentUser(permissions ?? [HrPermissionNames.ViewDepartments]));

  // ---- THE AUTHORITIES ARE STUBBED, THE RESOLVER IS NOT.
  //
  // These stand in for the Platform resolvers so the tests can state exactly what a user may reach. Whether
  // the Platform resolvers answer correctly is proven against real SQL elsewhere.
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

  private sealed class StubCurrentCompany(Guid? companyId) : ICurrentCompany
  {
    public Guid? CompanyId => companyId;
  }

  private sealed class StubCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => DepartmentScopeResolverTests.TenantId;
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
