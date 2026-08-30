using System.Net;
using SSAS.HR.Application.Permissions;

namespace SSAS.API.Tests.Positions;

// ==================================================================================================
// `Employee.PositionUnchanged` ON THE WIRE — THE ONE RULED CODE THAT LIVES ON THIS ROUTE.
// ==================================================================================================
//
// `POST /api/hr/employees/{employeeId}/change-position` is mapped in `PositionEndpointRouteBuilderExtensions`
// but answers through `EmployeeApiErrorMapper`, because the route answers about an EMPLOYEE. That mapper had
// no `Employee.Position*` arm at all until T-080, so this route answered `500 request.failed` to a caller
// who asked to move an employee to the position they already hold.
//
// It lives here rather than beside the other four because the route does: the test that would have caught
// the defect had to be on the host that mounts it, and no test on this host called the route's error path.
[Collection(PositionApiEndpointGroup.Name)]
public sealed class PositionChangeErrorWireContractTests : IClassFixture<PositionApiTestHost>
{
  private readonly PositionApiTestHost host;

  public PositionChangeErrorWireContractTests(PositionApiTestHost host)
  {
    this.host = host;
    host.Reset();
  }

  // ---- THE DESTINATION IS THE EMPLOYEE'S CURRENT POSITION, WHICH IS REFUSED RATHER THAN NO-OPPED.
  //
  // `ChangeEmployeePositionCommandHandler:96-99` refuses before looking the destination up, deliberately:
  // an unchanged destination answered with a success would report that something happened when nothing did,
  // and would write a history row for a move that never occurred.
  //
  // `PositionA` is the position the seeded employee already holds
  // (`EmployeeApiTestStubs.cs:255-263`, `StampInitialAssignment`), so sending it is the whole test.
  [Fact]
  public async Task Moving_an_employee_to_the_position_they_already_hold_is_a_400_request_invalid()
  {
    using var request = PositionApiTestHost.Request(
      HttpMethod.Post,
      $"/api/hr/employees/{PositionApiTestHost.EmployeeId}/change-position",
      host.TokenWith(HrPermissionNames.UpdateEmployees, HrPermissionNames.ViewEmployees),
      $$"""
      {"positionId":"{{Employees.EmployeeApiTestHost.PositionA}}","expectedRowVersion":"AAAAAAAAB9E="}
      """);

    using var response = await host.Client.SendAsync(request);

    // ⚠ 422 AND `position.unchanged` SINCE T-274 -- THIS TEST PINNED A DIVERGENCE, NOT A CONTRACT.
    //
    // It asserted `400 request.invalid`. **It was not failing; it recorded what the product did.** What
    // the product did disagreed with `FP-008 api-contracts.md:167` -- `422 position.unchanged` -- and with
    // `AC-POS-0034`, in BOTH the status and the code. Nothing anywhere recorded a reason to differ, so the
    // code moved to the contract rather than the contract to the code.
    //
    // 422 is the contract's own distinction and it is the right one: the body is well-formed and every
    // field is valid. What is refused is the request's MEANING against current state.
    Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

    using var document = System.Text.Json.JsonDocument.Parse(
      await response.Content.ReadAsStringAsync());

    Assert.Equal("position.unchanged", document.RootElement.GetProperty("code").GetString());

    // The field survives the status change: the caller sent `positionId` and it is still the input at
    // fault. Status is the category, the code is the instruction, and the field is which input.
    Assert.Equal("positionId", document.RootElement.GetProperty("field").GetString());
  }
}
