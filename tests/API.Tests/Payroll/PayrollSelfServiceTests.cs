using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Routing;
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
  // AC-SS-0007 — THE CONTRACT NAMES NO EMPLOYEE, ON ALL FOUR SURFACES.
  // ================================================================================================
  //
  // Asserted against the CONTRACT rather than a handler's behaviour, as the criterion requires: a handler
  // that happened to ignore an employee parameter would still be a contract that carried one.
  //
  // ---- THIS IS THE FIRST TEST IN THE TREE TO INSPECT HANDLER PARAMETERS, SO THE LINE IT DRAWS IS
  // ---- INHERITED BY EVERYONE AFTER IT.
  //
  // A minimal-API handler's parameters mix two populations: values BOUND FROM THE REQUEST, and SERVICES
  // resolved from the container. Only the first is part of the contract — an injected `IPayrollReadService`
  // is not something a caller can set, and a sweep that treated it as a contract member would be asserting
  // nonsense.
  //
  // **The classification is stated in the failure message rather than assumed**, so a future reader can
  // dispute which side a parameter landed on instead of discovering the line by reading this comment.
  [Fact]
  [Trait("Criterion", "AC-SS-0007")]
  public void The_self_route_contract_names_no_employee_on_any_surface()
  {
    var endpoint = host.MappedRoutes().Single(route => route.Pattern == Route);

    // PATH. The route pattern is the whole path surface, and it carries no parameters at all.
    Assert.DoesNotContain("employee", endpoint.Pattern, StringComparison.OrdinalIgnoreCase);

    var handler = host.MappedEndpoint(Route).Metadata.GetMetadata<MethodInfo>();
    Assert.NotNull(handler);

    // QUERY, HEADER AND BODY. Every parameter the handler declares, split into the two populations, with
    // the split reported either way.
    var bound = new List<string>();
    var injected = new List<string>();

    foreach (var parameter in handler!.GetParameters())
    {
      var type = parameter.ParameterType;

      // A service is one the container can hand over. Everything else is bound from the request — which is
      // the conservative direction: an unrecognised type is treated as part of the contract, not excused
      // from it.
      var isService = type.IsInterface ||
        type == typeof(CancellationToken) ||
        type == typeof(Microsoft.AspNetCore.Http.HttpContext);

      (isService ? injected : bound).Add($"{type.Name} {parameter.Name}");
    }

    // NOT VACUOUS. A handler whose parameters could not be read at all would leave both lists empty and
    // every assertion below would pass over nothing.
    Assert.NotEmpty(injected);

    Assert.True(
      bound.Count == 0,
      $"The self-service route must bind nothing from the request. Bound: [{string.Join(", ", bound)}]. " +
      $"Injected (not part of the contract): [{string.Join(", ", injected)}].");

    Assert.DoesNotContain(
      injected,
      parameter => parameter.Contains("employee", StringComparison.OrdinalIgnoreCase));
  }

  private Task<HttpResponseMessage> Send(params string[] permissions) =>
    host.Client.SendAsync(PayrollApiTestHost.Request(
      HttpMethod.Get, Route, host.TokenWith(permissions)));
}
