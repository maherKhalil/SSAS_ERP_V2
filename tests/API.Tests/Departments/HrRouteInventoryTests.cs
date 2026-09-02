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
  // ⚠ CITED BY 265: `AC-DEP-0040` -- *each of the four department permissions is required by exactly the
  // operations listed in `authorization-model.md`, AND BY NO OTHERS*. A SUPERSET for the third time in this
  // file, on the same grounds as `AC-DEP-0032` below.
  //
  // THE *AND BY NO OTHERS* HALF IS THE HARD ONE, AND IT IS CARRIED BY `Assert.Equal` OVER SETS RATHER THAN
  // BY ANY CLAUSE ABOUT DEPARTMENTS. Set equality fails on an UNLISTED route just as it fails on a missing
  // one, so no route can quietly acquire a department permission. A per-permission assertion could not do
  // this: it would have to enumerate the routes that must NOT carry the permission, which is the open set.
  //
  // THE PAIRINGS WERE READ AGAINST THE DOCUMENT OPERATION BY OPERATION, not assumed from the names:
  //   View       -> read one, list, read hierarchy  = the three GETs above
  //   Create     -> create                          = POST /departments/
  //   Update     -> rename/change code, move, manager = PUT + /move + /move-to-root + /manager + /manager/remove
  //   Deactivate -> deactivate AND reactivate       = /activate + /deactivate (one permission, both directions)
  // and the document's two NEGATIVE rows are here too: change-department and the employee search filter
  // carry EMPLOYEE permissions, which is `AC-DEP-0042` restated.
  //
  // ⚠ THE BOUND, STATED SO IT IS NOT MISTAKEN FOR MORE: `MappedRoutes()` reads the three HR harnesses, so
  // *no others* means NO OTHER HR ROUTE. A route in another module demanding `HR.Departments.*` is
  // constructible and would not be seen here. Nothing pushes toward it and no guard is proposed; it is
  // recorded as the edge of the instrument rather than as a gap.
  [Trait("Criterion", "AC-DEP-0040")]
  // ⚠ ALSO CITED BY 269: `AC-POS-0058`'s HARNESS half — *routes and handlers stand 1:1; the exact route
  // inventory matches.* The exact set equality is what makes it 1:1 in both directions.
  //
  // ⚠⚠ CITED IN PART. The criterion says the inventory matches in the module harness AND THE HOST
  // COMPOSITION, and *a route reachable in one and not the other fails this criterion.* `MappedRoutes()`
  // reads the three HR harnesses only, so this test alone is the harness side.
  //
  // ⚠⚠⚠ CORRECTED BY 271. THIS COMMENT SAID I HAD SEARCHED FOR A HOST-SIDE HR ROUTE INVENTORY AND FOUND NO
  // HR EQUIVALENT, AND THAT THE HOST HALF WAS THEREFORE COVERED ONLY BY AN ARGUMENT. THAT WAS FALSE, AND
  // FALSE IN THE WAY THAT MATTERS: I searched for a per-module HR INVENTORY FILE, which is a search over
  // NAMES, and concluded an absence of the MECHANISM.
  // `ApiContractRowGuardTests.Every_documented_route_row_is_live_or_carries_a_marker` calls
  // `PlatformRouteInventory.Under(factory, "/api")` — the entire live surface of the real Host `Program`,
  // HR routes included — and asserts that every unmarked route row in `FP-008-hr-position/api-contracts.md`
  // EXISTS THERE. That is an executable Host-composition assertion reaching this package's routes.
  //
  // ⚠ WHAT IT CLOSES IS ONE DIRECTION, AND THE RESIDUE IS NOW THE HONEST ONE. The row guard runs
  // DOCUMENTED -> LIVE-HOST: a route ruled and documented here but absent from the Host composition
  // reddens it. Nothing runs LIVE-HOST -> RULED, so a route the Host maps that this file never ruled is
  // still seen by neither instrument, and the guard reaches only routes `api-contracts.md` documents
  // WITHOUT a marker. The citation stays partial — for that residue, not for the absence I recorded.
  //
  // The structural argument in this file's header still holds and is still an argument: both harnesses call
  // the PRODUCTION mapping extensions, so a route the Host maps through a different path would leave the
  // harness set SHORT and fail the exact list. It is no longer the ONLY thing covering the Host half.
  [Trait("Criterion", "AC-POS-0058")]
  // ⚠ ALSO CITED BY 274: `AC-POS-0068` — *names every HR route exactly, as an ordered set pinned by pattern
  // AND permission, with the count owned by the test rather than by the document.* That is this test's
  // SUBJECT rather than a consequence of it, which is the line that admits it and refused `AC-POS-0024`:
  // that criterion's subject is a permission on two transitions, and this test would have carried it only
  // as a by-product of asserting the whole surface.
  //
  // The count clause is carried by the exact set equality below owning the number outright — the criterion
  // deliberately states no figure, because 41 -> 46 would buy until the next route and re-arm the same trap
  // `CutoverManifestArchitectureTests` had when "ALL TWENTY" stood above a list of thirty-five.
  //
  // ⚠⚠ IT NEEDS NO PARTIAL NOTE, AND THAT IS A CHANGE TO THE CRITERION RATHER THAN TO THIS TEST. As first
  // reworded it repeated `AC-POS-0058`'s harness/Host clause, which both duplicated that criterion and
  // claimed a Host reach `MappedRoutes()` does not have. The clause was removed. The division now is:
  // `0058` owns 1:1 and harness/Host equivalence, `0068` owns exactness and permission-pinning — a route
  // reachable only through `Program.cs` fails `0058`; a route present under the WRONG PERMISSION fails this.
  [Trait("Criterion", "AC-POS-0068")]
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
        $"GET /api/hr/departments/{{departmentId}} => {Policy(HrPermissionNames.ViewDepartments)}",
        $"GET /api/hr/departments/{{departmentId}}/children => {Policy(HrPermissionNames.ViewDepartments)}",
        $"GET /api/hr/employees/ => {Policy(HrPermissionNames.ViewEmployees)}",
        $"GET /api/hr/employees/{{employeeId}} => {Policy(HrPermissionNames.ViewEmployees)}",
        $"GET /api/hr/employees/{{employeeId}}/branch-history => {Policy(HrPermissionNames.ViewEmployees)}",
        $"POST /api/hr/departments/ => {Policy(HrPermissionNames.CreateDepartments)}",
        // Activate and deactivate BOTH carry Deactivate: that permission governs whether a department may
        // receive employees, and both directions change that answer.
        $"POST /api/hr/departments/{{departmentId}}/activate => {Policy(HrPermissionNames.DeactivateDepartments)}",
        $"POST /api/hr/departments/{{departmentId}}/deactivate => {Policy(HrPermissionNames.DeactivateDepartments)}",
        $"POST /api/hr/departments/{{departmentId}}/manager => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/departments/{{departmentId}}/manager/remove => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/departments/{{departmentId}}/move => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/departments/{{departmentId}}/move-to-root => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"POST /api/hr/employees/ => {Policy(HrPermissionNames.CreateEmployees)}",
        $"POST /api/hr/employees/{{employeeId}}/activate => {Policy(HrPermissionNames.UpdateEmployees)}",
        // A department change is an ordinary employee update: DepartmentId is a classification, not a
        // security partition (ADR-024), so nothing crosses an authorization boundary.
        $"POST /api/hr/employees/{{employeeId}}/change-department => {Policy(HrPermissionNames.UpdateEmployees)}",
        $"POST /api/hr/employees/{{employeeId}}/deactivate => {Policy(HrPermissionNames.UpdateEmployees)}",
        $"POST /api/hr/employees/{{employeeId}}/terminate => {Policy(HrPermissionNames.TerminateEmployees)}",
        // Transfer moves a record across a security partition and holds a permission of its own.
        $"POST /api/hr/employees/{{employeeId}}/transfer => {Policy(HrPermissionNames.TransferEmployees)}",
        $"PUT /api/hr/departments/{{departmentId}} => {Policy(HrPermissionNames.UpdateDepartments)}",
        $"PUT /api/hr/employees/{{employeeId}} => {Policy(HrPermissionNames.UpdateEmployees)}",

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
        $"GET /api/hr/employees/{{employeeId}}/position-history => {Policy(HrPermissionNames.ViewEmployees)}",
        $"POST /api/hr/employees/{{employeeId}}/change-position => {Policy(HrPermissionNames.UpdateEmployees)}",

        $"GET /api/hr/positions/ => {Policy(HrPermissionNames.ViewPositions)}",
        $"GET /api/hr/positions/{{positionId}} => {Policy(HrPermissionNames.ViewPositions)}",
        $"POST /api/hr/positions/ => {Policy(HrPermissionNames.CreatePositions)}",
        $"POST /api/hr/positions/{{positionId}}/activate => {Policy(HrPermissionNames.DeactivatePositions)}",
        $"POST /api/hr/positions/{{positionId}}/deactivate => {Policy(HrPermissionNames.DeactivatePositions)}",
        $"PUT /api/hr/positions/{{positionId}} => {Policy(HrPermissionNames.UpdatePositions)}",

        $"GET /api/hr/job-grades/ => {Policy(HrPermissionNames.ViewJobGrades)}",
        $"GET /api/hr/job-grades/{{jobGradeId}} => {Policy(HrPermissionNames.ViewJobGrades)}",
        $"POST /api/hr/job-grades/ => {Policy(HrPermissionNames.CreateJobGrades)}",
        $"POST /api/hr/job-grades/{{jobGradeId}}/activate => {Policy(HrPermissionNames.DeactivateJobGrades)}",
        $"POST /api/hr/job-grades/{{jobGradeId}}/deactivate => {Policy(HrPermissionNames.DeactivateJobGrades)}",
        $"PUT /api/hr/job-grades/{{jobGradeId}} => {Policy(HrPermissionNames.UpdateJobGrades)}",

        $"GET /api/hr/salary-grades/ => {Policy(HrPermissionNames.ViewSalaryGrades)}",
        $"GET /api/hr/salary-grades/{{salaryGradeId}} => {Policy(HrPermissionNames.ViewSalaryGrades)}",
        $"POST /api/hr/salary-grades/ => {Policy(HrPermissionNames.CreateSalaryGrades)}",
        $"POST /api/hr/salary-grades/{{salaryGradeId}}/activate => {Policy(HrPermissionNames.DeactivateSalaryGrades)}",
        $"POST /api/hr/salary-grades/{{salaryGradeId}}/deactivate => {Policy(HrPermissionNames.DeactivateSalaryGrades)}",
        $"PUT /api/hr/salary-grades/{{salaryGradeId}} => {Policy(HrPermissionNames.UpdateSalaryGrades)}",

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
  // ⚠ CITED BY ITEM 220: `AC-EMP-0017` bans a delete ENDPOINT for Employee. This asserts it for the WHOLE HR surface, so it is
  // a SUPERSET -- named as one rather than duplicated by a narrower Employee-only test (item 220).
  [Trait("Criterion", "AC-EMP-0017")]
  // ⚠ CITED BY B18 pass 16: `AC-DEP-0032`'s API-ROUTE clause, and a SUPERSET for the second time. The
  // criterion bans a delete route for DEPARTMENT; this asserts the whole HR surface mounts no DELETE
  // verb at all, which covers it and Employee's ban together.
  [Trait("Criterion", "AC-DEP-0032")]
  // ⚠ CITED BY 269: `AC-POS-0027`'s TRANSPORT clause — *the composed HTTP surface exposes no `DELETE`
  // verb.* A SUPERSET for the third time in this file, on the same grounds as the two above.
  //
  // ⚠⚠ THE CRITERION'S FIRST CLAUSE IS NOT THIS TEST: *no route, HANDLER, OR REPOSITORY METHOD deletes a
  // position or a grade* is a claim about application and persistence code that a route scan cannot reach.
  //
  // ⚠⚠⚠ CORRECTED, SAME SWEEP: THIS COMMENT ORIGINALLY SAID THE NEAREST NEIGHBOURS WERE ONLY THE SCHEMA
  // REFUSAL AND THE PERMISSION ABSENCE, AND THAT NEITHER ASSERTED THE ABSENCE OF A DELETE METHOD. THAT WAS
  // WRONG. `PositionApplicationArchitectureTests.No_position_delete_command_or_handler_exists` scans the HR
  // application assembly for any Position/JobGrade/SalaryGrade type named `Delete` or `Remove` and asserts
  // the set is empty — which is the COMMAND and HANDLER half, executably. It is cited for this criterion.
  //
  // What genuinely remains is narrower than I first wrote: that scan is over TYPE NAMES in
  // `SSAS.HR.Application`, so a repository METHOD named `Delete` on a type not so named, in
  // `SSAS.HR.Infrastructure`, is outside it. Supporting neighbours:
  // `PositionSchemaSqlServerTests.Deleting_a_referenced_grade_or_position_is_refused` (the DATABASE refuses
  // it) and `No_position_family_offers_a_delete_or_manage_permission` (nobody could be authorised to).
  [Trait("Criterion", "AC-POS-0027")]
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
