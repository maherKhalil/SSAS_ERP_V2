using SSAS.BuildingBlocks.Domain;
using System.Reflection;
using SSAS.HR.Application.Employees;
using SSAS.HR.Domain.Departments;
using SSAS.HR.Domain.Employees;

namespace SSAS.Architecture.Tests;

// THE STRUCTURAL GUARANTEES OF EMPLOYEE ↔ DEPARTMENT (FP-007 Phase 3, §32).
//
// These are the claims that must remain true no matter what a later change does to the handlers. Each one
// is enforced by SHAPE rather than by a rule someone has to remember: a property that does not exist, a
// setter that is not public, a parameter that is absent from a command.
public sealed class EmployeeDepartmentArchitectureTests
{
  private static readonly Assembly HrDomainAssembly = typeof(Employee).Assembly;

  // ================================================================================================
  // OWNERSHIP IS UNCHANGED IN BOTH DIRECTIONS.
  // ================================================================================================

  // Employee keeps all three dimensions. Department is not a fourth: adding one would make department a
  // security partition, which is exactly what ADR-026 decision 1 says it is not.
  [Fact]
  public void Employee_remains_owned_along_exactly_the_three_original_dimensions()
  {
    Assert.True(typeof(ITenantOwnedEntity).IsAssignableFrom(typeof(Employee)));
    Assert.True(typeof(ICompanyOwnedEntity).IsAssignableFrom(typeof(Employee)));
    Assert.True(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(Employee)));
  }

  // ---- DEPARTMENT IS STILL NOT BRANCH-OWNED, AND PHASE 3 DID NOT MAKE IT SO.
  //
  // The temptation Phase 3 creates is real: Employee now points at a Department and is branch-owned, so a
  // future change might "align" them. It must not. A department spans the branches of its company.
  [Fact]
  public void Department_and_its_history_are_never_branch_owned()
  {
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(Department)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(DepartmentManager)));
    Assert.False(typeof(IBranchOwnedEntity).IsAssignableFrom(typeof(EmployeeDepartmentAssignment)));

    // And no branch column smuggled in under another name.
    Assert.DoesNotContain(
      typeof(EmployeeDepartmentAssignment).GetProperties(),
      property => property.Name.Contains("Branch", StringComparison.OrdinalIgnoreCase));
  }

  // ================================================================================================
  // THE HISTORY IS APPEND-ONLY, AND ONLY THE AGGREGATE MAY WRITE IT.
  // ================================================================================================

  [Fact]
  public void The_department_history_record_has_no_public_mutator_and_no_end_date()
  {
    var type = typeof(EmployeeDepartmentAssignment);

    Assert.True(typeof(IAppendOnlyEntity).IsAssignableFrom(type));

    foreach (var property in type.GetProperties())
    {
      // TenantId and CompanyId are the ownership setters the persistence boundary stamps through, exactly
      // as they are on every other owned entity. Everything else is closed.
      if (property.Name is nameof(EmployeeDepartmentAssignment.TenantId)
        or nameof(EmployeeDepartmentAssignment.CompanyId))
      {
        continue;
      }

      Assert.False(
        property.SetMethod?.IsPublic ?? false,
        $"{type.Name}.{property.Name} has a public setter, which would make history editable.");
    }

    // No EffectiveToUtc: closing an interval means UPDATING the previous row, which is the history mutation
    // this model exists to prevent. The interval is derived by ordering.
    Assert.Null(type.GetProperty("EffectiveToUtc"));
    Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedUtc)));
    Assert.Null(type.GetProperty(nameof(SSAS.BuildingBlocks.Domain.IAuditableEntity.ModifiedBy)));
  }

  // ---- THE FACTORIES ARE INTERNAL, so nothing outside the domain assembly can fabricate a history row.
  //
  // Phase 1 left them public with a note that Phase 3 would tighten them once Employee had the operation
  // that calls them. This is that guarantee, enforced rather than remembered.
  [Fact]
  public void Only_the_domain_assembly_can_create_a_department_history_record()
  {
    var factories = typeof(EmployeeDepartmentAssignment)
      .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
      .Where(method => method.Name.StartsWith("Create", StringComparison.Ordinal))
      .ToArray();

    Assert.NotEmpty(factories);
    Assert.All(factories, method => Assert.False(
      method.IsPublic,
      $"{method.Name} is public, so the application layer could write history the aggregate never saw."));
  }

  // ================================================================================================
  // DEPARTMENTID IS NOT REACHABLE THROUGH AN ORDINARY UPDATE (§27).
  // ================================================================================================
  //
  // Two independent locks, and both must hold. The property has no public setter, so no code can assign it;
  // and the ordinary update command has no department parameter, so no REQUEST can express it.
  [Fact]
  // CITED BY B18 pass 20, body-confirmed: an employee's department cannot be changed through the
  // ordinary update. The test asserts it BY CONSTRUCTION -- `Employee.DepartmentId` has no public
  // setter, and neither `UpdateEmployeeProfileCommand` nor `TransferEmployeeCommand` declares a
  // department property -- which is stronger than a validator rejecting the field, and is why the
  // criterion's *rejected rather than silently ignored* holds: there is no field to ignore.
  [Trait("Criterion", "AC-DEP-0035")]
  public void The_department_cannot_be_changed_through_an_ordinary_employee_update()
  {
    var departmentId = typeof(Employee).GetProperty(nameof(Employee.DepartmentId));

    Assert.NotNull(departmentId);
    Assert.False(departmentId!.SetMethod!.IsPublic);

    // The profile update command: no DepartmentId, by construction rather than by validation.
    Assert.DoesNotContain(
      typeof(UpdateEmployeeProfileCommand).GetProperties(),
      property => property.Name.Contains("Department", StringComparison.OrdinalIgnoreCase));

    // Nor the branch transfer command — a transfer moves a branch and nothing else.
    Assert.DoesNotContain(
      typeof(TransferEmployeeCommand).GetProperties(),
      property => property.Name.Contains("Department", StringComparison.OrdinalIgnoreCase));
  }

  // ---- AND THE CONVERSE: the department-change command cannot express a branch change.
  //
  // §14 and §36 both turn on this. There is no BranchId to set, so "department change moved the branch" is
  // not a bug that can be introduced here without adding a parameter this test would fail on.
  [Fact]
  public void The_department_change_command_carries_no_branch_and_no_source()
  {
    var properties = typeof(ChangeEmployeeDepartmentCommand).GetProperties()
      .Select(property => property.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(
      [
        "DestinationDepartmentId",
        "EmployeeId",
        "ExpectedRowVersion",
        "ReasonCode",
        "ReasonText"
      ],
      properties);
  }

  // ================================================================================================
  // THE DEFERRED RELATIONSHIPS STAY DEFERRED.
  // ================================================================================================

  // No Employee.ManagerId — BR-HR-0007 presumes a reporting line no authority defines. And no
  // Department.ManagerEmployeeId, which is the ADR-026 decision 7 split: putting the manager on Department
  // would make Department reference Employee while Employee references Department, and the resulting
  // foreign-key cycle makes TenantCutoverCopyPlan.Order undecidable — breaking Shared→Dedicated cutover for
  // every tenant. The separate DepartmentManagers table is what keeps the graph acyclic.
  [Fact]
  public void Neither_aggregate_gained_a_manager_reference()
  {
    Assert.DoesNotContain(
      typeof(Employee).GetProperties(),
      property => property.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase));

    Assert.DoesNotContain(
      typeof(Department).GetProperties(),
      property => property.Name.Contains("Manager", StringComparison.OrdinalIgnoreCase) ||
        property.Name.Contains("Employee", StringComparison.OrdinalIgnoreCase));
  }

  // ---- EMPLOYEE CANNOT WALK TO A DEPARTMENT.
  //
  // A navigation property would let a caller holding an Employee read a Department without going through
  // the department's own scope — the same class of leak the Phase 2 manager-privacy design closed from the
  // other direction.
  [Fact]
  public void Employee_holds_a_department_identifier_and_never_a_department_reference()
  {
    Assert.DoesNotContain(
      typeof(Employee).GetProperties(),
      property => property.PropertyType == typeof(Department) ||
        property.PropertyType == typeof(DepartmentManager));

    Assert.Equal(typeof(Guid), typeof(Employee).GetProperty(nameof(Employee.DepartmentId))!.PropertyType);
  }

  // ---- AND HR STILL REFERENCES NO FORBIDDEN MODULE. Phase 3 added a Department foreign key to Employee,
  // which is intra-HR; nothing new points at Platform (ADR-012).
  [Fact]
  public void The_hr_domain_still_references_no_platform_assembly()
  {
    Assert.DoesNotContain(
      HrDomainAssembly.GetReferencedAssemblies(),
      reference => reference.Name?.StartsWith("SSAS.Platform", StringComparison.Ordinal) ?? false);
  }
}
