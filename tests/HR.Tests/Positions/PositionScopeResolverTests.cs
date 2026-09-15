using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions.Reads;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Tests.Positions;

// THE POSITION SCOPE RESOLVER (FP-008 Phase 2, ADR-025 decisions 8 and 10, DEC-POS-0018, DEC-POS-0020).
//
// The decision tests: which requests are refused, which are permitted, and — just as important — what is
// NOT consulted. Whether the composed predicate actually restricts what the database returns is a different
// question, answered against real SQL in Integration.Tests.
//
// ---- THE ONE THING THIS FILE PROVES THAT THE DEPARTMENT EQUIVALENT DOES NOT.
//
// There are THREE view permissions over three families, and the separation is only worth having if holding
// one grants none of the others. That is what the cross-family theory below is for: `HR.SalaryGrades.View`
// exists because pay bands are more sensitive than job titles, and a caller holding every position and job
// grade permission must still be refused the amounts.
public sealed class PositionScopeResolverTests
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
    var scope = await Resolver(permissions: []).ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, scope.Error);
  }

  // ---- THE ONE THAT MATTERS MOST (ADR-025 decision 8).
  //
  // `Platform.Tenant.Administer` widens the COMPANY dimension — an administrator reaches every active
  // company in the tenant — and grants NO operation. An administrator who was never given the HR permission
  // cannot read a position, and the scope they would have had is irrelevant to that.
  //
  // ⚠ CITED BY 269: `AC-POS-0044`, THE CLAUSE THIS CAN REACH — *`Platform.Tenant.Administer` … grants none
  // of these permissions.* The criterion's other clause, *widens company scope*, is NOT asserted here: the
  // generous `companies: [CompanyA, CompanyB]` above is ARRANGEMENT, not an assertion, and the widening is
  // a Platform behaviour (`ADR-025` d8) proven against real SQL by the administrator company-access tests
  // in `CompanyOwnershipBoundarySqlServerTests`. Cited for the half it carries, with the half it cannot.
  //
  // The write half is the Theory below, over all nine write permissions in all three families.
  [Fact]
  [Trait("Decision", "ADR-025")]
  [Trait("Criterion", "AC-POS-0044")]
  public async Task Tenant_administration_alone_does_not_grant_the_position_read()
  {
    var resolver = Resolver(
      permissions: ["Platform.Tenant.Administer"],
      // Deliberately generous: the administrator's scope resolves to everything.
      companies: [CompanyA, CompanyB]);

    var scope = await resolver.ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, scope.Error);
  }

  // ---- AND THE SAME HOLDS FOR EVERY WRITE PERMISSION IN ALL THREE FAMILIES.
  [Theory]
  [InlineData(HrPermissionNames.CreatePositions)]
  [InlineData(HrPermissionNames.UpdatePositions)]
  [InlineData(HrPermissionNames.DeactivatePositions)]
  [InlineData(HrPermissionNames.CreateJobGrades)]
  [InlineData(HrPermissionNames.UpdateJobGrades)]
  [InlineData(HrPermissionNames.DeactivateJobGrades)]
  [InlineData(HrPermissionNames.CreateSalaryGrades)]
  [InlineData(HrPermissionNames.UpdateSalaryGrades)]
  [InlineData(HrPermissionNames.DeactivateSalaryGrades)]
  [Trait("Decision", "ADR-025")]
  // ⚠ CITED BY 269: `AC-POS-0044`'s write half, paired with the read test above. Nine inline cases rather
  // than one, because "grants no write" is a claim about every write permission and a single case would
  // prove it of whichever one happened to be chosen.
  [Trait("Criterion", "AC-POS-0044")]
  public async Task Tenant_administration_alone_grants_no_position_write(string permission)
  {
    var resolver = Resolver(permissions: ["Platform.Tenant.Administer"], companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(permission, CompanyA);

    Assert.True(authorized.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, authorized.Error);
  }

  // Holding the employee and department permissions says nothing about positions. The sets are independent.
  [Fact]
  public async Task Employee_and_department_permissions_do_not_grant_position_reads()
  {
    var resolver = Resolver(permissions:
    [
      HrPermissionNames.ViewEmployees,
      HrPermissionNames.UpdateEmployees,
      HrPermissionNames.ViewDepartments,
      HrPermissionNames.UpdateDepartments
    ]);

    var scope = await resolver.ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, scope.Error);
  }

  // ================================================================================================
  // THE THREE FAMILIES ARE THREE PERMISSIONS, AND HOLDING ONE GRANTS NONE OF THE OTHERS (DEC-POS-0018)
  // ================================================================================================
  //
  // The most important row is the last: a caller holding EVERY position and job grade permission still
  // cannot obtain a `SalaryGradeReadScope`. That separation is the reason `HR.SalaryGrades.View` exists —
  // reading the organization chart must not also disclose the pay structure.
  //
  // ⚠ CITED BY 269: `AC-POS-0045` — *`HR.SalaryGrades.View` is a distinct permission; holding
  // `HR.Positions.View` ALONE does not read salary amounts.* This is the criterion's literal sentence. Two
  // siblings carry the rest of the claim and all three are cited: the next test proves it survives a caller
  // holding EVERY position and job-grade permission plus the salary WRITE permission, and the one after
  // proves the CONVERSE — `HR.SalaryGrades.View` alone grants only the salary scope, so the separation is
  // not merely "positions do not reach pay" but a genuine partition.
  //
  // Each carries its own positive control: the position scope is asserted to SUCCEED before the two grade
  // scopes are asserted to fail, so a resolver refusing everything cannot pass.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  [Trait("Criterion", "AC-POS-0045")]
  public async Task The_position_view_permission_grants_neither_grade_ladder()
  {
    var resolver = Resolver(permissions: [HrPermissionNames.ViewPositions]);

    Assert.True((await resolver.ResolvePositionsAsync(new PositionScopeRequest())).IsSuccess);

    var jobGrades = await resolver.ResolveJobGradesAsync(new PositionScopeRequest());
    Assert.True(jobGrades.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, jobGrades.Error);

    var salaryGrades = await resolver.ResolveSalaryGradesAsync(new PositionScopeRequest());
    Assert.True(salaryGrades.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, salaryGrades.Error);
  }

  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  // ⚠ CITED BY 269: `AC-POS-0045`'s strongest leg — *ALONE* stretched to its limit. Nine permissions,
  // including the salary WRITE permission, and the pay band is still not readable.
  [Trait("Criterion", "AC-POS-0045")]
  public async Task Every_position_and_job_grade_permission_together_still_discloses_no_pay_band()
  {
    var resolver = Resolver(permissions:
    [
      HrPermissionNames.ViewPositions,
      HrPermissionNames.CreatePositions,
      HrPermissionNames.UpdatePositions,
      HrPermissionNames.DeactivatePositions,
      HrPermissionNames.ViewJobGrades,
      HrPermissionNames.CreateJobGrades,
      HrPermissionNames.UpdateJobGrades,
      HrPermissionNames.DeactivateJobGrades,
      // Deliberately included: the WRITE permission on salary grades is not the READ permission, and a
      // caller who may price a band is not thereby entitled to browse the ladder.
      HrPermissionNames.UpdateSalaryGrades
    ]);

    Assert.True((await resolver.ResolveJobGradesAsync(new PositionScopeRequest())).IsSuccess);

    var salaryGrades = await resolver.ResolveSalaryGradesAsync(new PositionScopeRequest());

    Assert.True(salaryGrades.IsFailure);
    Assert.Equal(PositionErrors.PermissionDenied, salaryGrades.Error);
  }

  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  // ⚠ CITED BY 269: `AC-POS-0045`'s CONVERSE. Without this leg the criterion is satisfied by a permission
  // that grants nothing at all; with it, `HR.SalaryGrades.View` is proven to be a real permission granting
  // exactly one scope. *Distinct* is a two-way claim.
  [Trait("Criterion", "AC-POS-0045")]
  public async Task The_salary_grade_view_permission_grants_only_the_salary_grade_scope()
  {
    var resolver = Resolver(permissions: [HrPermissionNames.ViewSalaryGrades]);

    Assert.True((await resolver.ResolveSalaryGradesAsync(new PositionScopeRequest())).IsSuccess);
    Assert.True((await resolver.ResolvePositionsAsync(new PositionScopeRequest())).IsFailure);
    Assert.True((await resolver.ResolveJobGradesAsync(new PositionScopeRequest())).IsFailure);
  }

  // ================================================================================================
  // THE COMPANY DIMENSION
  // ================================================================================================

  [Fact]
  public async Task The_current_company_is_resolved_and_materialized()
  {
    var scope = await Resolver().ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(scope.IsSuccess);
    Assert.Equal(TenantId, scope.Value.TenantId);
    Assert.Equal([CompanyA], scope.Value.Companies.CompanyIds);
  }

  [Fact]
  public async Task All_authorized_companies_resolves_to_the_whole_permitted_set()
  {
    var resolver = Resolver(companies: [CompanyA, CompanyB]);

    var scope = await resolver.ResolvePositionsAsync(
      new PositionScopeRequest(PositionCompanyScopeMode.AllAuthorizedCompanies));

    Assert.True(scope.IsSuccess);
    Assert.Equal([CompanyA, CompanyB], scope.Value.Companies.CompanyIds);
  }

  // ---- AN EMPTY AUTHORIZED SET REFUSES. It never degrades to unfiltered.
  //
  // Asserted for all three families, because the refusal lives in the shared half of the resolver and a
  // change there would break all three at once — which is exactly the kind of change a single-family test
  // would let through for the other two.
  [Theory]
  [InlineData(HrPermissionNames.ViewPositions)]
  [InlineData(HrPermissionNames.ViewJobGrades)]
  [InlineData(HrPermissionNames.ViewSalaryGrades)]
  // ⚠ CITED BY 269: `AC-POS-0008` — *a caller whose authorized company set resolves empty is refused; NO
  // READ RETURNS UNFILTERED RESULTS.* The second half is why "refused" is the right assertion and an empty
  // result set would be the wrong one: an empty result claims something about the DATA, a refusal claims
  // something about the CALLER, and only the second is true here.
  [Trait("Criterion", "AC-POS-0008")]
  public async Task An_empty_authorized_company_set_is_refused_rather_than_unfiltered(string permission)
  {
    var resolver = Resolver(permissions: [permission], companies: []);
    var request = new PositionScopeRequest(PositionCompanyScopeMode.AllAuthorizedCompanies);

    var error = permission switch
    {
      HrPermissionNames.ViewJobGrades => (await resolver.ResolveJobGradesAsync(request)).Error,
      HrPermissionNames.ViewSalaryGrades => (await resolver.ResolveSalaryGradesAsync(request)).Error,
      _ => (await resolver.ResolvePositionsAsync(request)).Error
    };

    Assert.Equal(PositionErrors.CompanyScopeDenied, error);
  }

  [Fact]
  public async Task An_unestablished_company_context_is_refused()
  {
    var resolver = Resolver(companyEstablished: false);

    var scope = await resolver.ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(scope.IsFailure);
    Assert.Equal(PositionErrors.CompanyScopeDenied, scope.Error);
  }

  [Fact]
  public async Task A_company_outside_the_authorized_set_is_refused()
  {
    // The permission IS held, so the refusal below can only come from the company dimension. Without this
    // the test would pass on a PermissionDenied and prove nothing about scope.
    var resolver = Resolver(
      permissions: [HrPermissionNames.UpdatePositions],
      selectedCompany: CompanyB,
      companies: [CompanyA]);

    var authorized = await resolver.AuthorizeAsync(HrPermissionNames.UpdatePositions, CompanyB);

    Assert.True(authorized.IsFailure);
    Assert.Equal(PositionErrors.CompanyScopeDenied, authorized.Error);
  }

  // ---- IT RE-ASKS, EVERY TIME, RATHER THAN CACHING (NFR-POS-0303).
  //
  // Company access is revocable inside a request's lifetime, and a read served from a set captured earlier
  // is precisely the failure the live resolution exists to prevent. Three resolutions across three
  // families, because one shared cache would be invisible to a single-family count.
  [Fact]
  [Trait("Requirement", "NFR-POS-0303")]
  public async Task The_company_authority_is_consulted_on_every_resolution()
  {
    var access = new RecordingCompanyAccess([CompanyA]);
    var resolver = Resolver(
      permissions:
      [
        HrPermissionNames.ViewPositions,
        HrPermissionNames.ViewJobGrades,
        HrPermissionNames.ViewSalaryGrades,
        HrPermissionNames.UpdatePositions
      ],
      companyAccess: access);

    await resolver.ResolvePositionsAsync(new PositionScopeRequest());
    await resolver.ResolveJobGradesAsync(new PositionScopeRequest());
    await resolver.ResolveSalaryGradesAsync(new PositionScopeRequest());
    await resolver.AuthorizeAsync(HrPermissionNames.UpdatePositions, CompanyA);

    Assert.Equal(4, access.Calls);
  }

  // ---- AND RE-ASKING IS NOT THE SAME AS HONOURING THE NEW ANSWER (`AC-POS-0009`, 269).
  //
  // ⚠ THE TEST ABOVE COUNTS CALLS. `Assert.Equal(4, access.Calls)` proves the authority is CONSULTED four
  // times and passes, unchanged, against a resolver that asks every time, ignores every reply, and serves a
  // set captured on the first call. CONSULTING AN AUTHORITY AND OBEYING IT ARE TWO CLAIMS, and only the
  // first was asserted.
  //
  // ⚠⚠ ITS COMMENT MAKES A REAL AND CORRECT ARGUMENT — *three resolutions across three families, because
  // one shared cache would be invisible to a single-family count* — AND EVERY WORD OF THAT CARE IS SPENT
  // INSIDE A MEASURE OF THE INSTRUMENT'S OWN ACTIVITY RATHER THAN THE SUBJECT'S BEHAVIOUR. Careful
  // reasoning inside a wrong frame is more convincing than careless reasoning, which is why this survived.
  //
  // THE REVOCATION LEAVES THE SET NON-EMPTY, ON PURPOSE. Setting `Permitted` to `[]` would refuse for the
  // reason `An_empty_authorized_company_set_is_refused_rather_than_unfiltered` already owns, and this test
  // would silently become a second copy of it. `[CompanyB]` means the authority still answers with a real
  // grant that no longer covers the company this caller established.
  //
  // ONE FAMILY, DELIBERATELY. The refusal lives in the resolver's shared half and the empty-set Theory
  // above already spans all three — but this criterion is about the POSITION read, and a three-family
  // assertion would redden under a job-grade regression while naming a position criterion.
  //
  // NO NEW TOKEN: one resolver instance, one established company context, asked twice. That clause is
  // carried by REUSING `resolver` rather than by any assertion.
  //
  // ANTI-VACUITY: the first resolution is asserted to SUCCEED and to carry `[CompanyA]`. Without that leg
  // a resolver refusing every read passes perfectly while asserting nothing about revocation.
  [Fact]
  [Trait("Requirement", "NFR-POS-0303")]
  [Trait("Criterion", "AC-POS-0009")]
  public async Task Revoking_company_access_mid_session_refuses_the_next_position_read()
  {
    var access = new RecordingCompanyAccess([CompanyA]);
    var resolver = Resolver(companyAccess: access);

    var before = await resolver.ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(before.IsSuccess, before.IsFailure ? before.Error.Code : null);
    Assert.Equal([CompanyA], before.Value.Companies.CompanyIds);

    access.Permitted = [CompanyB];

    var after = await resolver.ResolvePositionsAsync(new PositionScopeRequest());

    Assert.True(after.IsFailure);
    Assert.Equal(PositionErrors.CompanyScopeDenied, after.Error);
  }

  // ================================================================================================
  // WHAT THE RESOLVER DELIBERATELY DOES NOT CONSULT (DEC-POS-0020)
  // ================================================================================================
  //
  // There is no branch dimension. A Position is not branch-owned, so branch scope does not decide whether
  // one is visible — and the resolver takes no branch resolver at all, which is a stronger statement than
  // "it does not call one".
  [Fact]
  [Trait("Decision", "DEC-POS-0020")]
  public void The_resolver_takes_no_branch_dependency()
  {
    var parameters = typeof(PositionScopeResolver)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();

    Assert.DoesNotContain(parameters, name => name.Contains("Branch", StringComparison.Ordinal));
  }

  // ---- AND NO SCOPE CAN BE FABRICATED.
  //
  // Private constructor, internal factory, one call site. Asserted rather than assumed, because the whole
  // "a read cannot omit a scope predicate" design rests on it: a public constructor anywhere would turn
  // every guarantee in `PositionReadScopes.cs` into a comment.
  [Theory]
  [InlineData(typeof(PositionReadScope))]
  [InlineData(typeof(JobGradeReadScope))]
  [InlineData(typeof(SalaryGradeReadScope))]
  [InlineData(typeof(AuthorizedPositionCompanyScope))]
  public void No_read_scope_exposes_a_public_constructor_or_factory(Type scopeType)
  {
    Assert.Empty(scopeType.GetConstructors());

    Assert.DoesNotContain(
      scopeType.GetMethods(
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.Static |
        System.Reflection.BindingFlags.DeclaredOnly),
      method => method.Name == "Create");
  }

  private static PositionScopeResolver Resolver(
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
      new StubCurrentUser(permissions ?? [HrPermissionNames.ViewPositions]));

  // ---- THE AUTHORITIES ARE STUBBED, THE RESOLVER IS NOT.
  //
  // These stand in for the Platform resolvers so the tests can state exactly what a user may reach. Whether
  // the Platform resolvers answer correctly is proven against real SQL elsewhere.
  private sealed class RecordingCompanyAccess(IReadOnlyList<Guid> permitted) : ITenantCompanyAccessResolver
  {
    public int Calls { get; private set; }

    // ⚠ SETTABLE, AND ITS ABSENCE WAS THE GAP (`AC-POS-0009`, 269). This stub read an immutable
    // primary-constructor parameter, so NO TEST IN THIS FILE COULD EXPRESS A REVOCATION — the authority
    // could not give a different answer the second time it was asked. That is why the mid-session
    // company×read cell was empty here and in the department resolver: not an oversight in a list of
    // tests, but a fixture that made the test unwritable.
    public IReadOnlyList<Guid> Permitted { get; set; } = permitted;

    public Task<Result<IReadOnlyList<CompanyAccessSummary>>> GetPermittedCompaniesAsync(
      Guid tenantId, long tenantUserId, CancellationToken cancellationToken = default)
    {
      Calls++;

      return Task.FromResult(Result.Success<IReadOnlyList<CompanyAccessSummary>>(
        Permitted.Select(id => new CompanyAccessSummary(id, "CODE", "Name")).ToArray()));
    }

    public Task<Result> AuthorizeCompanyAsync(
      Guid tenantId, long tenantUserId, Guid companyId, CancellationToken cancellationToken = default)
    {
      Calls++;

      return Task.FromResult(Permitted.Contains(companyId)
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
    public Guid? TenantId => PositionScopeResolverTests.TenantId;
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
