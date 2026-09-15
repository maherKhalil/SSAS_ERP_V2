using System.Reflection;
using SSAS.HR.Application.Permissions;
using SSAS.HR.Application.Positions;
using SSAS.HR.Application.Positions.Reads;

namespace SSAS.Architecture.Tests;

// THE POSITION APPLICATION BOUNDARIES (FP-008 Phase 2, ADR-025, DEC-POS-0018, DEC-POS-0020).
//
// Most of what makes this slice correct is a SHAPE rather than a behaviour — which command can express
// which change, which dimension a scope carries, which permission produces which scope type. A shape is
// invisible at the call site and silent when it regresses, so these pin the ones with a real failure mode.
public sealed class PositionApplicationArchitectureTests
{
  private static readonly Assembly HrApplicationAssembly = typeof(CreatePositionCommand).Assembly;

  // The three families' command types, named once. Every theory below runs over all of them, because a rule
  // that held for positions and quietly stopped holding for salary grades is exactly the regression these
  // guards exist to catch.
  private static readonly Type[] MutationCommands =
  [
    typeof(CreatePositionCommand),
    typeof(UpdatePositionCommand),
    typeof(DeactivatePositionCommand),
    typeof(ReactivatePositionCommand),
    typeof(CreateJobGradeCommand),
    typeof(UpdateJobGradeCommand),
    typeof(DeactivateJobGradeCommand),
    typeof(ReactivateJobGradeCommand),
    typeof(CreateSalaryGradeCommand),
    typeof(UpdateSalaryGradeCommand),
    typeof(DeactivateSalaryGradeCommand),
    typeof(ReactivateSalaryGradeCommand)
  ];

  // ================================================================================================
  // NO COMMAND CARRIES A TENANT, AND NONE CARRIES A BRANCH OR A DEPARTMENT
  // ================================================================================================
  //
  // Not "is ignored" — CANNOT BE EXPRESSED (`BRULE-POS-0001`). Tenant is stamped by the persistence
  // boundary from trusted server context; a branch would contradict `DEC-POS-0001`, and a department would
  // contradict `OD-POS-003` by creating a second source of truth for an employee's department.
  [Fact]
  [Trait("Decision", "DEC-POS-0001")]
  public void No_position_command_carries_a_tenant_branch_or_department()
  {
    foreach (var command in MutationCommands)
    {
      var properties = command.GetProperties().Select(property => property.Name).ToArray();

      Assert.DoesNotContain(properties, name => name.Contains(Names.Tenant, StringComparison.Ordinal));
      Assert.DoesNotContain(properties, name => name.Contains(Names.Branch, StringComparison.Ordinal));
      Assert.DoesNotContain(properties, name => name.Contains(Names.Department, StringComparison.Ordinal));
    }
  }

  // ---- THE ORDINARY UPDATE CANNOT MUTATE STATUS OR OWNERSHIP.
  //
  // Status has its own operation with its own permission (`DEC-DEP-0025`), so an update command carrying a
  // status field would let a caller holding only `Update` close a position someone deliberately opened.
  [Theory]
  [InlineData(typeof(UpdatePositionCommand))]
  [InlineData(typeof(UpdateJobGradeCommand))]
  [InlineData(typeof(UpdateSalaryGradeCommand))]
  [Trait("Decision", "DEC-POS-0011")]
  public void The_ordinary_update_carries_no_status_and_no_company(Type command)
  {
    var properties = command.GetProperties().Select(property => property.Name).ToArray();

    Assert.DoesNotContain(properties, name => name.Contains(Names.Status, StringComparison.Ordinal));
    Assert.DoesNotContain(properties, name => name.Contains(Names.Company, StringComparison.Ordinal));
  }

  // ---- EVERY MUTATION OF AN EXISTING RECORD CARRIES A ROW VERSION (NFR-POS-0302, DEC-POS-0021).
  //
  // The create commands are excluded because there is nothing yet to be stale about. Everything else is
  // included, including the lifecycle pair — a deactivation racing an update is exactly the interleave the
  // token exists to lose.
  [Fact]
  [Trait("Requirement", "NFR-POS-0302")]
  // ⚠ CITED BY 269: `AC-POS-0047`'s UNIVERSAL quantifier — *EVERY position and grade mutation*. Its partner
  // `PositionApplicationSqlServerTests.A_stale_row_version_is_refused_on_every_family` proves the REFUSAL
  // behaves correctly but exercises one mutation per family; this enumerates the mutation commands and
  // asserts each carries the token, which is the only leg that scales to a command added tomorrow.
  //
  // Note the exclusion is principled and stated: creates are skipped because there is nothing yet to be
  // stale about, so the filter is not narrowing the population to make the assertion pass.
  [Trait("Criterion", "AC-POS-0047")]
  public void Every_position_mutation_of_an_existing_record_requires_a_row_version()
  {
    foreach (var command in MutationCommands.Where(type =>
      !type.Name.StartsWith("Create", StringComparison.Ordinal)))
    {
      var rowVersion = command.GetProperty("RowVersion");

      Assert.True(rowVersion is not null, $"{command.Name} carries no RowVersion.");
      Assert.Equal(typeof(byte[]), rowVersion!.PropertyType);
    }
  }

  // The append-only assignment record carries none, because it is never updated (`DEC-POS-0021`). Asserted
  // rather than assumed: adding one would suggest the history is editable.
  //
  // ⚠ CITED BY 269 FOR TWO CRITERIA. `AC-POS-0037` — *the entity implements `IAppendOnlyEntity` and the
  // guard that asserts append-only entities carry no `RowVersion` covers it* — and `AC-POS-0057`, which is
  // the ownership classification. Both are named by the criteria and both are now asserted here.
  //
  // ⚠⚠ STRENGTHENED BY 269, APPLYING A LESSON THIS REPOSITORY ALREADY LEARNED ELSEWHERE. This read
  // `GetProperty("RowVersion")` with a BARE STRING and no positive. `Type.GetProperty` returns null for a
  // property that is ABSENT and for one that is MISSPELT, so a rename left it green over a lookup that
  // could not hit. `JournalDomainTests.A_posted_journal_has_no_row_version_and_a_draft_does` fixed exactly
  // this in item 258 and its comment states the reason.
  // Both halves are now bound to a compiled symbol, and the POSITIVE is what proves the lookup can hit.
  //
  // ⚠⚠⚠ CORRECTED BY 271, AND THE CORRECTION IS THE USEFUL PART. This said *the Position analogue never
  // received the fix*, which reads as an OVERSIGHT inside a sweep that covered this ground. 258's commit
  // (`3063cbe`) touches SIX FILES: `AccountDomainTests`, `JournalDomainTests`, `AuthenticationDomainTests`,
  // `CompanyDomainTests`, `PlatformSupportAuthorityTests`, `TenantLifecycleDomainTests` — all in
  // `Finance.Tests` and `Platform.Tests`. `Architecture.Tests` AND `HR.Tests` WERE NEVER IN ITS POPULATION.
  // The analogue was not missed; it was outside the boundary, which is a different defect with a different
  // fix: 258 bound the sites it enumerated, and nothing has ever enumerated these two suites.
  //
  // ⚠ SO THE RESIDUE IS LIVE AND NAMED RATHER THAN IMPLIED. `EmployeePositionAssignmentDomainTests` line
  // 186 asserts `Assert.Null(typeof(EmployeePositionAssignment).GetProperty("EffectiveToUtc"))` — a
  // BARE-STRING NEGATIVE with no positive, in this package, carrying the identical defect: it passes when
  // the property is absent AND when the literal is misspelt. `EmployeeDomainTests` line 519 is the same
  // assertion on the employee side. Both are outside 269's citation lane and are recorded here rather than
  // changed, because a sweep that names a defect class and steps over two instances of it in its own
  // neighbourhood is the finding, not the fix.
  //
  // ---- ⚠⚠ WHICH CLAUSES THIS CARRIES, AND WHICH IT DOES NOT.
  //
  // `AC-POS-0057` is carried WHOLE here — tenant- and company-owned, not branch-owned, three assertions
  // for three clauses, and no enforcement half exists for it to be missing. *It is an interface claim and
  // this is an interface test.*
  //
  // `AC-POS-0037` is THREE clauses and this file holds two: the marker, and the absent `RowVersion`.
  // ***THE FIRST CLAUSE — "no update or delete path exists" — IS NOT ASSERTED HERE.*** It is the exact
  // member set in `EmployeeReadScopeArchitectureTests.The_employee_repository_surface_is_the_approved_
  // write_path_only`, which carried no citation until it was given one alongside this note.
  //
  // ⚠ AND THE RUNTIME REFUSAL IS A FOURTH THING AGAIN, newly gated. A marker without an enforcer is the
  // appearance of immutability and none of it; the enforcer is `TenantDbContext.PreventAppendOnlyMutation`,
  // driven behaviourally by `TenantAppendOnlyGuardTests` **since `69c2f0a` and by Integration alone before
  // that.** *So this criterion reads tier 1 today and would have read tier 1 yesterday on weaker evidence.*
  [Fact]
  [Trait("Decision", "DEC-POS-0021")]
  [Trait("Criterion", "AC-POS-0037")]
  [Trait("Criterion", "AC-POS-0057")]
  public void The_append_only_assignment_carries_no_row_version()
  {
    var interfaces = typeof(SSAS.HR.Domain.Positions.EmployeePositionAssignment).GetInterfaces();

    // `AC-POS-0037`'s first clause, and `AC-POS-0057`: tenant- and company-owned, append-only, and NOT
    // branch-owned. The absence is asserted beside the presences, so it cannot be a lookup over nothing.
    Assert.Contains(typeof(SSAS.BuildingBlocks.Domain.IAppendOnlyEntity), interfaces);
    Assert.Contains(typeof(SSAS.BuildingBlocks.Domain.ITenantOwnedEntity), interfaces);
    Assert.Contains(typeof(SSAS.BuildingBlocks.Domain.ICompanyOwnedEntity), interfaces);
    Assert.DoesNotContain(typeof(SSAS.BuildingBlocks.Domain.IBranchOwnedEntity), interfaces);

    // THE NEGATIVE, bound to a symbol rather than a string.
    Assert.Null(
      typeof(SSAS.HR.Domain.Positions.EmployeePositionAssignment)
        .GetProperty(nameof(SSAS.HR.Domain.Positions.Position.RowVersion)));

    // THE POSITIVE CONTROL, on the same name: `Position` DOES carry one, so the lookup above is proven
    // capable of finding a property called that. Without this line a rename makes the negative vacuous.
    Assert.NotNull(
      typeof(SSAS.HR.Domain.Positions.Position)
        .GetProperty(nameof(SSAS.HR.Domain.Positions.Position.RowVersion)));
  }

  // ================================================================================================
  // THE READ SCOPES CARRY NO BRANCH DIMENSION (DEC-POS-0020)
  // ================================================================================================
  //
  // A Position is not branch-owned, so branch scope does not decide whether one is VISIBLE. The resolver
  // takes no branch dependency at all, which is a stronger statement than "it does not call one".
  //
  // ⚠ CITED BY 269: `AC-POS-0046` HAS THREE CLAUSES AND THIS IS THE SECOND — *carries no branch scope*.
  // The other two are in this file: *cannot be constructed outside its resolver* is
  // `No_position_read_scope_can_be_constructed_from_outside_the_application`, and *no read method omits it*
  // is `Every_position_read_takes_its_own_scope_as_the_first_parameter`. All three are cited; no one of
  // them is honest alone.
  //
  // Note this asserts the SCOPE TYPES' properties AND the resolver's constructor parameters — two claims,
  // because a scope with no branch property served by a resolver that takes a branch resolver would satisfy
  // the letter of the first while reintroducing the dimension.
  [Fact]
  [Trait("Decision", "DEC-POS-0020")]
  [Trait("Criterion", "AC-POS-0046")]
  public void No_position_scope_carries_a_branch_dimension()
  {
    foreach (var scopeType in new[]
      { typeof(PositionReadScope), typeof(JobGradeReadScope), typeof(SalaryGradeReadScope) })
    {
      var properties = scopeType.GetProperties().Select(property => property.Name).ToArray();

      Assert.DoesNotContain(properties, name => name.Contains(Names.Branch, StringComparison.Ordinal));
    }

    var resolverParameters = typeof(PositionScopeResolver)
      .GetConstructors()
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType.Name)
      .ToArray();

    Assert.DoesNotContain(resolverParameters, name => name.Contains(Names.Branch, StringComparison.Ordinal));
  }

  // ---- AND NO SCOPE CAN BE FABRICATED.
  //
  // Private constructor, internal factory. A read that omitted a scope predicate must not be something a
  // reviewer has to notice, because it must not be something a caller can express — and that is only a
  // guarantee while the factory stays internal.
  [Theory]
  [InlineData(typeof(PositionReadScope))]
  [InlineData(typeof(JobGradeReadScope))]
  [InlineData(typeof(SalaryGradeReadScope))]
  [InlineData(typeof(AuthorizedPositionCompanyScope))]
  [Trait("Decision", "DEC-POS-0020")]
  // ⚠ CITED BY 269: `AC-POS-0046`'s FIRST clause — *cannot be constructed outside its resolver*. Asserts
  // both halves of that: no public constructor AND the `Create` factory is internal. `Assert.NotNull` on
  // the factory is the control — without it, a renamed factory would make `GetMethod` return null and the
  // internal-ness assertion would never run.
  [Trait("Criterion", "AC-POS-0046")]
  public void No_position_read_scope_can_be_constructed_from_outside_the_application(Type scopeType)
  {
    Assert.Empty(scopeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    var factory = scopeType.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic);

    Assert.NotNull(factory);
    Assert.True(factory!.IsAssembly, $"{scopeType.Name}'s factory must remain internal.");
  }

  // ================================================================================================
  // EVERY READ TAKES A SCOPE, AND THE SCOPE TYPE IS THE PERMISSION (DEC-POS-0018)
  // ================================================================================================
  //
  // The strongest guard in this file. A salary grade read accepting a `PositionReadScope` would make
  // `HR.SalaryGrades.View` decorative — the pay structure would be reachable by anyone who could read the
  // organization chart, which is precisely the disclosure the separate permission exists to prevent.
  [Theory]
  [InlineData(typeof(IPositionReadService), typeof(PositionReadScope))]
  [InlineData(typeof(IJobGradeReadService), typeof(JobGradeReadScope))]
  [InlineData(typeof(ISalaryGradeReadService), typeof(SalaryGradeReadScope))]
  [Trait("Decision", "DEC-POS-0018")]
  // ⚠ CITED BY 269: `AC-POS-0046`'s THIRD clause — *no read method omits it*. `Assert.NotEmpty(methods)` is
  // the anti-vacuity control and it is load-bearing: `Assert.Equal` inside a `foreach` over an empty method
  // set passes, so an interface that lost its reads would satisfy this test perfectly without it.
  [Trait("Criterion", "AC-POS-0046")]
  public void Every_position_read_takes_its_own_scope_as_the_first_parameter(
    Type readService, Type expectedScope)
  {
    var methods = readService.GetMethods();

    Assert.NotEmpty(methods);

    foreach (var method in methods)
    {
      Assert.Equal(expectedScope, method.GetParameters()[0].ParameterType);
    }
  }

  // ---- AND EACH SCOPE HAS EXACTLY ONE PRODUCER, WHICH CHECKS ITS OWN VIEW PERMISSION.
  //
  // Read from the resolver's SOURCE rather than by reflection, because what matters is which permission
  // constant each method compares against — a fact no signature carries. The failure this catches is a
  // copy-paste that resolves a salary grade scope after checking `ViewPositions`.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  public void Each_scope_resolution_checks_its_own_view_permission()
  {
    var file = ReadApplicationSource("Positions", "Reads", "PositionScopeResolver.cs");

    // FROM THE IMPLEMENTATION, NOT THE INTERFACE. The three methods are declared twice in this file — once
    // on `IPositionScopeResolver` and once on the class — and the interface declaration carries no body, so
    // searching the whole file finds a "body" containing no permission at all. That is how this guard first
    // failed, and it is worth the extra line: a guard that reads the wrong half of a file proves nothing.
    var implementation = file.IndexOf(
      "public sealed class PositionScopeResolver", StringComparison.Ordinal);
    Assert.True(implementation >= 0, "The resolver implementation is missing from the file.");

    var source = file[implementation..];

    foreach (var (method, permission) in new[]
    {
      ("ResolvePositionsAsync", nameof(HrPermissionNames.ViewPositions)),
      ("ResolveJobGradesAsync", nameof(HrPermissionNames.ViewJobGrades)),
      ("ResolveSalaryGradesAsync", nameof(HrPermissionNames.ViewSalaryGrades))
    })
    {
      var start = source.IndexOf(method, StringComparison.Ordinal);
      Assert.True(start >= 0, $"{method} is missing from the resolver implementation.");

      // The body between this method's declaration and the next closing brace at method indentation. Crude
      // by design: a precise parser here would be a second implementation to keep correct.
      var body = source[start..source.IndexOf("\n  }", start, StringComparison.Ordinal)];

      Assert.Contains($"HrPermissionNames.{permission}", body, StringComparison.Ordinal);

      // And it checks NO OTHER family's view permission, which is what a copy-paste would leave behind.
      foreach (var other in new[]
        {
          nameof(HrPermissionNames.ViewPositions),
          nameof(HrPermissionNames.ViewJobGrades),
          nameof(HrPermissionNames.ViewSalaryGrades)
        }.Where(name => name != permission))
      {
        Assert.DoesNotContain($"HrPermissionNames.{other}", body, StringComparison.Ordinal);
      }
    }
  }

  // ---- NO READ SERVICE REACHES THE EMPLOYEE SET.
  //
  // `employeeCount` is specified in the Position wire representation and is computed within the caller's
  // EMPLOYEE read scope, which is branch-scoped. A join from a position read would disclose branch-scoped
  // membership on the strength of company-scoped visibility — the same trap `DepartmentReadService` is
  // guarded against, arriving here in Phase 3 when `Employee.PositionId` exists.
  [Fact]
  [Trait("Decision", "DEC-POS-0020")]
  public void No_position_read_service_reaches_the_employee_set()
  {
    var source = string.Concat(
      ReadInfrastructureSource("PositionReadService.cs"),
      ReadInfrastructureSource("GradeReadServices.cs"));

    Assert.DoesNotContain("Set<Employee>", source, StringComparison.Ordinal);
    Assert.DoesNotContain("IEmployeeReadService", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EmployeeReadScope", source, StringComparison.Ordinal);
  }

  // ================================================================================================
  // PERMISSIONS (DEC-POS-0018, FP-006P)
  // ================================================================================================
  //
  // NAMING THEM IS NOT REGISTERING THEM. FP-006P's failure was constants defined nowhere the
  // role-assignment path could see, so no role could hold one and every endpoint refused every caller.
  //
  // ⚠ CITED BY 269: `AC-POS-0043`, THIS PACKAGE'S HALF — *every permission this package names is defined in
  // the composed catalog and can be granted to a role.* This test carries the package-specific part: the
  // twelve position-family names are offered, and the exact counts refuse a thirteenth arriving quietly.
  //
  // ⚠⚠ IT DOES NOT CARRY THE CRITERION'S STATED FAILURE MODE — *a name present in `HrPermissionNames` but
  // ABSENT FROM THE CATALOG fails this criterion.* A thirteenth CONSTANT that was never contributed leaves
  // the catalog at 23 and passes here. I filed that as a gap and was WRONG: it is asserted, module-wide, by
  // `ModulePermissionContributionArchitectureTests.The_hr_contribution_derives_from_the_single_code_owned_
  // name_set`, which reflects every literal off `HrPermissionNames` and does `Assert.Equal(constants,
  // contributed)` — SET EQUALITY, so a constant with no catalog entry reddens and so does the reverse.
  //
  // And *the COMPOSED catalog* half — resolved from the real host container rather than from a contributor
  // constructed in a test — is `EndpointPermissionCatalogJoinTests.Every_permission_an_endpoint_requires_
  // is_defined_by_the_composed_catalog`, which joins every route's required permission against the
  // container's `IPermissionCatalog` and asserts the required set is non-empty first.
  //
  // Neither of those is cited: both are module- or product-wide and a criterion trait on them would read as
  // a position-specific assertion. Same reasoning as `AC-DEP-0043` next door, which cites the package test
  // and names the join in prose.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  [Trait("Criterion", "AC-POS-0043")]
  public void Position_permissions_are_contributed_explicitly_and_completely()
  {
    var offered = new HrPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .ToArray();

    foreach (var expected in new[]
    {
      HrPermissionNames.ViewPositions,
      HrPermissionNames.CreatePositions,
      HrPermissionNames.UpdatePositions,
      HrPermissionNames.DeactivatePositions,
      HrPermissionNames.ViewJobGrades,
      HrPermissionNames.CreateJobGrades,
      HrPermissionNames.UpdateJobGrades,
      HrPermissionNames.DeactivateJobGrades,
      HrPermissionNames.ViewSalaryGrades,
      HrPermissionNames.CreateSalaryGrades,
      HrPermissionNames.UpdateSalaryGrades,
      HrPermissionNames.DeactivateSalaryGrades
    })
    {
      Assert.Contains(expected, offered);
    }

    // TWELVE new ones, taking the HR plane to twenty-one. The count is asserted because "four per family"
    // is the discipline, and a thirteenth would mean someone grew the set without a decision.
    //
    // ---- TWENTY-THREE AS OF FP-009, and the two additions are a decision rather than a drift.
    //
    // `HR.Employees.Import` and `HR.Employees.Export` were ruled SEPARATE by `OD-DOC-005` — from `Create`,
    // from `View`, and from each other. They break the "four per family" shape deliberately: bulk in and
    // bulk out are not a CRUD quartet over a new aggregate, they are two operations over an existing one
    // whose RISK differs from the ordinary case. Export is the higher-risk half and the only operation in
    // the module that moves data outside the system's control.
    //
    // This count is what would have gone red if they had been added quietly, which is why it is here.
    Assert.Equal(27, offered.Length);

    Assert.Contains(HrPermissionNames.ImportEmployees, offered);
    Assert.Contains(HrPermissionNames.ExportEmployees, offered);

    Assert.Equal(
      12,
      offered.Count(name =>
        name.StartsWith("HR.Positions.", StringComparison.Ordinal) ||
        name.StartsWith("HR.JobGrades.", StringComparison.Ordinal) ||
        name.StartsWith("HR.SalaryGrades.", StringComparison.Ordinal)));
  }

  // ---- NO Delete, AND NO Manage CATCH-ALL, IN ANY FAMILY.
  //
  // Deletion does not exist (`BRULE-POS-0012`), so the permission would authorize nothing; and a permission
  // whose description cannot say what it lets someone DO is one nobody can grant responsibly.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  public void No_position_family_offers_a_delete_or_manage_permission()
  {
    var offered = new HrPermissionCatalogContributor().Permissions
      .Select(permission => permission.Name)
      .ToArray();

    Assert.DoesNotContain(offered, name =>
      name.EndsWith(".Delete", StringComparison.Ordinal) ||
      name.EndsWith(".Manage", StringComparison.Ordinal));
  }

  // ---- AND EVERY DESCRIPTION SAYS WHAT THE PERMISSION LETS SOMEONE DO.
  //
  // The descriptions are what a tenant administrator reads when deciding whether to grant one. The salary
  // grade view is checked by name because its description carries the disclosure warning that makes the
  // `DEC-POS-0018` separation legible to whoever is granting it.
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
  public void The_salary_grade_view_description_names_the_disclosure_it_authorizes()
  {
    var description = new HrPermissionCatalogContributor().Permissions
      .Single(permission => permission.Name == HrPermissionNames.ViewSalaryGrades)
      .Description;

    Assert.Contains("pay", description, StringComparison.OrdinalIgnoreCase);
  }

  // ================================================================================================
  // NO DELETE PATH, AND NO HIERARCHY (BRULE-POS-0012, OD-POS-006)
  // ================================================================================================
  [Fact]
  [Trait("Rule", "BRULE-POS-0012")]
  // ⚠ CITED BY 269: `AC-POS-0027`'s COMMAND AND HANDLER clause — *no route, handler, or repository method
  // deletes a position or a grade.* The route half is `HrRouteInventoryTests.The_hr_surface_exposes_no_
  // delete_verb`. The bound worth stating: this scans TYPE NAMES in `SSAS.HR.Application`, so a repository
  // METHOD named `Delete` on a type not so named, in `SSAS.HR.Infrastructure`, is outside it.
  //
  // Its absence predicates are backed by `Every_absence_predicate_can_match_something` below, which is the
  // known-positive control for this whole file.
  [Trait("Criterion", "AC-POS-0027")]
  public void No_position_delete_command_or_handler_exists()
  {
    var offenders = HrApplicationAssembly.GetTypes()
      .Where(type =>
        type.Name.Contains("Position", StringComparison.Ordinal) ||
        type.Name.Contains("JobGrade", StringComparison.Ordinal) ||
        type.Name.Contains("SalaryGrade", StringComparison.Ordinal))
      .Where(type =>
        type.Name.Contains("Delete", StringComparison.Ordinal) ||
        type.Name.Contains("Remove", StringComparison.Ordinal))
      .Select(type => type.Name)
      .ToArray();

    Assert.Empty(offenders);
  }

  // ---- `OD-POS-006` DEFERRED THE POSITION HIERARCHY, so no command may express a reporting line.
  //
  // The `BR-HR-0007` remainder transfers onward unchanged, and it stays transferred only while no field
  // exists to carry it.
  [Fact]
  [Trait("Decision", "OD-POS-006")]
  // ⚠ CITED BY 269: `AC-POS-0063` — *no `Employee.ManagerId` is introduced, and no
  // `Position.ReportsToPositionId`.* This carries the POSITION half over the command surface, and it bans
  // three spellings — `ReportsTo`, `Parent`, `Manager` — rather than the one the criterion names, which is
  // what stops the rule being satisfied by renaming the field.
  //
  // ⚠ ITS PREDICATES ARE NOT SELF-VERIFYING AND THE FILE KNOWS IT: every literal used here is proven to
  // MATCH SOMETHING by `Every_absence_predicate_can_match_something`, which holds a control type carrying
  // one property per banned term and asserts each literal finds it. Without that, a typo in `Names.Manager`
  // makes this pass over everything. That control is what makes the citation worth having.
  [Trait("Criterion", "AC-POS-0063")]
  public void No_position_command_expresses_a_reporting_line()
  {
    foreach (var command in MutationCommands)
    {
      var properties = command.GetProperties().Select(property => property.Name).ToArray();

      Assert.DoesNotContain(properties, name => name.Contains(Names.ReportsTo, StringComparison.Ordinal));
      Assert.DoesNotContain(properties, name => name.Contains(Names.Parent, StringComparison.Ordinal));
      Assert.DoesNotContain(properties, name => name.Contains(Names.Manager, StringComparison.Ordinal));
    }
  }

  // ---- AND NO COMPENSATION VALUE LIVES OUTSIDE THE SALARY GRADE (DEC-POS-0023, DEC-POS-0025).
  //
  // The amounts live on the salary grade and nowhere else. A headcount or establishment field would be the
  // other excluded thing: a Position is a job definition, not a budgeted seat.
  //
  // ---- `SalaryGradeId` IS PERMITTED, AND THE DISTINCTION IS THE WHOLE POINT.
  //
  // A job grade names the band it maps to; that is a STRUCTURAL POINTER, and it is the reference
  // `BRULE-POS-0010` requires. What must never appear is a compensation VALUE — an amount, a rate, a
  // salary figure — because no employee pay value exists in this package to constrain (`DEC-POS-0023`).
  // Matching on "Salary" alone would forbid the pointer and prove the wrong thing, which is how this guard
  // first failed.
  [Fact]
  [Trait("Decision", "DEC-POS-0023")]
  // ⚠ CITED BY 269 FOR TWO CRITERIA, over the COMMAND surface.
  //
  // `AC-POS-0062` — *no salary, wage, rate or compensation value is stored anywhere in this package.* The
  // `tenant.Employees` half of that criterion is a SCHEMA claim, carried by the position schema suite's
  // column checks; this is the application half.
  //
  // `AC-POS-0064` — *no headcount, establishment or vacancy column exists.* The second clause, *any number
  // of employees may hold one position*, is not asserted here: it is a statement about permitted DATA and
  // needs two employees sharing a position.
  //
  // ⚠⚠⚠ CORRECTED BY 271. THIS SAID I HAD SEARCHED FOR SUCH AN ARRANGEMENT AND NOT LOCATED ONE. THE
  // ARRANGEMENT IS EVERYWHERE, AND THE SEARCH IS WHY I MISSED IT. `EmployeeFixture.NewEmployee` declares
  // `Guid? position = null` and resolves `position ?? PositionA`, so EVERY test that creates two employees
  // without mentioning a position puts both on ONE position. `EmployeeBoundarySqlServerTests` lines 587-588
  // create `EMP-502` and `EMP-503` and assert BOTH creations succeed; lines 547-548, 354-358 and 2611-2615
  // do the same. Two employees holding one position, with the success asserted, exists many times over.
  //
  // ⚠ I SEARCHED THE P-SERIES — the position-TAGGED tests — AND CALLED IT THE SUITE. The arrangement lives
  // in the ordinary employee tests, spelled by a DEFAULT PARAMETER NOBODY PASSES, so no line of any test
  // contains the word `position` at the point where the sharing happens. A search over what tests SAY
  // cannot see what a default argument DOES; this is the same false absence as `AC-POS-0034`, where the
  // coverage sat under the other party to the relationship.
  //
  // ⚠⚠ WHAT IT IS NOT: an ASSERTION of this criterion. Those tests are about national-id and employee-number
  // uniqueness; the shared position is arrangement, and a failure there would report a duplicate-number
  // defect. A headcount constraint added tomorrow WOULD redden line 588 — so the protection is real and
  // executable, and it attributes to nothing. INCIDENTAL PROTECTION IS PROTECTION; IT IS NOT AN ASSERTION
  // OF THE CRITERION — the same ruling this sweep made for `AC-POS-0017`. The clause stays uncited, on
  // accurate grounds this time.
  //
  // ⚠ The comment above is the reason the ban is not simply "Salary": matching on that alone would forbid
  // `SalaryGradeId`, which is the STRUCTURAL POINTER the package requires, and the named exemption list is
  // asserted rather than assumed. That is how this guard first failed.
  [Trait("Criterion", "AC-POS-0062")]
  [Trait("Criterion", "AC-POS-0064")]
  public void No_position_command_carries_a_compensation_value_or_headcount()
  {
    foreach (var command in MutationCommands.Where(type =>
      !type.Name.Contains("SalaryGrade", StringComparison.Ordinal)))
    {
      var properties = command.GetProperties()
        .Select(property => property.Name)
        // The permitted structural pointers, named exactly. Anything else carrying "Salary" is a value.
        .Where(name => name is not ("SalaryGradeId" or "JobGradeId"))
        .ToArray();

      Assert.DoesNotContain(properties, name =>
        name.Contains(Names.Amount, StringComparison.Ordinal) ||
        name.Contains(Names.Salary, StringComparison.Ordinal) ||
        name.Contains(Names.Wage, StringComparison.Ordinal) ||
        name.Contains(Names.Pay, StringComparison.Ordinal) ||
        name.Contains(Names.Rate, StringComparison.Ordinal) ||
        name.Contains(Names.Headcount, StringComparison.Ordinal) ||
        name.Contains(Names.Seat, StringComparison.Ordinal));
    }

    // And the salary grade commands carry EXACTLY the three amounts and no fourth money field.
    foreach (var command in new[]
      { typeof(CreateSalaryGradeCommand), typeof(UpdateSalaryGradeCommand) })
    {
      var money = command.GetProperties()
        .Where(property => property.PropertyType == typeof(decimal?))
        .Select(property => property.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

      Assert.Equal(["MaximumAmount", "MidpointAmount", "MinimumAmount"], money);
    }
  }

  // ================================================================================================
  // MODULE BOUNDARIES (ADR-012)
  // ================================================================================================
  //
  // Adding an application slice is exactly the kind of change that would tempt someone to reference
  // Platform for a resolver, an authorizer, or a currency type. HR reaches those through the module-facing
  // tenancy contracts — and the salary grade read model deliberately carries no currency for this reason.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_hr_application_still_references_no_platform_assembly()
  {
    // ⚠ DECLARED AND EMITTED, BECAUSE THEY FAIL ON DIFFERENT DAYS (272). This file already reads the
    // `.csproj` files for `AC-POS-0067` below, on exactly this reasoning — the emitted check cannot see a
    // `ProjectReference` no type is taken from. The same bound applies here and the same pair closes it.
    // The control proves the predicate fires where a Platform reference legitimately exists.
    Assert.Contains(
      DeclaredDependencies.Of("SSAS.Host.API"),
      name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));

    var referenced = HrApplicationAssembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name ?? string.Empty)
      .ToArray();

    Assert.DoesNotContain(referenced, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
    Assert.DoesNotContain(
      DeclaredDependencies.Of(HrApplicationAssembly),
      name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
  }

  // ---- AND THE SAME RULE OVER EVERY HR ASSEMBLY, NOT THREE OF THEM (`AC-POS-0060`, `AC-POS-0067`, 269).
  //
  // ⚠ THE TEST ABOVE CHECKS `SSAS.HR.Application`. Two others check `SSAS.HR.Domain` and the repository
  // assembly. `SSAS.HR.API` — WHICH IS THE ASSEMBLY BOTH CRITERIA NAME — WAS CHECKED BY NOTHING. Three of
  // four boundaries guarded is not a decision, it is an omission: whoever wrote them understood the rule.
  //
  // So this enumerates rather than adding a fourth instance. Three assemblies is not every HR assembly, in
  // the same way three families was not every mutation for `AC-POS-0047` — and a hand-written fourth would
  // leave the identical defect one assembly further out.
  //
  // ⚠⚠ THE ALLOWED SET IS EMPTY, AND THAT IS VERIFIED RATHER THAN ASSUMED. I read all five HR `.csproj`
  // files: Domain, Contracts, Application, Infrastructure and API. NONE references Platform — Infrastructure
  // reaches the tenant plane through `SSAS.BuildingBlocks.Tenancy`, not through Platform, which is the case
  // one would expect to need an exemption. There is therefore no exemption list, and if one is ever needed
  // it must be written here WITH ITS GROUNDS rather than appearing as the shape of a filter.
  //
  // THE FAILURE NAMES THE ASSEMBLY. An enumeration that reported only "something references Platform" over
  // five candidates would make a red worse than useless, so offenders are collected as `assembly -> reference`.
  [Fact]
  [Trait("Decision", "ADR-012")]
  [Trait("Criterion", "AC-POS-0060")]
  [Trait("Criterion", "AC-POS-0067")]
  public void No_hr_assembly_references_a_platform_assembly()
  {
    // LOADED BY TYPE, never by name: a renamed assembly then fails to compile rather than silently
    // dropping out of the population.
    (string Name, Assembly Assembly)[] hrAssemblies =
    [
      ("SSAS.HR.Domain", typeof(SSAS.HR.Domain.Positions.Position).Assembly),
      ("SSAS.HR.Contracts", typeof(SSAS.HR.Contracts.Employment.IEmployeeRoster).Assembly),
      ("SSAS.HR.Application", HrApplicationAssembly),
      ("SSAS.HR.Infrastructure", typeof(SSAS.HR.Infrastructure.Persistence.HrTenantModelContributor).Assembly),
      ("SSAS.HR.API", typeof(SSAS.HR.API.Departments.DepartmentApiErrorMapper).Assembly)
    ];

    // POPULATION CONTROL. Five is every project under `src/Modules/HR`; a sixth added without being
    // enumerated here is the failure this whole test exists to stop recurring.
    Assert.Equal(5, hrAssemblies.Length);

    // And each entry really is the assembly its label claims, so the labels in a failure can be trusted.
    foreach (var (name, assembly) in hrAssemblies)
    {
      Assert.Equal(name, assembly.GetName().Name);
    }

    // ⚠ PREDICATE CONTROL, in the spirit of `Every_absence_predicate_can_match_something` below: prove the
    // filter RECOGNISES a Platform assembly when it sees one. Without this, a wrong prefix makes `offenders`
    // empty by construction and the ban holds over nothing.
    Assert.StartsWith(
      "SSAS.Platform",
      typeof(SSAS.Platform.Domain.Companies.Company).Assembly.GetName().Name,
      StringComparison.Ordinal);

    var used = hrAssemblies
      .SelectMany(entry => entry.Assembly.GetReferencedAssemblies()
        .Select(reference => $"{entry.Name} -> {reference.Name}"))
      .Where(pair => pair.Contains("-> SSAS.Platform", StringComparison.Ordinal))
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(used);

    // ================================================================================================
    // ⚠⚠ AND THE PROJECT FILES, BECAUSE THE ASSERTION ABOVE CANNOT SEE WHAT THE CRITERION FORBIDS.
    // ================================================================================================
    //
    // `AC-POS-0067` says *a build in which `HR.API` CAN SEE `SSAS.Platform.Domain` fails this criterion
    // REGARDLESS OF WHAT IT READS.* ⚠ `GetReferencedAssemblies()` cannot assert that: the C# compiler
    // OMITS a reference no type actually uses, so an unused `ProjectReference` is invisible in the emitted
    // metadata. MEASURED, NOT ASSUMED — adding the forbidden `ProjectReference` to `SSAS.HR.API.csproj`
    // left the assembly-level assertion above GREEN, and that plant is what sent me here.
    //
    // The three pre-existing boundary guards — over HR.Domain, HR.Application and the repository assembly —
    // share the same bound: each measures DOES USE, none measures CAN SEE. That is a bound on the
    // instrument, not a defect in them.
    //
    // ⚠⚠ AND THE STRONGER PROPERTY IS THE RIGHT ONE, STATED HERE BECAUSE *the emitted-reference check
    // already covers this* IS EXACTLY THE ARGUMENT A LATER CLEANUP WILL MAKE. A declared-but-unused
    // reference is LATENT CAPABILITY: the `.csproj` edit is already merged, the friction is already gone,
    // and the next developer reaching for a Platform type meets nothing in the way. The emitted-reference
    // assertion fires only AFTER that coupling exists. **`can see` catches the capability; `does use`
    // catches the consequence** — and by then the boundary has already been crossed once.
    //
    // So the declared dependency is read from the project files, which is where "can see" is decided.
    // ---- ⚠⚠ RECURSES UNDER `src/` WITH NO `bin`/`obj` EXCLUSION, SAFE BY SEARCH PATTERN ONLY (T-191).
    //
    // The walk descends into every HR project's `obj/` and `bin/`. What keeps generated files out is the
    // pattern alone: **build output contains no `.csproj`.** *Measured 2026-09-07: zero `.csproj` under any
    // `bin` or `obj` in `src/`; five under `src/Modules/HR`, matching the control below.*
    //
    // ⚠⚠⚠ **AND UNLIKE `DeclaredDependencies.ProjectFileOf`, WHICH RECURSES THE SAME WAY, THIS DOES NOT FAIL
    // LOUD.** That one contracts on exactly one match and throws otherwise. Here an extra path would simply
    // join `projects` and be read as another HR project — **the population would grow and the equality
    // control below would fail with a count mismatch that says nothing about where the extra file came
    // from.** *A pattern is a weaker mitigation than a filter because nothing states it; that is the whole
    // reason this paragraph is here rather than a `.Where(...)` that has nothing to exclude today.*
    var projects = Directory
      .GetFiles(Path.Combine(RepositoryRootDirectory(), "src", "Modules", "HR"), "*.csproj",
        SearchOption.AllDirectories)
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
      .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
      .OrderBy(path => path, StringComparer.Ordinal)
      .ToArray();

    // The same population control, from the other direction: five projects on disk, five enumerated above.
    Assert.Equal(hrAssemblies.Length, projects.Length);

    // ⚠ MATCHED ON THE ELEMENT, NOT ON THE NAME. A bare `Contains("SSAS.Platform")` reported
    // `SSAS.HR.Domain` as an offender on a clean tree — because a COMMENT in that project file cites
    // `SSAS.Platform.Domain` as a naming precedent. Prose is not a dependency, and the population control
    // above is what surfaced the false positive before this shipped.
    // ⚠ `RepositoryPaths.ProjectNameFromFile`, NOT `Path.GetFileNameWithoutExtension` — which
    // `RepositoryPathPortabilityTests` bans outright in this suite, and which reddened this test on its
    // first gate run. The ban is a BLANKET one on purpose: the framework helper is correct for a path the
    // filesystem produced and wrong for an MSBuild `Include` attribute, and the two are indistinguishable
    // at a glance. My use was the correct kind; complying is still right, because an exemption would
    // reintroduce exactly the judgement whose unreliability created the rule.
    var declared = projects
      .Select(path => (Project: RepositoryPaths.ProjectNameFromFile(path), Lines: File.ReadAllLines(path)))
      .Where(entry => entry.Lines.Any(line =>
        line.Contains("ProjectReference", StringComparison.Ordinal) &&
        line.Contains("SSAS.Platform", StringComparison.Ordinal)))
      .Select(entry => $"{entry.Project} declares a Platform ProjectReference")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(declared);
  }

  // ---- THE POSITION SURFACE ANSWERS IN ITS OWN NAMESPACES (`AC-POS-0059`, 269).
  //
  // ⚠⚠ THE CRITERION IS WRITTEN MORE STRICTLY THAN THE PRODUCT, AND THE STRICT READING WOULD REDDEN ON
  // CORRECT CODE. It says position errors answer in `position.*` / `job_grade.*` / `salary_grade.*` AND
  // NEVER in `employee.*` or `department.*`. The mapper declares EIGHTEEN errors and seventeen match those
  // three prefixes — the eighteenth is `CompanyScopeDenied = "company.scope_denied"`, which is DELIBERATE:
  // `PositionErrors`' own header records that scope refusals are answered by the Platform boundaries with
  // their generic errors and are never restated in the module's vocabulary.
  //
  // So the assertion is the criterion's ENFORCEABLE half — the two forbidden namespaces — plus a positive
  // that the position family is actually represented. A guard written to the literal first clause would
  // fail on a design decision, which is the false positive that gets guards deleted.
  //
  // ⚠⚠ AND THE FORBIDDEN CASE IS NOT HYPOTHETICAL — IT HAS HAPPENED IN THIS CODEBASE. `PositionApiErrorMapper`'s
  // own header records it: *`DEC-DEP-0026` … a shared table once answered a DEPARTMENT MANAGER CONFLICT
  // with `employee.number_conflict`, because its only unique-constraint arm had been written for the
  // employee-number pre-check.* That is exactly this ban's subject, in the sibling feature, already once.
  // The pressure is not inferred from a comment explaining a non-action — it is a recorded defect.
  [Fact]
  [Trait("Decision", "ADR-023")]
  [Trait("Criterion", "AC-POS-0059")]
  public void No_position_api_error_answers_in_the_employee_or_department_namespace()
  {
    var errors = typeof(SSAS.HR.API.Positions.PositionApiErrorMapper)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.FieldType == typeof(SSAS.BuildingBlocks.Api.Transport.ApiError))
      .Select(field => (
        field.Name,
        Code: ((SSAS.BuildingBlocks.Api.Transport.ApiError)field.GetValue(null)!).Code))
      .ToArray();

    // POPULATION CONTROL. An empty or collapsed reflection walk satisfies every ban below.
    Assert.Equal(18, errors.Length);

    // THE CLAIM: never the neighbouring modules' namespaces. Naming a `department.*` or `employee.*` code
    // here would make a position refusal indistinguishable from another aggregate's.
    var offenders = errors
      .Where(entry =>
        entry.Code.StartsWith("employee.", StringComparison.Ordinal) ||
        entry.Code.StartsWith("department.", StringComparison.Ordinal))
      .Select(entry => $"{entry.Name} = '{entry.Code}'")
      .OrderBy(line => line, StringComparer.Ordinal)
      .ToArray();

    Assert.Empty(offenders);

    // ⚠ POSITIVE CONTROL, AND IT CARRIES THE CRITERION'S FIRST CLAUSE IN THE FORM THE PRODUCT SUPPORTS:
    // all three position-family namespaces are represented, so the ban above is not holding over a set
    // that answers in no namespace at all.
    foreach (var prefix in new[] { "position.", "job_grade.", "salary_grade." })
    {
      Assert.Contains(errors, entry => entry.Code.StartsWith(prefix, StringComparison.Ordinal));
    }
  }

  // ---- NO REFLECTION-BASED PERMISSION DISCOVERY IN THE POSITION SLICE.
  [Fact]
  [Trait("Decision", "ADR-012")]
  public void The_position_slice_discovers_no_permission_by_reflection()
  {
    var source = ReadApplicationSource("Positions", "Reads", "PositionScopeResolver.cs");

    foreach (var forbidden in new[] { "GetTypes()", "Assembly.Load", "Activator.CreateInstance" })
    {
      Assert.DoesNotContain(forbidden, source, StringComparison.Ordinal);
    }
  }

  private static string ReadApplicationSource(params string[] segments)
  {
    var path = Path.Combine(
      [RepositoryRootDirectory(), "src", "Modules", "HR", "SSAS.HR.Application", .. segments]);

    Assert.True(File.Exists(path), path);

    return File.ReadAllText(path);
  }

  private static string ReadInfrastructureSource(string fileName)
  {
    var path = Path.Combine(
      RepositoryRootDirectory(),
      "src", "Modules", "HR", "SSAS.HR.Infrastructure", "Persistence", fileName);

    Assert.True(File.Exists(path), path);

    return File.ReadAllText(path);
  }

  // Located by walking up to the solution file rather than by a relative hop count, so the guard survives a
  // change in output directory depth — and by NAME rather than by enumeration, which TEST-001 showed can
  // differ between operating systems.
  private static string RepositoryRootDirectory()
  {
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
      directory is not null;
      directory = directory.Parent)
    {
      if (File.Exists(Path.Combine(directory.FullName, "SSAS.ERP.sln")))
      {
        return directory.FullName;
      }
    }

    throw new DirectoryNotFoundException("Unable to locate the repository root containing SSAS.ERP.sln.");
  }

  // ================================================================================================
  // ⚠⚠⚠ THE ABSENCE PREDICATES CAN MATCH SOMETHING (252).
  // ================================================================================================
  //
  // Every `Assert.DoesNotContain(names, name => name.Contains("X"))` in this file PASSES WHEN THE
  // PREDICATE MATCHES NOTHING, so it cannot distinguish *no command carries X* from *I misspelled X*.
  // Measured on this exact shape elsewhere in the suite: one literal planted as `"Departmentt"` returned
  // PASSED, 6 of 6.
  //
  // ---- ⚠⚠ NEITHER STANDARD REMEDY WORKS FOR AN ABSENCE-OF-NAME ASSERTION, WHICH IS WHY THIS IS ODD.
  //
  // `nameof` is UNAVAILABLE BY CONSTRUCTION — you cannot `nameof` a property whose whole point is that it
  // must not exist. And a floor on the collection does not help either: `name.Contains("Tenantt")` matches
  // nothing over a fully populated array just as happily as over an empty one. A floor closes vacuity;
  // this is not vacuity.
  //
  // ---- SO THE LITERAL IS SHARED, AND THE CONTROL BELOW PROVES IT MATCHES.
  //
  // ⚠ A control carrying its OWN copy of each literal would prove nothing — a typo at a call site would
  // leave the control passing. The constants are the SAME symbols the assertions use, so:
  //
  //   * misspell a constant  -> `Every_absence_predicate_can_match_something` FAILS
  //   * misspell at a site   -> unknown identifier, and it does not compile
  //
  // `Marker` is appended so the control's property names are not identical to the constants: the predicate
  // under test is a SUBSTRING match, and a control that only ever matched whole names would not exercise it.
  private static class Names
  {
    public const string Amount = "Amount";
    public const string Branch = "Branch";
    public const string Company = "Company";
    public const string Department = "Department";
    public const string Headcount = "Headcount";
    public const string Manager = "Manager";
    public const string Parent = "Parent";
    public const string Pay = "Pay";
    public const string Rate = "Rate";
    public const string ReportsTo = "ReportsTo";
    public const string Salary = "Salary";
    public const string Seat = "Seat";
    public const string Status = "Status";
    public const string Tenant = "Tenant";
    public const string Wage = "Wage";
  }

  private sealed class NameControl
  {
    public string AmountMarker { get; set; } = string.Empty;
    public string BranchMarker { get; set; } = string.Empty;
    public string CompanyMarker { get; set; } = string.Empty;
    public string DepartmentMarker { get; set; } = string.Empty;
    public string HeadcountMarker { get; set; } = string.Empty;
    public string ManagerMarker { get; set; } = string.Empty;
    public string ParentMarker { get; set; } = string.Empty;
    public string PayMarker { get; set; } = string.Empty;
    public string RateMarker { get; set; } = string.Empty;
    public string ReportsToMarker { get; set; } = string.Empty;
    public string SalaryMarker { get; set; } = string.Empty;
    public string SeatMarker { get; set; } = string.Empty;
    public string StatusMarker { get; set; } = string.Empty;
    public string TenantMarker { get; set; } = string.Empty;
    public string WageMarker { get; set; } = string.Empty;
  }

  [Fact]
  [Trait("Decision", "DEC-POS-0001")]
  public void Every_absence_predicate_can_match_something()
  {
    var control = typeof(NameControl).GetProperties().Select(property => property.Name).ToArray();

    // Anti-vacuity for the control itself, which is otherwise the same trap one level down.
    Assert.Equal(15, control.Length);

    foreach (var literal in new[]
    {
      Names.Amount,
      Names.Branch,
      Names.Company,
      Names.Department,
      Names.Headcount,
      Names.Manager,
      Names.Parent,
      Names.Pay,
      Names.Rate,
      Names.ReportsTo,
      Names.Salary,
      Names.Seat,
      Names.Status,
      Names.Tenant,
      Names.Wage,
    })
    {
      Assert.Contains(
        control,
        name => name.Contains(literal, StringComparison.Ordinal));
    }
  }

}
