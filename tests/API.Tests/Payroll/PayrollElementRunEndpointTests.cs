using System.Net;
using SSAS.Payroll.Application.Permissions;

namespace SSAS.API.Tests.Payroll;

// =================================================================================================
// PAYROLL'S SEVEN UNCALLED ROUTES (T-206): ELEMENTS, THE RUN CALCULATION, AND A PAYSLIP.
// =================================================================================================
//
// The other half of the fourteen money-adjacent routes no request had ever reached. Payroll's host already
// composes its write path — unlike Attendance's, it registers each handler explicitly — so these were
// untested for want of tests rather than for want of a composition.
//
// ---- ⚠ THE PAYSLIP PAIR IS THE ONE THAT WOULD COST MOST TO GET WRONG.
//
// `Payroll.Payslips.View` reads ANY employee's payslip. `Payroll.Payslips.ViewOwn` reads the caller's own.
// `PayrollPermissionNames` says it in its own comment — *"it shares a prefix with `ViewPayslips` and shares
// nothing else"* — and the administrative route must refuse the self-service permission. A route that
// accepted it would hand every employee the whole company's pay.
//
// **A sweep asserting each route requires SOME permission passes either way**, because both are permissions
// and both are on the payslip family. Only a request with the wrong one can tell them apart.
//
// ---- AND EACH REFUSAL HAS A CONTROL, WHICH GL TAUGHT THIS AFTERNOON.
//
// Regrading GL's balance route left its refusal test green, because the handler enforced the same
// permission a second time. **A refusal assertion alone cannot distinguish a route that guards correctly
// from one that has stopped guarding while something behind it still does** — so the payslip and the
// calculation carry the allowed case too.
public sealed class PayrollElementRunEndpointTests(PayrollApiTestHost host)
  : IClassFixture<PayrollApiTestHost>
{
  [Fact]
  public async Task The_administrative_payslip_read_refuses_the_self_service_permission()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get,
      $"/api/payroll/runs/{Guid.NewGuid()}/payslips/{PayrollApiTestHost.EmployeeId}",
      host.TokenWith(PayrollPermissionNames.ViewOwnPayslips)));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // The control. Without it a payslip route that refused everyone would satisfy the assertion above.
  [Fact]
  public async Task The_administrative_permission_reaches_the_payslip_and_answers_about_the_run()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get,
      $"/api/payroll/runs/{Guid.NewGuid()}/payslips/{PayrollApiTestHost.EmployeeId}",
      host.TokenWith(PayrollPermissionNames.ViewPayslips)));

    Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  // ⚠ APPROVING A RUN IS A HIGHER AUTHORITY THAN CALCULATING ONE AND STILL DOES NOT CONFER IT.
  // Permissions are not ordered. An approver signs off what someone else computed, and letting the approval
  // authority recalculate would let one person compute and approve the same figures.
  [Fact]
  public async Task Approving_runs_does_not_confer_the_authority_to_calculate_one()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{Guid.NewGuid()}/calculation",
      host.TokenWith(PayrollPermissionNames.ApproveRuns), "{}"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Managing_runs_reaches_the_calculation_and_answers_about_the_run()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/runs/{Guid.NewGuid()}/calculation",
      host.TokenWith(PayrollPermissionNames.ManageRuns), "{}"));

    Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  // A one-off payment is the only route on this list that moves money on its own rather than as part of a
  // run, so its authority is compensation management — not run management, which is the neighbour a
  // payroll administrator is most likely to already hold.
  [Fact]
  public async Task A_one_off_payment_needs_compensation_management_not_run_management()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/employees/{PayrollApiTestHost.EmployeeId}/one-off-payments",
      host.TokenWith(PayrollPermissionNames.ManageRuns),
      """{"payElementId":"11111111-1111-1111-1111-111111111111","amount":500,"note":"Bonus"}"""));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Reading_an_element_and_changing_one_are_different_authorities()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Put, $"/api/payroll/elements/{Guid.NewGuid()}",
      host.TokenWith(PayrollPermissionNames.ViewElements),
      """{"name":"Basic Salary","isTaxable":false}"""));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Activating_an_element_needs_element_management()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/elements/{Guid.NewGuid()}/activation",
      host.TokenWith(PayrollPermissionNames.ViewElements), "{}"));

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Deactivating_an_unknown_element_is_a_not_found_rather_than_a_server_error()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Post, $"/api/payroll/elements/{Guid.NewGuid()}/deactivation",
      host.TokenWith(PayrollPermissionNames.ManageElements), "{}"));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task An_unknown_element_read_is_a_not_found()
  {
    host.ResetToAuthorizedState();

    var response = await host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get, $"/api/payroll/elements/{Guid.NewGuid()}",
      host.TokenWith(PayrollPermissionNames.ViewElements)));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }
}
