using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.HR.Application.Permissions;
using SSAS.API.Tests.Employees;

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
  : IClassFixture<EmployeeApiTestHost>, IClassFixture<DepartmentApiTestHost>
{
  private readonly EmployeeApiTestHost employees;
  private readonly DepartmentApiTestHost departments;

  public HrRouteInventoryTests(EmployeeApiTestHost employees, DepartmentApiTestHost departments)
  {
    this.employees = employees;
    this.departments = departments;
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
      [
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
        $"PUT /api/hr/employees/{{employeeId:guid}} => {Policy(HrPermissionNames.UpdateEmployees)}"
      ],
      routes);
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
      .DistinctBy(route => $"{route.Method} {route.Pattern}")
  ];
}
