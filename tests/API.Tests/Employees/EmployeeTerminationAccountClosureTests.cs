using System.Net;
using SSAS.BuildingBlocks.Domain;
using SSAS.HR.Application.Permissions;

namespace SSAS.API.Tests.Employees;

// ==================================================================================================
// REQ-SS-0007 — TERMINATION CLOSES THE ACCOUNT, AND THE ORDERING IS ASSERTED (T-091).
// ==================================================================================================
//
// The second of two guards. T-090's is at `IUserEmployeeResolver` and closes self-service per request
// against live state; this one closes authentication. **Neither is on the link.**
//
// ---- WHAT THESE TESTS ARE REALLY ABOUT: THE ORDER, NOT THE CALL.
//
// That the handler calls the deactivator is one assertion and the easy one. **The ruling was about what
// happens when one of the two databases refuses**, and every assertion below is about which side moved:
//
//   deactivation fails  -> the termination ROLLS BACK. Nothing happened, and the operator's retry is safe.
//   commit fails after  -> a half-state, and it is REPORTED with a code that names the repair.
//
// A test suite that only checked the happy path would have passed against `commit termination, then
// deactivate` — the order that fails OPEN into a state nothing can undo, because termination is terminal.
[Collection(EmployeeApiEndpointGroup.Name)]
public sealed class EmployeeTerminationAccountClosureTests : IClassFixture<EmployeeApiTestHost>
{
  private const string Route = "/api/hr/employees";

  private const string ValidTerminateBody = """
    {"terminationDate":"2027-01-31T00:00:00+00:00","reasonCode":"Resignation","expectedRowVersion":"AAAAAAAAB9E="}
    """;

  private readonly EmployeeApiTestHost host;

  public EmployeeTerminationAccountClosureTests(EmployeeApiTestHost host)
  {
    this.host = host;
    host.ResetToAuthorizedState();
  }

  // ---- THE CALL ITSELF, AND WITH THE RIGHT SUBJECT.
  //
  // `Asked` is compared against the LOADED aggregate's identifier rather than the route's. The handler
  // passes `employee.Id`, and that is the correct source: the route identifier is what the repository was
  // asked for, while the aggregate's is what was actually found. **A handler that closed the account of some
  // other employee would satisfy "the deactivator ran"** and be a worse defect than not running at all.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task Termination_closes_the_tenant_user_account()
  {
    var response = await Terminate();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Equal([host.Repository.Employee!.Id], host.TenantUsers.Asked);
    Assert.Equal(1, host.UnitOfWork.Commits);
  }

  // ================================================================================================
  // THE ORDERING RULING, ASSERTED: A FAILED CLOSURE ROLLS THE TERMINATION BACK.
  // ================================================================================================
  //
  // **This is the assertion that distinguishes the shipped order from the one that was refused.** Under
  // `commit termination -> deactivate`, this same failure would leave a terminated employee with a live
  // account — `AC-SS-0012`'s exposure, recreated by the change that closes it, and unrepairable because
  // nothing in the product can un-terminate an employee.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task A_failed_closure_refuses_the_termination_and_rolls_it_back()
  {
    host.TenantUsers.Failure = new Error("IdentityAccess.WriteFailure", "the platform database is down");

    var response = await Terminate();

    // The caller sees a failure, not a success with a hidden gap.
    Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

    // NOTHING WAS COMMITTED. Without this the test passes against a handler that committed first and
    // reported the closure failure afterwards — which is exactly the refused order.
    Assert.Equal(0, host.UnitOfWork.Commits);
    Assert.True(host.UnitOfWork.Rollbacks > 0);
  }

  // ================================================================================================
  // THE ONE REACHABLE HALF-STATE IS VISIBLE, WITH ITS OWN CODE.
  // ================================================================================================
  //
  // The account is closed and the tenant commit then fails. **A generic write failure would be true and
  // useless** — it tells the operator to retry, and a retry does succeed, but until then someone cannot
  // sign in and nothing says why.
  //
  // `employee.termination_incomplete` names the state and the repair, and the repair — the reactivation
  // route — ships in this same task. **T-076's finding is why the CODE is asserted and not merely a
  // non-200:** an unmapped error falls through the mapper with nothing thrown and nothing logged, so
  // `500 request.failed` would satisfy a weaker assertion.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task A_commit_that_fails_after_the_closure_is_reported_with_its_own_code()
  {
    host.UnitOfWork.CommitFailure = new InvalidOperationException("the tenant database went away");

    var response = await Terminate();

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    Assert.Equal("employee.termination_incomplete", await EmployeeApiTestHost.ProblemCodeAsync(response));

    // The account WAS closed — which is what makes this a half-state rather than a clean failure, and what
    // the operator has to repair.
    Assert.Equal([host.Repository.Employee!.Id], host.TenantUsers.Asked);
  }

  // ================================================================================================
  // `Inactive` DOES NOT CLOSE THE ACCOUNT — THE CONTROL THAT KEEPS THE GUARD NARROW.
  // ================================================================================================
  //
  // Deactivating an employee is unpaid leave or suspension: the employment relationship persists and is
  // fully reversible, and T-090 resolves such an employee normally. **Closing the account here would lock
  // someone out of their own payslips while they are still employed**, and nothing in either requirement
  // asks for that.
  //
  // Asserted here rather than trusted from the handler's shape, because the two operations sit next to each
  // other in the same file and are one copy-paste apart.
  [Fact]
  [Trait("Criterion", "REQ-SS-0007")]
  public async Task Deactivating_an_employee_does_not_close_their_account()
  {
    var response = await host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Post,
      $"{Route}/{EmployeeApiTestHost.EmployeeId}/deactivate",
      host.TokenWith(HrPermissionNames.UpdateEmployees, HrPermissionNames.ViewEmployees),
      """{"reasonCode":"Administrative","expectedRowVersion":"AAAAAAAAB9E="}"""));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Empty(host.TenantUsers.Asked);
  }

  private Task<HttpResponseMessage> Terminate() =>
    host.Client.SendAsync(EmployeeApiTestHost.Request(
      HttpMethod.Post,
      $"{Route}/{EmployeeApiTestHost.EmployeeId}/terminate",
      host.TokenWith(HrPermissionNames.TerminateEmployees, HrPermissionNames.ViewEmployees),
      ValidTerminateBody));
}
