using SSAS.Platform.Domain.Enums;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.HR.Application.Departments;
using SSAS.HR.Application.Permissions;
using SSAS.Platform.Application.Permissions;

namespace SSAS.Architecture.Tests;

// ==================================================================================================
// STRUCTURAL CHECKS THAT WERE STRANDED IN THE INTEGRATION SUITE (T-250).
// ==================================================================================================
//
// ---- HOW THESE WERE FOUND, AND WHY THE COST WAS NOT THE MILLISECONDS.
//
// A duration sweep of the Integration suite asked which tests report a time implausibly short for the work
// they claim. Eight of 806 came back under 10 ms, with **nothing at all between 10 ms and 2.4 seconds** —
// a gap that wide is a category rather than a tail.
//
// None was a false green. **All eight were structural checks that need no database**, sitting behind a
// suite that takes ~24 minutes and requires SQL Server. **`GATE_SCOPE=TASK` never runs that suite**, so
// these invariants were not being checked during ordinary development at all — the same shape as the 145
// Integration failures that went unread for eight days, where correctness depended on a suite nobody ran.
//
// Here they run in every gate, in about 72 seconds, with no database.
//
// ---- ⚠ ONLY TWO OF THE EIGHT COULD MOVE, AND THE OTHER SIX ARE NOT AN OVERSIGHT.
//
// The six `C6_*` cutover-manifest checks read `CutoverTenantModel.Source`, a helper defined in the
// Integration project and consumed by **five other Integration test files**. Moving it here would make
// Integration depend on Architecture.Tests, which is backwards; copying it would create the second list
// that its own header warns against — *"three test fixtures each maintaining their own list is how a
// fixture ends up proving a cutover that production does not run"*. **They stay put until that helper has
// somewhere neutral to live.**
//
// ---- ⚠ AND BOTH ARE PLANTED, BECAUSE "IT HAS ALWAYS BEEN GREEN" IS WEAKEST HERE.
//
// These spent their whole lives in a suite nobody routinely runs. A check never observed to fail, inside a
// suite never observed, is two layers of the same problem — so each was broken deliberately and watched to
// redden before being trusted in its new home.
public sealed class StructuralChecksMovedFromIntegrationTests
{
  // ---- THE ORDINARY UPDATE CANNOT REACH PARENT OR STATUS, and the proof is the type itself.
  //
  // There is no field to set, so this is a compile-time guarantee rather than a runtime refusal. Asserting
  // it records that the absence is load-bearing rather than incidental.
  //
  // PLANTED: adding a `ParentDepartmentId` property to `UpdateDepartmentCommand` reddens this on the
  // sequence comparison.
  [Fact]
  [Trait("Decision", "ADR-026")]
  public void The_update_command_carries_no_parent_status_or_manager()
  {
    var properties = typeof(UpdateDepartmentCommand)
      .GetProperties()
      .Select(property => property.Name)
      .ToArray();

    Assert.Equal(["DepartmentId", "Code", "Name", "RowVersion"], properties);
  }

  // ---- THE COMPOSED CATALOG DEFINES ALL FIVE HR PERMISSIONS AT TENANT SCOPE — PLATFORM'S ALONE DEFINES
  // NONE, and the second half is the control.
  //
  // The five names once existed as constants no catalog defined:
  // `AssignPermissionToRoleCommandHandler` refuses anything the catalog does not know, so **production
  // answered 403 to every caller while the suite was green**. The control is what makes this test say
  // something — without it, a catalog that defined everything would pass just as well.
  //
  // PLANTED: dropping `HrPermissionCatalogContributor` from the composition reddens the first assertion,
  // and asserting the platform catalog DOES define them reddens the control.
  [Fact]
  public void The_composed_catalog_defines_the_hr_permissions_and_the_platform_catalog_does_not()
  {
    var composed = new ComposedPermissionCatalog(
      new PlatformPermissionCatalog(), [new HrPermissionCatalogContributor()]);
    var platformOnly = new PlatformPermissionCatalog();

    foreach (var permission in new[]
    {
      HrPermissionNames.ViewEmployees,
      HrPermissionNames.CreateEmployees,
      HrPermissionNames.UpdateEmployees,
      HrPermissionNames.TransferEmployees,
      HrPermissionNames.TerminateEmployees
    })
    {
      Assert.True(composed.TryGet(permission, out var definition), permission);
      Assert.Equal(PermissionScope.Tenant, definition.Scope);
      Assert.False(string.IsNullOrWhiteSpace(definition.Description));

      Assert.False(platformOnly.TryGet(permission, out _), permission);
    }

    // Composing ADDED; it did not disturb what Platform already owned.
    Assert.All(platformOnly.All, definition => Assert.True(composed.TryGet(definition.Name.Value, out _)));
  }
}
