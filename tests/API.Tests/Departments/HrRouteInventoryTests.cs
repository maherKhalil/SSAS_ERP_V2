using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.HR.Application.Permissions;
using SSAS.API.Tests.Employees;
using SSAS.API.Tests.Positions;

namespace SSAS.API.Tests.Departments;

// ==================================================================================================
// EVERY HR ROUTE, ENUMERATED FROM THE REAL MAPPING (FP-007 Phase 4).
// ==================================================================================================
//
// ---- WHY THIS EXISTS.
//
// Phase 4 added a route that was mapped in Program.cs and invisible to every test: no harness mapped it,
// so nothing exercised it and nothing said so. It was found by reading the route table against the
// handlers — exactly the kind of check that should not depend on somebody remembering to do it.
//
// ---- AND WHY IT READS THE HARNESSES RATHER THAN A THROWAWAY APPLICATION.
//
// Both harnesses call the PRODUCTION mapping extensions, so what they registered is what the Host
// registers. That makes the reconciliation implicit: a route added to a module but not mapped by its
// harness does not appear here, the exact inventory fails, and the author is told before it ships
// untested. A purpose-built application would have proven only that the extensions map something —
// minimal-API parameter inference needs the whole handler graph, which only a real harness has.
//
// ---- IT ASSERTS THE PERMISSION, NOT ONLY THE PATH.
//
// A route with no RequirePermission is reachable by any authenticated caller in the tenant. That is a
// silent authorization hole rather than a visible one, because nothing about the route's code looks wrong.
[Collection(EmployeeApiEndpointGroup.Name)]
public sealed class HrRouteInventoryTests
  : IClassFixture<EmployeeApiTestHost>, IClassFixture<DepartmentApiTestHost>,
    IClassFixture<PositionApiTestHost>
{
  private readonly EmployeeApiTestHost employees;
  private readonly DepartmentApiTestHost departments;
  private readonly PositionApiTestHost positions;

  public HrRouteInventoryTests(
    EmployeeApiTestHost employees, DepartmentApiTestHost departments, PositionApiTestHost positions)
  {
    this.employees = employees;
    this.departments = departments;
    this.positions = positions;
  }

  // ---- EVERY MAPPED HR ROUTE CARRIES A PERMISSION POLICY.
  //
  // The strongest form of the check: it does not care WHICH permission, so it keeps passing as the surface
  // grows, and it fails the moment one is forgotten.
  [Fact]
  public void Every_hr_route_requires_a_permission()
  {
    var routes = MappedRoutes();

    Assert.NotEmpty(routes);

    var unprotected = routes
      .Where(route => string.IsNullOrEmpty(route.Policy))
      .Select(route => $"{route.Method} {route.Pattern}")
      .ToArray();

    Assert.Empty(unprotected);
  }

  // ---- THE EXACT INVENTORY.
  //
  // Method, pattern and the policy each route demands. Deliberately exact rather than a count: a count
  // would pass if a route were replaced by a different one, and the pairing of PATTERN to PERMISSION is
  // the part worth guarding — a route wired to the wrong constant is an authorization defect no functional
  // test of the happy path would notice.
  [Fact]
  public void The_hr_route_inventory_is_exactly_as_ruled()
  {
    var routes = MappedRoutes()
      .Select(route => $"{route.Method} {route.Pattern} => {route.Policy}")
      .OrderBy(route => route, StringComparer.Ordinal)
      .ToArray();

    // The policy prefix comes from the CONSTANT rather than a literal: renaming it is a framework-wide
    // change, and this guard should fail on a wrong PERMISSION, not on a prefix someone renamed correctly.
    // The trailing slash on the group's own route is what MapGroup produces for an empty pattern.
    static string Policy(string permission) => $"{PermissionPolicyNames.TenantPrefix}{permission}";

    Assert.Equal(
      new[]
      {
        $"GET /api/hr/departments/ => {Policy(HrPermissionNames.ViewDepartments)}",
        $"GET /api/hr/departments/{{departmentId:guid}} => {Policy(HrPermissionNames.ViewDepartments)}",
        $"GET /api/hr/departments/{{departmentId:guid}}/children => {Policy(HrPermissionNames.ViewDepartments)}",
        $"GET /api/hr/employees/ => {Policy(HrPermissionNames.ViewEmployees)}",
        $"GET /api/hr/employees/{{employeeId:guid}} => {Policy(HrPermissionNames.ViewEmployees)}",
        $"GET /api/hr/employees/{{employeeId:guid}}/branch-history => {Policy(HrPermissionNames.ViewEmployees)}",
        $"POST /api/hr/departments/ => {Policy(HrPermissionNames.CreateDepartments)}",
        // Activate and deactivate BOTH carry Deactivate: that permission governs whether a department may
        // receive employees, and both directions change that answer.
        $"POST /api/hr/departments/{{departmentId:guid}}/activate => {Policy(HrPermissionNames.DeactivateDepartments)}",
        $"POST /api/hr/departments/{{departmentId:guid}}/deactivate => {Policy(HrPermissionNames.DeactivateDepartments)}",
        $"POST /api/hr/departments/{{departmentId:guid}}/manager => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/departments/{{departmentId:guid}}/manager/remove => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/departments/{{departmentId:guid}}/move => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/departments/{{departmentId:guid}}/move-to-root => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/employees/ => {Policy(HrPermissionNames.CreateEmployees)}",
        $"POST /api/hr/employees/{{employeeId:guid}}/activate => {Policy(HrPermissionNames.UpdateEmployees)}",
        // A department change is an ordinary employee update: DepartmentId is a classification, not a
        // security partition (ADR-024), so nothing crosses an authorization boundary.
        $"POST /api/hr/employees/{{employeeId:guid}}/change-department => {Policy(HrPermissionNames.UpdateEmployees)}",
        $"POST /api/hr/employees/{{employeeId:guid}}/deactivate => {Policy(HrPermissionNames.UpdateEmployees)}",
        $"POST /api/hr/employees/{{employeeId:guid}}/terminate => {Policy(HrPermissionNames.TerminateEmployees)}",
        // Transfer moves a record across a security partition and holds a permission of its own.
        $"POST /api/hr/employees/{{employeeId:guid}}/transfer => {Policy(HrPermissionNames.TransferEmployees)}",
        $"PUT /api/hr/departments/{{departmentId:guid}} => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"PUT /api/hr/employees/{{employeeId:guid}} => {Policy(HrPermissionNames.UpdateEmployees)}",

        // ================================================================================================
        // FP-008. TWENTY MORE, TAKING THE HR SURFACE FROM 21 ROUTES TO 41.
        // ================================================================================================
        //
        // Six per aggregate on one shape, plus two on the employee prefix. Note what the PAIRING says, which
        // is the half a count could never guard:
        //
        //   * activate and deactivate carry the entity's **Deactivate** permission in all three families —
        //     `DEC-DEP-0025` carried over, because the permission names the capability and not the
        //     direction;
        //   * every `salary-grades` route carries an `HR.SalaryGrades.*` permission, which is what makes
        //     `DEC-POS-0018`'s pay-band separation real at the routing layer;
        //   * both employee-prefix routes carry EMPLOYEE permissions, never position ones — a change is
        //     `HR.Employees.Update` (`DEC-POS-0019`) and the history read is `HR.Employees.View`, because
        //     both are about a person rather than about the job catalog.
        $"GET /api/hr/employees/{{employeeId:guid}}/position-history => {Policy(HrPermissionNames.ViewEmployees)}",
        $"POST /api/hr/employees/{{employeeId:guid}}/change-position => {Policy(HrPermissionNames.UpdateEmployees)}",

        $"GET /api/hr/positions/ => {Policy(HrPermissionNames.ViewPositions)}",
        $"GET /api/hr/positions/{{positionId:guid}} => {Policy(HrPermissionNames.ViewPositions)}",
        $"POST /api/hr/positions/ => {Policy(HrPermissionNames.CreatePositions)}",
        $"POST /api/hr/positions/{{positionId:guid}}/activate => {Policy(HrPermissionNames.DeactivatePositions)}",
        $"POST /api/hr/positions/{{positionId:guid}}/deactivate => {Policy(HrPermissionNames.DeactivatePositions)}",
        $"PUT /api/hr/positions/{{positionId:guid}} => {Policy(HrPermissionNames.UpdatePositions)}",

        $"GET /api/hr/job-grades/ => {Policy(HrPermissionNames.ViewJobGrades)}",
        $"GET /api/hr/job-grades/{{jobGradeId:guid}} => {Policy(HrPermissionNames.ViewJobGrades)}",
        $"POST /api/hr/job-grades/ => {Policy(HrPermissionNames.CreateJobGrades)}",
        $"POST /api/hr/job-grades/{{jobGradeId:guid}}/activate => {Policy(HrPermissionNames.DeactivateJobGrades)}",
        $"POST /api/hr/job-grades/{{jobGradeId:guid}}/deactivate => {Policy(HrPermissionNames.DeactivateJobGrades)}",
        $"PUT /api/hr/job-grades/{{jobGradeId:guid}} => {Policy(HrPermissionNames.UpdateJobGrades)}",

        $"GET /api/hr/salary-grades/ => {Policy(HrPermissionNames.ViewSalaryGrades)}",
        $"GET /api/hr/salary-grades/{{salaryGradeId:guid}} => {Policy(HrPermissionNames.ViewSalaryGrades)}",
        $"POST /api/hr/salary-grades/ => {Policy(HrPermissionNames.CreateSalaryGrades)}",
        $"POST /api/hr/salary-grades/{{salaryGradeId:guid}}/activate => {Policy(HrPermissionNames.DeactivateSalaryGrades)}",
        $"POST /api/hr/salary-grades/{{salaryGradeId:guid}}/deactivate => {Policy(HrPermissionNames.DeactivateSalaryGrades)}",
        $"PUT /api/hr/salary-grades/{{salaryGradeId:guid}} => {Policy(HrPermissionNames.UpdateSalaryGrades)}",

        // ================================================================================================
        // FP-009 PHASE 2. FIVE MORE, TAKING THE HR SURFACE FROM 41 ROUTES TO 46.
        // ================================================================================================
        //
        // The PAIRING is what a count could never guard, and here it carries the whole of `OD-DOC-005`:
        //
        //   * the two routes that CREATE employees in bulk carry `HR.Employees.Import`, not `Create` — the
        //     capability was separated precisely because "may add one" must not mean "may add five
        //     thousand";
        //   * the one route that takes data OUT carries `HR.Employees.Export`, which is the higher-risk
        //     half and the only permission in the module guarding an operation that moves data beyond the
        //     system's control;
        //   * and BOTH audit listings carry `HR.Employees.View`, never `Import` or `Export`. Reading the
        //     record that an extraction happened is an employee read; gating it on `Export` would mean the
        //     people who audit extractions must also be able to perform them.
        //
        // Note the trailing-slash shapes: only the empty-suffix routes render with one, so these five do
        // not — the same form `position-history` and `change-position` already take.
        $"POST /api/hr/employees/import => {Policy(HrPermissionNames.ImportEmployees)}",
        $"POST /api/hr/employees/import/validate => {Policy(HrPermissionNames.ImportEmployees)}",
        $"GET /api/hr/employees/import-runs => {Policy(HrPermissionNames.ViewEmployees)}",
        $"GET /api/hr/employees/export => {Policy(HrPermissionNames.ExportEmployees)}",
        $"GET /api/hr/employees/export-runs => {Policy(HrPermissionNames.ViewEmployees)}"
      }
      .OrderBy(route => route, StringComparer.Ordinal),
      routes);

    // The count is asserted BESIDE the exact list rather than instead of it. The list guards the pairing of
    // pattern to permission; this one sentence is what makes a reviewer's "twenty new routes" checkable at a
    // glance, and it is the number `api-contracts.md` fixed.
    Assert.Equal(46, routes.Length);
  }

  // ---- THE HR SURFACE USES NO DELETE VERB, AND THAT IS A CONVENTION RATHER THAN AN ACCIDENT.
  //
  // Every state change is a named POST: activate, deactivate, terminate, transfer, move, manager/remove.
  // Removing a department's manager is not deleting a resource — the employee is untouched and only the
  // association ends — and a DELETE would say otherwise. Asserted so the next module inherits the
  // convention instead of relitigating it.
  [Fact]
  public void The_hr_surface_exposes_no_delete_verb()
  {
    var deletes = MappedRoutes()
      .Where(route => route.Method == "DELETE")
      .Select(route => route.Pattern)
      .ToArray();

    Assert.Empty(deletes);
  }

  // The union of the two harnesses. Distinct because both map the /api/hr/employees prefix — the employee
  // group and the change-department route — and a group registration is not a duplicate route.
  private IReadOnlyList<(string Method, string Pattern, string Policy)> MappedRoutes() =>
  [
    .. employees.MappedRoutes()
      .Concat(departments.MappedRoutes())
      .Concat(positions.MappedRoutes())
      .DistinctBy(route => $"{route.Method} {route.Pattern}")
  ];
}
