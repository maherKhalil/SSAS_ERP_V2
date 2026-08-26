using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SSAS.Attendance.API;
using SSAS.BuildingBlocks.Api.Authorization;
using SSAS.GL.API;
using SSAS.HR.API.Departments;
using SSAS.HR.API.Employees;
using SSAS.HR.API.Positions;
using SSAS.Payroll.API;

namespace SSAS.API.Tests.Infrastructure;

// ==================================================================================================
// A HOST THAT MOUNTS MODULE ROUTES WITHOUT THE GATE'S DEPENDENCY FAILS AT STARTUP (T-034).
// ==================================================================================================
//
// ---- WHAT THIS REPLACES, AND WHY THE OLD FAILURE MODE WAS THE PROBLEM.
//
// Before T-034 the omission was invisible until a request arrived, and then it was a 500 — 171 of them
// across five suites, looking like a product bug rather than a missing registration. Nothing pointed at
// the test hosts from the module side, so nothing could have told them.
//
// **Each of the ten mapping entry points now asserts its own dependency**, so the failure happens where
// the host is composed. These tests are that promise, one per entry point: map without the registration
// and the exception names the module, the service and the remedy.
//
// ---- WHY THE HOST IS BUILT HERE RATHER THAN REUSING A TEST HOST.
//
// The five API test hosts all register the service — that is the point of them now — so none of them can
// demonstrate the failure. This builds the deliberately-incomplete host that no other suite has reason
// to, which is the only way to exercise a composition defect.
public sealed class ModuleEndpointRequirementTests
{
  // Every public module mapping entry point in the product. Named rather than discovered: a scan could
  // pass vacuously, and naming them means an eleventh added without a line here is a visible omission in
  // review rather than a silent gap in coverage.
  public static TheoryData<string, Action<IEndpointRouteBuilder>> MappingEntryPoints => new()
  {
    { nameof(EmployeeEndpointRouteBuilderExtensions.MapHrEmployeeEndpoints),
      endpoints => endpoints.MapHrEmployeeEndpoints() },
    { nameof(DepartmentEndpointRouteBuilderExtensions.MapHrDepartmentEndpoints),
      endpoints => endpoints.MapHrDepartmentEndpoints() },
    { nameof(DepartmentEndpointRouteBuilderExtensions.MapHrEmployeeDepartmentEndpoints),
      endpoints => endpoints.MapHrEmployeeDepartmentEndpoints() },
    { nameof(PositionEndpointRouteBuilderExtensions.MapHrPositionEndpoints),
      endpoints => endpoints.MapHrPositionEndpoints() },
    { nameof(PositionEndpointRouteBuilderExtensions.MapHrJobGradeEndpoints),
      endpoints => endpoints.MapHrJobGradeEndpoints() },
    { nameof(PositionEndpointRouteBuilderExtensions.MapHrSalaryGradeEndpoints),
      endpoints => endpoints.MapHrSalaryGradeEndpoints() },
    { nameof(PositionEndpointRouteBuilderExtensions.MapHrEmployeePositionEndpoints),
      endpoints => endpoints.MapHrEmployeePositionEndpoints() },
    { nameof(GlEndpointRouteBuilderExtensions.MapGlEndpoints),
      endpoints => endpoints.MapGlEndpoints() },
    { nameof(PayrollEndpointRouteBuilderExtensions.MapPayrollEndpoints),
      endpoints => endpoints.MapPayrollEndpoints() },
    { nameof(AttendanceEndpointRouteBuilderExtensions.MapAttendanceEndpoints),
      endpoints => endpoints.MapAttendanceEndpoints() },
  };

  // ---- THE GUARD. THIS IS THE TEST THAT FAILS ON THE CODE AS IT STOOD BEFORE T-034.
  //
  // Every one of the ten mapped cleanly without the registration before this task and then answered 500
  // per request. Now each refuses to map at all.
  [Theory]
  [MemberData(nameof(MappingEntryPoints))]
  public void Mapping_a_module_without_the_entitlement_registration_fails_at_map_time(
    string entryPoint, Action<IEndpointRouteBuilder> map)
  {
    var application = WebApplication.CreateBuilder().Build();

    var failure = Assert.Throws<InvalidOperationException>(() => map(application));

    Assert.Contains(nameof(ITenantModuleEntitlement), failure.Message, StringComparison.Ordinal);
    Assert.Contains("500", failure.Message, StringComparison.Ordinal);
    Assert.False(string.IsNullOrWhiteSpace(entryPoint));
  }

  // ---- AND IT ADMITS A HOST THAT DID REGISTER ONE.
  //
  // The other half of the assertion, and not a formality: a check that refused everything would also
  // "pass" the test above, and would break all five hosts and the product Host with it.
  [Theory]
  [MemberData(nameof(MappingEntryPoints))]
  public void Mapping_a_module_with_the_entitlement_registration_succeeds(
    string entryPoint, Action<IEndpointRouteBuilder> map)
  {
    var builder = WebApplication.CreateBuilder();
    builder.Services.AddScoped<ITenantModuleEntitlement, TransitionalGrantsEveryModuleEntitlement>();
    var application = builder.Build();

    map(application);

    Assert.False(string.IsNullOrWhiteSpace(entryPoint));
  }

  // ---- THE DIAGNOSIS HAS TO BE ACTIONABLE, NOT MERELY LOUD.
  //
  // The original incident was expensive because the symptom named nothing: a 500 with no clue that a
  // registration was missing. Asserting the message carries the module, the contract and the remedy is
  // asserting the thing that would actually have saved the time.
  [Fact]
  public void The_failure_names_the_module_the_contract_and_the_remedy()
  {
    var application = WebApplication.CreateBuilder().Build();

    var failure = Assert.Throws<InvalidOperationException>(() => application.MapPayrollEndpoints());

    Assert.Contains(PayrollModuleEnablement.Key, failure.Message, StringComparison.Ordinal);
    Assert.Contains(nameof(ITenantModuleEntitlement), failure.Message, StringComparison.Ordinal);
    Assert.Contains(
      nameof(TransitionalGrantsEveryModuleEntitlement), failure.Message, StringComparison.Ordinal);
    Assert.Contains("misconfigured", failure.Message, StringComparison.OrdinalIgnoreCase);
  }
}
