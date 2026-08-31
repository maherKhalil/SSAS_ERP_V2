using SSAS.Attendance.Application.Permissions;
using SSAS.Attendance.Application.Reads;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;

namespace SSAS.Attendance.Tests.Reads;

// ================================================================================================
// THE ATTENDANCE SCOPE RESOLVER — THE COMPANY DIMENSION (item 227).
// ================================================================================================
//
// **The resolver was named by five test files and asserted by none of them.** `AttendanceApiTestHost`
// registers it, the leave handler tests construct one to get past authorization, and the integration
// chain exercises it incidentally — ⚠ **`AttendanceScopeErrors.CompanyScopeDenied` appeared nowhere in
// `tests/` at all.**
//
// **REFERENCED IS NOT ASSERTED**, and the difference is the whole of this item: a file that names a type
// in order to build one proves the type EXISTS, which is not the claim.
//
// **The behaviour was there the whole time** — `AuthorizeAsync` has ended in
// `companies.Value.Any(company => company.CompanyId == companyId)` since it was written.
public sealed class AttendanceScopeResolverTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  private static readonly Guid BranchA = Guid.Parse("44444444-4444-4444-4444-444444444444");

  // The permission IS held and the company is NOT, so the refusal can only come from the company
  // dimension. Without that the test would pass on a `WritePermissionDenied` and prove nothing about
  // scope.
  [Fact]
  public async Task A_company_outside_the_authorized_set_is_refused()
  {
    var resolver = Resolver(permissions: [AttendancePermissionNames.ManageRecords], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(AttendancePermissionNames.ManageRecords, CompanyB);

    Assert.True(authorized.IsFailure);
    Assert.Equal(AttendanceScopeErrors.CompanyScopeDenied, authorized.Error);
  }

  // ---- ⚠ AND THE CONTROL. A guard never observed to permit anything is indistinguishable from one that
  // ---- is broken shut, and only the pair separates "refuses the widened company" from "refuses".
  [Fact]
  public async Task A_company_inside_the_authorized_set_is_permitted()
  {
    var resolver = Resolver(permissions: [AttendancePermissionNames.ManageRecords], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(AttendancePermissionNames.ManageRecords, CompanyA);

    Assert.True(authorized.IsSuccess);
  }

  // ---- AN EMPTY AUTHORIZED SET REFUSES, rather than degrading to an empty page.
  //
  // ⚠ **On the COMPANY-ONLY path, and deliberately only there.** On the full path an empty company set
  // also refuses — but it answers `BranchScopeDenied`, because the factory refuses on either empty set
  // and the resolver labels that refusal by the check it had just made. **Asserting that here would
  // cement a diagnosis that names the wrong grant**, so this pins the path whose answer is right and the
  // other is reported rather than fixed at the keyboard.
  [Fact]
  public async Task An_empty_authorized_company_set_is_refused_rather_than_served_as_an_empty_page()
  {
    var scope = await Resolver(companies: [])
      .ResolveCompanyOnlyAsync(AttendancePermissionNames.ViewRecords);

    Assert.True(scope.IsFailure);
    Assert.Equal(AttendanceScopeErrors.CompanyScopeDenied, scope.Error);
  }

  // ---- IT RE-ASKS EVERY TIME RATHER THAN CACHING. Company access is revocable inside a request.
  [Fact]
  public async Task The_company_authority_is_consulted_on_every_resolution()
  {
    var access = new RecordingCompanyAccess([CompanyA]);
    var resolver = Resolver(
      permissions: [AttendancePermissionNames.ViewRecords, AttendancePermissionNames.ManageRecords],
      companyAccess: access);

    await resolver.ResolveCompanyOnlyAsync(AttendancePermissionNames.ViewRecords);
    await resolver.ResolveCompanyOnlyAsync(AttendancePermissionNames.ViewRecords);
    await resolver.AuthorizeAsync(AttendancePermissionNames.ManageRecords, CompanyA);

    Assert.Equal(3, access.Calls);
  }

  private static AttendanceScopeResolver Resolver(
    IReadOnlyCollection<string>? permissions = null,
    IReadOnlyList<Guid>? companies = null,
    ITenantCompanyAccessResolver? companyAccess = null) =>
    new(
      companyAccess ?? new RecordingCompanyAccess(companies ?? [CompanyA]),
      new StubBranchAccess(),
      new StubCurrentTenant(),
      new StubCurrentTenantUser(),
      new StubCurrentUser(permissions ?? [AttendancePermissionNames.ViewRecords]));

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

  private sealed class StubBranchAccess : ITenantBranchAccessResolver
  {
    public Task<Result<IReadOnlyList<BranchAccessSummary>>> GetPermittedBranchesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success<IReadOnlyList<BranchAccessSummary>>(
        [new BranchAccessSummary(BranchA, "BR-A", "Branch A", true)]));

    public Task<Result> AuthorizeBranchAsync(
      Guid tenantId, long tenantUserId, Guid branchId, CancellationToken cancellationToken = default) =>
      Task.FromResult(branchId == BranchA
        ? Result.Success()
        : Result.Failure(new Error("Branch.Denied", "Denied.")));
  }

  private sealed class StubCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => AttendanceScopeResolverTests.TenantId;
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
