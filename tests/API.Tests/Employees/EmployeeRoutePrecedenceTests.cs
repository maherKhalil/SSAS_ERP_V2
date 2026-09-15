using System.Net;
using SSAS.HR.Application.Permissions;

namespace SSAS.API.Tests.Employees;

// ==================================================================================================
// A LITERAL SEGMENT BEATS A PARAMETER, AND THAT IS NOW THE ONLY THING KEEPING THESE ROUTES APART (T-264).
// ==================================================================================================
//
// ---- WHAT CHANGED, AND WHY THIS IS NOT A REGRESSION TO REPAIR.
//
// Three GET routes sit beside `/{employeeId}` on the same group with the same method:
//
//     /api/hr/employees/export        vs  /api/hr/employees/{employeeId}
//     /api/hr/employees/export-runs   vs  /api/hr/employees/{employeeId}
//     /api/hr/employees/import-runs   vs  /api/hr/employees/{employeeId}
//
// `{employeeId:guid}` used to refuse `export` outright, so the literal was the only candidate and the
// question never arose. **The constraint was removed deliberately** — a route constraint answers a
// malformed identifier with 404 before any application code runs, and a malformed identifier is a 400
// naming the parameter. Restoring it here would undo that for these routes.
//
// So both templates now match `GET /api/hr/employees/export`, and the winner is decided by ASP.NET Core
// preferring a literal segment over a parameter one. **That precedence is correct and stable, and it is
// documented by the framework and nowhere in this repository.** The behaviour is right and unasserted,
// which is the only thing wrong with it.
//
// ---- AND THE FAILURE MODE IS LOUDER THAN IT USED TO BE, WHICH IS WHY 200 IS THE ASSERTION.
//
// If precedence ever broke — a reordered registration, a catch-all added above these — `export` reaches
// the by-id handler, fails to bind as a `Guid`, and answers **400 naming the parameter**: an obviously
// wrong answer somebody reports. Under the old constraint the same misordering produced a **404**, which
// reads as *"no such endpoint"* and gets diagnosed as a missing route. **Asserting the status alone would
// not have distinguished them; asserting that the right handler answered does.**
public sealed class EmployeeRoutePrecedenceTests : IClassFixture<EmployeeApiTestHost>
{
  private readonly EmployeeApiTestHost host;

  public EmployeeRoutePrecedenceTests(EmployeeApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ⚠ THE CONTROL THAT MAKES EVERY TEST BELOW MEAN SOMETHING, AND THE NAME SAYS SO BECAUSE THE NAME IS
  // WHAT A DELETER READS.
  //
  // If `/{employeeId}` were removed, the literal routes would answer 200 for the trivial reason that
  // nothing competes with them, and this whole file would pass while asserting nothing about precedence.
  // A test that verifies a tie-break is worthless once there is no tie.
  [Fact]
  public void Both_competing_templates_are_registered_which_is_what_makes_the_precedence_tests_below_mean_anything()
  {
    var patterns = host.MappedRoutes()
      .Where(route => route.Method == "GET")
      .Select(route => route.Pattern)
      .ToArray();

    Assert.Contains("/api/hr/employees/{employeeId}", patterns);

    foreach (var literal in new[] { "export", "export-runs", "import-runs" })
    {
      Assert.Contains($"/api/hr/employees/{literal}", patterns);
    }
  }

  // The literal wins. Asserted through a real request rather than by inspecting the route table, because
  // the route table cannot tell which endpoint routing would SELECT — only that both are candidates.
  [Theory]
  [InlineData("export", HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)]
  [InlineData("export-runs", HrPermissionNames.ExportEmployees, HrPermissionNames.ViewEmployees)]
  [InlineData("import-runs", HrPermissionNames.ImportEmployees, HrPermissionNames.ViewEmployees)]
  public async Task A_literal_segment_reaches_its_own_handler_rather_than_the_employee_id_route(
    string literal, string first, string second)
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, $"/api/hr/employees/{literal}", host.TokenWith(first, second)));

    // 200 is only reachable from the literal's own handler. The by-id handler cannot answer 200 for
    // "export": it would fail to bind the identifier and answer 400, or find no such employee and
    // answer 404. Either way the value below would differ.
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // ⚠ THE OTHER HALF, WITHOUT WHICH THE ABOVE IS SATISFIED BY A BROKEN PARAMETER ROUTE.
  //
  // A parameter route that matched nothing at all would leave every literal winning by default and every
  // assertion above green. So the same position is exercised with a value only the parameter route can
  // serve, and it must reach the by-id handler.
  [Fact]
  public async Task An_identifier_in_the_same_position_still_reaches_the_employee_id_route()
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get,
      $"/api/hr/employees/{EmployeeApiTestHost.EmployeeId}",
      host.TokenWith(HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  // And the segment that is neither: not a literal route and not a valid identifier. It reaches the by-id
  // handler and is refused there, with the parameter named — the answer the constraint removal was for.
  // A 404 here would mean routing rejected it before the application saw it, which is the old behaviour.
  [Fact]
  public async Task A_segment_that_is_neither_a_literal_route_nor_an_identifier_is_a_request_error_not_a_missing_route()
  {
    using var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Get, "/api/hr/employees/not-a-guid", host.TokenWith(HrPermissionNames.ViewEmployees)));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
  }
}
