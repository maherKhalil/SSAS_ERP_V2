using System.Net;
using SSAS.API.Tests.Infrastructure;
using SSAS.Payroll.Application.Permissions;

namespace SSAS.API.Tests.Payroll;

// ==================================================================================================
// FP-015's FIRST VERTICAL SLICE: AN EMPLOYEE READS THEIR OWN PAYSLIPS (T-088).
// ==================================================================================================
//
// Four criteria, each with the control that makes it mean something:
//
//   AC-SS-0005   the administrative permission alone is refused here
//   AC-SS-0007   the contract names no employee, asserted against the CONTRACT
//   AC-SS-0008   an unmapped caller gets an ordinary refusal
//   AC-SS-0009   and it is a 404, asserted as a STATUS — not merely "nothing threw"
[Collection(PayrollApiEndpointGroup.Name)]
public sealed class PayrollSelfServiceTests : IClassFixture<PayrollApiTestHost>
{
  private const string Route = "/api/payroll/me/payslips";

  private readonly PayrollApiTestHost host;

  public PayrollSelfServiceTests(PayrollApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ---- THE SELF PERMISSION ALONE IS ENOUGH, AND THAT IS THE WHOLE POINT OF A DISTINCT PERMISSION.
  //
  // The caller holds `ViewOwn` and NOT the administrative `Payroll.Payslips.View`. If this ever needed
  // both, the permission would be a scope wearing a permission's name and `OD-SS-0001` would be unbuilt.
  [Fact]
  [Trait("Criterion", "AC-SS-0004")]
  [Trait("Criterion", "AC-SS-0003")]
  public async Task The_self_permission_alone_reads_the_callers_own_payslips()
  {
    using var response = await Send(PayrollPermissionNames.ViewOwnPayslips);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    // AND THE SUBJECT WAS RESOLVED FROM THE CALLER, not taken from anywhere a caller could set. Without
    // this the test passes against a handler that reads a hard-coded employee.
    Assert.NotEmpty(host.SelfService.AskedForUser);
  }

  // THE CONTROL. Holding neither permission is refused — so the success above is the permission working
  // rather than the route being open.
  [Fact]
  [Trait("Criterion", "AC-SS-0004")]
  public async Task Without_the_self_permission_the_route_is_refused()
  {
    using var response = await Send(PayrollPermissionNames.ViewRuns);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AC-SS-0005. THE DIRECTION THAT MATTERS COMMERCIALLY IS THE OTHER ONE, BUT THIS IS ITS PAIR.
  //
  // An administrator holding `Payroll.Payslips.View` — sight of EVERY employee's payslips — is refused
  // here. The two permissions share a prefix and share nothing else: the authorization stack compares
  // claim values ordinally and cannot see the resemblance.
  [Fact]
  [Trait("Criterion", "AC-SS-0005")]
  public async Task The_administrative_permission_alone_is_refused_at_the_self_route()
  {
    using var response = await Send(PayrollPermissionNames.ViewPayslips);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // ---- AC-SS-0008 AND AC-SS-0009. THE STATUS IS ASSERTED, AND THAT IS THE POINT OF THIS TEST.
  //
  // T-076's finding: a test that only checks "nothing threw" passes against `500 request.failed`, because
  // an unmapped error falls through the mapper without an exception and without a log entry. **So the
  // assertion is the code, not the absence of a crash.**
  //
  // A support administrator with no employee record is a normal caller — `ADR-030` Decision 5 — and this
  // is what tells them so rather than telling them the server broke.
  [Fact]
  [Trait("Criterion", "AC-SS-0009")]
  public async Task An_unmapped_caller_is_told_so_rather_than_receiving_a_server_error()
  {
    host.SelfService.LinkedEmployee = null;

    using var response = await Send(PayrollPermissionNames.ViewOwnPayslips);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("payroll.no_linked_employee", await PayrollApiTestHost.ProblemCodeAsync(response));
  }

  // ================================================================================================
  // AC-SS-0007 / TS-SS-0003 — THE CONTRACT NAMES NO EMPLOYEE, ON ALL FOUR SURFACES.
  // ================================================================================================
  //
  // Asserted against the CONTRACT rather than a handler's behaviour, as the criterion requires: a handler
  // that happened to ignore an employee parameter would still be a contract that carried one.
  //
  // ---- THE RULE MOVED OUT OF THIS FILE IN T-089, AND THE MOVE IS THE POINT.
  //
  // T-088 wrote it here as `bound.Count == 0`, which was correct for a route that binds nothing. **Attendance
  // then added `/me/records`, which legitimately binds `fromDate` and `toDate`** — a filter that narrows the
  // caller's own data and cannot widen it. Left where it was, the rule would have had to be relaxed for that
  // route, and a guard with a per-route exception stops meaning anything.
  //
  // `SelfServiceContractRule` states the general property instead — **no bound parameter may be the SUBJECT
  // of the read** — in ONE place, so the two modules cannot drift into two different definitions of the same
  // criterion. `DEC-L-072`: the assertable claim belongs somewhere one edit reaches every caller.
  //
  // **This route still binds nothing, and that remains true under the general rule** — it is simply no
  // longer the thing being asserted.
  [Fact]
  [Trait("Criterion", "AC-SS-0007")]
  public void The_self_route_contract_names_no_employee_on_any_surface() =>
    SelfServiceContractRule.AssertNoSubjectOnAnySurface(host.MappedEndpoint(Route));

  private Task<HttpResponseMessage> Send(params string[] permissions) =>
    host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get, Route, host.TokenWith(permissions)));
}
