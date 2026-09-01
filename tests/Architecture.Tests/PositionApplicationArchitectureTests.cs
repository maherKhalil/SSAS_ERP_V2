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
  [Fact]
  [Trait("Decision", "DEC-POS-0021")]
  public void The_append_only_assignment_carries_no_row_version()
  {
    Assert.Null(
      typeof(SSAS.HR.Domain.Positions.EmployeePositionAssignment).GetProperty("RowVersion"));
  }

  // ================================================================================================
  // THE READ SCOPES CARRY NO BRANCH DIMENSION (DEC-POS-0020)
  // ================================================================================================
  //
  // A Position is not branch-owned, so branch scope does not decide whether one is VISIBLE. The resolver
  // takes no branch dependency at all, which is a stronger statement than "it does not call one".
  [Fact]
  [Trait("Decision", "DEC-POS-0020")]
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
  [Fact]
  [Trait("Decision", "DEC-POS-0018")]
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
    Assert.Equal(23, offered.Length);

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
    var referenced = HrApplicationAssembly
      .GetReferencedAssemblies()
      .Select(assembly => assembly.Name ?? string.Empty)
      .ToArray();

    Assert.DoesNotContain(referenced, name => name.StartsWith("SSAS.Platform", StringComparison.Ordinal));
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
