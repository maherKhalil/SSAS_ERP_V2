using System.Reflection;
using SSAS.Attendance.Application.Calendars;
using SSAS.HR.Application.Employees;
using SSAS.HR.Application.Permissions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// NO DELETION SURFACE EXISTS FOR EMPLOYEE — `AC-EMP-0017`'s STRUCTURAL HALF (item 220).
// ==================================================================================================
//
// `AC-EMP-0017` bans FIVE things: *"no delete command, repository method, permission, endpoint, or cascade
// exists for Employee or `EmployeeBranchAssignment`, and a persistence guard rejects physical deletion."*
//
// ---- ⚠ THREE WERE ALREADY PINNED, AND FINDING THAT OUT CAME FIRST.
//
// | ban | pinned by |
// |---|---|
// | **endpoint** | `HrRouteInventoryTests.The_hr_surface_exposes_no_delete_verb` |
// | **cascade** | `DeleteBehaviourArchitectureTests.Every_reference_foreign_key_still_restricts` |
// | **persistence guard** | `EmployeeBoundarySqlServerTests.An_employee_cannot_be_physically_deleted` |
//
// **So this asserts only the three that nothing covered: COMMAND, REPOSITORY METHOD, PERMISSION.** ⚠ Writing
// a test for all five would have duplicated three existing guards, and a duplicated guard is two places to
// edit and one place to forget.
//
// ---- WHY THE CRITERION NEEDS A STRUCTURAL TEST AT ALL.
//
// `An_employee_cannot_be_physically_deleted` proves a delete is REFUSED. That is the runtime half. **A
// delete command could be added tomorrow and still be refused by the persistence guard** — the criterion
// bans the surface from EXISTING, which is a claim about the type system, not about a request.
public sealed class EmployeeNoDeletionArchitectureTests
{
  private static readonly string[] DeletePrefixes = ["Delete", "Remove"];

  private static readonly string[] EmployeeSubjects = ["Employee", "EmployeeBranchAssignment"];

  [Fact]
  [Trait("Criterion", "AC-EMP-0017")]
  public void No_delete_command_repository_method_or_permission_names_employee()
  {
    var offenders = DeleteShapedNames(typeof(IEmployeeRepository).Assembly)
      .Where(name => EmployeeSubjects.Any(subject => name.Contains(subject, StringComparison.Ordinal)))
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      offenders.Length == 0,
      $"""
      A DELETION SURFACE NOW EXISTS FOR EMPLOYEE, AND AC-EMP-0017 BANS IT FROM EXISTING.

      Found: {string.Join(", ", offenders)}

      The criterion is not satisfied by refusing the delete at persistence time -- that is already proven
      by EmployeeBoundarySqlServerTests.An_employee_cannot_be_physically_deleted. This asserts the
      stronger claim: no command, repository method or permission NAMES a deletion of Employee or
      EmployeeBranchAssignment, so the operation cannot be requested at all.

      Employment ends by TERMINATION, which preserves the aggregate for history (AC-EMP-0014). If a
      deletion is genuinely required, that is a change to the criterion first.
      """);
  }

  // ==================================================================================================
  // ⚠ THE CONTROL: THE MATCHER MUST FIND DELETE-SHAPED NAMES THAT DO EXIST.
  // ==================================================================================================
  //
  // A ban whose matcher finds nothing is indistinguishable from a ban that holds — the vacuity that
  // retired `DEC-L-030`'s guard and that item 209's second plant reproduced deliberately. **If
  // `DeleteShapedNames` silently stopped matching — a renamed convention, a reflection flag, a typo in a
  // prefix — the assertion above would pass while proving nothing.**
  //
  // The known positives are LIVE CODE in another module: Attendance genuinely removes holidays, and that
  // is a legitimate deletion of a calendar entry rather than of a person.
  [Fact]
  [Trait("Criterion", "AC-EMP-0017")]
  public void The_matcher_finds_the_delete_shaped_names_that_do_exist_elsewhere()
  {
    var elsewhere = DeleteShapedNames(typeof(RemoveHolidayCommandHandler).Assembly);

    Assert.NotEmpty(elsewhere);
    Assert.Contains(elsewhere, name => name.Contains("Holiday", StringComparison.Ordinal));
  }

  // ⚠ AND THE PERMISSION HALF NEEDS ITS OWN CONTROL, because permissions are CONSTANTS rather than types
  // and are gathered by a different reflection path. `HrPermissionNames` must be reachable and non-empty,
  // or the permission third of the ban above is asserted over nothing.
  [Fact]
  [Trait("Criterion", "AC-EMP-0017")]
  public void The_permission_catalog_is_reachable_and_non_empty()
  {
    var permissions = PermissionConstants(typeof(HrPermissionNames));

    Assert.NotEmpty(permissions);
    Assert.Contains(permissions, name => name.Contains("Employees", StringComparison.Ordinal));
  }

  // Types, their public methods, and permission constants — the three shapes the criterion names, gathered
  // from one assembly. Interfaces are included deliberately: `IEmployeeRepository` is where a delete method
  // would be declared, and the criterion bans the METHOD rather than an implementation of it.
  private static string[] DeleteShapedNames(Assembly assembly)
  {
    var types = assembly.GetTypes().Where(type => type.IsPublic || type.IsNestedPublic).ToArray();

    var typeNames = types
      .Select(type => type.Name)
      .Where(IsDeleteShaped);

    var methodNames = types
      .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
      .Select(method => method.DeclaringType!.Name + "." + method.Name)
      .Where(name => IsDeleteShaped(name[(name.IndexOf('.', StringComparison.Ordinal) + 1)..]));

    var permissionNames = types
      .Where(type => type.Name.EndsWith("PermissionNames", StringComparison.Ordinal))
      .SelectMany(PermissionConstants)
      .Where(IsDeleteShaped);

    return [.. typeNames.Concat(methodNames).Concat(permissionNames).Distinct(StringComparer.Ordinal)];
  }

  private static string[] PermissionConstants(Type type) =>
    [.. type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
      .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
      .Select(field => field.Name)];

  private static bool IsDeleteShaped(string name) =>
    DeletePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal));
}
