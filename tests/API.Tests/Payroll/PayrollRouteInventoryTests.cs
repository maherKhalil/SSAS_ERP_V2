using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.Payroll.Application.Permissions;

namespace SSAS.API.Tests.Payroll;

[Collection(PayrollApiEndpointGroup.Name)]
public sealed class PayrollRouteInventoryTests(PayrollApiTestHost host) : IClassFixture<PayrollApiTestHost>
{
  // ---- THE INVENTORY IS PINNED BY NAME, NOT BY COUNT.
  //
  // A count alone passes when one route is removed and another added. Naming every route means a change to
  // the surface has to be acknowledged here, which is where a reviewer will see it.
  private static readonly (string Method, string Pattern, string Permission)[] Expected =
  [
    ("POST", "/api/payroll/employees/{employeeId:guid}/compensation", PayrollPermissionNames.ManageCompensation),
    ("GET", "/api/payroll/employees/{employeeId:guid}/compensation", PayrollPermissionNames.ViewCompensation),
    ("GET", "/api/payroll/employees/{employeeId:guid}/compensation/current", PayrollPermissionNames.ViewCompensation),
    ("GET", "/api/payroll/employees/{employeeId:guid}/payslips", PayrollPermissionNames.ViewPayslips),

    ("POST", "/api/payroll/elements", PayrollPermissionNames.ManageElements),
    ("GET", "/api/payroll/elements", PayrollPermissionNames.ViewElements),
    ("GET", "/api/payroll/elements/{payElementId:guid}", PayrollPermissionNames.ViewElements),
    ("PUT", "/api/payroll/elements/{payElementId:guid}", PayrollPermissionNames.ManageElements),
    ("POST", "/api/payroll/elements/{payElementId:guid}/deactivation", PayrollPermissionNames.ManageElements),
    ("POST", "/api/payroll/elements/{payElementId:guid}/activation", PayrollPermissionNames.ManageElements),

    ("POST", "/api/payroll/periods", PayrollPermissionNames.ManageRuns),
    ("GET", "/api/payroll/periods", PayrollPermissionNames.ViewRuns),

    ("POST", "/api/payroll/runs", PayrollPermissionNames.ManageRuns),
    ("GET", "/api/payroll/runs", PayrollPermissionNames.ViewRuns),
    ("GET", "/api/payroll/runs/{payrollRunId:guid}", PayrollPermissionNames.ViewRuns),
    ("POST", "/api/payroll/runs/{payrollRunId:guid}/calculation", PayrollPermissionNames.ManageRuns),
    ("POST", "/api/payroll/runs/{payrollRunId:guid}/approval", PayrollPermissionNames.ApproveRuns),
    ("POST", "/api/payroll/runs/{payrollRunId:guid}/posting", PayrollPermissionNames.PostRuns),
    ("POST", "/api/payroll/runs/{payrollRunId:guid}/reversals", PayrollPermissionNames.PostRuns),

    ("GET", "/api/payroll/runs/{payrollRunId:guid}/payslips/{employeeId:guid}", PayrollPermissionNames.ViewPayslips)
  ];

  [Fact]
  public void The_payroll_route_surface_is_exactly_the_documented_inventory()
  {
    var actual = host.MappedRoutes()
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    var expected = Expected
      .Select(route => (route.Method, route.Pattern))
      .OrderBy(route => route.Pattern, StringComparer.Ordinal)
      .ThenBy(route => route.Method, StringComparer.Ordinal)
      .ToArray();

    Assert.Equal(expected, actual);
  }

  [Fact]
  public void Every_payroll_route_requires_a_permission()
  {
    // A route without a policy is reachable by any authenticated caller. On a surface carrying what people
    // are paid, that is the single worst mistake available — and it is invisible in a diff that forgets one
    // line.
    var unprotected = host.MappedRoutes()
      .Where(route => string.IsNullOrEmpty(route.Policy))
      .Select(route => $"{route.Method} {route.Pattern}")
      .ToArray();

    Assert.Empty(unprotected);
  }

  [Fact]
  public void Every_route_requires_the_permission_the_inventory_names()
  {
    var actual = host.MappedRoutes()
      .ToDictionary(route => $"{route.Method} {route.Pattern}", route => route.Policy, StringComparer.Ordinal);

    foreach (var (method, pattern, permission) in Expected)
    {
      var key = $"{method} {pattern}";

      Assert.True(actual.ContainsKey(key), $"{key} is not mapped");
      Assert.Equal($"{PermissionPolicyNames.TenantPrefix}{permission}", actual[key]);
    }
  }

  [Fact]
  public void No_payroll_route_responds_to_delete()
  {
    // ---- THE ABSENCE IS THE ASSERTION, AND PAYROLL IS STRICTER THAN GL.
    //
    // GL has one destructive route (discarding a draft, which was never part of the ledger). Payroll has
    // NONE: compensation is dated history, elements deactivate, and approved run lines are append-only. A
    // future route adding the verb would have to delete this test.
    var deleteRoutes = host.MappedRoutes()
      .Where(route => string.Equals(route.Method, "DELETE", StringComparison.OrdinalIgnoreCase))
      .Select(route => route.Pattern)
      .ToArray();

    Assert.Empty(deleteRoutes);
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0003")]
  public void Compensation_has_no_put_because_a_change_is_a_new_record()
  {
    // The absent verb IS the ruling. An update route would make `BR-PAY-0002` a convention rather than a
    // property of the surface.
    Assert.DoesNotContain(
      host.MappedRoutes(),
      route => route.Pattern.EndsWith("/compensation", StringComparison.Ordinal) &&
        string.Equals(route.Method, "PUT", StringComparison.OrdinalIgnoreCase));
  }

  [Fact]
  [Trait("Decision", "OD-PAY-0009")]
  public void Approval_and_posting_carry_different_permissions_from_each_other_and_from_management()
  {
    var byKey = host.MappedRoutes()
      .ToDictionary(route => $"{route.Method} {route.Pattern}", route => route.Policy, StringComparer.Ordinal);

    var calculate = byKey["POST /api/payroll/runs/{payrollRunId:guid}/calculation"];
    var approve = byKey["POST /api/payroll/runs/{payrollRunId:guid}/approval"];
    var post = byKey["POST /api/payroll/runs/{payrollRunId:guid}/posting"];

    // Three distinct grants, so preparing, authorizing and committing can be three different people.
    Assert.NotEqual(calculate, approve);
    Assert.NotEqual(approve, post);
    Assert.NotEqual(calculate, post);
  }
}
