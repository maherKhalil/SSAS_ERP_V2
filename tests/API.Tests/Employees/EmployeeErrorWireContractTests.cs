using System.Net;
using SSAS.HR.Application.Permissions;

namespace SSAS.API.Tests.Employees;

// ==================================================================================================
// THE WIRE CONTRACT FOR THE CODES T-080 RULED — STATUS AND PROBLEM CODE, NOT "NOT A 500".
// ==================================================================================================
//
// ---- WHY THIS EXISTS, AND THE MEASUREMENT THAT PROMPTED IT.
//
// T-080 mapped thirteen errors that had been answering `500 request.failed`. T-081 planted one of the new
// arms back out — a real response reverting from 400 to 500 — and **730 API tests and 326 HR tests stayed
// green.** Nothing in the tree called these routes' error paths. The arms were asserted by a source-text
// guard and by nothing that made a request.
//
// ---- THEY ASSERT THE EXACT CODE, DELIBERATELY.
//
// `Assert.NotEqual(500)` would pass against the next wrong answer. The ruled contract is a status AND a
// problem code, so both are asserted: a future change that turns `request.invalid` into
// `position.not_found` is a contract change and has to be made deliberately here.
//
// ---- WHAT IS DELIBERATELY ABSENT.
//
// Eight of the thirteen have no test, for three separate reasons, none of which is an oversight:
//
//   BY DESIGN        `PositionHistoryImmutable`, `InvalidCounts`, `InvalidColumnSet`, `InvalidActor` are
//                    500 precisely because a caller cannot cause them. A test would have to manufacture
//                    the state, which would assert the manufacture rather than the contract.
//   BY DEAD CODE     `PositionInDifferentCompany` has zero raise sites in `src/`; the Payroll overtime-tier
//                    pair is raised only inside `PayElement.SetOvertimeTier`, which has no caller in
//                    `src/`. Their arms stay as pre-mapping — the day a caller is wired, the answer is
//                    already right — but there is nothing to call today.
//   BY NORMALISATION `InvalidFileName` cannot fire from the route: `TryFileName`
//                    (`EmployeeEndpointRouteBuilderExtensions.cs:836-869`) defaults, rejects control
//                    characters before the handler, and truncates to 260. The aggregate keeps its check
//                    anyway, correctly — it is not the route's guard to rely on.
//
// **A test for any of those eight is a test that cannot fail.**
[Collection(EmployeeApiEndpointGroup.Name)]
public sealed class EmployeeErrorWireContractTests : IClassFixture<EmployeeApiTestHost>
{
  private const string Route = "/api/hr/employees";

  private readonly EmployeeApiTestHost host;

  public EmployeeErrorWireContractTests(EmployeeApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ---- AN ALL-ZERO GUID IS THE ONLY WAY TO REACH `PositionRequired` FROM THE WIRE.
  //
  // The route requires `positionId` and demands a JSON string, so a missing or null one is refused at
  // transport with `request.invalid` before any handler runs. `00000000-...` is a WELL-FORMED Guid string:
  // it passes transport, binds, and reaches `positionId == Guid.Empty` in
  // `CreateEmployeeCommandHandler.ValidatePositionAsync:222`. Without that path the error would be
  // unreachable and would belong in the absent list above.
  [Fact]
  public async Task An_empty_position_identifier_is_a_400_request_invalid()
  {
    using var response = await Send(Body("00000000-0000-0000-0000-000000000000"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ---- UNKNOWN AND OUT-OF-COMPANY ARE ONE ANSWER, WHICH IS WHY ONLY ONE OF THEM IS TESTABLE.
  //
  // `FindAssignablePositionAsync` returns null for a position that does not exist AND for one belonging to
  // another company — the collapse `BR-PLT-0002` requires, made one layer below the mapper. So this test
  // covers the only observable behaviour, and `Employee.PositionInDifferentCompany` has no raise site
  // precisely because the repository already refuses to make the distinction.
  [Fact]
  public async Task An_unknown_position_is_a_400_request_invalid()
  {
    using var response = await Send(Body("12345678-1234-1234-1234-123456789abc"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // An inactive position keeps the employees who hold it and accepts no new ones (`BRULE-POS-0013`). It is
  // named separately from "unknown" because the caller can already see it — naming it discloses nothing.
  [Fact]
  public async Task An_inactive_position_is_a_400_request_invalid()
  {
    using var response = await Send(Body(EmployeeApiTestHost.PositionInactive.ToString()));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // ---- THE IMPORT KEY IS THE ONE IMPORT-RUN GUARD A CALLER CAN ACTUALLY TRIP.
  //
  // `ImportKey.Create` (`ImportKey.cs:38-45`) refuses an empty, overlong or control-character key, and the
  // handler returns that failure directly (`ImportEmployeesCommandHandler.cs:97-101`). The query parameter
  // is required at transport, so the reachable case is a value that is PRESENT and invalid — an overlong
  // one, used here.
  //
  // Its three siblings on the same aggregate are not reachable: the file name is normalised by the route
  // before the aggregate sees it, and the counts and the actor are server-supplied.
  [Fact]
  public async Task An_overlong_import_key_is_a_400_request_invalid()
  {
    var key = new string('K', 300);

    using var request = EmployeeApiTestHost.CsvRequest(
      HttpMethod.Post,
      $"/api/hr/employees/import?importKey={key}",
      host.TokenWith(HrPermissionNames.ImportEmployees, HrPermissionNames.CreateEmployees),
      "employeeNumber,fullName,employmentDate,departmentCode,positionCode");

    using var response = await host.Client.SendAsync(request);

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    Assert.Equal("request.invalid", await EmployeeApiTestHost.ProblemCodeAsync(response));
  }

  // The create body with one substituted position, so each case differs from the others in exactly the
  // field under test and nothing else.
  private static string Body(string positionId) =>
    $$"""
    {"employeeNumber":"EMP-00147","fullName":"Layla Haddad","employmentDate":"2026-03-01T00:00:00+00:00","nationalId":"2990112345678","departmentId":"88888888-8888-8888-8888-888888888888","positionId":"{{positionId}}"}
    """;

  private Task<HttpResponseMessage> Send(string body) =>
    host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Post,
      Route,
      host.TokenWith(HrPermissionNames.CreateEmployees, HrPermissionNames.ViewEmployees),
      body));
}
