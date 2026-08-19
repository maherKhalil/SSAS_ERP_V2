using SSAS.BuildingBlocks.Infrastructure.Persistence;
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
  // contribution. HR is the only contributing module today.
  public static readonly ITenantModelContributor[] Contributors = [new HrTenantModelContributor()];

  public static ITenantModelSource Source { get; } = new ComposedTenantModelSource(Contributors);

  // Nothing contributed — the model as it was before FP-006C6 wired the contributor set through. Kept so a
  // test can prove the two are genuinely different rather than assuming it.
  public static ITenantModelSource ContributorFreeSource { get; } = new ComposedTenantModelSource([]);
}
