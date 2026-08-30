using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Pagination;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;

namespace SSAS.HR.Tests.Employees;

// THE SCOPE RESOLVER AND THE QUERY HANDLERS (FP-006C4, ADR-023 decision 22, ADR-025 decision 10).
//
// These are the decision tests: which requests are refused, which are permitted, and — just as important —
// what is NOT consulted. The real-SQL proofs live in Integration.Tests and answer the different question of
// whether the composed predicate actually restricts what the database returns.
public sealed class EmployeeReadScopeTests
{
  private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
  private static readonly Guid CompanyA = Guid.Parse("22222222-2222-2222-2222-222222222222");
  private static readonly Guid CompanyB = Guid.Parse("33333333-3333-3333-3333-333333333333");
  private static readonly Guid BranchA = Guid.Parse("44444444-4444-4444-4444-444444444444");
  private static readonly Guid BranchB = Guid.Parse("55555555-5555-5555-5555-555555555555");
  private static readonly Guid BranchC = Guid.Parse("66666666-6666-6666-6666-666666666666");

  // ================================================================================================
  // THE FUNCTIONAL DIMENSION
  // ================================================================================================

  [Fact]
  public async Task A_user_without_the_view_permission_is_refused()
  {
    var resolver = Resolver(permissions: []);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.ReadPermissionDenied, scope.Error);
  }

  // ---- THE ONE THAT MATTERS MOST (ADR-025 decision 8).
  //
  // Platform.Tenant.Administer widens the two SCOPE dimensions — an administrator reaches every active
  // company and branch in the tenant — and grants NO operation. An administrator who was never given the HR
  // permission cannot read an employee, and the scope they would have had is irrelevant to that.
  [Fact]
  public async Task Tenant_administration_does_not_grant_the_employee_read()
  {
    var resolver = Resolver(
      permissions: ["Platform.Tenant.Administer"],
      // Deliberately generous: the administrator's scope resolves to everything.
      companies: [CompanyA, CompanyB],
      branches: [BranchA, BranchB, BranchC]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.ReadPermissionDenied, scope.Error);
  }

  // The permission is checked BEFORE either scope authority is consulted, so a caller with no HR authority
  // cannot use timing or a differing refusal to learn anything about the tenant's shape.
  [Fact]
  public async Task The_permission_is_checked_before_any_scope_is_resolved()
  {
    var companies = new RecordingCompanyAccess([CompanyA]);
    var branches = new RecordingBranchAccess([BranchA]);

    var resolver = Resolver(permissions: [], companyAccess: companies, branchAccess: branches);

    await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.Equal(0, companies.Calls);
    Assert.Equal(0, branches.Calls);
  }

  // ================================================================================================
  // THE COMPANY DIMENSION
  // ================================================================================================

  [Fact]
  public async Task The_current_company_is_re_authorized_rather_than_trusted()
  {
    var companies = new RecordingCompanyAccess([CompanyA]);

    var resolver = Resolver(selectedCompany: CompanyA, companyAccess: companies);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.True(scope.IsSuccess);
    Assert.Equal([CompanyA], scope.Value.Companies.CompanyIds);
    Assert.True(companies.Calls > 0);
  }

  [Fact]
  public async Task A_selected_company_the_user_cannot_reach_is_refused()
  {
    // The context reports CompanyB; the authority permits only CompanyA. The authority wins.
    var resolver = Resolver(selectedCompany: CompanyB, companies: [CompanyA]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.CompanyScopeDenied, scope.Error);
  }

  [Fact]
  public async Task No_established_company_refuses_rather_than_widening()
  {
    var resolver = Resolver(companyEstablished: false);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.CompanyScopeDenied, scope.Error);
  }

  [Fact]
  public async Task All_authorized_companies_materializes_the_set()
  {
    var resolver = Resolver(companies: [CompanyA, CompanyB]);

    var scope = await resolver.ResolveAsync(
      new EmployeeScopeRequest(CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies));

    Assert.True(scope.IsSuccess);
    Assert.Equal([CompanyA, CompanyB], scope.Value.Companies.CompanyIds);
  }

  // ---- AN EMPTY SET REFUSES. It never degrades to unfiltered.
  [Fact]
  public async Task An_empty_authorized_company_set_refuses_the_read()
  {
    var resolver = Resolver(companies: []);

    var scope = await resolver.ResolveAsync(
      new EmployeeScopeRequest(CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.CompanyScopeDenied, scope.Error);
  }

  // ================================================================================================
  // THE BRANCH DIMENSION
  // ================================================================================================

  [Fact]
  public async Task The_default_scope_is_the_current_branch_alone()
  {
    var resolver = Resolver(currentBranch: BranchA, branches: [BranchA, BranchB]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest());

    Assert.True(scope.IsSuccess);
    Assert.Equal([BranchA], scope.Value.Branches.BranchIds);
  }

  [Fact]
  public async Task A_selected_subset_of_authorized_branches_is_permitted()
  {
    var resolver = Resolver(branches: [BranchA, BranchB, BranchC]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: [BranchA, BranchC]));

    Assert.True(scope.IsSuccess);
    Assert.Equal([BranchA, BranchC], scope.Value.Branches.BranchIds);
  }

  // ---- REFUSED, NOT INTERSECTED.
  //
  // Quietly dropping BranchC and returning BranchA's employees would tell the caller they had seen every
  // branch they asked for. They had not.
  [Fact]
  public async Task A_selection_that_is_not_a_subset_is_refused_rather_than_narrowed()
  {
    var resolver = Resolver(branches: [BranchA]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: [BranchA, BranchC]));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.BranchScopeDenied, scope.Error);
  }

  // Unauthorized, inactive and nonexistent are indistinguishable, so the read cannot be used to probe for
  // the existence of a branch identifier.
  [Fact]
  public async Task An_unknown_branch_is_refused_identically_to_an_unauthorized_one()
  {
    var unauthorized = await Resolver(branches: [BranchA]).ResolveAsync(new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: [BranchB]));

    var unknown = await Resolver(branches: [BranchA]).ResolveAsync(new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: [Guid.NewGuid()]));

    Assert.True(unauthorized.IsFailure);
    Assert.Equal(unauthorized.Error, unknown.Error);
  }

  [Fact]
  public async Task All_authorized_branches_materializes_the_set()
  {
    var resolver = Resolver(branches: [BranchA, BranchB, BranchC]);

    var scope = await resolver.ResolveAsync(
      new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches));

    Assert.True(scope.IsSuccess);
    Assert.Equal([BranchA, BranchB, BranchC], scope.Value.Branches.BranchIds);
  }

  [Fact]
  public async Task An_empty_authorized_branch_set_refuses_the_read()
  {
    var resolver = Resolver(branches: []);

    var scope = await resolver.ResolveAsync(
      new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.BranchScopeDenied, scope.Error);
  }

  // ---- A STRAY BRANCH LIST IS A MALFORMED REQUEST, not something to ignore.
  //
  // Ignoring it would let a caller believe they had narrowed a read that in fact ran against the current
  // branch only — or, worse, against all of them.
  [Theory]
  [InlineData(EmployeeBranchScopeMode.CurrentBranch)]
  [InlineData(EmployeeBranchScopeMode.AllAuthorizedBranches)]
  public async Task Branch_identifiers_supplied_for_another_mode_are_rejected(EmployeeBranchScopeMode mode)
  {
    var resolver = Resolver(branches: [BranchA, BranchB]);

    var scope = await resolver.ResolveAsync(
      new EmployeeScopeRequest(BranchScope: mode, SelectedBranchIds: [BranchA]));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.InvalidReadScope, scope.Error);
  }

  [Fact]
  public async Task A_selection_mode_with_no_branch_identifiers_is_rejected()
  {
    var resolver = Resolver(branches: [BranchA]);

    var scope = await resolver.ResolveAsync(
      new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches));

    Assert.True(scope.IsFailure);
    Assert.Equal(EmployeeErrors.InvalidReadScope, scope.Error);
  }

  // ---- NOTHING IS CACHED.
  //
  // Access is revocable inside a request's lifetime, so both authorities are re-asked on every resolution. A
  // resolver that answered the second call from the first one's result would serve a read on authority that
  // had already been taken away.
  [Fact]
  public async Task Every_resolution_re_asks_both_authorities()
  {
    var companies = new RecordingCompanyAccess([CompanyA]);
    var branches = new RecordingBranchAccess([BranchA, BranchB]);
    var resolver = Resolver(companyAccess: companies, branchAccess: branches);

    var request = new EmployeeScopeRequest(BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches);

    await resolver.ResolveAsync(request);
    var firstCompanyCalls = companies.Calls;
    var firstBranchCalls = branches.Calls;

    await resolver.ResolveAsync(request);

    Assert.Equal(firstCompanyCalls * 2, companies.Calls);
    Assert.Equal(firstBranchCalls * 2, branches.Calls);
  }

  // ---- A RESOLVED SCOPE CANNOT BE EDITED AFTERWARDS.
  //
  // An IReadOnlyList<Guid> holding a Guid[] casts straight back to Guid[]. Without a genuinely immutable
  // backing collection the "read-only" is a suggestion: anything holding a scope could append a company or a
  // branch to it AFTER the authorization that produced it had already passed, and every structural guard
  // would still call that scope proven. The resolver decides what is in a scope; this keeps it that way.
  [Fact]
  public async Task A_resolved_scope_cannot_be_widened_after_the_fact()
  {
    var resolver = Resolver(companies: [CompanyA, CompanyB], branches: [BranchA, BranchB]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest(
      CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies,
      BranchScope: EmployeeBranchScopeMode.AllAuthorizedBranches));

    Assert.True(scope.IsSuccess);

    Assert.Throws<NotSupportedException>(
      () => ((IList<Guid>)scope.Value.Companies.CompanyIds).Add(Guid.NewGuid()));

    Assert.Throws<NotSupportedException>(
      () => ((IList<Guid>)scope.Value.Branches.BranchIds).Add(Guid.NewGuid()));
  }

  // Mutating the list the CALLER supplied must not reach into a scope already resolved from it either.
  [Fact]
  public async Task Mutating_the_requested_branch_list_afterwards_does_not_widen_the_scope()
  {
    var requested = new List<Guid> { BranchA };
    var resolver = Resolver(branches: [BranchA, BranchB]);

    var scope = await resolver.ResolveAsync(new EmployeeScopeRequest(
      BranchScope: EmployeeBranchScopeMode.SelectedAuthorizedBranches,
      SelectedBranchIds: requested));

    Assert.True(scope.IsSuccess);

    requested.Add(BranchB);

    Assert.Equal([BranchA], scope.Value.Branches.BranchIds);
  }

  // ================================================================================================
  // THE HANDLERS
  // ================================================================================================

  [Fact]
  public async Task A_refused_scope_never_reaches_the_database()
  {
    var reads = new RecordingReadService();
    var handler = new GetEmployeeQueryHandler(Resolver(permissions: []), reads);

    var result = await handler.HandleAsync(new GetEmployeeQuery(Guid.NewGuid()));

    Assert.True(result.IsFailure);
    Assert.Equal(0, reads.Calls);
  }

  // Out of scope and nonexistent give the same answer, so the read cannot confirm that an employee exists
  // in a company or branch the caller cannot reach.
  [Fact]
  public async Task An_employee_outside_the_scope_is_reported_as_not_found()
  {
    var handler = new GetEmployeeQueryHandler(Resolver(), new RecordingReadService());

    var result = await handler.HandleAsync(new GetEmployeeQuery(Guid.NewGuid()));

    Assert.True(result.IsFailure);
    Assert.Equal(EmployeeErrors.NotFound, result.Error);
  }

  // Cross-company reach exists to make a SEARCH meaningful. An identifier lookup has no such need, so
  // Milestone 1 does not offer it there.
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task Single_employee_reads_are_current_company_only(bool history)
  {
    var request = new EmployeeScopeRequest(CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies);
    var resolver = Resolver(companies: [CompanyA, CompanyB]);
    var reads = new RecordingReadService();

    var error = history
      ? (await new GetEmployeeBranchHistoryQueryHandler(resolver, reads)
        .HandleAsync(new GetEmployeeBranchHistoryQuery(Guid.NewGuid(), request))).Error
      : (await new GetEmployeeQueryHandler(resolver, reads)
        .HandleAsync(new GetEmployeeQuery(Guid.NewGuid(), request))).Error;

    Assert.Equal(EmployeeErrors.InvalidReadScope, error);
    Assert.Equal(0, reads.Calls);
  }

  // ---- REFUSED, NOT CLAMPED.
  //
  // Silently reducing 5000 to 200 would return a page the caller did not ask for while letting them believe
  // they had seen the rest.
  // ⚠ EACH ROW NOW NAMES THE PARAMETER IT REFUSES (T-260). One code for three conditions meant this
  // theory asserted the same thing four times; a client fixing the wrong parameter retried and failed
  // identically, and the test could not have told the difference either.
  [Theory]
  [InlineData(0, 50, false)]
  [InlineData(1, 0, true)]
  [InlineData(1, 201, true)]
  [InlineData(-1, 50, false)]
  public async Task Out_of_range_paging_is_refused(int pageNumber, int pageSize, bool sizeIsTheFault)
  {
    var reads = new RecordingReadService();
    var handler = new SearchEmployeesQueryHandler(Resolver(), reads);

    var result = await handler.HandleAsync(new SearchEmployeesQuery(PageNumber: pageNumber, PageSize: pageSize));

    Assert.True(result.IsFailure);
    Assert.Equal(
      sizeIsTheFault ? EmployeeErrors.InvalidPageSize : EmployeeErrors.InvalidPageNumber,
      result.Error);
    Assert.Equal(0, reads.Calls);
  }

  [Fact]
  public async Task The_search_defaults_are_the_documented_ones()
  {
    var reads = new RecordingReadService();
    var handler = new SearchEmployeesQueryHandler(Resolver(), reads);

    var result = await handler.HandleAsync(new SearchEmployeesQuery());

    Assert.True(result.IsSuccess);
    Assert.NotNull(reads.LastCriteria);
    Assert.Equal(1, reads.LastCriteria!.PageNumber);
    Assert.Equal(50, reads.LastCriteria.PageSize);

    // Null statuses, not a list — the read service is what applies "Active and Inactive", in SQL, so the
    // default cannot be lost by a caller passing an explicitly empty list.
    Assert.Null(reads.LastCriteria.Statuses);
  }

  [Fact]
  public async Task The_maximum_page_size_is_accepted()
  {
    var handler = new SearchEmployeesQueryHandler(Resolver(), new RecordingReadService());

    var result = await handler.HandleAsync(new SearchEmployeesQuery(PageSize: 200));

    Assert.True(result.IsSuccess);
  }

  // ---- SEARCH IS THE ONE READ THAT MAY SPAN COMPANIES.
  [Fact]
  public async Task Search_accepts_all_authorized_companies()
  {
    var reads = new RecordingReadService();
    var handler = new SearchEmployeesQueryHandler(Resolver(companies: [CompanyA, CompanyB]), reads);

    var result = await handler.HandleAsync(new SearchEmployeesQuery(
      new EmployeeScopeRequest(CompanyScope: EmployeeCompanyScopeMode.AllAuthorizedCompanies)));

    Assert.True(result.IsSuccess);
    Assert.Equal([CompanyA, CompanyB], reads.LastScope!.Companies.CompanyIds);
  }

  private static EmployeeScopeResolver Resolver(
    IReadOnlyCollection<string>? permissions = null,
    Guid? selectedCompany = null,
    bool companyEstablished = true,
    Guid? currentBranch = null,
    IReadOnlyList<Guid>? companies = null,
    IReadOnlyList<Guid>? branches = null,
    ITenantCompanyAccessResolver? companyAccess = null,
    ITenantBranchAccessResolver? branchAccess = null) =>
    new(
      companyAccess ?? new RecordingCompanyAccess(companies ?? [CompanyA]),
      branchAccess ?? new RecordingBranchAccess(branches ?? [BranchA]),
      new StubCurrentBranch(currentBranch ?? BranchA),
      new StubCurrentCompany(companyEstablished ? selectedCompany ?? CompanyA : null),
      new StubCurrentTenant(),
      new StubCurrentTenantUser(),
      new StubCurrentUser(permissions ?? [HrPermissionNames.ViewEmployees]));

  // ---- THE AUTHORITIES ARE STUBBED, THE RESOLVER IS NOT.
  //
  // These stand in for the Platform resolvers so the tests can state exactly what a user may reach. The
  // question they answer is whether the RESOLVER composes those answers correctly and refuses when it must;
  // whether the Platform resolvers answer correctly is proven against real SQL elsewhere.
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
        : Result.Failure(new Error("Company.ScopeDenied", "denied")));
    }
  }

  private sealed class RecordingBranchAccess(IReadOnlyList<Guid> permitted) : ITenantBranchAccessResolver
  {
    public int Calls { get; private set; }

    public Task<Result<IReadOnlyList<BranchAccessSummary>>> GetPermittedBranchesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default)
    {
      Calls++;

      return Task.FromResult(Result.Success<IReadOnlyList<BranchAccessSummary>>(
        permitted.Select(id => new BranchAccessSummary(id, "CODE", "Name", false)).ToArray()));
    }

    public Task<Result> AuthorizeBranchAsync(
      Guid tenantId, long tenantUserId, Guid branchId, CancellationToken cancellationToken = default)
    {
      Calls++;

      return Task.FromResult(permitted.Contains(branchId)
        ? Result.Success()
        : Result.Failure(new Error("Branch.ScopeDenied", "denied")));
    }
  }

  private sealed class RecordingReadService : IEmployeeReadService
  {
    public int Calls { get; private set; }

    public EmployeeReadScope? LastScope { get; private set; }

    public EmployeeSearchCriteria? LastCriteria { get; private set; }

    public Task<EmployeeDetail?> GetEmployeeAsync(
      EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;

      return Task.FromResult<EmployeeDetail?>(null);
    }

    public Task<PagedResult<EmployeeSummary>> SearchEmployeesAsync(
      EmployeeReadScope scope, EmployeeSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;
      LastCriteria = criteria;

      return Task.FromResult(new PagedResult<EmployeeSummary>([], criteria.PageNumber, criteria.PageSize, 0));
    }

    public Task<IReadOnlyList<EmployeeBranchHistoryEntry>?> GetEmployeeBranchHistoryAsync(
      EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;

      return Task.FromResult<IReadOnlyList<EmployeeBranchHistoryEntry>?>(null);
    }

    // The position history, recording the scope like every other read here (FP-008 Phase 4).
    public Task<IReadOnlyList<EmployeePositionHistoryEntry>?> GetEmployeePositionHistoryAsync(
      EmployeeReadScope scope, Guid employeeId, CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;

      return Task.FromResult<IReadOnlyList<EmployeePositionHistoryEntry>?>(null);
    }

    // Records the scope like every other method here, which is what lets the scope-required tests treat the
    // holder count as one more read that cannot be issued without one (FP-008 Phase 3).
    public Task<int> CountEmployeesByPositionAsync(
      EmployeeReadScope scope, Guid positionId, CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;

      return Task.FromResult(0);
    }

    // The department count is the same kind of read and is recorded the same way (FP-007 employeeCount,
    // shipped 2026-08-22).
    // FP-009. Recorded on the same terms as every other read here: the SCOPE is what this double exists to
    // capture, so an export that reached the read service without one would be visible in `LastScope`.
    public Task<IReadOnlyList<EmployeeExportRow>> ExportEmployeesAsync(
      EmployeeReadScope scope,
      EmployeeSearchCriteria criteria,
      int ceiling,
      CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;

      return Task.FromResult<IReadOnlyList<EmployeeExportRow>>([]);
    }

    public Task<int> CountEmployeesByDepartmentAsync(
      EmployeeReadScope scope, Guid departmentId, CancellationToken cancellationToken = default)
    {
      Calls++;
      LastScope = scope;

      return Task.FromResult(0);
    }
  }

  private sealed class StubCurrentBranch(Guid branchId) : ICurrentBranchResolver
  {
    public Task<Result<Guid>> ResolveCurrentBranchAsync(CancellationToken cancellationToken = default) =>
      Task.FromResult(Result.Success(branchId));
  }

  private sealed class StubCurrentCompany(Guid? companyId) : ICurrentCompany
  {
    public Guid? CompanyId => companyId;
  }

  private sealed class StubCurrentTenant : ICurrentTenant
  {
    public Guid? TenantId => EmployeeReadScopeTests.TenantId;
  }

  private sealed class StubCurrentTenantUser : ICurrentTenantUser
  {
    public long? TenantUserId => 42;
  }

  private sealed class StubCurrentUser(IReadOnlyCollection<string> permissions) : ICurrentUser
  {
    public string? UserId => "hr-tests";

    public string? UserName => "hr-tests";

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => permissions;
  }
}
