using SSAS.BuildingBlocks.Infrastructure.Persistence;
using SSAS.GL.Infrastructure.Persistence;
using SSAS.Payroll.Infrastructure.Persistence;
using SSAS.HR.Infrastructure.Persistence;
using SSAS.Platform.Infrastructure.Persistence.TenantErp;

namespace SSAS.Integration.Tests;

// ==================================================================================================
// THE CONTRIBUTOR SET THE HOST REGISTERS, IN ONE PLACE FOR THE CUTOVER TESTS (FP-006C6).
// ==================================================================================================
//
// Production resolves IEnumerable<ITenantModelContributor> from the container, where HR registered its
// contributor through AddHrInfrastructure. These tests construct the copy service directly, so they must
// supply the same set — and supply it from ONE definition, because three test fixtures each maintaining
// their own list is how a fixture ends up proving a cutover that production does not run.
//
// A test passing an empty set here would be testing a cutover that cannot see Employee, which is exactly the
// defect FP-006C6 closes. That is asserted against directly by C6-14 in TenantCutoverCopySqlServerTests.
internal static class CutoverTenantModel
{
  // The same shape the Host composes: Platform's own tenant entities plus every registered module's
  // contribution. TWO modules contribute today — HR and, from FP-011, GL.
  //
  // ---- THIS LIST IS THE ONE THAT MATTERS, AND IT IS NOT THE ONLY LIST.
  //
  // Twenty-one test sites construct a contributor array. Most build a model to prove something about ONE
  // module's schema and correctly name only that module; adding GL to them would put seven irrelevant
  // tables into fixtures that assert about Employees. THIS site is different: the cutover manifest tests
  // DERIVE their expected set from it, so a module missing here is a module missing from every derived
  // assertion — and an incomplete manifest is not an error, it is a shorter list that passes.
  public static readonly ITenantModelContributor[] Contributors =
    [new HrTenantModelContributor(), new GlTenantModelContributor(), new PayrollTenantModelContributor()];

  public static ITenantModelSource Source { get; } = new ComposedTenantModelSource(Contributors);

  // Nothing contributed — the model as it was before FP-006C6 wired the contributor set through. Kept so a
  // test can prove the two are genuinely different rather than assuming it.
  public static ITenantModelSource ContributorFreeSource { get; } = new ComposedTenantModelSource([]);
}
