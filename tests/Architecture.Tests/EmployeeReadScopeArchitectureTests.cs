using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Application.Abstractions.Time;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.BuildingBlocks.Tenancy;
using SSAS.BuildingBlocks.Tenancy.Branches;
using SSAS.HR.Contracts.Employment;
using SSAS.BuildingBlocks.Tenancy.Companies;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Employees.Reads;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Domain.Employees;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// THE EXECUTABLE FORM OF ADR-023 DECISION 22 AND ADR-025 DECISION 10 (FP-006C4).
// ==================================================================================================
//
// Both decisions say the same thing about employee reads: the tenant, company and branch dimensions are
// stated EXPLICITLY in every query, "all" is always a materialized list of identifiers, and no global query
// filter is permitted to stand in for any of it. A decision recorded only in a document is a decision that
// survives exactly as long as everyone who read it — these tests are the version that fails a build.
//
// ---- WHAT THESE TESTS DELIBERATELY DO NOT DO.
//
// They do not scan for a naming convention, a comment or an attribute. Every one of them asserts a property
// of the compiled model, the type system or the composed query that a rename cannot satisfy and a comment
// cannot fake — because a guard that can be satisfied by writing the right words is a guard that will be.
//
// In particular, none of these can be made to pass by ADDING A GLOBAL QUERY FILTER: the filter test asserts
// the opposite, and the scope-parameter tests are about the shape of the API, which a filter never touches.
// ---- PLANT RECORD (T-249): the floors here were observed to fire.
//
// An audit listed this file as having no recorded plant. Collapsing every file walk in it -- changing
// the search pattern to `*.csx`, so directories still exist and simply match nothing -- reddens two
// tests. **That is the silent shape**: a walk rooted at a directory THROWS if the directory is gone,
// so the only failure that passes quietly is downstream of the root.
public sealed class EmployeeReadScopeArchitectureTests
{
  private static readonly Assembly HrApplicationAssembly = typeof(IEmployeeReadService).Assembly;

  private static readonly Type[] ScopeTypes =
    [typeof(EmployeeReadScope), typeof(AuthorizedCompanyScope), typeof(AuthorizedBranchScope)];

  // ---- 1. THE SCOPE IS NOT OPTIONAL, AND IT IS NOT LAST.
  //
  // Every read takes it as the FIRST parameter, so an unscoped read is a compile error rather than a review
  // finding. First rather than anywhere is not cosmetic: a trailing optional parameter is the shape that
  // acquires a default value, and a default scope is precisely the thing that must not exist.
  [Fact]
  public void Every_employee_read_requires_a_resolved_scope_as_its_first_parameter()
  {
    var methods = typeof(IEmployeeReadService).GetMethods();

    Assert.NotEmpty(methods);

    foreach (var method in methods)
    {
      var parameters = method.GetParameters();

      Assert.True(
        parameters.Length > 0 && parameters[0].ParameterType == typeof(EmployeeReadScope),
        $"{method.Name} must take an {nameof(EmployeeReadScope)} as its first parameter.");

      Assert.False(
        parameters[0].IsOptional,
        $"{method.Name} must not give its scope a default value.");
    }
  }

  // ---- 2. THERE IS NO SECOND, UNSCOPED ROUTE TO THE SAME DATA.
  //
  // Test 1 protects the interface; this protects against someone adding a helper beside it. Anything in the
  // HR application assembly that returns an employee projection must demand a scope to do it.
  [Fact]
  public void No_other_type_returns_an_employee_projection_without_a_scope()
  {
    Type[] projections =
      [typeof(EmployeeDetail), typeof(EmployeeSummary), typeof(EmployeeBranchHistoryEntry)];

    var unscoped = HrApplicationAssembly.GetTypes()
      .Where(type => type != typeof(IEmployeeReadService))
      .SelectMany(type => type.GetMethods(
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly))
      .Where(method => !method.IsSpecialName && !method.Name.Contains('<', StringComparison.Ordinal))
      .Where(method => projections.Any(projection => Mentions(method.ReturnType, projection)))
      .Where(method => !method.GetParameters().Any(parameter =>
        parameter.ParameterType == typeof(EmployeeReadScope)))
      // The query handlers return projections and take a QUERY, not a scope — they obtain one from the
      // resolver before reading, which is the composition this whole design is built around.
      .Where(method => !method.DeclaringType!.Name.EndsWith("QueryHandler", StringComparison.Ordinal))
      .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
      .ToArray();

    Assert.Empty(unscoped);
  }

  // ---- 3. A SCOPE CANNOT BE FABRICATED.
  //
  // No public constructor, no public factory, no settable property, no subclass. Without this, every other
  // guarantee here reduces to "please pass a real one": a caller could construct a scope naming any company
  // and any branch and the read service would faithfully honour it.
  [Fact]
  public void A_read_scope_cannot_be_constructed_outside_the_assembly_that_resolves_it()
  {
    foreach (var type in ScopeTypes)
    {
      Assert.True(type.IsSealed, $"{type.Name} must be sealed so it cannot be subclassed.");

      Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

      var publicFactories = type
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => Mentions(method.ReturnType, type))
        .ToArray();

      Assert.Empty(publicFactories);

      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        Assert.True(
          property.SetMethod is null || !property.SetMethod.IsPublic,
          $"{type.Name}.{property.Name} must not be publicly settable.");
      }
    }

    // The runtime half of this — that the identifier lists cannot be cast back to a mutable array and
    // appended to after the authorization has passed — is proven in
    // SSAS.HR.Tests.Employees.EmployeeReadScopeTests, which can resolve a real scope through the resolver.
  }

  // ---- 4. EXACTLY ONE FILE CAN MINT ONE, AND IT IS THE RESOLVER.
  //
  // The factories are internal, so the assembly boundary is what stops the outside world; this is what stops
  // the inside. If a second file ever calls a Create factory, that file becomes a second authorization path,
  // and the question "was this scope checked?" stops having one answer.
  [Fact]
  public void Only_the_scope_resolver_mints_a_scope()
  {
    var minting = Directory
      .EnumerateFiles(
        Path.Combine(RepositoryRoot(), "src", "Modules", "HR"), "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
      .Where(path => ScopeTypes.Any(type =>
        File.ReadAllText(path).Contains($"{type.Name}.Create(", StringComparison.Ordinal)))
      .Select(Path.GetFileName)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(["EmployeeScopeResolver.cs"], minting);
  }

  // ---- 5. THE RESOLVER ASKS ALL THREE QUESTIONS.
  //
  // Its constructor is the proof: it depends on the functional permission source, the company authority and
  // the branch authority. A resolver that stopped consulting one of them would have to keep an unused
  // dependency to pass this — and an unused dependency is exactly what a reviewer notices.
  [Fact]
  public void The_scope_resolver_depends_on_all_three_authorization_sources()
  {
    var dependencies = typeof(EmployeeScopeResolver)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    Assert.Contains(typeof(ICurrentUser), dependencies);
    Assert.Contains(typeof(ITenantCompanyAccessResolver), dependencies);
    Assert.Contains(typeof(ITenantBranchAccessResolver), dependencies);
    Assert.Contains(typeof(ICurrentBranchResolver), dependencies);
  }

  // ---- 6. THE TWO SCOPE DIMENSIONS ARE RESOLVED SEPARATELY.
  //
  // Company and Branch are SIBLINGS beneath the tenant, never nested. One resolver answering both would make
  // it possible for a change in either to widen the other — a branch grant quietly conferring company reach,
  // or the reverse.
  [Fact]
  public void Company_and_branch_scope_come_from_separate_authorities()
  {
    Assert.NotSame(typeof(ITenantCompanyAccessResolver), typeof(ITenantBranchAccessResolver));

    Assert.False(
      typeof(ITenantCompanyAccessResolver).IsAssignableFrom(typeof(ITenantBranchAccessResolver)) ||
      typeof(ITenantBranchAccessResolver).IsAssignableFrom(typeof(ITenantCompanyAccessResolver)),
      "Neither scope authority may derive from the other.");

    // Neither authority may name the other's dimension in its own contract: a company method that took a
    // branch identifier, or a branch method that took a company identifier, would be the nesting itself.
    Assert.DoesNotContain(
      typeof(ITenantCompanyAccessResolver).GetMethods().SelectMany(method => method.GetParameters()),
      parameter => parameter.Name!.Contains("branch", StringComparison.OrdinalIgnoreCase));

    Assert.DoesNotContain(
      typeof(ITenantBranchAccessResolver).GetMethods().SelectMany(method => method.GetParameters()),
      parameter => parameter.Name!.Contains("company", StringComparison.OrdinalIgnoreCase));
  }

  // ---- 7. TENANT ADMINISTRATION IS NOT AN HR PERMISSION.
  //
  // Platform.Tenant.Administer widens the two SCOPE dimensions and grants no operation. The resolver must
  // therefore not name it, and must not treat it as an alternative to HR.Employees.View — an administrator
  // who has never been given the HR permission cannot read an employee (ADR-025 decision 8).
  [Fact]
  public void Tenant_administration_cannot_substitute_for_the_employee_view_permission()
  {
    var resolver = ReadHrCode("SSAS.HR.Application", "Employees", "Reads", "EmployeeScopeResolver.cs");

    Assert.Contains($"{nameof(HrPermissionNames)}.{nameof(HrPermissionNames.ViewEmployees)}", resolver, StringComparison.Ordinal);
    Assert.DoesNotContain("Administer", resolver, StringComparison.OrdinalIgnoreCase);

    // And the permission is a genuinely separate value, not an alias of a platform one.
    Assert.StartsWith("HR.", HrPermissionNames.ViewEmployees, StringComparison.Ordinal);

    var hrPermissions = typeof(HrPermissionNames)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Select(field => (string)field.GetValue(null)!)
      .ToArray();

    Assert.Equal(hrPermissions.Length, hrPermissions.Distinct(StringComparer.Ordinal).Count());

    // EVERY HR permission is in HR's own namespace — never an alias of a Platform one, which is what would
    // let tenant administration substitute for a functional permission. FP-007 added a second resource
    // beneath it, so the assertion is on the module prefix rather than on one resource.
    Assert.All(
      hrPermissions,
      permission => Assert.StartsWith("HR.", permission, StringComparison.Ordinal));

    // The resource list is EXACT rather than a prefix wildcard, so a new HR resource has to be added here
    // deliberately. FP-007 added Departments beneath Employees; FP-008 added three more, and the whole
    // point of the enumeration is that each arrival is a decision someone made rather than a name that
    // slipped in (`DEC-POS-0018`).
    Assert.All(
      hrPermissions,
      permission => Assert.True(
        permission.StartsWith("HR.Employees.", StringComparison.Ordinal) ||
        permission.StartsWith("HR.Departments.", StringComparison.Ordinal) ||
        permission.StartsWith("HR.Positions.", StringComparison.Ordinal) ||
        permission.StartsWith("HR.JobGrades.", StringComparison.Ordinal) ||
        permission.StartsWith("HR.SalaryGrades.", StringComparison.Ordinal),
        $"Unexpected HR permission resource: {permission}"));
  }

  // ==================================================================================================
  // 8. THE HEADLINE GUARD: NO GLOBAL QUERY FILTER SCOPES COMPANY OR BRANCH.
  // ==================================================================================================
  //
  // This is ADR-025 decision 10 stated against the COMPOSED EF MODEL — the real one, with the HR contributor
  // applied — so it cannot be satisfied by naming, by a comment, or by testing a different context.
  //
  // WHY A FILTER IS FORBIDDEN RATHER THAN MERELY UNUSED:
  //
  //   * It is SINGLE-VALUED. A filter reads one ambient value, so it cannot express SelectedAuthorizedBranches
  //     or AllAuthorizedBranches at all. Adding one would not implement the scope model; it would quietly
  //     replace it with a narrower one and make the multi-branch modes silently return nothing.
  //   * It is INVISIBLE at the call site. Nobody reading a query can see whether it is scoped.
  //   * It is REMOVABLE with IgnoreQueryFilters(), which turns a scoped read into a tenant-wide one with one
  //     method call, no ceremony and no compiler complaint.
  //
  // The tenant filter stays, and this test asserts it stays: tenant is a routing invariant with exactly one
  // value per context, which is the case a filter actually fits.
  [Fact]
  public void No_global_query_filter_scopes_company_or_branch()
  {
    using var context = ComposedTenantContext();

    foreach (var entity in context.Model.GetEntityTypes())
    {
      var filter = entity.GetQueryFilter()?.ToString();
      if (filter is null)
      {
        continue;
      }

      Assert.DoesNotContain("CompanyId", filter, StringComparison.Ordinal);
      Assert.DoesNotContain("BranchId", filter, StringComparison.Ordinal);
    }
  }

  // ---- 9. AND THE TENANT FILTER IS STILL THERE.
  //
  // The prohibition above is specific. Removing the tenant filter to "be consistent" would delete the one
  // boundary that is genuinely a routing invariant, so the guard asserts both halves.
  [Fact]
  public void The_employee_entity_keeps_its_tenant_filter_and_only_that()
  {
    using var context = ComposedTenantContext();

    var employee = context.Model.FindEntityType(typeof(Employee));
    Assert.NotNull(employee);

    var filter = employee!.GetQueryFilter()?.ToString();
    Assert.NotNull(filter);
    Assert.Contains("TenantId", filter!, StringComparison.Ordinal);
  }

  // ---- 10. THE READS COMPOSE ALL THREE PREDICATES, IN ONE PLACE.
  //
  // Every employee read starts from a single scoped-query method that states tenant, company and branch. A
  // second entry point to the entity set is how one read comes to be written without one of them.
  [Fact]
  public void Every_employee_read_is_composed_through_one_scoped_query()
  {
    var source = ReadHrCode("SSAS.HR.Infrastructure", "Persistence", "EmployeeReadService.cs");

    Assert.Equal(1, CountOccurrences(source, "Set<Employee>()"));

    var scoped = source[source.IndexOf("private static IQueryable<Employee> Scoped(", StringComparison.Ordinal)..];

    Assert.Contains("TenantId == scope.TenantId", scoped, StringComparison.Ordinal);
    Assert.Contains("scope.Companies.CompanyIds.Contains", scoped, StringComparison.Ordinal);
    Assert.Contains("scope.Branches.BranchIds.Contains", scoped, StringComparison.Ordinal);

    // A read that bypassed the filters it does have would defeat the point of writing them.
    Assert.DoesNotContain("IgnoreQueryFilters", source, StringComparison.Ordinal);
  }

  // ---- 11. "ALL" IS A LIST, SO IT CANNOT BECOME "NO PREDICATE".
  //
  // The scope carries materialized identifier lists and NOTHING ELSE: no mode, no flag, no "all" marker. The
  // failure this prevents is specific and historically common — a boolean IsAllBranches that a query reads
  // as "skip the branch condition", which is predicate omission wearing a scope's clothes.
  [Fact]
  public void The_scope_carries_materialized_identifiers_and_no_all_marker()
  {
    Assert.Equal(typeof(IReadOnlyList<Guid>), typeof(AuthorizedCompanyScope).GetProperty("CompanyIds")!.PropertyType);
    Assert.Equal(typeof(IReadOnlyList<Guid>), typeof(AuthorizedBranchScope).GetProperty("BranchIds")!.PropertyType);

    // ⚠ THE LOOPS BELOW ARE SILENT WHEN EMPTY. `ScopeTypes` collapsing, or a binding-flag change that
    // stops yielding public instance properties, leaves every `Assert.False` unexecuted and this green.
    // The exact assertions above guard two NAMED properties; nothing guarded the walk over the rest.
    Assert.NotEmpty(ScopeTypes);

    Assert.NotEmpty(ScopeTypes
      .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      .ToArray());

    foreach (var type in ScopeTypes)
    {
      foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        Assert.False(
          property.PropertyType == typeof(bool) || property.PropertyType.IsEnum,
          $"{type.Name}.{property.Name} would let a query branch on intent instead of filtering on values.");
      }
    }

    // The request MODES exist — a caller has to be able to ask — but they live on the request, which carries
    // no authority, and they are gone by the time a scope exists.
    Assert.True(typeof(EmployeeBranchScopeMode).IsEnum);
    Assert.Empty(typeof(EmployeeReadScope).GetProperties()
      .Where(property => property.PropertyType == typeof(EmployeeBranchScopeMode) ||
        property.PropertyType == typeof(EmployeeCompanyScopeMode)));
  }

  // ---- 12. NO DEFERRED QUERY TYPE CROSSES THE APPLICATION BOUNDARY.
  //
  // Handing a caller a composable query would let them append to — or strip from — the predicate after the
  // scope had been applied. That is the same hole as an unscoped read, reached by a longer route.
  [Fact]
  public void The_read_surface_exposes_no_deferred_query_type()
  {
    var leaking = HrApplicationAssembly.GetExportedTypes()
      .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => !method.IsSpecialName && !method.Name.Contains('<', StringComparison.Ordinal))
        .Select(method => (Member: $"{type.Name}.{method.Name}", Types: method.GetParameters()
          .Select(parameter => parameter.ParameterType)
          .Append(method.ReturnType))))
      .Where(entry => entry.Types.Any(IsDeferredQuery))
      .Select(entry => entry.Member)
      .ToArray();

    Assert.Empty(leaking);
  }

  // ---- 13. THE PAGE SIZE IS BOUNDED, AND OUT-OF-RANGE IS REFUSED RATHER THAN CLAMPED.
  //
  // Search is the widest read in the module. Clamping 5000 to 200 would return a page the caller did not ask
  // for while letting them believe they had seen the rest, so the handler refuses instead.
  [Fact]
  public void Search_paging_is_bounded_and_out_of_range_is_refused()
  {
    Assert.Equal(200, EmployeeSearchCriteria.MaxPageSize);
    Assert.Equal(50, EmployeeSearchCriteria.DefaultPageSize);
    Assert.Equal(1, EmployeeSearchCriteria.DefaultPageNumber);

    var handler = ReadHrCode("SSAS.HR.Application", "Employees", "Reads", "SearchEmployeesQueryHandler.cs");

    Assert.Contains(nameof(EmployeeErrors.InvalidPageNumber), handler, StringComparison.Ordinal);
    Assert.Contains(nameof(EmployeeErrors.InvalidPageSize), handler, StringComparison.Ordinal);
    Assert.DoesNotContain("Math.Min", handler, StringComparison.Ordinal);
    Assert.DoesNotContain("Math.Clamp", handler, StringComparison.Ordinal);
  }

  // ---- 14. THE HISTORY IS REACHED THROUGH ITS EMPLOYEE.
  //
  // EmployeeBranchAssignment is company-owned but NOT branch-owned, so no branch predicate can be written
  // over it and its scope has to be inherited. The read therefore proves the EMPLOYEE is in scope first —
  // without that ordering, a caller confined to one branch could name any employee identifier and learn
  // every branch that employee has ever worked in.
  [Fact]
  public void Branch_history_is_scoped_through_its_employee()
  {
    Assert.True(typeof(ICompanyOwnedEntity).IsAssignableFrom(typeof(EmployeeBranchAssignment)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(EmployeeBranchAssignment)));

    var source = ReadHrCode("SSAS.HR.Infrastructure", "Persistence", "EmployeeReadService.cs");

    var history = source[source.IndexOf(
      "public async Task<IReadOnlyList<EmployeeBranchHistoryEntry>?> GetEmployeeBranchHistoryAsync",
      StringComparison.Ordinal)..];

    var scopeCheck = history.IndexOf("Scoped(context, scope)", StringComparison.Ordinal);
    var assignmentRead = history.IndexOf("Set<EmployeeBranchAssignment>()", StringComparison.Ordinal);

    Assert.True(scopeCheck >= 0, "The history read must establish the employee is in scope.");
    Assert.True(assignmentRead >= 0);
    Assert.True(scopeCheck < assignmentRead, "The scope check must come before the assignment read.");

    // And the assignment query is company- and tenant-scoped in its own right.
    Assert.Contains("assignment.TenantId == scope.TenantId", history, StringComparison.Ordinal);
    Assert.Contains("scope.Companies.CompanyIds.Contains(assignment.CompanyId)", history, StringComparison.Ordinal);
  }

  // ================================================================================================
  // 15. THE WRITE REPOSITORY CANNOT BECOME A READ BACK DOOR.
  // ================================================================================================
  //
  // `IEmployeeRepository` is the WRITE path: it hands back a tracked aggregate for a command to mutate, and
  // it is scoped by the caller already knowing an identifier. It is also the most obvious place for someone
  // to add `GetAllAsync()` or `FindByCompanyAsync(...)` — a method that would compile, look idiomatic, and
  // bypass every guarantee in this file, because none of the tests above examine it.
  //
  // So its surface is ENUMERATED. Adding a method here fails this test and forces the author to justify it,
  // which is exactly the conversation that should happen.
  [Fact]
  public void The_employee_repository_surface_is_the_approved_write_path_only()
  {
    var methods = typeof(IEmployeeRepository).GetMethods()
      .Select(method => method.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(
      [
        "AddAsync",
        // Appends one history row. Append-only: there is no update and no remove.
        "AppendBranchAssignmentAsync",
        // FP-007 Phase 3. The same, for the department log, with the same absence of any counterpart.
        "AppendDepartmentAssignmentAsync",
        // FP-008 Phase 3. The third history log, identical in shape and in its lack of a counterpart.
        "AppendPositionAssignmentAsync",
        // Uniqueness probes. They return a BOOLEAN, never a row, so they disclose nothing beyond the answer
        // the unique index would give anyway — and they are company-scoped by argument.
        "EmployeeNumberExistsAsync",
        // ---- FP-007 PHASE 3. THE ONE METHOD HERE THAT READS A DIFFERENT TABLE, and the justification this
        // test exists to force.
        //
        // Employee creation and department change must both prove a destination department exists in the
        // caller's company and is Active. That fact lives on Departments, so something has to read it.
        //
        // It is NOT a read back door, and the shape is what makes that true: it takes the company as an
        // argument and filters on it, it returns a two-field record rather than a Department, and it returns
        // NULL for anything outside the company instead of a refusal. No employee row is reachable through
        // it, and no department outside the caller's company is distinguishable from one that does not
        // exist.
        "FindAssignableDepartmentAsync",
        // ---- FP-009 PHASE 1. THE SAME QUESTION, ASKED BY CODE, AND THE JUSTIFICATION THIS TEST FORCES.
        //
        // An import file names a department by CODE, because nobody types a GUID into a spreadsheet
        // (`OD-DOC-004`). The shape is identical to its by-identifier sibling in every respect that makes
        // that one safe: the company is an argument and is filtered on, a two-field record comes back rather
        // than a Department, and anything outside the company is NULL rather than a refusal.
        //
        // THAT LAST PROPERTY MATTERS MORE HERE THAN ANYWHERE. A code is human-readable and a file can carry
        // five thousand guesses; if a code in another company were distinguishable from one that exists
        // nowhere, an import would be an enumeration oracle for other companies' org structure, one
        // rejection message at a time. It is a SIBLING rather than a parameter on the existing method
        // precisely so that predicate is written out and visible rather than selected by a discriminator.
        "FindAssignableDepartmentByCodeAsync",
        // ---- FP-008 PHASE 3. THE SECOND METHOD READING A DIFFERENT TABLE, on identical terms.
        //
        // Employee creation and `ChangePosition` must both prove a destination position exists in the
        // caller's company and is Active (`BRULE-POS-0016`, `BRULE-POS-0013`), and that fact lives on
        // Positions. Same shape, same justification: company as an argument, a two-field record rather than
        // a Position, and NULL rather than a refusal for anything outside the company.
        //
        // NOTE WHAT IS NOT HERE: no count of employees by position. That capability exists, but it belongs
        // to `IEmployeeReadService` where an `EmployeeReadScope` is required — a count taken on this
        // interface would be unscoped by branch, which is the disclosure `api-contracts.md` documents the
        // field as avoiding. It was written here first and moved; the move is the point.
        "FindAssignablePositionAsync",
        // FP-009 Phase 1. The position half of the by-code pair, on exactly the terms stated above.
        "FindAssignablePositionByCodeAsync",
        // Single aggregate by identifier, tracked, for a command about to mutate it.
        "GetByIdAsync",
        "NationalIdExistsAsync"
      ],
      methods);

    // AND NONE OF THEM RETURNS A COLLECTION. A multi-row read here would be an unscoped search under
    // another name — the single-aggregate shape is what keeps this path from becoming one.
    foreach (var method in typeof(IEmployeeRepository).GetMethods())
    {
      Assert.False(
        ReturnsManyEmployees(method.ReturnType),
        $"{method.Name} must not return more than one Employee.");
    }
  }

  // ================================================================================================
  // 18. THE STANDING DIRECTORY HAS EXACTLY ONE CALLER, AND IT IS THE SEAM (T-090, AC-SS-0012).
  // ================================================================================================
  //
  // `IEmploymentStandingDirectory` is answered by the SAME class as the placement directory, so it opens no
  // new door in the employee-set list. What it does open is a second question that can be asked ABOUT an
  // employee from outside HR, and the value of the answer is that ONE place acts on it.
  //
  // **The ruling that put the refusal at the resolver rather than in each self-service read only holds
  // while there is one caller.** A second injection site would be a second place deciding what a terminated
  // employee may reach — which is the per-handler shape `REQ-SS-0003` rejected, arriving through a caller
  // instead of through a handler.
  //
  // So: an exact inventory of one, the same shape as its neighbour. **A second requires a person.**
  [Fact]
  [Trait("Criterion", "AC-SS-0012")]
  public void Only_the_user_employee_resolver_injects_the_standing_directory()
  {
    // Every assembly that references SSAS.BuildingBlocks.Tenancy and could therefore ask for this contract.
    var candidates = new[]
    {
      typeof(SSAS.Platform.Infrastructure.Persistence.Queries.UserEmployeeResolver).Assembly,
      typeof(SSAS.Platform.Application.Permissions.PlatformPermissionNames).Assembly,
      typeof(SSAS.Payroll.Application.Reads.PayrollSelfServiceScopeResolver).Assembly,
      typeof(SSAS.Attendance.Application.Approval.LeaveApprovalRouter).Assembly,
      HrApplicationAssembly,
      typeof(SSAS.HR.Infrastructure.ServiceCollectionExtensions).Assembly
    };

    var injecting = candidates
      .Distinct()
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .Any(constructor => constructor.GetParameters()
          .Any(parameter => parameter.ParameterType == typeof(IEmploymentStandingDirectory))))
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // NOT VACUOUS. An empty result would mean the sweep stopped finding anything — and this assertion
    // would then pass forever while the caller set grew unwatched.
    Assert.NotEmpty(injecting);

    // ---- TWO, AS OF T-092, AND THE SECOND WAS APPROVED RATHER THAN ADMITTED.
    //
    // `LinkEmployeeToTenantUserCommandHandler` asks the same question for the opposite reason: the resolver
    // asks *may this employee still be reached*, the link handler asks *does this employee exist and is
    // their employment current* before writing a row that cannot have a foreign key.
    //
    // **The two act on the answer DIFFERENTLY and that is the point of them both being listed.** The
    // resolver collapses `Unknown` and `Ended` into one refusal, because its caller is an end user and
    // telling them apart would disclose that a record exists. The link handler distinguishes them, because
    // its caller is an administrator acting on an employee they named and can already read.
    //
    // A third injector still requires a person — and would have to state which of those two it is.
    Assert.Equal(
      ["LinkEmployeeToTenantUserCommandHandler", "UserEmployeeResolver"],
      injecting);
  }

  // ================================================================================================
  // 17. AND ONLY ONE TYPE MAY INJECT THE UNAUTHORIZED DOOR (FP-015, T-088).
  // ================================================================================================
  //
  // `EmployeeCompanyDirectoryService` is the one employee read that applies NO company authorization. Its
  // safety rests on two things, and only the first is structural:
  //
  //   1. tenant isolation, enforced by the tenant database's global filter;
  //   2. **the identifier never being caller-supplied** — it arrives from `UserEmployeeLink`, keyed by
  //      tenant and tenant-user, so the only reachable value is the caller's own employee.
  //
  // **The second lives entirely in who calls it.** A second injection site could pass any employee
  // identifier it liked and would face no company check — which is the property the door list polices,
  // arriving through a caller instead of through a file.
  //
  // So the caller set is an exact inventory, the same shape as the door list. **A second injection site
  // requires a person, exactly as a fourth door did.**
  [Fact]
  [Trait("Decision", "DEC-PAY-0017")]
  public void Only_the_self_service_scope_resolvers_inject_the_placement_directory()
  {
    // Every assembly that references SSAS.HR.Contracts and could therefore ask for this contract.
    var candidates = new[]
    {
      typeof(SSAS.Payroll.Application.Reads.PayrollSelfServiceScopeResolver).Assembly,
      typeof(SSAS.Attendance.Application.Approval.LeaveApprovalRouter).Assembly,
      HrApplicationAssembly,
      typeof(SSAS.HR.Infrastructure.ServiceCollectionExtensions).Assembly
    };

    var injecting = candidates
      .Distinct()
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .Any(constructor => constructor.GetParameters()
          .Any(parameter => parameter.ParameterType == typeof(IEmployeePlacementDirectory))))
      .Select(type => type.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // NOT VACUOUS. An empty result would mean the sweep stopped finding anything — and this assertion
    // would then pass forever while the caller set grew unwatched.
    Assert.NotEmpty(injecting);

    // TWO, AS OF T-089. `AttendanceSelfServiceScopeResolver` joined by the same route the first one did:
    // it asks *which employee am I*, derives its scope from that employee's own placement, and never takes
    // an employee identifier from a caller. **A third still needs a person** — that is the entire point of
    // an exact set rather than a `.Any()`.
    Assert.Equal(
      ["AttendanceSelfServiceScopeResolver", "PayrollSelfServiceScopeResolver"],
      injecting);
  }

  // ================================================================================================
  // 16. ONLY TWO FILES MAY TOUCH THE EMPLOYEE ENTITY SET AT ALL.
  // ================================================================================================
  //
  // Test 10 proves the READ SERVICE composes its query in one place. This proves nobody opens a THIRD
  // door: a new file anywhere in HR calling `context.Set<Employee>()` would be a query no guard here
  // inspects, scoped by nothing but its author's memory.
  [Fact]
  public void Only_the_read_service_and_the_write_repository_reach_the_employee_set()
  {
    var touching = HrProductionSources()
      .Where(path =>
      {
        var source = ReadCode(path);

        return source.Contains("Set<Employee>()", StringComparison.Ordinal) ||
          source.Contains("DbSet<Employee>", StringComparison.Ordinal) ||
          source.Contains("Set<EmployeeBranchAssignment>()", StringComparison.Ordinal) ||
          source.Contains("DbSet<EmployeeBranchAssignment>", StringComparison.Ordinal);
      })
      .Select(Path.GetFileName)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    // ---- AND A FOURTH FILE, RULED 2026-08-28 (FP-015, T-088). ITS LOCK IS DIFFERENT AND THAT IS THE RULING.
  //
  // `EmployeeCompanyDirectoryService` serves FP-015's self-service read: given an employee, which company.
  // **It applies NO company authorization, unlike the three above** — and that is not a lost check, it is
  // the ruling. The caller it exists for is an ordinary employee reading their own record, and an employee
  // is not necessarily granted authority to administer the company they work for. Requiring one would
  // refuse exactly the caller the door was opened for.
  //
  // **A second door with a good lock is a sanctioned shape. This door's lock has two parts:**
  //
  //   1. TENANT ISOLATION — the tenant database's global filter, so another tenant's employee is not found.
  //   2. A SINGLE ASSERTED CALLER — the identifier is never caller-supplied. It arrives from
  //      `UserEmployeeLink`, keyed by tenant and tenant-user, so the only reachable value is the caller's
  //      own employee.
  //
  // **Part 2 lives entirely in WHO CALLS IT, which is prose, and prose expires (`DEC-L-072`).** So it is
  // asserted rather than described — see `Only_the_self_service_scope_resolvers_inject_the_placement_directory`
  // below. A second injection site then requires a person, exactly as a fourth door did.

  // ---- THREE FILES, BECAUSE THERE ARE NOW TWO SANCTIONED READ SHAPES (RULED 2026-08-24, DEC-PAY-0017).
    //
    // `EmployeeRosterService.cs` joined this list by a RULING, not by growing an exception. The distinction
    // is the whole reason the list is exact:
    //
    //   * `EmployeeReadService` serves HR CALLERS — tenant + company + BRANCH, every predicate from a proven
    //     `EmployeeReadScope`. Guarded by tests 10 and 12 above.
    //   * `EmployeeRosterService` serves the PAYROLL MODULE across a contract — tenant + company, no branch,
    //     with the company set resolved LIVE inside the implementation and never accepted from a caller.
    //     Guarded by the roster tests below, which are as strict as tests 10 and 12.
    //
    // The branch predicate never protected "employees" in the abstract; it protects HR callers from
    // exceeding their branch authority. A cross-module read is a different authority regime and gets its own
    // structural shape — **with its own lock**. A second door with a good lock is a sanctioned shape; a
    // second door with a note saying "this one is fine" is an exception.
    //
    // ---- AND A FOURTH, BY THE SAME MECHANISM (RULED 2026-08-25, OD-ATT-0007).
    //
    // The paragraph above set the condition: *a fourth file is a defect until someone rules otherwise AND
    // WRITES IT A GUARD.* Both halves are discharged here rather than one.
    //
    //   * `EmployeeApproverDirectoryService` serves ATTENDANCE across a contract — tenant + company, no
    //     branch, company set resolved LIVE inside the implementation. It walks the department-manager
    //     chain `OD-ATT-0007` ruled, which Attendance cannot do itself because departments are HR's.
    //
    // NO BRANCH PREDICATE, and for a THIRD distinct reason worth distinguishing from the roster's: approval
    // runs through the DEPARTMENT tree, and `Employee` carries branch and department as SIBLING dimensions.
    // A branch predicate would silently truncate the chain for anyone whose manager sits elsewhere, and the
    // failure would read as "no approver found" rather than as a bug.
    //
    // Guarded by 16c below, which is as strict as 16b.
    //
    // A FIFTH file appearing here is a defect until someone rules otherwise and writes it a guard.
    Assert.Equal(
      [
        "EmployeeApproverDirectoryService.cs",
        "EmployeePlacementDirectoryService.cs",
        "EmployeeReadService.cs",
        "EmployeeRepository.cs",
        "EmployeeRosterService.cs"
      ],
      touching);
  }

  // ================================================================================================
  // 16b. THE ROSTER SHAPE, PINNED AS STRICTLY AS THE FIRST (DEC-PAY-0017).
  // ================================================================================================
  //
  // Everything test 10 asserts about the HR read shape, asserted about the roster's — because a sanctioned
  // second shape that nobody guards is an exception wearing a ruling's clothes.
  [Fact]
  [Trait("Decision", "DEC-PAY-0017")]
  public void The_roster_read_is_composed_through_one_scoped_query()
  {
    var source = ReadHrCode("SSAS.HR.Infrastructure", "Persistence", "EmployeeRosterService.cs");

    // Exactly one query site, exactly as the HR read service is held to.
    Assert.Equal(1, CountOccurrences(source, "Set<Employee>()"));

    var scoped = source[source.IndexOf("private static IQueryable<Employee> RosterScoped(", StringComparison.Ordinal)..];

    Assert.Contains("employee.TenantId == tenantId", scoped, StringComparison.Ordinal);
    Assert.Contains("employee.CompanyId == companyId", scoped, StringComparison.Ordinal);

    // NO BRANCH PREDICATE, BY DESIGN — and asserted, so that adding one is a deliberate act rather than a
    // tidy-up. Payroll pays the company; a branch-scoped roster would mean payroll ran per branch, which
    // contradicts company-owned runs (`OD-PAY-0005`), company-scoped periods (`OD-PAY-0002`) and
    // `OD-GL-0005`'s precedent that finance is not branch-dimensional.
    Assert.DoesNotContain("BranchId", scoped, StringComparison.Ordinal);

    Assert.DoesNotContain("IgnoreQueryFilters", source, StringComparison.Ordinal);
  }

  // ================================================================================================
  // 16c. THE APPROVER-DIRECTORY SHAPE, PINNED AS STRICTLY AS THE OTHER TWO (OD-ATT-0007).
  // ================================================================================================
  //
  // Everything 16b asserts about the roster's shape, asserted about this one — because a sanctioned third
  // shape that nobody guards is an exception wearing a ruling's clothes, which is precisely what the ruling
  // that created the SECOND shape refused to allow.
  [Fact]
  [Trait("Decision", "OD-ATT-0007")]
  public void The_approver_directory_read_is_composed_through_one_scoped_query()
  {
    var source = ReadHrCode("SSAS.HR.Infrastructure", "Persistence", "EmployeeApproverDirectoryService.cs");

    // Exactly TWO `Set<Employee>()` sites, and the number is deliberate rather than tolerated: one resolves
    // the requester's own department through `ApproverScoped`, the other excludes terminated managers inside
    // the seat join. A third would be a query no guard inspects.
    Assert.Equal(2, CountOccurrences(source, "Set<Employee>()"));

    var scoped = source[source.IndexOf("private static IQueryable<Employee> ApproverScoped(", StringComparison.Ordinal)..];

    Assert.Contains("employee.TenantId == tenantId", scoped, StringComparison.Ordinal);
    Assert.Contains("employee.CompanyId == companyId", scoped, StringComparison.Ordinal);

    // NO BRANCH PREDICATE, BY DESIGN — approval runs through the DEPARTMENT tree, and branch is a SIBLING
    // dimension. Asserted so that adding one is a deliberate act rather than a tidy-up, and so the reason is
    // discoverable from the failure rather than only from the source comment.
    Assert.DoesNotContain("BranchId", scoped, StringComparison.Ordinal);

    Assert.DoesNotContain("IgnoreQueryFilters", source, StringComparison.Ordinal);

    // ---- IT REFUSES RATHER THAN RETURNING AN EMPTY LIST, AND HERE THAT MATTERS MORE THAN USUAL.
    //
    // An empty list is a MEANINGFUL ANSWER on this contract: it means "the chain is exhausted, use the root
    // fallback". Returning it for an authorization failure would route an unauthorized caller into the
    // permission-holder fallback path instead of refusing them.
    Assert.Contains("throw new UnauthorizedAccessException", source, StringComparison.Ordinal);

    // ---- AND IT NEVER WRITES (DEC-ATT-0003).
    //
    // Targeted at the EF WRITE APIs specifically. A bare `.Add(` was the first form of this assertion and
    // it matched `visited.Add(node)` — a HashSet guarding the parent-chain walk against a cycle. The guard
    // was wrong, not the code, and a guard that fires on the wrong thing teaches people to edit guards.
    Assert.DoesNotContain("SaveChanges", source, StringComparison.Ordinal);
    Assert.DoesNotContain("AddAsync(", source, StringComparison.Ordinal);
    Assert.DoesNotContain("context.Add", source, StringComparison.Ordinal);
    Assert.DoesNotContain(".Update(", source, StringComparison.Ordinal);
    Assert.DoesNotContain("RemoveRange(", source, StringComparison.Ordinal);
    Assert.DoesNotContain("ExecuteDelete", source, StringComparison.Ordinal);
    Assert.DoesNotContain("ExecuteUpdate", source, StringComparison.Ordinal);
  }

  // ---- THE AUTHORITY IS RESOLVED LIVE, NEVER ACCEPTED.
  //
  // The property that makes a scope trustworthy is *checked live, just now*. The roster has no scope object,
  // so it must earn that property by doing the work itself: it resolves permitted companies from
  // `ITenantCompanyAccessResolver` on every call. A set accepted as a parameter would be forgeable by
  // whoever called, which is precisely what the scope types exist to prevent.
  [Fact]
  [Trait("Decision", "DEC-PAY-0017")]
  public void The_roster_resolves_its_own_company_authority_and_accepts_none()
  {
    var source = ReadHrCode("SSAS.HR.Infrastructure", "Persistence", "EmployeeRosterService.cs");

    Assert.Contains("ITenantCompanyAccessResolver", source, StringComparison.Ordinal);
    Assert.Contains("GetPermittedCompaniesAsync", source, StringComparison.Ordinal);

    // No scope or company-set type may appear in the roster's own signature: the contract passes a company
    // IDENTIFIER, and authority is resolved rather than supplied.
    foreach (var method in typeof(SSAS.HR.Contracts.Employment.IEmployeeRoster).GetMethods())
    {
      foreach (var parameter in method.GetParameters())
      {
        Assert.False(
          parameter.ParameterType.Name.Contains("Scope", StringComparison.Ordinal) ||
          parameter.ParameterType.Name.Contains("AuthorizedCompanySet", StringComparison.Ordinal),
          $"{method.Name} accepts {parameter.ParameterType.Name}; the roster must resolve its own authority.");
      }
    }
  }

  // ---- THE FIELD LIST NEVER WIDENS (the field-never-leaves pattern, FP-009).
  //
  // A contract is forever-ish. A roster that returned `EmployeeDetail` would let every future Payroll
  // feature read HR personal data with NO CALL-SITE CHANGE for anyone to review — the widening would be
  // invisible in a diff of the consumer. So the shape is pinned by name, and `NationalId` is named
  // explicitly because it is the field `OD-DOC-006` protected.
  [Fact]
  [Trait("Decision", "DEC-PAY-0017")]
  public void The_roster_projection_carries_only_the_ratified_fields()
  {
    var properties = typeof(SSAS.HR.Contracts.Employment.EmploymentRecord)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .Select(property => property.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(
      ["CompanyId", "EmployeeId", "EmploymentDateUtc", "TerminationDateUtc"],
      properties);

    // Named individually, so a future widening has to delete an assertion that says why it exists.
    foreach (var forbidden in new[] { "NationalId", "FullName", "EmployeeNumber", "BranchId", "DepartmentId", "PositionId", "Status" })
    {
      Assert.DoesNotContain(forbidden, properties, StringComparer.Ordinal);
    }
  }

  // ---- 17. NO DEFERRED QUERY OVER EMPLOYEE ESCAPES INFRASTRUCTURE.
  //
  // Test 12 guards the application assembly's exported surface. This guards the INFRASTRUCTURE assembly too,
  // where the query actually lives: a public `IQueryable<Employee>` there would let a caller append to — or
  // strip from — the composed predicate after the scope had been applied. The one such query in the design
  // is `EmployeeReadService.Scoped`, which is private.
  [Fact]
  public void No_employee_query_escapes_the_infrastructure_boundary()
  {
    var leaking = new[] { HrApplicationAssembly, typeof(HrTenantModelContributor).Assembly }
      .SelectMany(assembly => assembly.GetTypes())
      .Where(type => type.IsPublic || type.IsNestedPublic)
      .SelectMany(type => type
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(method => !method.IsSpecialName)
        .Select(method => (Member: $"{type.Name}.{method.Name}", Types: method.GetParameters()
          .Select(parameter => parameter.ParameterType)
          .Append(method.ReturnType)))
        .Concat(type
          .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
          .Select(property => (Member: $"{type.Name}.{property.Name}", Types: new[] { property.PropertyType }.AsEnumerable()))))
      .Where(entry => entry.Types.Any(IsDeferredQuery))
      .Select(entry => entry.Member)
      .ToArray();

    Assert.Empty(leaking);
  }

  // ================================================================================================
  // 18. THE MODEL UNDER TEST IS THE COMPOSED ONE.
  // ================================================================================================
  //
  // THIS IS THE TEST THAT KEEPS TESTS 8 AND 9 HONEST.
  //
  // `TenantDbContext` builds a genuinely different model when no contributor is supplied — that is by design
  // (`FP-006C3-pre`), and maintenance tooling relies on it. A filter test run against the contributor-free
  // model would find no Employee at all, iterate a handful of Platform entity types, and PASS VACUOUSLY
  // while proving nothing whatsoever about HR.
  //
  // So the composed model is asserted to actually contain both HR entities before any conclusion is drawn
  // from it, and the contributor-free model is asserted NOT to — which is what makes "composed" a
  // distinction rather than an assumption.
  [Fact]
  public void The_model_the_guards_inspect_is_the_hr_contributed_one()
  {
    using var composed = ComposedTenantContext();

    Assert.NotNull(composed.Model.FindEntityType(typeof(Employee)));
    Assert.NotNull(composed.Model.FindEntityType(typeof(EmployeeBranchAssignment)));

    using var contributorFree = ContributorFreeTenantContext();

    Assert.Null(contributorFree.Model.FindEntityType(typeof(Employee)));
    Assert.Null(contributorFree.Model.FindEntityType(typeof(EmployeeBranchAssignment)));
  }

  // ================================================================================================
  // 19. THE SECURITY SCOPE PARTICIPATES BEFORE PAGINATION.
  // ================================================================================================
  //
  // Paging a wider set and then filtering it is the classic version of this bug. It looks correct — every
  // returned row is authorized — but the page is drawn from rows the caller may not see, so pages come back
  // short or empty, the total count leaks the size of the wider set, and which rows are missing depends on
  // data the caller has no right to know about.
  //
  // The scope is the query ROOT here, so Skip/Take can only ever narrow an already-scoped set. This asserts
  // that ordering in the source, and that the count is taken from the same scoped query.
  [Fact]
  public void The_scope_is_applied_before_paging_and_the_count_uses_the_same_query()
  {
    var source = ReadHrCode("SSAS.HR.Infrastructure", "Persistence", "EmployeeReadService.cs");

    var search = source[source.IndexOf(
      "public async Task<PagedResult<EmployeeSummary>> SearchEmployeesAsync", StringComparison.Ordinal)..];

    var scoped = search.IndexOf("Scoped(context, scope)", StringComparison.Ordinal);
    var count = search.IndexOf("CountAsync(", StringComparison.Ordinal);
    var skip = search.IndexOf(".Skip(", StringComparison.Ordinal);
    var take = search.IndexOf(".Take(", StringComparison.Ordinal);

    Assert.True(scoped >= 0, "The search must start from the scoped query.");
    Assert.True(count > scoped, "The total count must be taken from the scoped query.");
    Assert.True(skip > scoped && take > scoped, "Paging must be applied to the scoped query.");

    // AND THE FILTERS ARE IN SQL, not applied to a materialized list. A ToArrayAsync/ToListAsync before the
    // paging would mean the rows were fetched first and narrowed in memory.
    var materialize = search.IndexOf("ToArrayAsync(", StringComparison.Ordinal);
    Assert.True(materialize > take, "The query must be materialized only after paging.");
    Assert.DoesNotContain("AsEnumerable(", search, StringComparison.Ordinal);
    Assert.DoesNotContain("ToListAsync(", search, StringComparison.Ordinal);
  }

  private static bool ReturnsManyEmployees(Type type)
  {
    if (type.IsGenericType)
    {
      var arguments = type.GetGenericArguments();

      if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) &&
        arguments.Any(argument => argument == typeof(Employee)))
      {
        return true;
      }

      return arguments.Any(ReturnsManyEmployees);
    }

    return false;
  }

  private static IEnumerable<string> HrProductionSources() => Directory
    .EnumerateFiles(Path.Combine(RepositoryRoot(), "src", "Modules", "HR"), "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
      !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

  private static bool IsDeferredQuery(Type type) =>
    type.Name.StartsWith("IQueryable", StringComparison.Ordinal) ||
    type.Name.StartsWith("IOrderedQueryable", StringComparison.Ordinal) ||
    (type.IsGenericType && type.GetGenericArguments().Any(IsDeferredQuery));

  private static bool Mentions(Type type, Type target) =>
    type == target || (type.IsGenericType && type.GetGenericArguments().Any(argument => Mentions(argument, target)));

  private static int CountOccurrences(string source, string value)
  {
    var count = 0;
    for (var index = source.IndexOf(value, StringComparison.Ordinal);
      index >= 0;
      index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
    {
      count++;
    }

    return count;
  }

  // The REAL model: Platform's tenant entities plus HR's contribution, exactly as the Host composes it. A
  // filter test run against a contributor-free context would prove nothing about Employee.
  private static TenantDbContext ComposedTenantContext()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    return new TenantDbContext(
      options,
      new ModelUser(),
      new ModelTenant(),
      new ModelClock(),
      modelContributors: [new HrTenantModelContributor()]);
  }

  // ---- CODE ONLY, COMMENTS STRIPPED.
  //
  // These guards assert what the code DOES. Reading the prose alongside it would make a test fail because
  // someone explained the rule it enforces, and pass because someone deleted the explanation — exactly
  // backwards.
  private static string ReadCode(string path) => StripComments(File.ReadAllText(path));

  private static string ReadHrCode(params string[] segments) => StripComments(ReadHrSource(segments));

  private static string StripComments(string source) =>
    string.Join(
      Environment.NewLine,
      source
        .Split('\n')
        .Select(line =>
        {
          var comment = line.IndexOf("//", StringComparison.Ordinal);
          return comment >= 0 ? line[..comment] : line;
        }));

  private static TenantDbContext ContributorFreeTenantContext()
  {
    var options = new DbContextOptionsBuilder<TenantDbContext>()
      .UseSqlServer("Server=model-only;Database=model-only;Integrated Security=True")
      .Options;

    return new TenantDbContext(options, new ModelUser(), new ModelTenant(), new ModelClock());
  }

  private static string ReadHrSource(params string[] segments)
  {
    var path = Path.Combine(
      new[] { RepositoryRoot(), "src", "Modules", "HR" }.Concat(segments).ToArray());

    Assert.True(File.Exists(path), $"Source not found: {path}");

    return File.ReadAllText(path);
  }

  private static string RepositoryRoot()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new InvalidOperationException("Repository root not found.");
  }

  private sealed class ModelUser : ICurrentUser
  {
    public string? UserId => "architecture-tests";

    public string? UserName => "architecture-tests";

    public string? Email => null;

    public Guid? CompanyId => null;

    public string? SessionId => null;

    public string? TokenId => null;

    public IReadOnlyCollection<string> Roles => [];

    public IReadOnlyCollection<string> Permissions => [];
  }

  private sealed class ModelTenant : ICurrentTenant
  {
    public Guid? TenantId => null;
  }

  private sealed class ModelClock : IDateTimeProvider
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }
}
